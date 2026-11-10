using System.IO;
using System.Text;
using IniFileParser = WpfLcuCtrlLib.IniFileParser;

namespace WpfApp1_cmd.Services;

// =============================================================================
// UpdateCommonService
// -----------------------------------------------------------------------------
// 役割:
//   装置(LCU)側へ配置する UpdateCommon.inf の「読込 → 編集 → 書出し → 転送 → 検証」を
//   一括して扱うサービス。
//
// 処理の流れ (ユニット転送後):
//   1. Read                    : 元となる UpdateCommon.inf を読み込む (パーサーと生の行配列)
//   2. ClearMissingFuserPaths  : 転送対象ファイルが存在しないユニットの FuserPath を空にする
//   3. SetVersions/SetVersion  : 行配列中の各ユニットの Version= 行を転送したバージョンへ書き換える
//   4. WriteAsync              : 行配列をローカルへ書き出す (必要に応じて検査用タイムスタンプ行を付加)
//   5. DeployAsync             : 書き出したファイルを装置へアップロードし、必要なら再ダウンロードする
//   6. Inspect                 : 再ダウンロードしたファイルの Version と期待値を比較する
//   DeployIfNeededAsync は 4〜6 を「1 件以上転送した場合のみ」実行するまとめ処理。
//
// 備考:
//   - 装置との通信は IUpdateCommonDeploymentGateway に委譲し、本サービスは通信方式に依存しない。
//   - getCurrentTime はタイムスタンプ行の時刻をテストで固定するための差し替え口。
// =============================================================================

#region 型定義

/// <summary>
/// UpdateCommon.inf を装置との間でアップロード/ダウンロードする通信窓口。
/// </summary>
public interface IUpdateCommonDeploymentGateway
{
	/// <summary>ローカルディレクトリのファイルを装置の指定パスへアップロードする。</summary>
	Task<bool> UploadAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativePath,
		string lcuRelativePath,
		string localDirectory,
		CancellationToken cancellationToken);

	/// <summary>装置の指定パスのファイルをローカルディレクトリへダウンロードする。</summary>
	Task<bool> DownloadAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativePath,
		string lcuRelativePath,
		string localDirectory,
		CancellationToken cancellationToken);
}

/// <summary>UpdateCommon.inf の読込結果。ファイルが無い場合は両方 null。</summary>
public sealed record UpdateCommonSourceReadResult(
	IniFileParser? Parser,
	string[]? Lines);

/// <summary>
/// UpdateCommon.inf を書き出して装置へ配置するための要求。
/// </summary>
/// <param name="MachineRelativeDirectory">装置上の配置先ディレクトリ (相対)。</param>
/// <param name="Lines">書き出す行配列。</param>
/// <param name="LocalDirectory">一旦書き出すローカルディレクトリ。</param>
/// <param name="InspectionRootDirectory">検証用に再ダウンロードする先のルート。</param>
/// <param name="AppendInspectionTimestamp">末尾に「;UpdateCommon.inf(日時)」行を付けるか。</param>
/// <param name="VerifyAfterUpload">アップロード後に再ダウンロードして検証するか。</param>
public sealed record UpdateCommonDeploymentRequest(
	string MachineName,
	string IpAddress,
	int Position,
	string MachineRelativeDirectory,
	string[] Lines,
	string LocalDirectory,
	string InspectionRootDirectory,
	string FileName,
	string EncodingName,
	bool AppendInspectionTimestamp,
	bool VerifyAfterUpload);

/// <summary>配置処理の結果 (アップロード/再ダウンロードの成否と各ファイルパス)。</summary>
public sealed record UpdateCommonDeploymentResult(
	bool UploadSucceeded,
	bool InspectionDownloadSucceeded,
	string LocalFilePath,
	string InspectionFilePath);

/// <summary>検証対象ユニット。CurrentVersion は期待するバージョン。</summary>
public sealed record UpdateCommonVersionUnit(string Name, string? CurrentVersion);

/// <summary>検証結果の判定。</summary>
public enum UpdateCommonVersionStatus
{
	/// <summary>期待値と一致。</summary>
	Matched,
	/// <summary>期待値と不一致。</summary>
	Mismatched,
	/// <summary>セクションに対応するユニットが検証対象に無い。</summary>
	UnitNotFound
}

/// <summary>UpdateCommon.inf の 1 セクション分の検証結果。</summary>
public sealed record UpdateCommonVersionEntry(
	string Section,
	string Version,
	string? CurrentVersion,
	UpdateCommonVersionStatus Status);

/// <summary>
/// UpdateCommon.inf 全体の検証結果。
/// ファイルが無い場合は FileExists=false、読込に失敗した場合は ReadError に理由が入る。
/// </summary>
public sealed record UpdateCommonVersionInspectionResult(
	bool FileExists,
	string? ReadError,
	IReadOnlyList<UpdateCommonVersionEntry> Entries);

/// <summary>
/// ユニット転送後に UpdateCommon.inf を配置するかどうかを含めた要求。
/// 転送件数が開始時から増えていない場合は配置しない。
/// </summary>
public sealed record ModuleUpdateCommonDeploymentRequest(
	int TransferredCountAtStart,
	int TransferredCountAtEnd,
	UpdateCommonDeploymentRequest Deployment,
	IReadOnlyList<UpdateCommonVersionUnit> Units);

/// <summary>
/// 転送後配置の結果。WasDeployed=false の場合は配置をスキップしたことを示す。
/// Inspection は検証を行った場合のみ設定される。
/// </summary>
public sealed record ModuleUpdateCommonDeploymentResult(
	bool WasDeployed,
	bool UploadSucceeded,
	bool InspectionDownloadSucceeded,
	UpdateCommonVersionInspectionResult? Inspection);

#endregion

/// <summary>
/// 装置へ配置する UpdateCommon.inf の読込・編集・書出し・配置・検証を行うサービス。
/// </summary>
/// <param name="getCurrentTime">タイムスタンプ行に使う現在時刻の取得関数 (省略時は DateTime.Now)。</param>
public sealed class UpdateCommonService(Func<DateTime>? getCurrentTime = null)
{
	#region 1. 読込

	/// <summary>
	/// UpdateCommon.inf を INI パーサーと行配列の両方で読み込む。
	/// ファイルが存在しない場合は (null, null) を返す。
	/// </summary>
	public UpdateCommonSourceReadResult Read(
		string path,
		string encodingName,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentException.ThrowIfNullOrWhiteSpace(encodingName);
		cancellationToken.ThrowIfCancellationRequested();

		if (!System.IO.File.Exists(path))
		{
			return new(null, null);
		}

		IniFileParser parser = new(path);
		string[] lines = System.IO.File.ReadAllLines(path, Encoding.GetEncoding(encodingName));
		cancellationToken.ThrowIfCancellationRequested();
		return new(parser, lines);
	}

	#endregion

	#region 2. FuserPath のクリア

	/// <summary>
	/// 指定ユニットのセクションの FuserPath を空文字にする。
	/// 転送元ファイルが見つからないユニットに対し、装置側が存在しないファイルを参照しないようにするため。
	/// </summary>
	public void ClearMissingFuserPaths(
		IniFileParser parser,
		IReadOnlyList<(string UnitName, string? FuserPath)> fuserPathsToClear,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(parser);
		ArgumentNullException.ThrowIfNull(fuserPathsToClear);
		cancellationToken.ThrowIfCancellationRequested();

		foreach ((string unitName, _) in fuserPathsToClear)
		{
			parser.SetValue(unitName, "FuserPath", string.Empty);
		}
	}

	#endregion

	#region 3. バージョン書換え

	/// <summary>
	/// 行配列中の複数ユニットの Version= 行をまとめて書き換える (配列をその場で更新)。
	/// 各ユニットの最初の [ユニット名] セクション行より後ろで最初に現れる Version= 行が対象。
	/// 途中でキャンセルされた場合は元の配列を変更しない。
	/// </summary>
	public static void SetVersions(
		string[] lines,
		IReadOnlyList<(string UnitName, string Version)> updates,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lines);
		ArgumentNullException.ThrowIfNull(updates);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, string> sectionNamesByHeader = new(StringComparer.Ordinal);
		foreach ((string unitName, _) in updates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			sectionNamesByHeader.TryAdd($"[{unitName}]", unitName);
		}

		Dictionary<string, int> firstSectionIndexes = new(StringComparer.Ordinal);
		for (int index = 0; index < lines.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (sectionNamesByHeader.TryGetValue(lines[index], out string? unitName))
			{
				firstSectionIndexes.TryAdd(unitName, index);
			}
		}

		// 各行から見て次に現れる Version= 行の位置を後ろから求めておく
		int[] nextVersionIndexes = new int[lines.Length];
		int nextVersionIndex = -1;
		for (int index = lines.Length - 1; index >= 0; index--)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (lines[index].StartsWith("Version="))
			{
				nextVersionIndex = index;
			}
			nextVersionIndexes[index] = nextVersionIndex;
		}

		string[] updatedLines = (string[])lines.Clone();
		foreach ((string unitName, string version) in updates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!firstSectionIndexes.TryGetValue(unitName, out int sectionIndex)
				|| sectionIndex + 1 >= lines.Length)
			{
				continue;
			}

			int versionIndex = nextVersionIndexes[sectionIndex + 1];
			if (versionIndex >= 0)
			{
				updatedLines[versionIndex] = "Version=" + version;
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		Array.Copy(updatedLines, lines, lines.Length);
	}

	/// <summary>
	/// 行配列中の 1 ユニットの Version= 行を書き換える (配列をその場で更新)。
	/// [ユニット名] 行以降で最初に現れる Version= 行が対象。
	/// </summary>
	public static void SetVersion(
		string[] lines,
		string unitName,
		string version,
		CancellationToken cancellationToken = default)
	{
		int section = 0;
		foreach (ref string line in lines.AsSpan())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (line.Equals($"[{unitName}]"))
			{
				section = 1;
			}
			if (section == 1 && line.StartsWith("Version="))
			{
				line = "Version=" + version;
				break;
			}
		}
	}

	#endregion

	#region 4. ローカルへの書出し

	/// <summary>
	/// 行配列を指定エンコーディングでファイルへ書き出す。出力先ディレクトリが無ければ作成する。
	/// appendInspectionTimestamp=true の場合は末尾に「;UpdateCommon.inf(yyyy/MM/dd HH:mm:ss)」行を付加する。
	/// </summary>
	public async Task WriteAsync(
		string[] lines,
		string path,
		string encodingName,
		bool appendInspectionTimestamp,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(lines);
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentException.ThrowIfNullOrWhiteSpace(encodingName);
		cancellationToken.ThrowIfCancellationRequested();

		string[] outputLines = lines;
		if (appendInspectionTimestamp)
		{
			outputLines =
			[
				.. lines,
				$";UpdateCommon.inf({(getCurrentTime?.Invoke() ?? DateTime.Now):yyyy/MM/dd HH:mm:ss})"
			];
		}

		string? directory = Path.GetDirectoryName(path);
		cancellationToken.ThrowIfCancellationRequested();
		if (!string.IsNullOrEmpty(directory))
		{
			await Task.Run(
				() => Directory.CreateDirectory(directory),
				cancellationToken);
		}

		cancellationToken.ThrowIfCancellationRequested();
		await System.IO.File.WriteAllLinesAsync(
			path,
			outputLines,
			Encoding.GetEncoding(encodingName),
			cancellationToken);
	}

	#endregion

	#region 5. 装置への配置

	/// <summary>
	/// UpdateCommon.inf をローカルへ書き出して装置へアップロードする。
	/// VerifyAfterUpload=true の場合は検証用に InspectionRootDirectory 配下へ再ダウンロードする。
	/// </summary>
	public async Task<UpdateCommonDeploymentResult> DeployAsync(
		UpdateCommonDeploymentRequest request,
		IUpdateCommonDeploymentGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentNullException.ThrowIfNull(request.Lines);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.LocalDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.InspectionRootDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.MachineRelativeDirectory);

		cancellationToken.ThrowIfCancellationRequested();
		string localFilePath = Path.Combine(request.LocalDirectory, request.FileName);
		await WriteAsync(
			request.Lines,
			localFilePath,
			request.EncodingName,
			request.AppendInspectionTimestamp,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		string machineRelativePath =
			$"{request.MachineRelativeDirectory.TrimEnd('/', '\\')}/{request.FileName}";
		string lcuRelativePath = request.FileName;
		bool uploadSucceeded = await gateway.UploadAsync(
			request.MachineName,
			request.IpAddress,
			request.Position,
			machineRelativePath,
			lcuRelativePath,
			request.LocalDirectory,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		string inspectionFilePath = Path.Combine(
			request.InspectionRootDirectory,
			machineRelativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
		bool inspectionDownloadSucceeded = false;
		if (request.VerifyAfterUpload)
		{
			inspectionDownloadSucceeded = await gateway.DownloadAsync(
				request.MachineName,
				request.IpAddress,
				request.Position,
				machineRelativePath,
				request.FileName,
				request.InspectionRootDirectory,
				cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
		}

		return new UpdateCommonDeploymentResult(
			uploadSucceeded,
			inspectionDownloadSucceeded,
			localFilePath,
			inspectionFilePath);
	}

	#endregion

	#region 6. 配置後の検証

	/// <summary>
	/// UpdateCommon.inf の各セクションの Version を、検証対象ユニットの期待バージョンと比較する。
	/// 同名ユニットが複数ある場合は先頭のものを採用する。
	/// 読込時の I/O 系例外は ReadError に格納して返す (例外は送出しない)。
	/// </summary>
	public UpdateCommonVersionInspectionResult Inspect(
		string updateCommonPath,
		IReadOnlyList<UpdateCommonVersionUnit> units,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		ArgumentNullException.ThrowIfNull(units);
		cancellationToken.ThrowIfCancellationRequested();

		if (!System.IO.File.Exists(updateCommonPath))
		{
			return new(false, null, []);
		}

		try
		{
			IniFileParser parser = new(updateCommonPath);
			Dictionary<string, UpdateCommonVersionUnit> unitsByName =
				new(StringComparer.Ordinal);
			foreach (UpdateCommonVersionUnit unit in units)
			{
				cancellationToken.ThrowIfCancellationRequested();
				unitsByName.TryAdd(unit.Name, unit);
			}

			List<UpdateCommonVersionEntry> entries = [];
			foreach (string section in parser.SectionCount())
			{
				cancellationToken.ThrowIfCancellationRequested();
				string version = parser.GetValue(section, "Version");
				if (!unitsByName.TryGetValue(section, out UpdateCommonVersionUnit? unit))
				{
					entries.Add(new UpdateCommonVersionEntry(
						section,
						version,
						null,
						UpdateCommonVersionStatus.UnitNotFound));
					continue;
				}

				entries.Add(new UpdateCommonVersionEntry(
						section,
						version,
						unit.CurrentVersion,
						unit.CurrentVersion != version
							? UpdateCommonVersionStatus.Mismatched
							: UpdateCommonVersionStatus.Matched));
			}

			cancellationToken.ThrowIfCancellationRequested();
			return new(true, null, entries);
		}
		catch (Exception exception) when (
			exception is System.IO.IOException
				or UnauthorizedAccessException
				or ArgumentException
				or KeyNotFoundException)
		{
			return new(true, exception.Message, []);
		}
	}

	#endregion

	#region 7. 転送後の配置 (まとめ処理)

	/// <summary>
	/// ユニット転送後の UpdateCommon.inf 配置をまとめて行う。
	/// 転送件数が開始時から増えていなければ何もしない (WasDeployed=false, UploadSucceeded=true)。
	/// 配置後、再ダウンロードに成功していれば Inspect で内容を検証する。
	/// </summary>
	public async Task<ModuleUpdateCommonDeploymentResult> DeployIfNeededAsync(
		ModuleUpdateCommonDeploymentRequest request,
		IUpdateCommonDeploymentGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(request.Deployment);
		ArgumentNullException.ThrowIfNull(request.Units);
		ArgumentNullException.ThrowIfNull(gateway);

		cancellationToken.ThrowIfCancellationRequested();
		if (request.TransferredCountAtEnd <= request.TransferredCountAtStart)
		{
			return new(false, true, false, null);
		}

		UpdateCommonDeploymentResult deployment = await DeployAsync(
			request.Deployment,
			gateway,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		UpdateCommonVersionInspectionResult? inspection = null;
		if (request.Deployment.VerifyAfterUpload && deployment.InspectionDownloadSucceeded)
		{
			inspection = await Task.Run(
				() => Inspect(
					deployment.InspectionFilePath,
					request.Units,
					cancellationToken),
				cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
		}

		return new(
			true,
			deployment.UploadSucceeded,
			deployment.InspectionDownloadSucceeded,
			inspection);
	}

	#endregion
}
