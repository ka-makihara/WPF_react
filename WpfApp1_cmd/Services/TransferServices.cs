namespace WpfApp1_cmd.Services
{
using WpfLcuCtrlLib;

public sealed class LcuModuleTransferInspectionGateway(LcuCtrl lcuCtrl)
	: IModuleTransferInspectionGateway
{
	public Task<bool> DownloadFilesAsync(
		string machineName,
		string IpAddress,
		int modulePosition,
		IReadOnlyList<string> machineFiles,
		IReadOnlyList<string> lcuFiles,
		string targetDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFiles(
			machineName,
			IpAddress,
			modulePosition,
			machineFiles.ToList(),
			lcuFiles.ToList(),
			targetDirectory,
			cancellationToken);
}
}

namespace WpfApp1_cmd.Services
{
using WpfLcuCtrlLib;

public sealed class LcuPostMcFileTransferGateway(LcuCtrl lcuCtrl) : IPostMcFileTransferGateway
{
	public Task<string> SendAsync(
		PostMcFileTransferRequest request,
		CancellationToken cancellationToken)
	{
		string command = PostMcFile.Command(
			request.MachineName,
			request.ModulePosition,
			request.User,
			request.Password,
			request.MachineFiles.ToList(),
			request.LcuFiles.ToList());
		return lcuCtrl.LCU_Command(command, cancellationToken);
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;

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
}

namespace WpfApp1_cmd.Services
{
using System.Text.Json;

public sealed record PostMcFileResponse(
	bool HasError,
	bool HasErrorData,
	int ErrorIndex,
	string? ErrorCode,
	string? ErrorMessage);

public static class PostMcFileResponseParser
{
	public static PostMcFileResponse Parse(string response)
	{
		using JsonDocument document = JsonDocument.Parse(response);
		if (document.RootElement.ValueKind == JsonValueKind.Null)
		{
			return new PostMcFileResponse(false, false, -1, null, null);
		}

		if (!document.RootElement.TryGetProperty("ftp", out JsonElement ftp) ||
			ftp.ValueKind != JsonValueKind.Object ||
			!ftp.TryGetProperty("data", out JsonElement data) ||
			data.ValueKind == JsonValueKind.Null)
		{
			return new PostMcFileResponse(true, false, -1, null, null);
		}

		if (data.ValueKind != JsonValueKind.Array)
		{
			return new PostMcFileResponse(true, false, -1, null, null);
		}

		int index = 0;
		foreach (JsonElement item in data.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object ||
				!item.TryGetProperty("errorCode", out JsonElement errorCode) ||
				errorCode.ValueKind == JsonValueKind.Null ||
				(errorCode.ValueKind == JsonValueKind.String && errorCode.GetString()!.Length != 0))
			{
				string? errorCodeText = item.ValueKind == JsonValueKind.Object &&
					item.TryGetProperty("errorCode", out JsonElement code)
					? code.ToString()
					: null;
				string? errorMessage = item.ValueKind == JsonValueKind.Object &&
					item.TryGetProperty("errorMessage", out JsonElement message)
					? message.ToString()
					: null;
				return new PostMcFileResponse(true, true, index, errorCodeText, errorMessage);
			}

			index++;
		}

		return new PostMcFileResponse(false, data.GetArrayLength() > 0, -1, null, null);
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record PostMcFileTransferPresentation(
	bool Succeeded,
	ErrorCode? ErrorCode,
	bool CountTransferError,
	IReadOnlyList<string> LogMessages);

public sealed class PostMcFileTransferResultPresenter
{
	public PostMcFileTransferPresentation Create(
		PostMcFileTransferResult result,
		string lcuName,
		string machineName,
		string moduleName,
		string group,
		string accountErrorMessage)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(lcuName);
		ArgumentNullException.ThrowIfNull(machineName);
		ArgumentNullException.ThrowIfNull(moduleName);
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(accountErrorMessage);

		switch (result.Outcome)
		{
			case PostMcFileTransferOutcome.Succeeded:
				return new(true, null, false, []);
			case PostMcFileTransferOutcome.CredentialsMissing:
				return new(
					false,
					ErrorCode.FTP_ACCOUNT_ERROR,
					false,
					[$"[ERROR] {lcuName};{machineName};{moduleName}={accountErrorMessage}"]);
			case PostMcFileTransferOutcome.ConnectionFailed:
				return new(
					false,
					ErrorCode.LCU_CONNECT_ERROR,
					false,
					[$"[ERROR] {lcuName};{machineName};{moduleName}=WebAPI(PostMcFile) Error"]);
			case PostMcFileTransferOutcome.ResponseErrorDataMissing:
				return new(
					false,
					ErrorCode.FTP_TRANSFER_ERROR,
					true,
					[$"[ERROR] {lcuName};{machineName};{moduleName}=WebAPI(PostMcFile) Error:No Error Data"]);
			case PostMcFileTransferOutcome.TransferFailed:
				return new(
					false,
					ErrorCode.FTP_TRANSFER_ERROR,
					true,
					[
						$"[ERROR] {result.ErrorMessage} errCode={result.ErrorCode}",
						$"[Transfer] {lcuName};{machineName};{moduleName};{group}=NG:{result.ErrorMessage}"
					]);
			default:
				throw new ArgumentOutOfRangeException(
					nameof(result.Outcome),
					result.Outcome,
					"Unknown PostMcFile transfer outcome.");
		}
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record PostMcFileTransferRequest(
	string MachineName,
	int ModulePosition,
	string User,
	string Password,
	IReadOnlyList<string> MachineFiles,
	IReadOnlyList<string> LcuFiles);

public enum PostMcFileTransferOutcome
{
	Succeeded,
	CredentialsMissing,
	ConnectionFailed,
	ResponseErrorDataMissing,
	TransferFailed
}

public sealed record PostMcFileTransferResult(
	PostMcFileTransferOutcome Outcome,
	string? ErrorCode = null,
	string? ErrorMessage = null)
{
	public bool IsSuccess => Outcome == PostMcFileTransferOutcome.Succeeded;
}

public interface IPostMcFileTransferGateway
{
	Task<string> SendAsync(
		PostMcFileTransferRequest request,
		CancellationToken cancellationToken);
}

public sealed class PostMcFileTransferService
{
	public async Task<PostMcFileTransferResult> SendAsync(
		PostMcFileTransferRequest request,
		IPostMcFileTransferGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentNullException.ThrowIfNull(request.MachineFiles);
		ArgumentNullException.ThrowIfNull(request.LcuFiles);

		if (string.IsNullOrEmpty(request.User) || string.IsNullOrEmpty(request.Password))
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.CredentialsMissing);
		}

		cancellationToken.ThrowIfCancellationRequested();
		string response = await gateway.SendAsync(request, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (response.Length == 0 || response == "Internal Server Error")
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.ConnectionFailed);
		}

		PostMcFileResponse parsedResponse = PostMcFileResponseParser.Parse(response);
		if (!parsedResponse.HasError)
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.Succeeded);
		}

		if (!parsedResponse.HasErrorData)
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.ResponseErrorDataMissing);
		}

		return new PostMcFileTransferResult(
			PostMcFileTransferOutcome.TransferFailed,
			parsedResponse.ErrorCode,
			parsedResponse.ErrorMessage);
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TransferCompletionResultPresentation(
	string DialogTitle,
	string SuccessMessage,
	string FailureMessage,
	string InformationMessage);

public sealed class TransferCompletionResultPresenter
{
	public TransferCompletionResultPresentation Create(
		int successCount,
		int failureCount,
		string dialogTitle,
		string successLabel,
		string failureLabel,
		string informationMessage)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(successCount);
		ArgumentOutOfRangeException.ThrowIfNegative(failureCount);
		ArgumentNullException.ThrowIfNull(dialogTitle);
		ArgumentNullException.ThrowIfNull(successLabel);
		ArgumentNullException.ThrowIfNull(failureLabel);
		ArgumentNullException.ThrowIfNull(informationMessage);

		return new(
			dialogTitle,
			$"{successLabel}={successCount}",
			$"{failureLabel}={failureCount}",
			informationMessage);
	}

	public bool ShouldOpenResult(string dialogResult) =>
		dialogResult == "YES";
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TransferModuleWork(
	string? Name,
	bool IsSelected,
	bool HasUnitVersions,
	Func<CancellationToken, Task<bool>> Transfer);

public sealed record TransferMachineWork(
	string? Name,
	bool HasChildren,
	bool IsSelected,
	IReadOnlyList<TransferModuleWork> Modules);

public sealed record TransferLcuWork(
	string? Name,
	bool? IsSelected,
	bool HasChildren,
	bool HasController,
	Func<CancellationToken, Task<bool>>? UploadFiles,
	IReadOnlyList<TransferMachineWork> Machines);

public enum TransferExecutionFailure
{
	None,
	LcuUpload,
	UpdateInfoLoad,
	ModuleTransfer,
	Cancelled
}

public sealed record TransferExecutionResult(
	bool IsSuccess,
	TransferExecutionFailure Failure);

public sealed class TransferExecutionService
{
	public async Task<TransferExecutionResult> ExecuteAsync(
		IReadOnlyList<TransferLcuWork> lcus,
		Func<CancellationToken, Task<long>> loadModulesUpdateInfo,
		Action<long> onModulesLoaded,
		Action<string?> onLcuSkipped,
		Action<string?, string?> onMachineSkipped,
		Action<string?, string?, string?> onModuleSkipped,
		Action<string?, string?, string?> onModuleStarted,
		Action<string?, string?, string?> onModuleEnded,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		ArgumentNullException.ThrowIfNull(loadModulesUpdateInfo);
		ArgumentNullException.ThrowIfNull(onModulesLoaded);
		ArgumentNullException.ThrowIfNull(onLcuSkipped);
		ArgumentNullException.ThrowIfNull(onMachineSkipped);
		ArgumentNullException.ThrowIfNull(onModuleSkipped);
		ArgumentNullException.ThrowIfNull(onModuleStarted);
		ArgumentNullException.ThrowIfNull(onModuleEnded);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (TransferLcuWork lcu in lcus)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (lcu.IsSelected == false || lcu.UploadFiles == null)
				{
					continue;
				}

				bool uploadSucceeded = await lcu.UploadFiles(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!uploadSucceeded)
				{
					return new TransferExecutionResult(false, TransferExecutionFailure.LcuUpload);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			long moduleCount = await loadModulesUpdateInfo(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (moduleCount == 0)
			{
				return new TransferExecutionResult(false, TransferExecutionFailure.UpdateInfoLoad);
			}
			onModulesLoaded(moduleCount);
			cancellationToken.ThrowIfCancellationRequested();

			foreach (TransferLcuWork lcu in lcus)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!lcu.HasChildren || !lcu.HasController || lcu.IsSelected == false)
				{
					onLcuSkipped(lcu.Name);
					cancellationToken.ThrowIfCancellationRequested();
					continue;
				}

				foreach (TransferMachineWork machine in lcu.Machines)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!machine.HasChildren || machine.Name == null || !machine.IsSelected)
					{
						onMachineSkipped(lcu.Name, machine.Name);
						cancellationToken.ThrowIfCancellationRequested();
						continue;
					}

					foreach (TransferModuleWork module in machine.Modules)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (!module.IsSelected || !module.HasUnitVersions)
						{
							onModuleSkipped(lcu.Name, machine.Name, module.Name);
							cancellationToken.ThrowIfCancellationRequested();
							continue;
						}

						onModuleStarted(lcu.Name, machine.Name, module.Name);
						cancellationToken.ThrowIfCancellationRequested();
						bool succeeded = await module.Transfer(cancellationToken);
						cancellationToken.ThrowIfCancellationRequested();
						onModuleEnded(lcu.Name, machine.Name, module.Name);
						cancellationToken.ThrowIfCancellationRequested();
						if (!succeeded)
						{
							return new TransferExecutionResult(false, TransferExecutionFailure.ModuleTransfer);
						}
					}
				}

			}

			cancellationToken.ThrowIfCancellationRequested();
			return new TransferExecutionResult(true, TransferExecutionFailure.None);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new TransferExecutionResult(false, TransferExecutionFailure.Cancelled);
		}
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed class TransferPauseService
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	public async Task<bool> WaitUntilResumedAsync(
		Func<bool> isResumed,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(isResumed);

		while (true)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			if (isResumed())
			{
				return !cancellationToken.IsCancellationRequested;
			}

			try
			{
				await Task.Delay(PollInterval, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
		}
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TransferPreflightPresentation(
	bool ShowDialog,
	string? DialogTitle,
	string? Message,
	string? Detail);

public sealed class TransferPreflightResultPresenter
{
	public TransferPreflightPresentation Create(
		TransferPreflightOutcome outcome,
		string confirmTitle,
		string noTargetMessage,
		string targetSelectDetail)
	{
		ArgumentNullException.ThrowIfNull(confirmTitle);
		ArgumentNullException.ThrowIfNull(noTargetMessage);
		ArgumentNullException.ThrowIfNull(targetSelectDetail);

		return outcome switch
		{
			TransferPreflightOutcome.Ready => new(false, null, null, null),
			TransferPreflightOutcome.NoTargetSelected => new(
				true,
				confirmTitle,
				noTargetMessage,
				targetSelectDetail),
			_ => throw new ArgumentOutOfRangeException(
				nameof(outcome),
				outcome,
				"Unknown transfer preflight outcome.")
		};
	}
}
}

namespace WpfApp1_cmd.Services
{


public enum TransferPreflightOutcome
{
	Ready,
	NoTargetSelected
}

public sealed class TransferPreflightValidator
{
	public TransferPreflightOutcome Validate(
		TransferSelectionSummary selection)
	{
		ArgumentNullException.ThrowIfNull(selection);

		if (selection.LcuCount == 0
			&& selection.MachineCount == 0
			&& selection.ModuleCount == 0)
		{
			return TransferPreflightOutcome.NoTargetSelected;
		}

		return TransferPreflightOutcome.Ready;
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed class TransferProgressCalculator
{
	private const double UploadStageMaximum = 250.0;
	private const double ModuleStageStart = 250.0;
	private const double TotalProgressMaximum = 1000.0;
	private const double ModuleStageRange = TotalProgressMaximum - ModuleStageStart;

	public double CalculateUploadProgress(long transferredBytes, long totalBytes) =>
		totalBytes > 0
			? ((double)transferredBytes / totalBytes) * UploadStageMaximum
			: 0;

	public double CalculateModuleProgress(long transferredCount, long totalCount)
	{
		if (totalCount <= 0)
		{
			return ModuleStageStart;
		}

		double progress = ModuleStageStart
			+ ((double)transferredCount / totalCount) * ModuleStageRange;
		return Math.Min(progress, TotalProgressMaximum);
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TransferSelectionGroupInput(
	IReadOnlyList<string> Components,
	string? Mode);

public sealed class TransferSelectionGroupBuilder
{
	public IReadOnlyList<TransferSelectionGroup> Build(
		IReadOnlyList<TransferSelectionGroupInput> groups,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		cancellationToken.ThrowIfCancellationRequested();

		TransferSelectionGroup[] result = new TransferSelectionGroup[groups.Count];
		for (int index = 0; index < groups.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TransferSelectionGroupInput group = groups[index];
			string[] components = new string[group.Components.Count];
			for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				components[componentIndex] = group.Components[componentIndex];
			}

			result[index] = new TransferSelectionGroup(components, group.Mode);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;

public sealed record TransferSelectionUnit(
	bool IsSelected,
	string? Name,
	string Path,
	string? FuserPath);

public sealed record TransferSelectionModule(
	bool IsSelected,
	IReadOnlyList<TransferSelectionUnit> Units);

public sealed record TransferSelectionMachine(
	bool IsSelected,
	IReadOnlyList<TransferSelectionModule> Modules);

public sealed record TransferSelectionLcu(
	bool IsSelected,
	IReadOnlyList<TransferSelectionMachine> Machines);

public sealed record TransferSelectionGroup(
	IReadOnlyList<string> Components,
	string? Mode);

public sealed record TransferSelectionSummary(
	long LcuCount,
	long MachineCount,
	long ModuleCount,
	long UnitCount,
	long FileCount,
	IReadOnlyList<string> Paths);

public sealed class TransferSelectionSummaryBuilder
{
	public long CalculateFtpTransferSize(
		long updateDataSize,
		IReadOnlyList<TransferSelectionLcu> lcus)
	{
		ArgumentNullException.ThrowIfNull(lcus);

		int selectedLcuCount = 0;
		foreach (TransferSelectionLcu lcu in lcus)
		{
			if (lcu.IsSelected)
			{
				selectedLcuCount++;
			}
		}

		return CalculateFtpTransferSize(updateDataSize, selectedLcuCount);
	}

	public long CalculateFtpTransferSize(long updateDataSize, int selectedLcuCount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(selectedLcuCount);
		return updateDataSize * selectedLcuCount;
	}

	public int CountSelectedUnits(
		IReadOnlyList<TransferSelectionLcu> lcus,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		cancellationToken.ThrowIfCancellationRequested();

		int count = 0;
		foreach (TransferSelectionLcu lcu in lcus)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!lcu.IsSelected)
			{
				continue;
			}

			foreach (TransferSelectionMachine machine in lcu.Machines)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!machine.IsSelected)
				{
					continue;
				}

				foreach (TransferSelectionModule module in machine.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!module.IsSelected)
					{
						continue;
					}

					foreach (TransferSelectionUnit unit in module.Units)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (unit.IsSelected)
						{
							count++;
						}
					}
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return count;
	}

	public TransferSelectionSummary Build(
		IReadOnlyList<TransferSelectionLcu> lcus,
		IReadOnlyList<TransferSelectionGroup> groups,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		ArgumentNullException.ThrowIfNull(groups);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, TransferSelectionGroup> groupsByComponent = new(StringComparer.Ordinal);
		TransferSelectionGroup? firstGroupWithNullComponent = null;
		foreach (TransferSelectionGroup group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in group.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component == null)
				{
					firstGroupWithNullComponent ??= group;
				}
				else
				{
					groupsByComponent.TryAdd(component, group);
				}
			}
		}

		long lcuCount = 0;
		long machineCount = 0;
		long moduleCount = 0;
		long unitCount = 0;
		long fileCount = 0;
		List<string> paths = [];
		HashSet<string> uniquePaths = new(StringComparer.Ordinal);
		Dictionary<string, string[]> folderFilesByDirectory =
			new(StringComparer.OrdinalIgnoreCase);

		foreach (TransferSelectionLcu lcu in lcus)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!lcu.IsSelected)
			{
				continue;
			}
			lcuCount++;

			foreach (TransferSelectionMachine machine in lcu.Machines)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!machine.IsSelected)
				{
					continue;
				}
				machineCount++;

				foreach (TransferSelectionModule module in machine.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!module.IsSelected)
					{
						continue;
					}
					moduleCount++;

					foreach (TransferSelectionUnit unit in module.Units)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (!unit.IsSelected)
						{
							continue;
						}
						unitCount++;

						TransferSelectionGroup? group = unit.Name == null
							? firstGroupWithNullComponent
							: groupsByComponent.GetValueOrDefault(unit.Name);
						if (group?.Mode == "Folder")
						{
							string sourcePath = (dataFolder + unit.Path).Replace("/", "\\");
							string directory = Path.GetDirectoryName(sourcePath) ?? ".\\";
							if (!folderFilesByDirectory.TryGetValue(directory, out string[]? folderFiles))
							{
								List<string> files = [];
								foreach (string file in Directory.EnumerateFiles(
									directory,
									"*",
									SearchOption.AllDirectories))
								{
									cancellationToken.ThrowIfCancellationRequested();
									files.Add(file);
								}

								cancellationToken.ThrowIfCancellationRequested();
								folderFiles = files.ToArray();
								folderFilesByDirectory.Add(directory, folderFiles);
							}

							foreach (string file in folderFiles)
							{
								cancellationToken.ThrowIfCancellationRequested();
								if (uniquePaths.Add(file))
								{
									fileCount++;
									paths.Add(file);
								}
							}
							continue;
						}

						string unitPath = dataFolder + unit.Path.Replace("/", "\\");
						if (uniquePaths.Add(unitPath))
						{
							paths.Add(unitPath);
						}
						fileCount++;
						if (!string.IsNullOrEmpty(unit.FuserPath))
						{
							string fuserPath = dataFolder + unit.FuserPath.Replace("/", "\\");
							if (uniquePaths.Add(fuserPath))
							{
								paths.Add(fuserPath);
							}
							fileCount++;
						}
					}
				}
			}
		}

		string[] resultPaths = new string[paths.Count];
		for (int index = 0; index < paths.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			resultPaths[index] = paths[index];
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(
			lcuCount,
			machineCount,
			moduleCount,
			unitCount,
			fileCount,
			resultPaths);
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TransferSelectionUnitInput(
	bool? IsSelected,
	string? Name,
	string Path,
	string? FuserPath,
	bool IsSupported = true,
	bool IsUpdateProhibited = false);

public sealed record TransferSelectionModuleInput(
	bool? IsSelected,
	IReadOnlyList<TransferSelectionUnitInput> Units);

public sealed record TransferSelectionMachineInput(
	bool? IsSelected,
	IReadOnlyList<TransferSelectionModuleInput> Modules);

public sealed record TransferSelectionLcuInput(
	bool? IsSelected,
	IReadOnlyList<TransferSelectionMachineInput> Machines);

public sealed class TransferSelectionTreeBuilder
{
	public IReadOnlyList<TransferSelectionLcu> Build(
		IReadOnlyList<TransferSelectionLcuInput> lcus,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		cancellationToken.ThrowIfCancellationRequested();

		TransferSelectionLcu[] result = new TransferSelectionLcu[lcus.Count];
		for (int lcuIndex = 0; lcuIndex < lcus.Count; lcuIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TransferSelectionLcuInput lcu = lcus[lcuIndex];
			TransferSelectionMachine[] machines =
				new TransferSelectionMachine[lcu.Machines.Count];
			for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				TransferSelectionMachineInput machine = lcu.Machines[machineIndex];
				TransferSelectionModule[] modules =
					new TransferSelectionModule[machine.Modules.Count];
				for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					TransferSelectionModuleInput module = machine.Modules[moduleIndex];
					TransferSelectionUnit[] units =
						new TransferSelectionUnit[module.Units.Count];
					for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
					{
						cancellationToken.ThrowIfCancellationRequested();
						TransferSelectionUnitInput unit = module.Units[unitIndex];
						units[unitIndex] = new TransferSelectionUnit(
							unit.IsSupported && !unit.IsUpdateProhibited && unit.IsSelected == true,
							unit.Name,
							unit.Path,
							unit.FuserPath);
					}

					modules[moduleIndex] = new TransferSelectionModule(
						module.IsSelected != false,
						units);
				}

				machines[machineIndex] = new TransferSelectionMachine(
					machine.IsSelected != false,
					modules);
			}

			result[lcuIndex] = new TransferSelectionLcu(
				lcu.IsSelected != false,
				machines);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TransferStartConfirmationPresentation(
	string DialogTitle,
	string DialogText);

public sealed class TransferStartConfirmationPresenter
{
	public TransferStartConfirmationPresentation Create(
		long moduleCount,
		string confirmTitle,
		string question)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(moduleCount);
		ArgumentNullException.ThrowIfNull(confirmTitle);
		ArgumentNullException.ThrowIfNull(question);

		return new(
			confirmTitle,
			$"{moduleCount} Module\n{question}");
	}

	public bool ShouldStart(object? dialogResult) =>
		dialogResult != null && dialogResult.ToString() != "CANCEL";

	public bool ShouldStopTransfer(object? dialogResult) =>
		dialogResult != null && dialogResult.ToString() != "CANCEL";
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TransferModuleNode<TModule>(
	string? Name,
	bool? IsSelected,
	bool HasUnitVersions,
	TModule Context);

public sealed record TransferMachineNode<TMachine, TModule>(
	string? Name,
	bool HasChildren,
	bool? IsSelected,
	TMachine Context,
	IReadOnlyList<TransferModuleNode<TModule>> Modules);

public sealed record TransferLcuNode<TLcu, TMachine, TModule>(
	string? Name,
	bool? IsSelected,
	bool HasChildren,
	bool HasController,
	TLcu Context,
	IReadOnlyList<TransferMachineNode<TMachine, TModule>> Machines);

public sealed class TransferWorkBuilder
{
	public IReadOnlyList<TransferLcuWork> Build<TLcu, TMachine, TModule>(
		IReadOnlyList<TransferLcuNode<TLcu, TMachine, TModule>> lcus,
		Func<TLcu, CancellationToken, Task<bool>>? uploadFiles,
		Func<TLcu, TMachine, TModule, CancellationToken, Task<bool>> transferModule,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		ArgumentNullException.ThrowIfNull(transferModule);
		cancellationToken.ThrowIfCancellationRequested();

		TransferLcuWork[] work = new TransferLcuWork[lcus.Count];
		for (int lcuIndex = 0; lcuIndex < lcus.Count; lcuIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TransferLcuNode<TLcu, TMachine, TModule> lcu = lcus[lcuIndex];
			TransferMachineWork[] machines = new TransferMachineWork[lcu.Machines.Count];
			for (int machineIndex = 0; machineIndex < lcu.Machines.Count; machineIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				TransferMachineNode<TMachine, TModule> machine = lcu.Machines[machineIndex];
				TransferModuleWork[] modules = new TransferModuleWork[machine.Modules.Count];
				for (int moduleIndex = 0; moduleIndex < machine.Modules.Count; moduleIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					TransferModuleNode<TModule> module = machine.Modules[moduleIndex];
					modules[moduleIndex] = new TransferModuleWork(
						module.Name,
						module.IsSelected != false,
						module.HasUnitVersions,
						token => transferModule(
							lcu.Context,
							machine.Context,
							module.Context,
							token));
				}

				machines[machineIndex] = new TransferMachineWork(
					machine.Name,
					machine.HasChildren,
					machine.IsSelected != false,
					modules);
			}

			work[lcuIndex] = new TransferLcuWork(
				lcu.Name,
				lcu.IsSelected,
				lcu.HasChildren,
				lcu.HasController,
				uploadFiles == null ? null : token => uploadFiles(lcu.Context, token),
				machines);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return work;
	}
}
}
