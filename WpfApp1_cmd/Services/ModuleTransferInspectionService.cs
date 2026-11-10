using System.IO;

namespace WpfApp1_cmd.Services;

// =============================================================================
// ModuleTransferInspectionService
// -----------------------------------------------------------------------------
// 役割:
//   モジュールへ転送したファイルが正しく配置されたかを検証するサービス。
//
// 検証方法は 2 種類:
//   A. 内容比較 (InspectAsync → InspectFiles → CompareFiles)
//      転送したファイルを装置から検査用フォルダへダウンロードし直し、
//      更新データ側の元ファイルとバイト単位で比較する。
//   B. 存在確認 (InspectTransferredDirectory / InspectTransferredFiles)
//      転送先 (ダウンロードしたフォルダ、またはファイル一覧) に、
//      選択された各ユニットの Path / FuserPath が存在するかを確認する。
//
// 備考:
//   - 装置からのダウンロードは IModuleTransferInspectionGateway に委譲する。
// =============================================================================

#region 型定義

/// <summary>検証用に装置からファイルをダウンロードする通信窓口。</summary>
public interface IModuleTransferInspectionGateway
{
	/// <summary>装置のファイル群を targetDirectory 配下へダウンロードする。</summary>
	Task<bool> DownloadFilesAsync(
		string machineName,
		string IpAddress,
		int modulePosition,
		IReadOnlyList<string> machineFiles,
		IReadOnlyList<string> lcuFiles,
		string targetDirectory,
		CancellationToken cancellationToken);
}

/// <summary>
/// 内容比較による検証の要求。
/// </summary>
/// <param name="MachineFiles">検証する装置側の相対パス一覧。</param>
/// <param name="LcuFiles">MachineFiles に対応する LCU 側パス一覧。</param>
/// <param name="Units">ファイルとユニット名の対応付けに使うユニット一覧。</param>
/// <param name="DataFolder">比較元 (更新データ) のルート。</param>
/// <param name="InspectionFolder">ダウンロード先 (比較対象) のルート。</param>
public sealed record ModuleTransferInspectionRequest(
	IReadOnlyList<string> MachineFiles,
	IReadOnlyList<string> LcuFiles,
	IReadOnlyList<ModuleInspectionUnit> Units,
	string DataFolder,
	string InspectionFolder);

/// <summary>検証対象の装置とモジュール位置。</summary>
public sealed record ModuleTransferInspectionContext(
	string MachineName,
	string IpAddress,
	int ModulePosition);

/// <summary>内容比較の結果。ダウンロードに失敗した場合 FileResults は空。</summary>
public sealed record ModuleTransferInspectionResult(
	bool DownloadSucceeded,
	IReadOnlyList<ModuleFileInspectionResult> FileResults);

/// <summary>ファイルとユニットの対応付けに使うユニット情報。</summary>
public sealed record ModuleInspectionUnit(string Name, string? Path, string? FusePath);

/// <summary>1 ファイル分の内容比較結果。対応するユニットが無い場合 UnitName は空文字。</summary>
public sealed record ModuleFileInspectionResult(string UnitName, string FilePath, bool IsMatch);

/// <summary>存在確認の対象ユニット。IsSelected=false のユニットは対象外。</summary>
public sealed record TransferredUnitFile(
	string? UnitName,
	string? Path,
	string? FuserPath,
	bool? IsSelected);

/// <summary>1 ファイル分の存在確認結果。IsFuserPath=true は FuserPath 側のファイル。</summary>
public sealed record TransferredFilePresence(
	string? UnitName,
	string? Path,
	bool IsFuserPath,
	bool Exists);

#endregion

/// <summary>
/// モジュールへ転送したファイルの内容比較・存在確認を行うサービス。
/// </summary>
public sealed class ModuleTransferInspectionService
{
	#region A. 内容比較

	/// <summary>
	/// 転送したファイルを装置から InspectionFolder へダウンロードし、元ファイルと内容を比較する。
	/// ダウンロードに失敗した場合は比較を行わず DownloadSucceeded=false を返す。
	/// </summary>
	public async Task<ModuleTransferInspectionResult> InspectAsync(
		ModuleTransferInspectionRequest request,
		ModuleTransferInspectionContext context,
		IModuleTransferInspectionGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentNullException.ThrowIfNull(request.MachineFiles);
		ArgumentNullException.ThrowIfNull(request.LcuFiles);
		ArgumentNullException.ThrowIfNull(request.Units);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.DataFolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.InspectionFolder);

		cancellationToken.ThrowIfCancellationRequested();
		bool downloadSucceeded = await gateway.DownloadFilesAsync(
			context.MachineName,
			context.IpAddress,
			context.ModulePosition,
			request.MachineFiles,
			request.LcuFiles,
			request.InspectionFolder,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!downloadSucceeded)
		{
			return new ModuleTransferInspectionResult(false, []);
		}

		IReadOnlyList<ModuleFileInspectionResult> fileResults = await Task.Run(
			() => InspectFiles(
				request.MachineFiles,
				request.Units,
				request.DataFolder,
				request.InspectionFolder,
				cancellationToken),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return new ModuleTransferInspectionResult(true, fileResults);
	}

	/// <summary>
	/// 各ファイルについて「dataFolder + ファイル」と「inspectionFolder + ファイル」の内容を比較する。
	/// ユニット名は、Peripheral 以降の相対パスにユニットの Path / FusePath を含む最初のユニットとする。
	/// </summary>
	public IReadOnlyList<ModuleFileInspectionResult> InspectFiles(
		IReadOnlyList<string> transferredFiles,
		IReadOnlyList<ModuleInspectionUnit> units,
		string dataFolder,
		string inspectionFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(transferredFiles);
		ArgumentNullException.ThrowIfNull(units);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(inspectionFolder);

		cancellationToken.ThrowIfCancellationRequested();
		(string Name, string? Path, string? FusePath)[] unitPaths =
			new (string Name, string? Path, string? FusePath)[units.Count];
		for (int index = 0; index < units.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ModuleInspectionUnit unit = units[index];
			unitPaths[index] = (
				unit.Name,
				string.IsNullOrEmpty(unit.Path) ? null : GetPeripheralRelativePath(unit.Path),
				string.IsNullOrEmpty(unit.FusePath) ? null : GetPeripheralRelativePath(unit.FusePath));
		}
		cancellationToken.ThrowIfCancellationRequested();

		List<ModuleFileInspectionResult> results = [];
		foreach (string file in transferredFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string sourcePath = (dataFolder + file).Replace("/", "\\");
			string inspectedPath = (inspectionFolder + file).Replace("/", "\\");
			string relativeFile = GetPeripheralRelativePath(file);
			string unitName = "";
			foreach ((string? name, string? path, string? fusePath) in unitPaths)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if ((path != null && relativeFile.Contains(path)) ||
					(fusePath != null && relativeFile.Contains(fusePath)))
				{
					unitName = name ?? "";
					break;
				}
			}

			results.Add(new ModuleFileInspectionResult(
				unitName,
				file,
				CompareFiles(sourcePath, inspectedPath, cancellationToken) == 0));
		}

		return results;
	}

	/// <summary>
	/// 2 つのファイルをバイト単位で比較する。
	/// 戻り値: 0 = 一致、-1 = どちらかが無い/サイズ不一致、正数 = 最初に異なるバイト位置 (1 始まり)。
	/// </summary>
	public static int CompareFiles(
		string file1,
		string file2,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(file1);
		ArgumentException.ThrowIfNullOrWhiteSpace(file2);
		cancellationToken.ThrowIfCancellationRequested();
		if (!File.Exists(file1) || !File.Exists(file2))
		{
			return -1;
		}

		using FileStream first = new(
			file1,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			bufferSize: 64 * 1024,
			FileOptions.SequentialScan);
		using FileStream second = new(
			file2,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			bufferSize: 64 * 1024,
			FileOptions.SequentialScan);
		if (first.Length != second.Length)
		{
			return -1;
		}

		byte[] firstBuffer = new byte[64 * 1024];
		byte[] secondBuffer = new byte[firstBuffer.Length];
		int position = 0;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
			int secondRead = second.Read(secondBuffer, 0, secondBuffer.Length);
			if (firstRead != secondRead)
			{
				return -1;
			}
			if (firstRead == 0)
			{
				return 0;
			}

			for (int index = 0; index < firstRead; index++)
			{
				if (firstBuffer[index] != secondBuffer[index])
				{
					return position + index + 1;
				}
			}

			position += firstRead;
		}
	}

	/// <summary>パス中の "Peripheral" より後ろの部分を返す ("Peripheral" が無ければそのまま)。</summary>
	private static string GetPeripheralRelativePath(string path)
	{
		int peripheralIndex = path.IndexOf("Peripheral", StringComparison.Ordinal);
		return peripheralIndex < 0
			? path
			: path[(peripheralIndex + "Peripheral".Length)..];
	}

	#endregion

	#region B. 存在確認

	/// <summary>
	/// directory 配下の全ファイルを "/相対パス" 形式で列挙し、各ユニットのファイルが存在するか確認する。
	/// </summary>
	public IReadOnlyList<TransferredFilePresence> InspectTransferredDirectory(
		string directory,
		IReadOnlyList<TransferredUnitFile> units,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		ArgumentNullException.ThrowIfNull(units);

		cancellationToken.ThrowIfCancellationRequested();
		HashSet<string?> transferredFiles = new(StringComparer.Ordinal);
		foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
		{
			cancellationToken.ThrowIfCancellationRequested();
			transferredFiles.Add("/" + Path.GetRelativePath(directory, file).Replace("\\", "/"));
		}

		return InspectPresence(transferredFiles, units, cancellationToken);
	}

	/// <summary>
	/// 転送済みファイル一覧に、各ユニットのファイルが含まれるか確認する。
	/// </summary>
	public IReadOnlyList<TransferredFilePresence> InspectTransferredFiles(
		IReadOnlyList<string> transferredFiles,
		IReadOnlyList<TransferredUnitFile> units,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(transferredFiles);
		ArgumentNullException.ThrowIfNull(units);

		cancellationToken.ThrowIfCancellationRequested();
		HashSet<string?> fileSet = new(StringComparer.Ordinal);
		foreach (string file in transferredFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			fileSet.Add(file);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return InspectPresence(fileSet, units, cancellationToken);
	}

	/// <summary>
	/// 選択中かつ名前のあるユニットについて、Path と FuserPath (あれば) の存在結果を作る。
	/// </summary>
	private static IReadOnlyList<TransferredFilePresence> InspectPresence(
		HashSet<string?> fileSet,
		IReadOnlyList<TransferredUnitFile> units,
		CancellationToken cancellationToken)
	{
		List<TransferredFilePresence> results = [];
		foreach (TransferredUnitFile unit in units)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (unit.IsSelected == false || string.IsNullOrEmpty(unit.UnitName))
			{
				continue;
			}

			results.Add(new(
				unit.UnitName,
				unit.Path,
				false,
				fileSet.Contains(unit.Path)));
			if (!string.IsNullOrEmpty(unit.FuserPath))
			{
				results.Add(new(
					unit.UnitName,
					unit.FuserPath,
					true,
					fileSet.Contains(unit.FuserPath)));
			}
		}

		return results;
	}

	#endregion
}
