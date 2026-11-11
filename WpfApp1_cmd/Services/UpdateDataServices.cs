namespace WpfApp1_cmd.Services
{
using WpfLcuCtrlLib;

public sealed class LcuUpdateCommonDeploymentGateway(LcuCtrl lcuCtrl) : IUpdateCommonDeploymentGateway
{
	public Task<bool> UploadAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativePath,
		string lcuRelativePath,
		string localDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.PutMachineFile(
			machineName,
			IpAddress,
			position,
			machineRelativePath,
			$"/MCFiles/{lcuRelativePath}",
			localDirectory,
			cancellationToken);

	public Task<bool> DownloadAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativePath,
		string lcuRelativePath,
		string localDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFiles(
			machineName,
			IpAddress,
			position,
			[$"/{machineRelativePath}"],
			[lcuRelativePath],
			localDirectory,
			cancellationToken);
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;
using System.Text;
using IniFileParser = WpfLcuCtrlLib.IniFileParser;

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

	public static string[] SetTransferredFilePaths(
		string[] lines,
		IReadOnlyList<ModuleUpdateVersion> updates,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lines);
		ArgumentNullException.ThrowIfNull(updates);
		Dictionary<string, ModuleUpdateVersion> bySection = new(StringComparer.Ordinal);
		foreach (ModuleUpdateVersion update in updates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (update.Name != null)
			{
				bySection.TryAdd(update.Name, update);
			}
		}

		List<string> result = [];
		ModuleUpdateVersion? current = null;
		bool hasPath = false;
		bool hasFuserPath = false;
		void CompleteSection()
		{
			if (current == null)
			{
				return;
			}
			if (!hasPath)
			{
				result.Add("Path=" + current.Path);
			}
			if (!hasFuserPath && !string.IsNullOrEmpty(current.FuserPath))
			{
				result.Add("FuserPath=" + current.FuserPath);
			}
		}

		foreach (string line in lines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string trimmed = line.Trim();
			if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
			{
				CompleteSection();
				current = bySection.GetValueOrDefault(trimmed[1..^1]);
				hasPath = false;
				hasFuserPath = false;
			}
			int separator = line.IndexOf('=');
			string key = separator >= 0 ? line[..separator].Trim() : "";
			if (current != null && key.Equals("Path", StringComparison.OrdinalIgnoreCase))
			{
				result.Add(line[..(separator + 1)] + current.Path);
				hasPath = true;
			}
			else if (current != null && key.Equals("FuserPath", StringComparison.OrdinalIgnoreCase))
			{
				result.Add(line[..(separator + 1)] + current.FuserPath);
				hasFuserPath = true;
			}
			else
			{
				result.Add(line);
			}
		}
		CompleteSection();
		cancellationToken.ThrowIfCancellationRequested();
		return result.ToArray();
	}

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
}

namespace WpfApp1_cmd.Services
{
using System.IO;
using IniFileParser = WpfLcuCtrlLib.IniFileParser;

// =====================================================================
//  アップデートデータ読み込みサービス
//
//  アップデートデータ側の UpdateCommon.inf を読み込み、画面表示用のデータを作る。
//
//  処理の流れ(Load):
//    1. BuildPathGroupDefinitions: unitLink.json(Config.units)をグループ定義に変換
//    2. BuildUnitGroupLookup     : 構成ユニット名 → グループ名 の辞書を作る
//    3. ReadUpdateCommon         : UpdateCommon.inf の各セクション(ユニット)を読み、
//                                  参照ファイルの欠落確認とデータサイズ算出を行う
//    4. BuildPathGroups          : グループ単位の転送ファイル一覧(UpdatePaths)を作る
//  結果の反映:
//    - CreateLoadPresentation    : 読み込み結果からログ/エラーコードを決める
//    - CreateUpdateInfoProjections / CreatePathGroupProjections: 画面モデル用に変換
//  ユーティリティ:
//    - GetDirectorySize / GetPeripheralSize: アップデートデータのサイズ算出
// =====================================================================

#region 入出力

/// <summary>UpdateCommon.inf の 1 セクション(ユニット)</summary>
public sealed record UpdateCommonInfoEntry(
	string Name,
	string Attribute,
	string Path,
	string Version,
	string FuserPath,
	string? UnitGroup);

/// <summary>UpdateCommon.inf の 1 セクションのパス情報(フォルダ指定のセクションも含む)</summary>
public sealed record UpdateCommonPathEntry(
	string Name,
	string Path,
	string FuserPath);

/// <summary>
///  UpdateCommon.inf の読み込み結果
/// </summary>
/// <param name="Entries">ユニット一覧(Path がフォルダのセクションは除く)</param>
/// <param name="DataSize">アップデートデータのサイズ</param>
/// <param name="ErrorMessage">読み込みエラー(null なら成功)</param>
/// <param name="PathEntries">全セクションのパス情報</param>
/// <param name="MissingFiles">Path または FuserPath に指定されているが存在しないファイル</param>
public sealed record UpdateCommonInfoReadResult(
	IReadOnlyList<UpdateCommonInfoEntry> Entries,
	long DataSize,
	string? ErrorMessage,
	IReadOnlyList<UpdateCommonPathEntry> PathEntries,
	IReadOnlyList<string> MissingFiles);

/// <summary>unitLink.json の 1 定義(スナップショット)</summary>
public sealed record UpdatePathGroupInput(
	IReadOnlyList<string> Components,
	IReadOnlyList<KeyValuePair<string, string>> Names,
	string? Mode);

/// <summary>
///  転送ファイルのグループ定義
/// </summary>
/// <param name="Components">グループを構成するユニット名</param>
/// <param name="GroupName">グループ名(Names の "*" の値)</param>
/// <param name="GroupNames">機種別を含む全グループ名(同名グループの統合に使用)</param>
/// <param name="Mode">"File"=Path のファイルのみ / "Folder"=Path のフォルダ配下全て</param>
public sealed record UpdatePathGroupDefinition(
	IReadOnlyList<string> Components,
	string GroupName,
	IReadOnlyList<string> GroupNames,
	string? Mode);

public sealed record UpdatePathGroup(string GroupName, IReadOnlyList<UpdatePathFile> Files);

public sealed record UpdatePathFile(string? Name, string? Path, string? FusePath);

/// <summary>
///  アップデートデータ読み込み結果
/// </summary>
/// <param name="UpdateCommonExists">UpdateCommon.inf が存在したか</param>
/// <param name="Entries">ユニット一覧</param>
/// <param name="DataSize">アップデートデータのサイズ(読み込み失敗時は null)</param>
/// <param name="ReadError">読み込みエラー</param>
/// <param name="PathGroups">グループ単位の転送ファイル一覧</param>
/// <param name="MissingFiles">存在しない参照ファイル</param>
public sealed record UpdateDataLoadResult(
	bool UpdateCommonExists,
	IReadOnlyList<UpdateCommonInfoEntry> Entries,
	long? DataSize,
	string? ReadError,
	IReadOnlyList<UpdatePathGroup> PathGroups,
	IReadOnlyList<string> MissingFiles);

public enum UpdateDataLoadPresentationOutcome
{
	Loaded,
	ReadFailed,
	UpdateCommonMissing
}

/// <summary>読み込み結果を画面/ログへ反映するための情報</summary>
public sealed record UpdateDataLoadPresentation(
	UpdateDataLoadPresentationOutcome Outcome,
	long? DataSize,
	ErrorCode? ErrorCode,
	string? LogMessage);

/// <summary>UpdateInfo(画面モデル)作成用のデータ</summary>
public sealed record UpdateInfoProjection(
	string Name,
	string Attribute,
	string Path,
	string Version,
	string FuserPath,
	string? UnitGroup,
	bool IsVisible);

/// <summary>UnitPath(画面モデル)作成用のファイル</summary>
public sealed record UpdatePathFileProjection(
	string? Name,
	string Path,
	string FusePath);

/// <summary>UnitPath(画面モデル)作成用のグループ</summary>
public sealed record UpdatePathGroupProjection(
	string GroupName,
	IReadOnlyList<UpdatePathFileProjection> Files);

#endregion

/// <summary>
///  アップデートデータの UpdateCommon.inf を読み込み、ユニット一覧・転送ファイル一覧を作る。
///  処理の流れはファイル先頭のコメントを参照。
/// </summary>
public sealed class UpdateDataLoadService
{
	/// <summary>
	///  アップデートデータを読み込む(UpdateCommon.inf 読み込み → 転送ファイル一覧作成)。
	///  UpdateCommon.inf が無い場合は UpdateCommonExists=false を返す。
	/// </summary>
	public UpdateDataLoadResult Load(
		string updateCommonPath,
		string dataFolder,
		IReadOnlyList<UpdatePathGroupDefinition> unitLinks,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(unitLinks);
		cancellationToken.ThrowIfCancellationRequested();

		if (!File.Exists(updateCommonPath))
		{
			return new(false, [], null, null, [], []);
		}

		IReadOnlyDictionary<string, string> unitGroupsByComponent =
			BuildUnitGroupLookup(unitLinks, cancellationToken);

		UpdateCommonInfoReadResult info = ReadUpdateCommon(
			updateCommonPath,
			dataFolder,
			unit => unitGroupsByComponent.GetValueOrDefault(unit),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (info.ErrorMessage != null)
		{
			return new(
				true,
				info.Entries,
				null,
				info.ErrorMessage,
				[],
				info.MissingFiles);
		}

		IReadOnlyList<UpdatePathGroup> pathGroups = BuildPathGroups(
			info.PathEntries,
			dataFolder,
			unitLinks,
			cancellationToken);

		return new(
			true,
			info.Entries,
			info.DataSize,
			null,
			pathGroups,
			info.MissingFiles);
	}

	/// <summary>
	///  読み込み結果から、画面に反映するサイズ・エラーコード・ログメッセージを決める。
	///  <list type="bullet">
	///   <item>UpdateCommon.inf 無し: UPDATE_UNDEFINED_ERROR</item>
	///   <item>読み込みエラー     : UPDATE_READ_ERROR</item>
	///   <item>成功               : "Read {path}" をログ出力</item>
	///  </list>
	/// </summary>
	public UpdateDataLoadPresentation CreateLoadPresentation(
		UpdateDataLoadResult result,
		string updateCommonPath,
		string missingUpdateCommonMessage)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(updateCommonPath);
		ArgumentNullException.ThrowIfNull(missingUpdateCommonMessage);

		if (!result.UpdateCommonExists)
		{
			return new(
				UpdateDataLoadPresentationOutcome.UpdateCommonMissing,
				null,
				WpfApp1_cmd.ErrorCode.UPDATE_UNDEFINED_ERROR,
				missingUpdateCommonMessage);
		}

		if (result.ReadError != null)
		{
			return new(
				UpdateDataLoadPresentationOutcome.ReadFailed,
				null,
				WpfApp1_cmd.ErrorCode.UPDATE_READ_ERROR,
				$"Read Error:{result.ReadError}");
		}

		return new(
			UpdateDataLoadPresentationOutcome.Loaded,
			result.DataSize,
			null,
			$"Read {updateCommonPath}");
	}

	#region 1〜2. グループ定義

	/// <summary>
	///  1. unitLink.json の定義をグループ定義に変換する。
	///  グループ名は Names の "*"(全機種共通)の値、無ければ空文字。
	/// </summary>
	public IReadOnlyList<UpdatePathGroupDefinition> BuildPathGroupDefinitions(
		IReadOnlyList<UpdatePathGroupInput> inputs,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(inputs);
		cancellationToken.ThrowIfCancellationRequested();

		UpdatePathGroupDefinition[] definitions = new UpdatePathGroupDefinition[inputs.Count];
		for (int index = 0; index < inputs.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdatePathGroupInput input = inputs[index];
			string[] components = new string[input.Components.Count];
			for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				components[componentIndex] = input.Components[componentIndex];
			}

			string[] names = new string[input.Names.Count];
			string groupName = "";
			for (int nameIndex = 0; nameIndex < input.Names.Count; nameIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				KeyValuePair<string, string> entry = input.Names[nameIndex];
				names[nameIndex] = entry.Value;
				if (entry.Key == "*")
				{
					groupName = entry.Value;
				}
			}

			definitions[index] = new UpdatePathGroupDefinition(
				components,
				groupName,
				names,
				input.Mode);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return definitions;
	}

	/// <summary>
	///  2. 「構成ユニット名 → グループ名」の辞書を作る(同名ユニットは先勝ち)。
	/// </summary>
	public IReadOnlyDictionary<string, string> BuildUnitGroupLookup(
		IReadOnlyList<UpdatePathGroupDefinition> definitions,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(definitions);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, string> groupsByComponent = new(StringComparer.Ordinal);
		foreach (UpdatePathGroupDefinition definition in definitions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in definition.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component != null)
				{
					groupsByComponent.TryAdd(component, definition.GroupName);
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return groupsByComponent;
	}

	#endregion

	#region 3. UpdateCommon.inf の読み込み

	/// <summary>
	///  3. UpdateCommon.inf を読み込む。
	///  <list type="bullet">
	///   <item>Path がフォルダのセクションはユニット一覧に含めない(PathEntries には含める)</item>
	///   <item>Path または FuserPath のファイルが無い場合は MissingFiles に追加する(ユニット一覧には含める)</item>
	///   <item>データサイズは Peripheral.bin があればそのサイズ、無ければフォルダ全体のサイズ</item>
	///   <item>IO エラー等は例外にせず ErrorMessage に設定する</item>
	///  </list>
	/// </summary>
	/// <param name="resolveUnitGroup">ユニット名からグループ名を求める関数</param>
	public UpdateCommonInfoReadResult ReadUpdateCommon(
		string updateCommonPath,
		string dataFolder,
		Func<string, string?> resolveUnitGroup,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(resolveUnitGroup);

		List<UpdateCommonInfoEntry> entries = [];
		List<string> missingFiles = [];
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			IniFileParser parser = new(updateCommonPath);
			List<UpdateCommonPathEntry> pathEntries = [];
			foreach (string section in parser.SectionCount())
			{
				cancellationToken.ThrowIfCancellationRequested();
				string relativePath = parser.GetValue(section, "Path");
				string fuserPath = parser.GetValue(section, "FuserPath");
				pathEntries.Add(new UpdateCommonPathEntry(section, relativePath, fuserPath));
				if (!string.IsNullOrEmpty(fuserPath) && !File.Exists(dataFolder + fuserPath))
				{
					missingFiles.Add(dataFolder + fuserPath);
				}
				string fullPath = dataFolder + relativePath;
				if (Directory.Exists(fullPath))
				{
					continue;
				}
				if (!string.IsNullOrEmpty(relativePath) && !File.Exists(fullPath))
				{
					missingFiles.Add(fullPath);
				}

				entries.Add(new UpdateCommonInfoEntry(
					section,
					parser.GetValue(section, "Attribute"),
					relativePath,
					parser.GetValue(section, "Version"),
					fuserPath,
					resolveUnitGroup(section)));
			}

			return new UpdateCommonInfoReadResult(
				entries,
				GetPeripheralSize(dataFolder, cancellationToken),
				null,
				pathEntries,
				missingFiles);
		}
		catch (Exception exception) when (
			exception is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return new UpdateCommonInfoReadResult([], 0, exception.Message, [], []);
		}
	}

	#endregion

	#region 4. 転送ファイル一覧(グループ単位)

	/// <summary>
	///  4. 指定した UpdateCommon.inf を読み込み、グループ単位の転送ファイル一覧を作る。
	/// </summary>
	public IReadOnlyList<UpdatePathGroup> BuildPathGroups(
		string updateCommonPath,
		string dataFolder,
		IReadOnlyList<UpdatePathGroupDefinition> unitLinks,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(unitLinks);
		cancellationToken.ThrowIfCancellationRequested();

		IniFileParser parser = new(updateCommonPath);
		List<UpdateCommonPathEntry> pathEntries = [];
		foreach ((string name, string path, string fuserPath) in parser.GetPathList())
		{
			cancellationToken.ThrowIfCancellationRequested();
			pathEntries.Add(new UpdateCommonPathEntry(name, path, fuserPath));
		}

		return BuildPathGroups(pathEntries, dataFolder, unitLinks, cancellationToken);
	}

	/// <summary>
	///  4. パス情報からグループ単位の転送ファイル一覧を作る。
	///  <list type="bullet">
	///   <item>ファイルが存在しない/フォルダ、またはグループ定義に無いユニットは対象外</item>
	///   <item>GroupNames のいずれかが既存グループと一致すれば、そのグループにまとめる</item>
	///   <item>Mode="File": Path のファイルのみ追加</item>
	///   <item>Mode="Folder": Path のフォルダ配下の全ファイルを追加
	///         (UpdateCommon.inf にあるファイルはそのセクション名、無いファイルはグループ名で追加)</item>
	///   <item>同じ (名前, パス) は 1 グループ内で重複させない</item>
	///  </list>
	/// </summary>
	public IReadOnlyList<UpdatePathGroup> BuildPathGroups(
		IReadOnlyList<UpdateCommonPathEntry> pathEntries,
		string dataFolder,
		IReadOnlyList<UpdatePathGroupDefinition> unitLinks,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(pathEntries);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(unitLinks);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, UpdatePathGroupDefinition> unitLinksByComponent =
			new(StringComparer.Ordinal);
		foreach (UpdatePathGroupDefinition unitLink in unitLinks)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in unitLink.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component != null)
				{
					unitLinksByComponent.TryAdd(component, unitLink);
				}
			}
		}

		// Folder モードで見つけたファイルを、UpdateCommon.inf のセクション名に戻すための索引
		Dictionary<string, List<string>> sectionsByPath =
			new(StringComparer.OrdinalIgnoreCase);
		foreach (UpdateCommonPathEntry entry in pathEntries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (entry.Path.Length == 0)
			{
				continue;
			}

			if (!sectionsByPath.TryGetValue(entry.Path, out List<string>? sections))
			{
				sections = [];
				sectionsByPath.Add(entry.Path, sections);
			}
			sections.Add(entry.Name);
		}

		List<(string GroupName, IReadOnlyList<string> GroupNames, List<UpdatePathFile> Files)> groups = [];
		Dictionary<string, int> groupIndexesByName = new(StringComparer.OrdinalIgnoreCase);
		Dictionary<int, HashSet<(string? Name, string? Path)>> addedFilesByGroup = [];
		Dictionary<string, string[]> folderFilesByDirectory = new(StringComparer.OrdinalIgnoreCase);
		foreach (UpdateCommonPathEntry entry in pathEntries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string name = entry.Name;
			string path = entry.Path;
			string fuserPath = entry.FuserPath;
			if (string.IsNullOrEmpty(path))
			{
				continue;
			}

			string fullPath = dataFolder + path;
			if (!Path.Exists(fullPath) || Directory.Exists(fullPath))
			{
				continue;
			}

			if (!unitLinksByComponent.TryGetValue(name, out UpdatePathGroupDefinition? unitLink))
			{
				continue;
			}

			string groupName = unitLink.GroupName;
			int groupIndex = int.MaxValue;
			foreach (string candidateGroupName in unitLink.GroupNames)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (candidateGroupName == null)
				{
					continue;
				}

				if (groupIndexesByName.TryGetValue(candidateGroupName, out int candidateIndex))
				{
					groupIndex = Math.Min(groupIndex, candidateIndex);
				}
			}

			if (groupIndex == int.MaxValue)
			{
				groupIndex = groups.Count;
				groups.Add((groupName, unitLink.GroupNames, []));
				addedFilesByGroup.Add(groupIndex, []);
				if (groupName != null)
				{
					groupIndexesByName.TryAdd(groupName, groupIndex);
				}
			}
			List<UpdatePathFile> groupFiles = groups[groupIndex].Files;
			HashSet<(string? Name, string? Path)> addedFiles = addedFilesByGroup[groupIndex];

			if (unitLink.Mode == "File")
			{
				AddUnique(groupFiles, addedFiles, new UpdatePathFile(name, path, fuserPath));
			}
			else if (unitLink.Mode == "Folder")
			{
				string directory = dataFolder + Path.GetDirectoryName(path);
				if (!folderFilesByDirectory.TryGetValue(directory, out string[]? folderFiles))
				{
					folderFiles = EnumerateFiles(directory, cancellationToken).ToArray();
					folderFilesByDirectory.Add(directory, folderFiles);
				}

				foreach (string file in folderFiles)
				{
					cancellationToken.ThrowIfCancellationRequested();
					string relativePath = "/" + Path.GetRelativePath(dataFolder, file).Replace('\\', '/');
					if (sectionsByPath.TryGetValue(relativePath, out List<string>? matchingSections))
					{
						foreach (string section in matchingSections)
						{
							cancellationToken.ThrowIfCancellationRequested();
							AddUnique(
								groupFiles,
								addedFiles,
								new UpdatePathFile(section, relativePath, ""));
						}
					}
					else
					{
						AddUnique(
							groupFiles,
							addedFiles,
							new UpdatePathFile(groupName, relativePath, ""));
					}
				}
			}
		}

		UpdatePathGroup[] result = new UpdatePathGroup[groups.Count];
		for (int index = 0; index < groups.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			(string groupName, _, List<UpdatePathFile> files) = groups[index];
			result[index] = new UpdatePathGroup(groupName, files);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	private static IEnumerable<string> EnumerateFiles(
		string directory,
		CancellationToken cancellationToken)
	{
		foreach (string file in Directory.EnumerateFiles(
			directory,
			"*",
			SearchOption.AllDirectories))
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return file;
		}
	}

	private static void AddUnique(
		List<UpdatePathFile> files,
		HashSet<(string? Name, string? Path)> addedFiles,
		UpdatePathFile file)
	{
		if (addedFiles.Add((file.Name, file.Path)))
		{
			files.Add(file);
		}
	}

	#endregion

	#region 画面モデル用への変換

	/// <summary>
	///  ユニット一覧を UpdateInfo(画面モデル)作成用データに変換する(全て表示対象)。
	/// </summary>
	public IReadOnlyList<UpdateInfoProjection> CreateUpdateInfoProjections(
		IReadOnlyList<UpdateCommonInfoEntry> entries,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entries);
		cancellationToken.ThrowIfCancellationRequested();

		UpdateInfoProjection[] result = new UpdateInfoProjection[entries.Count];
		for (int index = 0; index < result.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateCommonInfoEntry entry = entries[index];
			result[index] = new(
				entry.Name,
				entry.Attribute,
				entry.Path,
				entry.Version,
				entry.FuserPath,
				entry.UnitGroup,
				true);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	/// <summary>
	///  転送ファイル一覧を UnitPath(画面モデル)作成用データに変換する。
	///  null パスは空文字にし、重複ファイルと空グループは除く。
	/// </summary>
	public IReadOnlyList<UpdatePathGroupProjection> CreatePathGroupProjections(
		IReadOnlyList<UpdatePathGroup> groups,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		cancellationToken.ThrowIfCancellationRequested();

		List<UpdatePathGroupProjection> result = new(groups.Count);
		foreach (UpdatePathGroup group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			List<UpdatePathFileProjection> files = new(group.Files.Count);
			HashSet<(string? Name, string Path)> existingFiles = [];
			foreach (UpdatePathFile file in group.Files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string path = file.Path ?? "";
				if (!existingFiles.Add((file.Name, path)))
				{
					continue;
				}

				files.Add(new(file.Name, path, file.FusePath ?? ""));
			}

			if (files.Count > 0)
			{
				result.Add(new(group.GroupName, files));
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	#endregion

	#region サイズ算出

	/// <summary>
	///  フォルダ配下(サブフォルダ含む)の全ファイルサイズの合計を返す。
	/// </summary>
	public static long GetDirectorySize(
		DirectoryInfo directory,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(directory);
		cancellationToken.ThrowIfCancellationRequested();

		long size = 0;
		Stack<DirectoryInfo> pendingDirectories = new();
		pendingDirectories.Push(directory);
		while (pendingDirectories.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			DirectoryInfo currentDirectory = pendingDirectories.Pop();
			foreach (FileInfo file in currentDirectory.EnumerateFiles())
			{
				cancellationToken.ThrowIfCancellationRequested();
				size += file.Length;
			}

			foreach (DirectoryInfo subdirectory in currentDirectory.EnumerateDirectories())
			{
				cancellationToken.ThrowIfCancellationRequested();
				pendingDirectories.Push(subdirectory);
			}
		}

		return size;
	}

	/// <summary>
	///  アップデートデータのサイズを返す。
	///  フォルダ直下に Peripheral.bin があればそのサイズ、無ければフォルダ全体のサイズ。
	/// </summary>
	public static long GetPeripheralSize(
		string path,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		cancellationToken.ThrowIfCancellationRequested();

		DirectoryInfo directory = new(path);
		foreach (FileInfo peripheralFile in directory.EnumerateFiles(UpdateDataSourceService.ArchiveFileName))
		{
			cancellationToken.ThrowIfCancellationRequested();
			return peripheralFile.Length;
		}

		return GetDirectorySize(directory, cancellationToken);
	}

	#endregion
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// =====================================================================
//  アップデートデータの取得元決定サービス
//
//  起動時にアップデートデータ(UpdateCommon.inf と更新ファイル群)の置き場所を決める。
//    1. データフォルダ: --dataFolder 指定値、未指定ならアプリフォルダ\Data
//    2. データフォルダに Peripheral.bin(圧縮アーカイブ)があれば、空き容量を確認して展開する
//       展開に成功した場合は展開先(C:\DecompBin)をデータフォルダとする
//    3. データフォルダ + Peripheral パス + UpdateCommon.inf を読み込み対象とする
//    4. アーカイブ処理結果からログ/エラーコードを作る
//
//  Peripheral.bin の展開は mcAccount.dll(ネイティブ)で行う(PeripheralArchiveExtractor)。
// =====================================================================

/// <summary>Peripheral.bin の準備結果</summary>
public enum PeripheralArchivePreparationOutcome
{
	/// <summary>Peripheral.bin が無い(展開不要)</summary>
	ArchiveNotFound,
	/// <summary>C ドライブの空き容量不足で展開しなかった</summary>
	InsufficientDiskSpace,
	/// <summary>展開に失敗した</summary>
	DecompressionFailed,
	/// <summary>展開に成功した</summary>
	Decompressed
}

public sealed record PeripheralArchivePreparationResult(
	PeripheralArchivePreparationOutcome Outcome,
	long ArchiveSize = 0,
	long AvailableDiskSpace = 0);

/// <summary>
///  取得元の決定結果
/// </summary>
/// <param name="DataFolder">アップデートデータのルートフォルダ(展開した場合は展開先)</param>
/// <param name="UpdateCommonPath">読み込む UpdateCommon.inf のフルパス</param>
/// <param name="ArchivePath">Peripheral.bin のパス(存在しない場合もある)</param>
/// <param name="ArchivePreparation">Peripheral.bin の準備結果</param>
public sealed record UpdateDataSourceResult(
	string DataFolder,
	string UpdateCommonPath,
	string ArchivePath,
	PeripheralArchivePreparationResult ArchivePreparation);

/// <summary>
///  アーカイブ準備結果を画面/ログへ反映するための情報
/// </summary>
/// <param name="ArchiveSize">アップデートデータサイズとして表示する値(null なら更新しない)</param>
/// <param name="ErrorCode">設定するエラーコード(null なら設定しない)</param>
/// <param name="LogMessage">ログ出力するメッセージ(null なら出力しない)</param>
public sealed record UpdateDataSourceDiagnostic(
	long? ArchiveSize,
	ErrorCode? ErrorCode,
	string? LogMessage);

/// <summary>
///  アップデートデータの取得元(データフォルダ・UpdateCommon.inf)を決定する。
///  ファイルシステム/展開処理はコンストラクタで注入する(テストで差し替えるため)。
/// </summary>
public sealed class UpdateDataSourceService
{
	/// <summary>アップデートデータの圧縮アーカイブ名</summary>
	public const string ArchiveFileName = "Peripheral.bin";

	private readonly Func<string, bool> _pathExists;
	private readonly Func<string, long> _getFileSize;
	private readonly Func<long> _getAvailableDiskSpace;
	private readonly Func<string, int> _decompress;

	/// <param name="pathExists">パスの存在確認</param>
	/// <param name="getFileSize">ファイルサイズ取得</param>
	/// <param name="getAvailableDiskSpace">展開先ドライブの空き容量取得</param>
	/// <param name="decompress">Peripheral.bin の展開(戻り値が負なら失敗)</param>
	public UpdateDataSourceService(
		Func<string, bool> pathExists,
		Func<string, long> getFileSize,
		Func<long> getAvailableDiskSpace,
		Func<string, int> decompress)
	{
		ArgumentNullException.ThrowIfNull(pathExists);
		ArgumentNullException.ThrowIfNull(getFileSize);
		ArgumentNullException.ThrowIfNull(getAvailableDiskSpace);
		ArgumentNullException.ThrowIfNull(decompress);
		_pathExists = pathExists;
		_getFileSize = getFileSize;
		_getAvailableDiskSpace = getAvailableDiskSpace;
		_decompress = decompress;
	}

	/// <summary>
	///  取得元を決定する(必要なら Peripheral.bin を展開する)。
	/// </summary>
	/// <param name="configuredDataFolder">--dataFolder の指定値(空ならアプリフォルダ\Data)</param>
	/// <param name="applicationDirectory">アプリの実行フォルダ</param>
	/// <param name="extractionDirectory">Peripheral.bin の展開先</param>
	/// <param name="peripheralRelativePath">データフォルダから UpdateCommon.inf までの相対パス</param>
	/// <param name="updateCommonFileName">UpdateCommon.inf のファイル名</param>
	public async Task<UpdateDataSourceResult> PrepareAsync(
		string configuredDataFolder,
		string applicationDirectory,
		string extractionDirectory,
		string peripheralRelativePath,
		string updateCommonFileName,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(configuredDataFolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(extractionDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(peripheralRelativePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonFileName);
		cancellationToken.ThrowIfCancellationRequested();

		string dataFolder = string.IsNullOrWhiteSpace(configuredDataFolder)
			? Path.Combine(applicationDirectory, "Data")
			: configuredDataFolder;
		PeripheralArchivePreparationResult archivePreparation =
			await PrepareArchiveAsync(dataFolder, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		string archivePath = Path.Combine(dataFolder, ArchiveFileName);

		if (archivePreparation.Outcome == PeripheralArchivePreparationOutcome.Decompressed)
		{
			dataFolder = extractionDirectory;
		}

		string updateCommonPath = Path.Combine(
			dataFolder,
			peripheralRelativePath
				.Replace('/', Path.DirectorySeparatorChar)
				.TrimStart(Path.DirectorySeparatorChar),
			updateCommonFileName);
		return new UpdateDataSourceResult(
			dataFolder,
			updateCommonPath,
			archivePath,
			archivePreparation);
	}

	/// <summary>
	///  データフォルダの Peripheral.bin を確認し、空き容量が足りれば展開する。
	/// </summary>
	public async Task<PeripheralArchivePreparationResult> PrepareArchiveAsync(
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();
		string archivePath = Path.Combine(dataFolder, ArchiveFileName);
		(bool Exists, long ArchiveSize, long AvailableDiskSpace) archiveInfo =
			await Task.Run(() =>
			{
				bool exists = _pathExists(archivePath);
				if (!exists)
				{
					return (false, 0L, 0L);
				}

				return (true, _getFileSize(archivePath), _getAvailableDiskSpace());
			}, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		if (!archiveInfo.Exists)
		{
			return new(PeripheralArchivePreparationOutcome.ArchiveNotFound);
		}

		long archiveSize = archiveInfo.ArchiveSize;
		long availableDiskSpace = archiveInfo.AvailableDiskSpace;
		if (availableDiskSpace < archiveSize)
		{
			return new(
				PeripheralArchivePreparationOutcome.InsufficientDiskSpace,
				archiveSize,
				availableDiskSpace);
		}

		cancellationToken.ThrowIfCancellationRequested();
		if (_decompress(archivePath) < 0)
		{
			return new(
				PeripheralArchivePreparationOutcome.DecompressionFailed,
				archiveSize,
				availableDiskSpace);
		}

		return new(
			PeripheralArchivePreparationOutcome.Decompressed,
			archiveSize,
			availableDiskSpace);
	}

	/// <summary>
	///  アーカイブ準備結果から、表示サイズ・エラーコード・ログメッセージを作る。
	///  <list type="bullet">
	///   <item>アーカイブ無し: 何もしない</item>
	///   <item>空き容量不足: LCU_UPDATE_DISK_FULL</item>
	///   <item>展開失敗/成功: ログのみ</item>
	///  </list>
	/// </summary>
	public UpdateDataSourceDiagnostic CreateArchivePreparationDiagnostic(
		PeripheralArchivePreparationResult result,
		string archivePath)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(archivePath);

		return result.Outcome switch
		{
			PeripheralArchivePreparationOutcome.ArchiveNotFound =>
				new(null, null, null),
			PeripheralArchivePreparationOutcome.InsufficientDiskSpace =>
				new(
					result.ArchiveSize,
					ErrorCode.LCU_UPDATE_DISK_FULL,
					$"Not enough disk space:{result.AvailableDiskSpace} < UpdateData::{result.ArchiveSize}"),
			PeripheralArchivePreparationOutcome.DecompressionFailed =>
				new(result.ArchiveSize, null, $"DeCompress {archivePath} Fail."),
			PeripheralArchivePreparationOutcome.Decompressed =>
				new(result.ArchiveSize, null, $"DeCompress {archivePath} OK"),
			_ => throw new ArgumentOutOfRangeException(
				nameof(result),
				result.Outcome,
				"Unknown archive preparation outcome.")
		};
	}
}

#region Peripheral.bin の展開(ネイティブ DLL 呼び出し)

/// <summary>アーカイブイメージを展開する処理(テストで差し替えるためのインターフェース)</summary>
public interface IPeripheralArchiveImageExtractor
{
	int Extract(string destinationDirectory, byte[] image, uint imageSize);
}

/// <summary>
///  Peripheral.bin を読み込み、指定フォルダへ展開する。
///  ネイティブ側がカレントディレクトリを変更するため、展開後にアプリフォルダへ戻す。
/// </summary>
public sealed class PeripheralArchiveExtractor
{
	private readonly IPeripheralArchiveImageExtractor _imageExtractor;
	private readonly Action<string> _setCurrentDirectory;

	public PeripheralArchiveExtractor(
		IPeripheralArchiveImageExtractor imageExtractor,
		Action<string> setCurrentDirectory)
	{
		ArgumentNullException.ThrowIfNull(imageExtractor);
		ArgumentNullException.ThrowIfNull(setCurrentDirectory);
		_imageExtractor = imageExtractor;
		_setCurrentDirectory = setCurrentDirectory;
	}

	/// <returns>ネイティブ関数の戻り値(負なら失敗)</returns>
	public int Extract(
		string archivePath,
		string destinationDirectory,
		string applicationDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

		byte[] image = File.ReadAllBytes(archivePath);
		try
		{
			return _imageExtractor.Extract(
				destinationDirectory,
				image,
				checked((uint)image.Length));
		}
		finally
		{
			_setCurrentDirectory(applicationDirectory);
		}
	}
}

/// <summary>mcAccount.dll の ExtractFolderFromImage による展開</summary>
public sealed class McAccountPeripheralArchiveImageExtractor : IPeripheralArchiveImageExtractor
{
	[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
	private static extern int ExtractFolderFromImage(
		StringBuilder destinationDirectory,
		byte[] image,
		uint imageSize,
		UIntPtr errorInfo);

	[StructLayout(LayoutKind.Sequential)]
	private struct ErrorInfo
	{
		public long ErrorCode;
		public long SubErrorCode;
	}

	public int Extract(string destinationDirectory, byte[] image, uint imageSize)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
		ArgumentNullException.ThrowIfNull(image);
		if (imageSize > image.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(imageSize));
		}

		IntPtr errorInfo = Marshal.AllocHGlobal(Marshal.SizeOf<ErrorInfo>());
		try
		{
			Marshal.StructureToPtr(default(ErrorInfo), errorInfo, false);
			StringBuilder targetDirectory = new(destinationDirectory, 1024);
			return ExtractFolderFromImage(
				targetDirectory,
				image,
				imageSize,
				(UIntPtr)errorInfo);
		}
		finally
		{
			Marshal.FreeHGlobal(errorInfo);
		}
	}
}

#endregion
}

namespace WpfApp1_cmd.Services
{


public enum BackupOutcome
{
	Succeeded,
	Failed,
	Cancelled
}

public static class UpdateExecutionGate
{
	public static Func<Task<T>>? CreateUpdateOperation<T>(
		BackupOutcome backupOutcome,
		Func<Task<T>> startUpdate)
	{
		ArgumentNullException.ThrowIfNull(startUpdate);
		return backupOutcome == BackupOutcome.Succeeded ? startUpdate : null;
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record UpdateFileGroup(
	string Name,
	IReadOnlyList<string> NameAliases,
	IReadOnlyList<string> Components,
	string? Mode);

public sealed record UpdateFileVersion(
	string? Name,
	bool? IsSelected,
	string? Path,
	string? FuserPath,
	string? UnitGroup);

public sealed record UpdateFilePath(
	string GroupName,
	IReadOnlyList<UpdateFileEntry>? Units);

public sealed record UpdateFileEntry(string? Name, string? Path, string? FusePath);

public sealed record UpdateFileListEntry(string? Name, string? Path, string? FusePath);

public sealed record UpdateFileGroupResult(
	string GroupName,
	List<UpdateFileListEntry> Units);

public static class UpdateFileListBuilder
{
	public static int CountFiles(
		IReadOnlyList<UpdateFileGroupResult> groups,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		cancellationToken.ThrowIfCancellationRequested();

		int count = 0;
		foreach (UpdateFileGroupResult group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (UpdateFileListEntry unit in group.Units)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int fileCount =
					(string.IsNullOrEmpty(unit.Path) ? 0 : 1)
					+ (string.IsNullOrEmpty(unit.FusePath) ? 0 : 1);
				count = checked(count + fileCount);
			}
		}

		return count;
	}

	public static List<UpdateFileGroupResult> Create(
		IReadOnlyList<UpdateFileGroup> groups,
		IReadOnlyList<UpdateFileVersion> versions,
		IReadOnlyList<UpdateFilePath> updatePaths,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		ArgumentNullException.ThrowIfNull(versions);
		ArgumentNullException.ThrowIfNull(updatePaths);
		ArgumentNullException.ThrowIfNull(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, UpdateFileGroup> groupsByComponent = new(StringComparer.Ordinal);
		UpdateFileGroup? firstGroupWithNullComponent = null;
		foreach (UpdateFileGroup group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in group.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component == null)
				{
					firstGroupWithNullComponent ??= group;
					continue;
				}

				if (!groupsByComponent.TryAdd(component, group))
				{
					continue;
				}
			}
		}

		Dictionary<string, UpdateFileVersion> versionsByPath = new(StringComparer.Ordinal);
		UpdateFileVersion? firstVersionWithNullPath = null;
		Dictionary<string, int> selectedCountsByGroup = new(StringComparer.Ordinal);
		int selectedCountWithNullGroup = 0;
		foreach (UpdateFileVersion version in versions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (version.Path == null)
			{
				firstVersionWithNullPath ??= version;
			}
			else
			{
				versionsByPath.TryAdd(version.Path, version);
			}

			if (version.IsSelected == true)
			{
				if (version.UnitGroup == null)
				{
					selectedCountWithNullGroup++;
				}
				else
				{
					selectedCountsByGroup.TryGetValue(version.UnitGroup, out int count);
					selectedCountsByGroup[version.UnitGroup] = count + 1;
				}
			}
		}

		List<UpdateFileGroupResult> unitFiles = [];
		Dictionary<UpdateFileGroupResult, HashSet<(string? Name, string Path)>> addedFiles = [];
		StringComparer groupNameComparer = StringComparer.CurrentCultureIgnoreCase;
		Dictionary<string, int> firstResultIndexesByName = new(groupNameComparer);
		Dictionary<string, int> firstUpdatePathIndexesByName = new(groupNameComparer);
		for (int index = 0; index < updatePaths.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string groupName = updatePaths[index].GroupName;
			if (!string.IsNullOrEmpty(groupName))
			{
				firstUpdatePathIndexesByName.TryAdd(groupName, index);
			}
		}

		foreach (UpdateFileVersion item in versions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateFileGroup? group = item.Name == null
				? firstGroupWithNullComponent
				: groupsByComponent.GetValueOrDefault(item.Name);
			if (group == null)
			{
				continue;
			}

			int unitGroupIndex = FindFirstMatchIndex(
				group,
				firstResultIndexesByName,
				cancellationToken);
			UpdateFileGroupResult? unitGroup = unitGroupIndex >= 0
				? unitFiles[unitGroupIndex]
				: null;

			if (group.Mode == "File")
			{
				if (item.IsSelected == false)
				{
					continue;
				}

				if (unitGroup == null)
				{
					unitGroup = new UpdateFileGroupResult(group.Name, []);
					unitFiles.Add(unitGroup);
					addedFiles.Add(unitGroup, []);
					AddResultNames(
						group.Name,
						unitFiles.Count - 1,
						firstResultIndexesByName);
				}

				AddUnitFile(
					unitGroup,
					addedFiles[unitGroup],
					dataFolder,
					new UpdateFileEntry(item.Name, item.Path, item.FuserPath));
				continue;
			}

			int updatePathIndex = FindFirstMatchIndex(
				group,
				firstUpdatePathIndexesByName,
				cancellationToken);
			UpdateFilePath? updatePath = updatePathIndex >= 0
				? updatePaths[updatePathIndex]
				: null;
			if (updatePath!.Units == null || updatePath.Units.Count == 0)
			{
				continue;
			}

			foreach (UpdateFileEntry unit in updatePath.Units)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UpdateFileVersion? version = unit.Path == null
					? firstVersionWithNullPath
					: versionsByPath.GetValueOrDefault(unit.Path);
				if (version != null)
				{
					if (version.IsSelected == false)
					{
						continue;
					}

					if (unitGroup == null)
					{
						unitGroup = new UpdateFileGroupResult(group.Name, []);
						unitFiles.Add(unitGroup);
						addedFiles.Add(unitGroup, []);
						AddResultNames(
							group.Name,
							unitFiles.Count - 1,
							firstResultIndexesByName);
						AddUnitFile(unitGroup, addedFiles[unitGroup], dataFolder, unit);
					}
					else
					{
						AddUnitFile(
							unitGroup,
							addedFiles[unitGroup],
							dataFolder,
							unit with { FusePath = "" });
					}
				}
				else
				{
					int selectedCount = updatePath.GroupName == null
						? selectedCountWithNullGroup
						: selectedCountsByGroup.GetValueOrDefault(updatePath.GroupName);
					if (selectedCount != 0)
					{
						if (unitGroup == null)
						{
							unitGroup = new UpdateFileGroupResult(group.Name, []);
							unitFiles.Add(unitGroup);
							addedFiles.Add(unitGroup, []);
							AddResultNames(
								group.Name,
								unitFiles.Count - 1,
								firstResultIndexesByName);
							AddUnitFile(unitGroup, addedFiles[unitGroup], dataFolder, unit);
						}
						else
						{
							AddUnitFile(
								unitGroup,
								addedFiles[unitGroup],
								dataFolder,
								unit with { FusePath = "" });
						}
					}
				}
			}
		}

		return unitFiles;
	}

	private static int FindFirstMatchIndex(
		UpdateFileGroup group,
		IReadOnlyDictionary<string, int> indexesByName,
		CancellationToken cancellationToken)
	{
		int firstIndex = int.MaxValue;
		foreach (string alias in group.NameAliases)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!string.IsNullOrEmpty(alias)
				&& indexesByName.TryGetValue(alias, out int index))
			{
				firstIndex = Math.Min(firstIndex, index);
			}
		}

		return firstIndex == int.MaxValue ? -1 : firstIndex;
	}

	private static void AddResultNames(
		string groupName,
		int index,
		Dictionary<string, int> indexesByName)
	{
		if (!string.IsNullOrEmpty(groupName))
		{
			indexesByName.TryAdd(groupName, index);
		}
	}

	private static void AddUnitFile(
		UpdateFileGroupResult group,
		HashSet<(string? Name, string Path)> addedFiles,
		string dataFolder,
		UpdateFileEntry unit)
	{
		string path = dataFolder + unit.Path;
		if (!addedFiles.Add((unit.Name, path)))
		{
			return;
		}

		group.Units.Add(new UpdateFileListEntry(
			unit.Name,
			path,
			string.IsNullOrEmpty(unit.FusePath) ? "" : dataFolder + unit.FusePath));
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;

public sealed record UpdateFolderFileInfo(string? Path, string? FusePath);

public sealed record UpdateFolderUnit(bool IsSelected, string Path, string FusePath);

public sealed record UpdateFolderModule(bool IsSelected, IReadOnlyList<UpdateFolderUnit> Units);

public sealed record UpdateFolderMachine(bool IsSelected, IReadOnlyList<UpdateFolderModule> Modules);

public sealed class UpdateFolderSelectionService
{
	public List<string> CreateAllFolders(
		IReadOnlyList<string> updateDataPaths,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(updateDataPaths);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		List<string> folders = [];
		HashSet<string> uniqueFolders = new(StringComparer.Ordinal);
		foreach (string updateDataPath in updateDataPaths)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string localPath = dataFolder + updateDataPath;
			if (!Path.Exists(localPath))
			{
				continue;
			}

			if ((File.GetAttributes(localPath) & FileAttributes.Directory) == FileAttributes.Directory)
			{
				AddUniqueFolder(folders, uniqueFolders, localPath);
			}
			else
			{
				string? directory = Path.GetDirectoryName(localPath);
				if (directory != null)
				{
					AddUniqueFolder(folders, uniqueFolders, directory);
				}
			}
		}

		return folders;
	}

	public List<string> CreateSelectedFolders(
		IReadOnlyList<UpdateFolderMachine> machines,
		IReadOnlyList<UpdateFolderFileInfo> updateDataFiles,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(updateDataFiles);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		HashSet<string> updateDataPaths = new(StringComparer.Ordinal);
		foreach (UpdateFolderFileInfo file in updateDataFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (file.Path != null)
			{
				updateDataPaths.Add(file.Path);
			}
			if (file.FusePath != null)
			{
				updateDataPaths.Add(file.FusePath);
			}
		}

		List<string> folders = [];
		HashSet<string> uniqueFolders = new(StringComparer.Ordinal);
		foreach (UpdateFolderMachine machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!machine.IsSelected)
			{
				continue;
			}

			foreach (UpdateFolderModule module in machine.Modules)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!module.IsSelected)
				{
					continue;
				}

				if (module.Units.Count == 0)
				{
					List<string> selectedUpdateDataPaths = [];
					HashSet<string> uniquePaths = new(StringComparer.Ordinal);
					foreach (UpdateFolderFileInfo file in updateDataFiles)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (file.Path != null && uniquePaths.Add(file.Path))
						{
							selectedUpdateDataPaths.Add(file.Path);
						}
					}

					foreach (string folder in CreateAllFolders(
						selectedUpdateDataPaths,
						dataFolder,
						cancellationToken))
					{
						cancellationToken.ThrowIfCancellationRequested();
						AddUniqueFolder(folders, uniqueFolders, folder);
					}

					return folders;
				}

				foreach (UpdateFolderUnit unit in module.Units)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!unit.IsSelected)
					{
						continue;
					}

					if (!string.IsNullOrEmpty(unit.Path) && updateDataPaths.Contains(unit.Path))
					{
						AddUniqueFolder(
							folders,
							uniqueFolders,
							dataFolder + Path.GetDirectoryName(unit.Path));
					}
					if (!string.IsNullOrEmpty(unit.FusePath) && updateDataPaths.Contains(unit.FusePath))
					{
						AddUniqueFolder(
							folders,
							uniqueFolders,
							dataFolder + Path.GetDirectoryName(unit.FusePath));
					}
				}
			}
		}

		return folders;
	}

	private static void AddUniqueFolder(
		List<string> folders,
		HashSet<string> uniqueFolders,
		string folder)
	{
		if (uniqueFolders.Add(folder))
		{
			folders.Add(folder);
		}
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record UpdateFolderUnitSnapshot(
	bool? IsSelected,
	string Path,
	string FusePath);

public sealed record UpdateFolderModuleSnapshot(
	bool? IsSelected,
	IReadOnlyList<UpdateFolderUnitSnapshot> Units);

public sealed record UpdateFolderMachineSnapshot(
	bool? IsSelected,
	IReadOnlyList<UpdateFolderModuleSnapshot> Modules);

public sealed record UpdateFolderSelectionSnapshot(
	IReadOnlyList<UpdateFolderMachine> Machines,
	IReadOnlyList<UpdateFolderFileInfo> UpdateDataFiles);

public sealed class UpdateFolderSelectionSnapshotBuilder
{
	public UpdateFolderSelectionSnapshot Build(
		IReadOnlyList<UpdateFolderMachineSnapshot> machines,
		IReadOnlyList<UpdateFolderFileInfo> updateDataFiles,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(updateDataFiles);
		cancellationToken.ThrowIfCancellationRequested();

		UpdateFolderMachine[] result = new UpdateFolderMachine[machines.Count];
		for (int machineIndex = 0; machineIndex < machines.Count; machineIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateFolderMachineSnapshot machine = machines[machineIndex];
			UpdateFolderModule[] modules = new UpdateFolderModule[machine.Modules.Count];
			for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UpdateFolderModuleSnapshot module = machine.Modules[moduleIndex];
				UpdateFolderUnit[] units = new UpdateFolderUnit[module.Units.Count];
				for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					UpdateFolderUnitSnapshot unit = module.Units[unitIndex];
					units[unitIndex] = new UpdateFolderUnit(
						unit.IsSelected == true,
						unit.Path,
						unit.FusePath);
				}

				modules[moduleIndex] = new UpdateFolderModule(
					module.IsSelected != false,
					units);
			}

			result[machineIndex] = new UpdateFolderMachine(
				machine.IsSelected != false,
				modules);
		}

		UpdateFolderFileInfo[] fileResults = new UpdateFolderFileInfo[updateDataFiles.Count];
		for (int index = 0; index < fileResults.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateFolderFileInfo file = updateDataFiles[index];
			fileResults[index] = new UpdateFolderFileInfo(file.Path, file.FusePath);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(result, fileResults);
	}
}
}

namespace WpfApp1_cmd.Services
{


public enum InspectionDiagnosticDestination
{
	ApplicationLog,
	Debug
}

public sealed record InspectionDiagnostic(
	InspectionDiagnosticDestination Destination,
	string Message,
	bool IsError);

public sealed record ModuleInspectionDiagnostics(
	IReadOnlyList<InspectionDiagnostic> Entries,
	bool AllFilesMatch);

public sealed class UpdateInspectionDiagnosticService
{
	public IReadOnlyList<InspectionDiagnostic> CreateTransferredFilePresenceDiagnostics(
		IReadOnlyList<TransferredFilePresence> results)
	{
		ArgumentNullException.ThrowIfNull(results);

		List<InspectionDiagnostic> diagnostics = new(results.Count);
		foreach (TransferredFilePresence result in results)
		{
			string presence = result.Exists ? "is found in LCU." : "is not found in LCU.";
			string message = result.IsFuserPath
				? $"[INSPECTION] {result.Path} {presence}"
				: $"[INSPECTION] {result.UnitName}={result.Path} {presence}";
			diagnostics.Add(new(
				InspectionDiagnosticDestination.Debug,
				message,
				false));
		}

		return diagnostics;
	}

	public ModuleInspectionDiagnostics CreateModuleDiagnostics(
		string lcuName,
		string machineName,
		string moduleName,
		IReadOnlyList<ModuleFileInspectionResult> results,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcuName);
		ArgumentNullException.ThrowIfNull(machineName);
		ArgumentNullException.ThrowIfNull(moduleName);
		ArgumentNullException.ThrowIfNull(results);
		cancellationToken.ThrowIfCancellationRequested();

		List<InspectionDiagnostic> diagnostics = new(results.Count);
		bool allFilesMatch = true;
		foreach (ModuleFileInspectionResult result in results)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!result.IsMatch)
			{
				diagnostics.Add(new(
					InspectionDiagnosticDestination.ApplicationLog,
					$"[INSPECTION] {lcuName};{machineName};{moduleName};{result.UnitName}=NG:DiffFile Inspection Error({result.FilePath})",
					true));
				allFilesMatch = false;
			}
			else
			{
				diagnostics.Add(new(
					InspectionDiagnosticDestination.ApplicationLog,
					$"[INSPECTION] {lcuName};{machineName};{moduleName};{result.UnitName};{result.FilePath}=OK:Inspection Success",
					false));
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(diagnostics, allFilesMatch);
	}

	public IReadOnlyList<InspectionDiagnostic> CreateUpdateCommonDiagnostics(
		UpdateCommonVersionInspectionResult inspection,
		string updateCommonFile,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(inspection);
		ArgumentNullException.ThrowIfNull(updateCommonFile);
		cancellationToken.ThrowIfCancellationRequested();

		List<InspectionDiagnostic> diagnostics = [];
		if (!inspection.FileExists)
		{
			diagnostics.Add(new(
				InspectionDiagnosticDestination.ApplicationLog,
				$"[ERROR] UpdateCommon.inf is not exists: {updateCommonFile}",
				true));
			return diagnostics;
		}

		if (inspection.ReadError != null)
		{
			diagnostics.Add(new(
				InspectionDiagnosticDestination.ApplicationLog,
				$"iniファイル読み込みエラー: {inspection.ReadError}",
				true));
			return diagnostics;
		}

		foreach (UpdateCommonVersionEntry entry in inspection.Entries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			diagnostics.Add(new(
				InspectionDiagnosticDestination.Debug,
				$"Section={entry.Section}, Version={entry.Version}",
				false));
			switch (entry.Status)
			{
				case UpdateCommonVersionStatus.Mismatched:
					diagnostics.Add(new(
						InspectionDiagnosticDestination.ApplicationLog,
						$"[ERROR] {entry.Section} version mismatch: {entry.CurrentVersion} != {entry.Version}",
						true));
					break;
				case UpdateCommonVersionStatus.Matched:
					diagnostics.Add(new(
						InspectionDiagnosticDestination.ApplicationLog,
						$"[INFO] {entry.Section}=sameVersion ({entry.CurrentVersion} == {entry.Version})",
						false));
					break;
				case UpdateCommonVersionStatus.UnitNotFound:
					diagnostics.Add(new(
						InspectionDiagnosticDestination.ApplicationLog,
						$"[INFO] {entry.Section}=skip (not in inspection targets)",
						false));
					break;
				default:
					throw new ArgumentOutOfRangeException(
						nameof(entry.Status),
						entry.Status,
						"Unknown UpdateCommon version status.");
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return diagnostics;
	}
}
}
