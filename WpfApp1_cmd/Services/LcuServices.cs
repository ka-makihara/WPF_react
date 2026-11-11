namespace WpfApp1_cmd.Services
{
using WpfLcuCtrlLib;

public sealed class LcuCtrlInformationGateway(LcuCtrl lcuCtrl) : ILcuInformationGateway
{
	public async Task<LcuFtpAccount?> GetFtpAccountAsync(
		CancellationToken cancellationToken)
	{
		string response = await lcuCtrl.LCU_Command(FtpData.Command(), cancellationToken);
		FtpData? account = FtpData.FromJson(response);
		if (account == null)
		{
			return null;
		}

		string? password = account.username != null && account.password != null
			? FtpData.GetPasswd(account.username, account.password)
			: account.password;
		return new(account.username, password);
	}

	public async Task<IReadOnlyList<LcuVersionEntry>?> GetVersionAsync(
		CancellationToken cancellationToken)
	{
		string response = await lcuCtrl.LCU_Command(LcuVersion.Command(), cancellationToken);
		List<LcuVersion>? versions = LcuVersion.FromJson(response);
		return versions?
			.Select(version => new LcuVersionEntry(version.itemName, version.itemVersion))
			.ToArray();
	}

	public async Task<IReadOnlyList<LcuDiskDrive>?> GetDiskInfoAsync(
		CancellationToken cancellationToken)
	{
		List<LcuDiskInfo>? drives = await lcuCtrl.LCU_DiskInfo(cancellationToken);
		return drives?
			.Select(drive => new LcuDiskDrive(drive.driveLetter, drive.free))
			.ToArray();
	}
}
}

namespace WpfApp1_cmd.Services
{
using WpfLcuCtrlLib;

public sealed class LcuCtrlLineInfoGateway(LcuCtrl lcuCtrl) : ILcuLineInfoGateway
{
	public Task<string> GetLinesAsync(CancellationToken cancellationToken) =>
		lcuCtrl.LCU_HttpGet("lines", cancellationToken);
}
}

namespace WpfApp1_cmd.Services
{


public sealed record LcuFileUploadPresentation(
	bool Succeeded,
	bool Cancelled,
	IReadOnlyList<string> LogMessages);

public sealed class LcuFileUploadResultPresenter
{
	public LcuFileUploadPresentation Create(
		LcuUnitFileUploadResult result,
		string lcuName,
		CancellationToken cancellationToken = default,
		bool includeOutcomeMessages = true)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(lcuName);
		cancellationToken.ThrowIfCancellationRequested();

		switch (result.Outcome)
		{
			case LcuUnitFileUploadOutcome.Succeeded:
				return new(
					true,
					false,
					includeOutcomeMessages
						? [$"[Transfer] {lcuName}=UploadFiles End"]
						: []);
			case LcuUnitFileUploadOutcome.Cancelled:
				return new(
					false,
					true,
					includeOutcomeMessages
						? [$"[Transfer] {lcuName}=UploadFiles Cancelled"]
						: []);
			case LcuUnitFileUploadOutcome.Failed:
				List<string> messages = [];
				if (result.Error != null)
				{
					messages.Add(
						$"[Transfer] {lcuName}=UploadFilesWithFolder Error({result.Error.Message})");
				}
				if (includeOutcomeMessages)
				{
					messages.Add($"[Transfer] {lcuName}=UploadFilesWithFolder Error");
				}
				return new(false, false, messages);
			default:
				throw new ArgumentOutOfRangeException(
					nameof(result.Outcome),
					result.Outcome,
					"Unknown LCU upload outcome.");
		}
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;

public sealed record LcuFolderUploadPresentation(
	string Message,
	double Progress,
	string DebugMessage);

public sealed class LcuFolderUploadProgressPresenter(
	TransferProgressCalculator progressCalculator)
{
	private readonly TransferProgressCalculator _progressCalculator =
		progressCalculator ?? throw new ArgumentNullException(nameof(progressCalculator));

	public LcuFolderUploadPresentation Create(
		string lcuName,
		LcuFolderUploadProgress progress,
		long totalBytes)
	{
		ArgumentNullException.ThrowIfNull(lcuName);
		ArgumentNullException.ThrowIfNull(progress);

		return new(
			$"Transfering {lcuName}:{Path.GetFileName(progress.LocalFolder)}",
			_progressCalculator.CalculateUploadProgress(
				progress.TotalTransferredBytes,
				totalBytes),
			$"[FTP Upload] {progress.LocalFolder} => {lcuName}/{progress.RemoteFolder} ({progress.FolderBytes} bytes)");
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;
using WpfLcuCtrlLib;

public sealed record LcuFolderUploadProgress(
	string LocalFolder,
	string RemoteFolder,
	long FolderBytes,
	long TotalTransferredBytes);

public enum LcuFolderUploadOutcome
{
	Succeeded,
	Cancelled
}

public sealed record LcuFolderUploadResult(
	LcuFolderUploadOutcome Outcome,
	long TotalTransferredBytes);

public sealed class LcuFolderUploadService(IFileTransferClient fileTransferClient)
{
	/// <summary>モジュール転送前に必要ファイルを再配置する。共有ファイルも毎回上書きする。</summary>
	public async Task UploadModuleFilesAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<FileTransferRequest> files,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentNullException.ThrowIfNull(files);
		cancellationToken.ThrowIfCancellationRequested();
		IFileTransferSession session = await fileTransferClient.ConnectAsync(
			host, user, password, cancellationToken);
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (FileTransferRequest file in files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				FileTransferResult result = await session.UploadFileAsync(
					file.LocalPath, file.RemotePath, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!result.IsSuccess)
				{
					throw new IOException($"FTP staging failed: {file.LocalPath} -> {file.RemotePath}");
				}
			}
		}
		finally
		{
			await session.DisconnectAsync(CancellationToken.None);
		}
	}

	/// <summary>モジュール成功後に過去の残存分を含めMCFiles配下を空にする。MCFiles自体は残す。</summary>
	public async Task ClearModuleFilesAsync(
		string host,
		string user,
		string password,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		cancellationToken.ThrowIfCancellationRequested();
		IFileTransferSession session = await fileTransferClient.ConnectAsync(
			host, user, password, cancellationToken);
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await session.ClearStagingDirectoryAsync(cancellationToken);
		}
		finally
		{
			await session.DisconnectAsync(CancellationToken.None);
		}
	}

	public async Task<LcuFolderUploadResult> UploadAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<string> folders,
		string localRoot,
		string remoteRoot,
		Func<LcuFolderUploadProgress, Task> onFolderUploaded,
		Func<CancellationToken, Task<bool>> canContinue,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentNullException.ThrowIfNull(folders);
		ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);
		ArgumentNullException.ThrowIfNull(remoteRoot);
		ArgumentNullException.ThrowIfNull(onFolderUploaded);
		ArgumentNullException.ThrowIfNull(canContinue);

		cancellationToken.ThrowIfCancellationRequested();
		IFileTransferSession session = await fileTransferClient.ConnectAsync(
			host,
			user,
			password,
			cancellationToken);
		long totalTransferredBytes = 0;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			string normalizedLocalRoot = Path.GetFullPath(localRoot);
			foreach (string folder in folders)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string normalizedFolder = Path.GetFullPath(folder);
				string relativeFolder = Path.GetRelativePath(normalizedLocalRoot, normalizedFolder);
				if (Path.IsPathRooted(relativeFolder)
					|| relativeFolder == ".."
					|| relativeFolder.StartsWith(
						$"..{Path.DirectorySeparatorChar}",
						StringComparison.Ordinal)
					|| relativeFolder.StartsWith(
						$"..{Path.AltDirectorySeparatorChar}",
						StringComparison.Ordinal))
				{
					throw new ArgumentException($"Folder '{folder}' is outside local root '{localRoot}'.", nameof(folders));
				}

				string remoteFolder = relativeFolder == "."
					? remoteRoot
					: remoteRoot + Path.DirectorySeparatorChar + relativeFolder;
				while (true)
				{
					IReadOnlyList<FileTransferResult> results = await session.UploadDirectoryAsync(
						folder,
						remoteFolder,
						cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();
					bool hasFailure = results.Any(result => !result.IsSuccess && !result.IsSkipped);
					if (!hasFailure)
					{
						break;
					}

					cancellationToken.ThrowIfCancellationRequested();
				}

				long folderBytes = await Task.Run(
					() => UpdateDataLoadService.GetDirectorySize(
						new DirectoryInfo(folder),
						cancellationToken),
					cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				totalTransferredBytes += folderBytes;
				await onFolderUploaded(new LcuFolderUploadProgress(
					folder,
					remoteFolder,
					folderBytes,
					totalTransferredBytes));
				cancellationToken.ThrowIfCancellationRequested();

				bool shouldContinue = await canContinue(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!shouldContinue)
				{
					return new LcuFolderUploadResult(
						LcuFolderUploadOutcome.Cancelled,
						totalTransferredBytes);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			return new LcuFolderUploadResult(
				LcuFolderUploadOutcome.Succeeded,
				totalTransferredBytes);
		}
		finally
		{
			await session.DisconnectAsync(CancellationToken.None);
		}
	}
}
}

namespace WpfApp1_cmd.Services
{


// =============================================================================
// LcuInformationService
// -----------------------------------------------------------------------------
// 役割:
//   LCU の FTP アカウント・通信サービスのバージョン・空き容量を取得し、
//   ディスク容量確認ダイアログの表示内容を作る。
// 処理の流れ:
//   1. GetFtpAccountAsync          : 応答と資格情報を確認し、ユーザー名のドメインを除く。
//   2. GetVersionAsync             : Fuji LCU Communication Server Service の版を取り出す。
//   3. GetDiskSpaceAsync           : D ドライブの空き容量を数値へ変換する。
//   4. CreateDiskSpacePresentation : 空き容量または取得失敗の表示・ログを作る。
// 備考:
//   - 通信は ILcuInformationGateway に委譲し、取得不可と正常値を結果型で区別する。
// =============================================================================

#region 型定義

public sealed record LcuFtpAccount(string? Username, string? Password);

public sealed record LcuVersionEntry(string? ItemName, string? ItemVersion);

public sealed record LcuDiskDrive(string? DriveLetter, string? FreeSpace);

public interface ILcuInformationGateway
{
	Task<LcuFtpAccount?> GetFtpAccountAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<LcuVersionEntry>?> GetVersionAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<LcuDiskDrive>?> GetDiskInfoAsync(CancellationToken cancellationToken);
}

public enum LcuFtpAccountStatus
{
	Available,
	ResponseError,
	CredentialsError
}

public sealed record LcuFtpAccountResult(
	LcuFtpAccountStatus Status,
	string? Username = null,
	string? Password = null);

public sealed record LcuVersionResult(bool IsAvailable, string? Version);

public sealed record LcuDiskSpaceResult(bool IsAvailable, long FreeSpace);

public sealed record LcuDiskSpaceCheckPresentation(
	bool IsAvailable,
	string DialogTitle,
	string DialogMessage,
	string DialogDetail,
	bool IsError,
	IReadOnlyList<string> LogMessages);

#endregion

/// <summary>LCU の基本情報取得と、ディスク容量確認の表示内容を扱う。</summary>
public sealed class LcuInformationService(ILcuInformationGateway gateway)
{
	#region 1. FTP アカウント

	/// <summary>FTP アカウントを取得し、ドメイン付きユーザー名を正規化する。</summary>
	public async Task<LcuFtpAccountResult> GetFtpAccountAsync(
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		LcuFtpAccount? account = await gateway.GetFtpAccountAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (account == null)
		{
			return new(LcuFtpAccountStatus.ResponseError);
		}

		if (account.Username == null || account.Password == null)
		{
			return new(LcuFtpAccountStatus.CredentialsError);
		}

		string username = account.Username.Contains('\\')
			? account.Username.Split('\\')[1]
			: account.Username;
		return new(LcuFtpAccountStatus.Available, username, account.Password);
	}

	#endregion

	#region 2. バージョン

	/// <summary>LCU 通信サービスのバージョンを取得する。</summary>
	public async Task<LcuVersionResult> GetVersionAsync(
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<LcuVersionEntry>? versions =
			await gateway.GetVersionAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (versions == null)
		{
			return new(false, null);
		}

		LcuVersionEntry? versionEntry = versions.FirstOrDefault(
			item => item.ItemName == "Fuji LCU Communication Server Service");
		if (versionEntry == null || versionEntry.ItemVersion == null)
		{
			return new(false, null);
		}

		string version = versionEntry.ItemVersion;
		return new(true, version);
	}

	#endregion

	#region 3. ディスク容量

	/// <summary>D ドライブの空き容量をバイト単位で取得する。</summary>
	public async Task<LcuDiskSpaceResult> GetDiskSpaceAsync(
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<LcuDiskDrive>? drives =
			await gateway.GetDiskInfoAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (drives == null)
		{
			return new(false, 0);
		}

		LcuDiskDrive? dDrive = drives.FirstOrDefault(
			drive => drive.DriveLetter == "D");
		if (dDrive?.FreeSpace == null ||
			!long.TryParse(dDrive.FreeSpace, out long freeSpace))
		{
			return new(false, 0);
		}

		return new(true, freeSpace);
	}

	#endregion

	#region 4. ディスク容量の表示

	/// <summary>容量取得結果から、確認ダイアログとログの内容を作る。</summary>
	public static LcuDiskSpaceCheckPresentation CreateDiskSpacePresentation(
		LcuDiskSpaceResult result,
		string? lcuName,
		ErrorCode errorCode,
		string errorTitle,
		string confirmTitle,
		string errorMessage)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(errorTitle);
		ArgumentNullException.ThrowIfNull(confirmTitle);
		ArgumentNullException.ThrowIfNull(errorMessage);

		if (!result.IsAvailable)
		{
			return new(
				false,
				errorTitle,
				$"ErrorCode=0x{(int)errorCode:X}",
				errorMessage,
				true,
				[]);
		}

		long diskMegabytes = result.FreeSpace / 1024 / 1024;
		return new(
			true,
			confirmTitle,
			$"{lcuName}",
			$"DiskSpace={diskMegabytes} MB",
			false,
			[$"{lcuName}::DiskSpace={result.FreeSpace}"]);
	}

	#endregion
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;
using System.Net.Http;
using System.Xml.Serialization;

// =============================================================================
// LcuLineInfoService
// -----------------------------------------------------------------------------
// 役割:
//   LCU のライン情報取得・応答解析・順次読込と、結果の診断文生成をまとめる。
// 処理の流れ:
//   1. LoadSequentiallyAsync : LCU を順に読み込み、開始・完了・例外を呼出し元へ通知する。
//   2. GetLineInfoAsync      : 通信窓口から応答を取得し、ReadResponse で XML を解析する。
//   3. Create*              : DB取得・ライン取得・ツリー構築の進捗と診断文を作る。
// 備考:
//   - 通信は ILcuLineInfoGateway、画面操作はコールバックへ委譲する。
//   - HTTP例外時は次のLCUへ継続し、その他の例外・キャンセル時は読込を中断する。
// =============================================================================

#region 型定義

public interface ILcuLineInfoGateway
{
	Task<string> GetLinesAsync(CancellationToken cancellationToken);
}

public enum LcuLineInfoResponseOutcome
{
	Parsed,
	ErrorResponse,
	InvalidContent
}

public sealed record LcuLineInfoResponseResult<T>(
	LcuLineInfoResponseOutcome Outcome,
	T? Data);

public enum LcuLineInfoDiagnosticDestination
{
	ApplicationLog,
	Debug
}

public sealed record LcuLineInfoDiagnostic(
	LcuLineInfoDiagnosticDestination Destination,
	string Message);

public sealed record LcuLineInfoResponsePresentation(
	bool Succeeded,
	IReadOnlyList<LcuLineInfoDiagnostic> Diagnostics);

public sealed record LcuLineInfoDirectoryPresentation(
	bool Succeeded,
	IReadOnlyList<NeximLcuTarget> Targets,
	IReadOnlyList<LcuLineInfoDiagnostic> Diagnostics);

#endregion

/// <summary>LCU ライン情報の取得・解析・順次読込と診断内容の生成を行う。</summary>
public sealed class LcuLineInfoService
{
	#region 1. LCU の順次読込

	/// <summary>対象を順に読み込み、HTTP例外は通知して継続、それ以外の例外は通知して中断する。</summary>
	public async Task<bool> LoadSequentiallyAsync<T>(
		IEnumerable<T> targets,
		Func<T, Task<bool>> loadTargetAsync,
		Action<T> onLoadStarted,
		Action<T, bool> onLoadCompleted,
		Action<T, HttpRequestException> onRequestError,
		Action<T, Exception> onLoadError,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(targets);
		ArgumentNullException.ThrowIfNull(loadTargetAsync);
		ArgumentNullException.ThrowIfNull(onLoadStarted);
		ArgumentNullException.ThrowIfNull(onLoadCompleted);
		ArgumentNullException.ThrowIfNull(onRequestError);
		ArgumentNullException.ThrowIfNull(onLoadError);

		if (cancellationToken.IsCancellationRequested)
		{
			return false;
		}

		foreach (T target in targets)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			onLoadStarted(target);
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			bool loadSucceeded = true;
			try
			{
				loadSucceeded = await loadTargetAsync(target);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			catch (HttpRequestException exception)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return false;
				}

				loadSucceeded = false;
				onRequestError(target, exception);
			}
			catch (Exception exception)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return false;
				}

				onLoadError(target, exception);
				return false;
			}

			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			onLoadCompleted(target, loadSucceeded);
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
		}

		return !cancellationToken.IsCancellationRequested;
	}

	#endregion

	#region 2. ライン情報取得と応答解析

	/// <summary>ライン情報を取得して解析する。通信・検証後にもキャンセルを確認する。</summary>
	public async Task<LcuLineInfoResponseResult<T>> GetLineInfoAsync<T>(
		ILcuLineInfoGateway gateway,
		Func<T?, bool> isValid,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentNullException.ThrowIfNull(isValid);
		cancellationToken.ThrowIfCancellationRequested();
		string response = await gateway.GetLinesAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		LcuLineInfoResponseResult<T> result = ReadResponse(response, isValid);
		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	/// <summary>エラー応答を判別し、XML を型へ変換して必須内容を検証する。</summary>
	public LcuLineInfoResponseResult<T> ReadResponse<T>(
		string response,
		Func<T?, bool> isValid)
	{
		ArgumentNullException.ThrowIfNull(response);
		ArgumentNullException.ThrowIfNull(isValid);

		if (response.Contains("errorCode", StringComparison.Ordinal))
		{
			return new LcuLineInfoResponseResult<T>(
				LcuLineInfoResponseOutcome.ErrorResponse,
				default);
		}

		XmlSerializer serializer = new(typeof(T));
		T? data = (T?)serializer.Deserialize(new StringReader(response));
		if (!isValid(data))
		{
			return new LcuLineInfoResponseResult<T>(
				LcuLineInfoResponseOutcome.InvalidContent,
				data);
		}

		return new LcuLineInfoResponseResult<T>(
			LcuLineInfoResponseOutcome.Parsed,
			data);
	}

	#endregion

	#region 3. 表示内容と診断メッセージ

	/// <summary>DB から取得したライン情報のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateDirectoryLineDiagnostic(NeximLineRecord line)
	{
		ArgumentNullException.ThrowIfNull(line);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"{line.Name}::LineID={line.LineId} JobID={line.JobId} ActiveFlag={line.ActiveFlag} LcuID={line.LcuId}");
	}

	/// <summary>DB から取得したコンピューター情報のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateDirectoryComputerDiagnostic(NeximComputerRecord computer)
	{
		ArgumentNullException.ThrowIfNull(computer);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"ComputerID={computer.ComputerId} LcuName={computer.Name}");
	}

	/// <summary>DB の取得結果を対象 LCU 一覧と診断へ変換する。</summary>
	public LcuLineInfoDirectoryPresentation CreateDirectoryPresentation(
		NeximLcuDirectoryResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		if (result.ErrorMessage != null)
		{
			return new(
				false,
				result.Targets,
				[
					new(
						LcuLineInfoDiagnosticDestination.ApplicationLog,
						$"[ERROR] NeximDB LCU list={result.ErrorMessage}")
				]);
		}

		return new(true, result.Targets, []);
	}

	/// <summary>読込例外のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateLoadErrorDiagnostic(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"[ERROR] LoadLineInfo={exception.Message}");
	}

	/// <summary>対象 LCU の読込例外を進捗表示用に整形する。</summary>
	public string CreateTargetLoadProgressMessage(string? host, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return $"{host}={exception.Message}";
	}

	/// <summary>LCU 読込開始の進捗メッセージを作る。</summary>
	public string CreateLoadStartedProgressMessage(string? host) =>
		$"reading {host}";

	/// <summary>装置・モジュールのツリー構築進捗を整形する。</summary>
	public string CreateTreeProgressMessage(LcuLineTreeProgress progress)
	{
		ArgumentNullException.ThrowIfNull(progress);
		return progress.Stage switch
		{
			LcuLineTreeProgressStage.Machine => $"Machine={progress.Name}",
			LcuLineTreeProgressStage.Module => $"Module={progress.Name}",
			_ => throw new ArgumentOutOfRangeException(
				nameof(progress.Stage),
				progress.Stage,
				"Unknown LCU line tree progress stage.")
		};
	}

	/// <summary>応答解析結果を成否と従来の診断ログへ変換する。</summary>
	public LcuLineInfoResponsePresentation CreateResponsePresentation(
		LcuLineInfoResponseOutcome outcome,
		string? lcuName)
	{
		return outcome switch
		{
			LcuLineInfoResponseOutcome.Parsed => new(true, []),
			LcuLineInfoResponseOutcome.ErrorResponse =>
				new(
					false,
					[
						new(
							LcuLineInfoDiagnosticDestination.ApplicationLog,
							$"[ERROR] {lcuName};get lines error"),
						new(
							LcuLineInfoDiagnosticDestination.ApplicationLog,
							$"[LineInfo] {lcuName};get linesInfomation error")
					]),
			LcuLineInfoResponseOutcome.InvalidContent => new(false, []),
			_ => throw new ArgumentOutOfRangeException(
				nameof(outcome),
				outcome,
				"Unknown LCU line information response outcome.")
		};
	}

	/// <summary>読込中断のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateLoadCancelledDiagnostic() =>
		new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			"[ERROR] LoadLineInfo=Canceled");

	/// <summary>LCU ごとの読込結果をデバッグ出力用に整形する。</summary>
	public LcuLineInfoDiagnostic CreateLoadResultDiagnostic(
		string? lcuName,
		bool succeeded) =>
		new(
			LcuLineInfoDiagnosticDestination.Debug,
			$"[LineInfo] {lcuName}={succeeded}");

	/// <summary>選択された装置・モジュール・ユニット件数のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateSelectionSummaryDiagnostic(
		string? lcuName,
		LcuSelectionSummary summary)
	{
		ArgumentNullException.ThrowIfNull(summary);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"[LineInfo] {lcuName};Machine={summary.MachineCount};" +
			$"Module={summary.ModuleCount};Unit={summary.UnitCount}");
	}

	/// <summary>非対応機種を除外したログを作る。</summary>
	public LcuLineInfoDiagnostic CreateSkippedMachineDiagnostic(
		string? lcuName,
		string? machineName,
		string? machineType) =>
		new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"[LineInfo] {lcuName};{machineName}=Skip:{machineType}");

	#endregion
}
}

namespace WpfApp1_cmd.Services
{
using System.Net; // �� ���̍s��ǉ�

using WpfApp1_cmd.Models;
using WpfApp1_cmd.Resources;
using WpfLcuCtrlLib;

// =============================================================================
// LcuLineTreeItemBuilder
// -----------------------------------------------------------------------------
// 役割:
//   LcuLineTreeService の計画を WPF の装置・ベース・モジュールへ反映する。
// 処理の流れ:
//   1. バックグラウンドでライン情報の索引とツリー計画を作成する。
//   2. 呼出し元のコンテキストで項目・親子関係・選択状態・表示状態を準備する。
//   3. 全項目の準備後に LCU へ追加する。
// 備考:
//   - キャンセル時は途中まで準備した装置を LCU へ追加しない。
//   - 同一ベースの 2 番目以降のモジュールの IPv4 は、インデックスごとに 5 加算する。
// =============================================================================

/// <summary>画面に依存しないツリー計画から、表示用の LCU 配下項目を構築する。</summary>
public sealed class LcuLineTreeItemBuilder(LcuLineTreeService treeService)
{
	private sealed record BaseSource(
		WpfLcuCtrlLib.Base Data,
		Dictionary<(int ModuleId, int LogicalPosition), Module> Modules);

	private sealed record TreeSnapshot(
		LcuLineTreePlan Plan,
		Dictionary<(string? Name, string? MachineType), Machine> MachinesByKey,
		Dictionary<
			(string? Name, string? MachineType),
			Dictionary<(int BaseId, int Position), BaseSource>> BasesByMachine);

	/// <summary>項目を準備し、全件の構築成功後に LCU の子項目へ反映する。</summary>
	public async Task<LcuLineTreePlan> PopulateAsync(
		LcuInfo lcu,
		LineInfo lineInfo,
		IReadOnlyCollection<string?> supportedMachineTypes,
		bool ignoreMachineType,
		Action<LcuLineTreeProgress>? onProgress = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcu);
		ArgumentNullException.ThrowIfNull(lineInfo);
		ArgumentNullException.ThrowIfNull(supportedMachineTypes);

		cancellationToken.ThrowIfCancellationRequested();
		bool? lcuIsSelected = lcu.IsSelected.Value == true;
		TreeSnapshot snapshot = await Task.Run(
			() => CreateSnapshot(
				lineInfo,
				supportedMachineTypes,
				ignoreMachineType,
				lcuIsSelected,
				cancellationToken),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		Dictionary<(string? Name, string? MachineType), Machine> machinesByKey =
			snapshot.MachinesByKey;
		Dictionary<
			(string? Name, string? MachineType),
			Dictionary<(int BaseId, int Position), BaseSource>> basesByMachine =
			snapshot.BasesByMachine;
		LcuLineTreePlan treePlan = snapshot.Plan;

		List<MachineInfo> machineItems = [];
		foreach (LcuMachineTreePlan machinePlan in treePlan.Machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			(string? Name, string? MachineType) machineKey =
				(machinePlan.Name, machinePlan.MachineType);
			if (!machinesByKey.TryGetValue(machineKey, out Machine? machineData))
			{
				throw new InvalidOperationException("Sequence contains no matching element.");
			}

			Dictionary<(int BaseId, int Position), BaseSource> basesByKey =
				basesByMachine[machineKey];
			MachineInfo machine = new()
			{
				Name = machinePlan.Name,
				ItemType = MachineType.Machine,
				Machine = machineData,
				Parent = lcu,
				Status = machinePlan.IsSupported ? ItemStatus.OK : ItemStatus.NOT_SUPPORTED,
				ToolTipText = machinePlan.MachineType
			};
			if (!machinePlan.IsSupported)
			{
				machine.IconToolTipText = Resource.NotApplicable;
			}

			onProgress?.Invoke(new(
				LcuLineTreeProgressStage.Machine,
				machinePlan.Name));
			cancellationToken.ThrowIfCancellationRequested();
			foreach (LcuBaseTreePlan basePlan in machinePlan.Bases)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!basesByKey.TryGetValue(
					(basePlan.BaseId, basePlan.Position),
					out BaseSource? baseDataEntry))
				{
					throw new InvalidOperationException("Sequence contains no matching element.");
				}

				WpfLcuCtrlLib.Base baseData = baseDataEntry.Data;
				BaseInfo baseInfo = new()
				{
					Name = "",
					ItemType = MachineType.Base,
					Base = baseData,
					Parent = machine
				};
				machine.Bases.Add(baseInfo);
				machine.IsSelected.Value = machinePlan.IsSelected;

				int moduleIndex = 0;
				foreach (LcuModuleTreePlan modulePlan in basePlan.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!baseDataEntry.Modules.TryGetValue(
						(modulePlan.ModuleId, modulePlan.LogicalPosition),
						out Module? moduleData))
					{
						throw new InvalidOperationException("Sequence contains no matching element.");
					}

					ModuleInfo moduleItem = new()
					{
						Name = modulePlan.DisplayName,
						ItemType = MachineType.Module,
						Module = moduleData,
						Parent = machine,
						IPAddress = ResolveModuleIpAddress(modulePlan.IpAddress, moduleIndex),
						ToolTipText = Resource.UnitInfoNotAcquired,
						Status = machine.Status
					};
					moduleIndex++;

					onProgress?.Invoke(new(
						LcuLineTreeProgressStage.Module,
						modulePlan.DisplayName));
					cancellationToken.ThrowIfCancellationRequested();
					baseInfo.Children.Add(moduleItem);
					machine.Children.Add(moduleItem);
					moduleItem.IsSelected.Value = modulePlan.IsSelected;
					if (!machinePlan.IsSupported)
					{
						moduleItem.IconToolTipText = Resource.NotApplicable;
					}
				}
			}
			machineItems.Add(machine);
		}

		cancellationToken.ThrowIfCancellationRequested();
		foreach (MachineInfo machine in machineItems)
		{
			lcu.Children.Add(machine);
		}

		return treePlan;
	}

	private TreeSnapshot CreateSnapshot(
		LineInfo lineInfo,
		IReadOnlyCollection<string?> supportedMachineTypes,
		bool ignoreMachineType,
		bool? lcuIsSelected,
		CancellationToken cancellationToken)
	{
		List<Machine> machines = lineInfo.Line!.Machines!;
		List<LcuMachineDescriptor> machineDescriptors = [];
		Dictionary<(string? Name, string? MachineType), Machine> machinesByKey = [];
		Dictionary<
			(string? Name, string? MachineType),
			Dictionary<(int BaseId, int Position), BaseSource>> basesByMachine = [];

		foreach (Machine machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			(string? Name, string? MachineType) machineKey =
				(machine.MachineName, machine.MachineType);
			bool indexMachine = machinesByKey.TryAdd(machineKey, machine);
			Dictionary<(int BaseId, int Position), BaseSource>? basesByKey = null;
			if (indexMachine)
			{
				basesByKey = [];
				basesByMachine.Add(machineKey, basesByKey);
			}

			List<LcuBaseDescriptor> baseDescriptors = [];
			foreach (WpfLcuCtrlLib.Base baseInfo in machine.Bases!)
			{
				cancellationToken.ThrowIfCancellationRequested();
				List<LcuModuleDescriptor> moduleDescriptors = [];
				bool indexBase = indexMachine
					&& basesByKey!.TryAdd(
						(baseInfo.BaseId, baseInfo.Position),
						new BaseSource(baseInfo, []));
				Dictionary<(int ModuleId, int LogicalPosition), Module>? modulesByKey =
					indexBase
						? basesByKey![(baseInfo.BaseId, baseInfo.Position)].Modules
						: null;
				foreach (Module module in baseInfo.Modules!)
				{
					cancellationToken.ThrowIfCancellationRequested();
					modulesByKey?.TryAdd((module.ModuleId, module.LogicalPos), module);
					moduleDescriptors.Add(new LcuModuleDescriptor(
						module.ModuleId,
						module.LogicalPos,
						module.DispModule));
				}

				baseDescriptors.Add(new LcuBaseDescriptor(
					baseInfo.BaseId,
					baseInfo.BaseType,
					baseInfo.IpAddr,
					baseInfo.Position,
					baseInfo.FSSisConveyor,
					moduleDescriptors));
			}

			machineDescriptors.Add(new LcuMachineDescriptor(
				machine.MachineName,
				machine.MachineType,
				machine.ModelName,
				baseDescriptors));
		}

		LcuLineTreePlan plan = treeService.CreatePlan(
			machineDescriptors,
			supportedMachineTypes,
			ignoreMachineType,
			lcuIsSelected,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return new TreeSnapshot(plan, machinesByKey, basesByMachine);
	}

	private static string ResolveModuleIpAddress(string? ipAddress, int moduleIndex)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
		{
			return "";
		}

		// 1�ڂ�Module�͂��̂܂ܕԂ�
		if (moduleIndex <= 0)
		{
			return ipAddress;
		}

		// IPv4�ȊO��p�[�X�s�͂��̂܂ܕԂ�
		if (!IPAddress.TryParse(ipAddress, out IPAddress? parsed))
		{
			return ipAddress;
		}

		byte[] bytes = parsed.GetAddressBytes();
		if (bytes.Length != 4)
		{
			return ipAddress;
		}

		// 2�ڈȍ~�� +5 �Â��Z�i�J��オ��Ή��j
		int carry = 5 * moduleIndex;
		for (int index = bytes.Length - 1; index >= 0 && carry > 0; index--)
		{
			int sum = bytes[index] + carry;
			bytes[index] = (byte)(sum & 0xFF);
			carry = sum >> 8;
		}

		return new IPAddress(bytes).ToString();
	}
}
}

namespace WpfApp1_cmd.Services
{


// =============================================================================
// LcuLineTreeService
// -----------------------------------------------------------------------------
// 役割:
//   ライン情報から画面に依存しないツリー計画を作り、選択集計と読込完了状態を決める。
// 処理の流れ:
//   1. CreatePlan            : 対応機種の判定と装置・ベース・モジュールの選択状態を決める。
//   2. CreateSelectionSummary: 選択数を階層ごとに集計し、LCU の三状態選択を求める。
//   3. CreateCompletionState : エラーの有無から選択状態とツールチップを決める。
// 備考:
//   - WPF オブジェクトへの反映は LcuLineTreeItemBuilder が担当する。
//   - 機種名の大文字小文字の扱いと、未確定選択を含めた件数の数え方は変更しない。
// =============================================================================

#region 型定義

public sealed record LcuModuleDescriptor(
	int ModuleId,
	int LogicalPosition,
	string? DisplayName);

public sealed record LcuBaseDescriptor(
	int BaseId,
	int BaseType,
	string? IpAddress,
	int Position,
	string? Conveyor,
	IReadOnlyList<LcuModuleDescriptor> Modules);

public sealed record LcuMachineDescriptor(
	string? Name,
	string? MachineType,
	string? ModelName,
	IReadOnlyList<LcuBaseDescriptor> Bases);

public sealed record LcuModuleTreePlan(
	int ModuleId,
	int LogicalPosition,
	string? DisplayName,
	string? IpAddress,
	bool? IsSelected);

public sealed record LcuBaseTreePlan(
	int BaseId,
	int BaseType,
	string? IpAddress,
	int Position,
	string? Conveyor,
	IReadOnlyList<LcuModuleTreePlan> Modules);

public sealed record LcuMachineTreePlan(
	string? Name,
	string? MachineType,
	string? ModelName,
	bool IsSupported,
	bool? IsSelected,
	IReadOnlyList<LcuBaseTreePlan> Bases);

public sealed record LcuLineTreePlan(
	IReadOnlyList<(string? Name, string? MachineType)> SkippedMachines,
	IReadOnlyList<LcuMachineTreePlan> Machines);

public enum LcuLineTreeProgressStage
{
	Machine,
	Module
}

public sealed record LcuLineTreeProgress(
	LcuLineTreeProgressStage Stage,
	string? Name);

public sealed record LcuModuleSelection(bool? IsSelected, IReadOnlyList<bool?> UnitSelections);

public sealed record LcuMachineSelection(
	bool? IsSelected,
	IReadOnlyList<LcuModuleSelection> Modules);

public sealed record LcuSelectionSummary(
	int MachineCount,
	int ModuleCount,
	int UnitCount,
	bool? SelectionState);

public sealed record LcuLoadCompletionState(bool? IsSelected, string? ToolTip);

#endregion

/// <summary>LCU ツリーの構築計画・選択集計・読込完了状態を扱う。</summary>
public sealed class LcuLineTreeService
{
	#region 1. ツリー計画

	/// <summary>対応機種に応じて表示対象と選択状態を決め、階層構造の計画を作る。</summary>
	public LcuLineTreePlan CreatePlan(
		IReadOnlyList<LcuMachineDescriptor> machines,
		IReadOnlyCollection<string?> supportedMachineTypes,
		bool ignoreMachineType,
		bool? lcuIsSelected,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(supportedMachineTypes);
		cancellationToken.ThrowIfCancellationRequested();

		HashSet<string?> supportedTypes = new();
		foreach (string? supportedType in supportedMachineTypes)
		{
			cancellationToken.ThrowIfCancellationRequested();
			supportedTypes.Add(supportedType);
		}

		List<(string? Name, string? MachineType)> skippedMachines = [];
		List<LcuMachineTreePlan> machinePlans = [];
		foreach (LcuMachineDescriptor machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			bool isSupported = supportedTypes.Contains(machine.MachineType);
			if (!ignoreMachineType && !isSupported)
			{
				skippedMachines.Add((machine.Name, machine.MachineType));
				continue;
			}

			List<LcuBaseTreePlan> basePlans = [];
			bool? isSelected = true;
			foreach (LcuBaseDescriptor baseInfo in machine.Bases)
			{
				cancellationToken.ThrowIfCancellationRequested();
				isSelected = isSupported ? lcuIsSelected : false;
				List<LcuModuleTreePlan> modulePlans = [];
				foreach (LcuModuleDescriptor module in baseInfo.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					modulePlans.Add(new LcuModuleTreePlan(
						module.ModuleId,
						module.LogicalPosition,
						module.DisplayName,
						baseInfo.IpAddress,
						isSupported ? lcuIsSelected : false));
				}
				basePlans.Add(new LcuBaseTreePlan(
					baseInfo.BaseId,
					baseInfo.BaseType,
					baseInfo.IpAddress,
					baseInfo.Position,
					baseInfo.Conveyor,
					modulePlans.ToArray()));
			}

			machinePlans.Add(new LcuMachineTreePlan(
				machine.Name,
				machine.MachineType,
				machine.ModelName,
				isSupported,
				isSelected,
				basePlans));
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new LcuLineTreePlan(skippedMachines, machinePlans);
	}

	#endregion

	#region 2. 選択集計

	/// <summary>false 以外の項目を選択件数に含め、装置の選択状態から LCU の三状態選択を求める。</summary>
	public LcuSelectionSummary CreateSelectionSummary(
		IReadOnlyList<LcuMachineSelection> machines,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		cancellationToken.ThrowIfCancellationRequested();

		int machineCount = 0;
		int moduleCount = 0;
		int unitCount = 0;
		bool allMachinesSelected = machines.Count > 0;
		bool allMachinesUnselected = machines.Count > 0;
		foreach (LcuMachineSelection machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (machine.IsSelected != false)
			{
				machineCount++;
			}
			allMachinesSelected &= machine.IsSelected == true;
			allMachinesUnselected &= machine.IsSelected == false;

			foreach (LcuModuleSelection module in machine.Modules)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (module.IsSelected != false)
				{
					moduleCount++;
				}

				foreach (bool? unitSelection in module.UnitSelections)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (unitSelection != false)
					{
						unitCount++;
					}
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		bool? selectionState = machines.Count == 0
			? false
			: allMachinesSelected
			? true
			: allMachinesUnselected
				? false
				: null;

		return new(machineCount, moduleCount, unitCount, selectionState);
	}

	#endregion

	#region 3. 読込完了状態

	/// <summary>エラー時は LCU を未選択にし、正常時は集計した選択状態とバージョンを使う。</summary>
	public LcuLoadCompletionState CreateCompletionState(
		bool hasError,
		bool? selectionState,
		string? errorToolTip,
		string? version)
	{
		if (hasError)
		{
			ArgumentNullException.ThrowIfNull(errorToolTip);
			return new(false, errorToolTip);
		}

		return new(selectionState, version);
	}

	#endregion
}
}

namespace WpfApp1_cmd.Services
{


// =============================================================================
// LcuNetworkCheckService
// -----------------------------------------------------------------------------
// 役割:
//   LCU の疎通とバージョンを確認し、接続確認ダイアログの表示内容を作る。
// 処理の流れ:
//   1. CheckAsync         : Ping を最大 3 回行い、接続できればバージョンを取得する。
//   2. CreatePresentation : 接続結果をエラーまたは情報ダイアログへ変換する。
// 備考:
//   - Ping は ILcuReachabilityService、情報取得は ILcuInformationGateway に委譲する。
//   - バージョン取得不可時は、従来どおりダイアログを表示しない。
// =============================================================================

#region 型定義

public enum LcuNetworkCheckOutcome
{
	Unreachable,
	VersionUnavailable,
	Available
}

public sealed record LcuNetworkCheckResult(
	LcuNetworkCheckOutcome Outcome,
	string? Version = null);

public sealed record LcuNetworkCheckPresentation(
	bool ShowDialog,
	ErrorCode? ErrorCode,
	string? DialogTitle,
	string? Message,
	string? Detail,
	bool IsError);

#endregion

/// <summary>LCU の疎通・バージョン確認と、その結果の表示内容を扱う。</summary>
public sealed class LcuNetworkCheckService(ILcuReachabilityService reachabilityService)
{
	#region 1. 接続確認

	/// <summary>Ping 成功時にバージョンを取得し、接続確認結果を返す。</summary>
	public async Task<LcuNetworkCheckResult> CheckAsync(
		string host,
		ILcuInformationGateway informationGateway,
		Action<string>? onPingError = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentNullException.ThrowIfNull(informationGateway);
		cancellationToken.ThrowIfCancellationRequested();

		bool isReachable = await reachabilityService.IsReachableAsync(
			host,
			3,
			onPingError,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!isReachable)
		{
			return new(LcuNetworkCheckOutcome.Unreachable);
		}

		LcuVersionResult version = await new LcuInformationService(
			informationGateway).GetVersionAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return version.IsAvailable
			? new(LcuNetworkCheckOutcome.Available, version.Version)
			: new(LcuNetworkCheckOutcome.VersionUnavailable);
	}

	#endregion

	#region 2. 結果表示

	/// <summary>接続確認結果から、ダイアログとエラーコードの反映内容を作る。</summary>
	public static LcuNetworkCheckPresentation CreatePresentation(
		LcuNetworkCheckResult result,
		string errorTitle,
		string infoTitle,
		string errorMessage,
		string? version)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(errorTitle);
		ArgumentNullException.ThrowIfNull(infoTitle);
		ArgumentNullException.ThrowIfNull(errorMessage);

		return result.Outcome switch
		{
			LcuNetworkCheckOutcome.Unreachable => new(
				true,
				ErrorCode.LCU_CONNECT_ERROR,
				errorTitle,
				$"ErrorCode=0x{(int)ErrorCode.LCU_CONNECT_ERROR:X}",
				errorMessage,
				true),
			LcuNetworkCheckOutcome.VersionUnavailable => new(
				false,
				null,
				null,
				null,
				null,
				false),
			LcuNetworkCheckOutcome.Available => new(
				true,
				null,
				infoTitle,
				"Access OK",
				$"Version:{version}",
				false),
			_ => throw new ArgumentOutOfRangeException(
				nameof(result.Outcome),
				result.Outcome,
				"Unknown LCU network check outcome.")
		};
	}

	#endregion
}
}

namespace WpfApp1_cmd.Services
{
using WpfLcuCtrlLib;

public sealed class LcuPeripheralFileDownloadGateway(LcuCtrl lcuCtrl)
	: ILcuPeripheralFileDownloadGateway
{
	public async Task SetLcuAsync(int lcuId, CancellationToken cancellationToken)
	{
		await lcuCtrl.LCU_Command(SetLcu.Command(lcuId), cancellationToken);
	}

	public Task<bool> DownloadMachineFileAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativeFile,
		string lcuRelativeFile,
		string targetDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFile(
			machineName,
			IpAddress,
			position,
			machineRelativeFile,
			lcuRelativeFile,
			targetDirectory,
			cancellationToken);
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;

public interface ILcuPeripheralFileDownloadGateway
{
	Task SetLcuAsync(int lcuId, CancellationToken cancellationToken);

	Task<bool> DownloadMachineFileAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativeFile,
		string lcuRelativeFile,
		string targetDirectory,
		CancellationToken cancellationToken);
}

public sealed record LcuPeripheralFileDownloadRequest(
	int LcuId,
	string MachineName,
	string IpAddress,
	int Position,
	string PeripheralRelativeDirectory,
	string FileName,
	string TargetDirectory);

public sealed class LcuPeripheralFileDownloadService
{
	public async Task<bool> DownloadAsync(
		LcuPeripheralFileDownloadRequest request,
		ILcuPeripheralFileDownloadGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.MachineName);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.PeripheralRelativeDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetDirectory);

		string machineRelativeFile = request.PeripheralRelativeDirectory + request.FileName;
		string lcuRelativeFile = "/MCFiles/" + machineRelativeFile;
		string targetDirectory = request.TargetDirectory + request.PeripheralRelativeDirectory;

		cancellationToken.ThrowIfCancellationRequested();
		await gateway.SetLcuAsync(request.LcuId, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		await Task.Run(
			() => Directory.CreateDirectory(targetDirectory),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		bool downloaded = await gateway.DownloadMachineFileAsync(
			request.MachineName,
			request.IpAddress,
			request.Position,
			machineRelativeFile,
			lcuRelativeFile,
			request.TargetDirectory,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return downloaded;
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.Net.NetworkInformation;

public interface ILcuReachabilityService
{
	Task<bool> IsReachableAsync(
		string host,
		int attempts,
		Action<string>? onPingError = null,
		CancellationToken cancellationToken = default);
}

public sealed class LcuReachabilityService : ILcuReachabilityService
{
	private readonly Func<string, CancellationToken, Task<IPStatus>> _sendPing;
	private readonly Func<TimeSpan, CancellationToken, Task> _delay;

	public LcuReachabilityService()
		: this(
			async (host, cancellationToken) =>
			{
				using Ping ping = new();
				PingReply reply = await ping.SendPingAsync(
					host,
					TimeSpan.FromSeconds(5),
					new byte[32],
					new PingOptions(),
					cancellationToken);
				return reply.Status;
			},
			(delay, cancellationToken) => Task.Delay(delay, cancellationToken))
	{
	}

	public LcuReachabilityService(
		Func<string, CancellationToken, Task<IPStatus>> sendPing,
		Func<TimeSpan, CancellationToken, Task> delay)
	{
		ArgumentNullException.ThrowIfNull(sendPing);
		ArgumentNullException.ThrowIfNull(delay);
		_sendPing = sendPing;
		_delay = delay;
	}

	public async Task<bool> IsReachableAsync(
		string host,
		int attempts,
		Action<string>? onPingError = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);

		try
		{
			for (int attempt = 0; attempt < attempts; attempt++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				IPStatus status = await _sendPing(host, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (status == IPStatus.Success)
				{
					return true;
				}

				await _delay(TimeSpan.FromMilliseconds(100), cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
			}

			return false;
		}
		catch (PingException exception)
		{
			cancellationToken.ThrowIfCancellationRequested();
			onPingError?.Invoke(exception.Message);
			return false;
		}
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;

public enum LcuUnitFileUploadOutcome
{
	Succeeded,
	Cancelled,
	Failed
}

public sealed record LcuUnitFileUploadResult(
	LcuUnitFileUploadOutcome Outcome,
	IReadOnlyList<string> Folders,
	Exception? Error = null);

public sealed class LcuUnitFileUploadService(
	UpdateFolderSelectionService folderSelectionService,
	LcuFolderUploadService folderUploadService)
{
	public async Task<LcuUnitFileUploadResult> UploadSelectedAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<UpdateFolderMachine> machines,
		IReadOnlyList<UpdateFolderFileInfo> updateDataFiles,
		string dataFolder,
		string remoteRoot,
		Func<LcuFolderUploadProgress, Task> onFolderUploaded,
		Func<CancellationToken, Task<bool>> canContinue,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(updateDataFiles);

		List<string> folders;
		try
		{
			folders = await Task.Run(
				() => folderSelectionService.CreateSelectedFolders(
					machines,
					updateDataFiles,
					dataFolder,
					cancellationToken),
				cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new(LcuUnitFileUploadOutcome.Cancelled, []);
		}

		return await UploadAsync(
			host,
			user,
			password,
			folders,
			dataFolder,
			remoteRoot,
			onFolderUploaded,
			canContinue,
			cancellationToken);
	}

	public async Task<LcuUnitFileUploadResult> UploadAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<string> folders,
		string dataFolder,
		string remoteRoot,
		Func<LcuFolderUploadProgress, Task> onFolderUploaded,
		Func<CancellationToken, Task<bool>> canContinue,
		CancellationToken cancellationToken)
	{
		try
		{
			LcuFolderUploadResult result = await folderUploadService.UploadAsync(
				host,
				user,
				password,
				folders,
				dataFolder,
				remoteRoot,
				onFolderUploaded,
				canContinue,
				cancellationToken);
			return new(
				result.Outcome == LcuFolderUploadOutcome.Succeeded
					? LcuUnitFileUploadOutcome.Succeeded
					: LcuUnitFileUploadOutcome.Cancelled,
				folders);
		}
		catch (Exception exception) when (
			exception is FluentFTP.Exceptions.FtpException or
			IOException or
			UnauthorizedAccessException or
			InvalidOperationException or
			ArgumentException or
			System.Net.Sockets.SocketException)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return new(LcuUnitFileUploadOutcome.Cancelled, folders);
			}

			return new(LcuUnitFileUploadOutcome.Failed, folders, exception);
		}
	}
}
}

namespace WpfApp1_cmd.Services
{


// =============================================================================
// LcuUpdatePreparationService
// -----------------------------------------------------------------------------
// 役割:
//   更新前の LCU 接続・FTP アカウント・バージョン・空き容量を確認し、
//   画面へ反映するアクションと診断メッセージを作る。
// 処理の流れ:
//   1. PrepareAsync       : 接続確認後、未設定の場合のみアカウント・版・容量を順に取得する。
//   2. CreatePresentation : 結果からログ・進捗・エラー・取得情報の反映順序を決める。
//   3. Create*Message(s)  : 接続や容量の診断文を生成する。
// 備考:
//   - 空き容量不足は LCU を更新不可にするが、他の LCU の処理継続は妨げない。
//   - 通信は ILcuReachabilityService と LcuInformationService に委譲する。
// =============================================================================

#region 型定義

public enum LcuUpdatePreparationOutcome
{
	Ready,
	ConnectionError,
	FtpResponseError,
	FtpCredentialsError,
	VersionError,
	DiskInfoError
}

public sealed record LcuUpdatePreparationResult(
	LcuUpdatePreparationOutcome Outcome,
	LcuFtpAccountResult? FtpAccount = null,
	LcuVersionResult? Version = null,
	LcuDiskSpaceResult? DiskSpace = null,
	bool IsDiskSpaceInsufficient = false);

public enum LcuUpdatePreparationActionType
{
	Log,
	SetProgressMessage,
	SetLcuErrorCode,
	SetGlobalErrorCode,
	MarkConnectionError,
	MarkDiskInfoError,
	ApplyFtpAccount,
	ApplyVersion,
	ApplyDiskSpace,
	MarkDiskSpaceInsufficient,
	SetDiskSpaceErrorTooltip
}

public sealed record LcuUpdatePreparationAction(
	LcuUpdatePreparationActionType Type,
	string? Message = null,
	ErrorCode? ErrorCode = null);

public sealed record LcuUpdatePreparationPresentation(
	bool CanContinue,
	IReadOnlyList<LcuUpdatePreparationAction> Actions);

#endregion

/// <summary>更新前の LCU 情報確認と、画面・ログへ反映する内容の生成を行う。</summary>
public sealed class LcuUpdatePreparationService(
	ILcuReachabilityService reachabilityService,
	LcuInformationService informationService)
{
	#region 1. 更新準備

	/// <summary>疎通を確認し、FTP アカウント未設定の場合は版と空き容量まで取得する。</summary>
	public async Task<LcuUpdatePreparationResult> PrepareAsync(
		string host,
		bool ftpAccountAlreadyConfigured,
		long requiredDiskSpace,
		CancellationToken cancellationToken,
		Action<string>? onPingError = null,
		Action? onDiskSpaceCheckStarted = null)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(requiredDiskSpace);
		cancellationToken.ThrowIfCancellationRequested();

		bool isReachable = await reachabilityService.IsReachableAsync(
			host,
			3,
			onPingError,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!isReachable)
		{
			return new(LcuUpdatePreparationOutcome.ConnectionError);
		}

		if (ftpAccountAlreadyConfigured)
		{
			return new(LcuUpdatePreparationOutcome.Ready);
		}

		LcuFtpAccountResult account =
			await informationService.GetFtpAccountAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (account.Status == LcuFtpAccountStatus.ResponseError)
		{
			return new(LcuUpdatePreparationOutcome.FtpResponseError);
		}

		if (account.Status == LcuFtpAccountStatus.CredentialsError)
		{
			return new(LcuUpdatePreparationOutcome.FtpCredentialsError);
		}

		LcuVersionResult version =
			await informationService.GetVersionAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!version.IsAvailable)
		{
			return new(
				LcuUpdatePreparationOutcome.VersionError,
				account);
		}

		onDiskSpaceCheckStarted?.Invoke();
		LcuDiskSpaceResult diskSpace =
			await informationService.GetDiskSpaceAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!diskSpace.IsAvailable)
		{
			return new(
				LcuUpdatePreparationOutcome.DiskInfoError,
				account,
				version,
				diskSpace);
		}

		bool isDiskSpaceInsufficient = diskSpace.FreeSpace < requiredDiskSpace;
		return new(
			LcuUpdatePreparationOutcome.Ready,
			account,
			version,
			diskSpace,
			isDiskSpaceInsufficient);
	}

	#endregion

	#region 2. 結果の反映内容

	/// <summary>準備結果を、従来の順序で画面・ログへ反映するアクションへ変換する。</summary>
	public static LcuUpdatePreparationPresentation CreatePresentation(
		LcuUpdatePreparationResult result,
		string? lcuName,
		string? host,
		long requiredDiskSpace)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentOutOfRangeException.ThrowIfNegative(requiredDiskSpace);

		List<LcuUpdatePreparationAction> actions = [];
		switch (result.Outcome)
		{
			case LcuUpdatePreparationOutcome.ConnectionError:
				AddLogs(CreateConnectionErrorMessages(lcuName));
				AddProgress(CreateConnectionProgressMessage(host));
				AddLcuError(ErrorCode.LCU_CONNECT_ERROR);
				actions.Add(new(LcuUpdatePreparationActionType.MarkConnectionError));
				return new(false, actions);

			case LcuUpdatePreparationOutcome.FtpResponseError:
				AddProgress(CreateFtpResponseErrorProgressMessage(host));
				AddLcuError(ErrorCode.FTP_SERVER_ERROR);
				return new(false, actions);

			case LcuUpdatePreparationOutcome.FtpCredentialsError:
				AddProgress(CreateFtpCredentialsErrorProgressMessage(host));
				AddLcuError(ErrorCode.FTP_ACCOUNT_ERROR);
				return new(false, actions);

			case LcuUpdatePreparationOutcome.VersionError:
				AddFtpAccountIfPresent();
				AddLogs(CreateVersionErrorMessages(lcuName));
				AddProgress(CreateVersionErrorProgressMessage(host));
				AddLcuError(ErrorCode.LCU_VERSION_ERROR);
				return new(false, actions);

			case LcuUpdatePreparationOutcome.DiskInfoError:
				AddFtpAccountIfPresent();
				AddVersionIfPresent();
				AddProgress(CreateVersionProgressMessage(
					host,
					result.Version?.Version));
				actions.Add(new(
					LcuUpdatePreparationActionType.SetGlobalErrorCode,
					ErrorCode: ErrorCode.LCU_DISK_INFO_ERROR));
				AddLcuError(ErrorCode.LCU_DISK_INFO_ERROR);
				actions.Add(new(LcuUpdatePreparationActionType.MarkDiskInfoError));
				AddLogs([CreateDiskInfoErrorMessage(lcuName)]);
				return new(false, actions);

			case LcuUpdatePreparationOutcome.Ready:
				AddFtpAccountIfPresent();
				if (result.FtpAccount != null)
				{
					AddVersionIfPresent();
					AddProgress(CreateVersionProgressMessage(
						host,
						result.Version?.Version));
					AddDiskSpaceIfPresent();
					if (result.IsDiskSpaceInsufficient)
					{
						AddLcuError(ErrorCode.UPDATE_DISKSPACE_ERROR);
						actions.Add(new(LcuUpdatePreparationActionType.MarkDiskSpaceInsufficient));
						AddLogs(CreateDiskSpaceErrorMessages(
							lcuName,
							GetDiskSpace().FreeSpace,
							requiredDiskSpace));
						AddProgress(CreateDiskSpaceErrorProgressMessage(
							host,
							GetDiskSpace().FreeSpace,
							requiredDiskSpace));
						actions.Add(new(LcuUpdatePreparationActionType.SetDiskSpaceErrorTooltip));
					}
				}
				return new(true, actions);

			default:
				throw new ArgumentOutOfRangeException(
					nameof(result.Outcome),
					result.Outcome,
					"Unknown LCU update preparation outcome.");
		}

		void AddLogs(IEnumerable<string> messages)
		{
			foreach (string message in messages)
			{
				actions.Add(new(LcuUpdatePreparationActionType.Log, Message: message));
			}
		}

		void AddProgress(string message) =>
			actions.Add(new(LcuUpdatePreparationActionType.SetProgressMessage, Message: message));

		void AddLcuError(ErrorCode errorCode) =>
			actions.Add(new(
				LcuUpdatePreparationActionType.SetLcuErrorCode,
				ErrorCode: errorCode));

		void AddFtpAccountIfPresent()
		{
			if (result.FtpAccount != null)
			{
				actions.Add(new(LcuUpdatePreparationActionType.ApplyFtpAccount));
			}
		}

		void AddVersionIfPresent()
		{
			if (result.Version == null)
			{
				throw new InvalidOperationException(
					$"Preparation outcome '{result.Outcome}' requires version data.");
			}
			actions.Add(new(LcuUpdatePreparationActionType.ApplyVersion));
		}

		void AddDiskSpaceIfPresent()
		{
			_ = GetDiskSpace();
			actions.Add(new(LcuUpdatePreparationActionType.ApplyDiskSpace));
		}

		LcuDiskSpaceResult GetDiskSpace() =>
			result.DiskSpace ?? throw new InvalidOperationException(
				$"Preparation outcome '{result.Outcome}' requires disk space data.");
	}

	#endregion

	#region 3. 診断メッセージ

	/// <summary>接続失敗時の進捗メッセージを作る。</summary>
	public static string CreateConnectionProgressMessage(string? host) =>
		$"LCU:{host}=Access Fail";

	/// <summary>FTP 情報の応答取得失敗時の進捗メッセージを作る。</summary>
	public static string CreateFtpResponseErrorProgressMessage(string? host) =>
		$"LCU:{host}=FTP information Fail";

	/// <summary>FTP 資格情報の不足を示す進捗メッセージを作る。</summary>
	public static string CreateFtpCredentialsErrorProgressMessage(string? host) =>
		$"LCU:{host}=FTP user, password Fail";

	/// <summary>バージョン取得失敗時の進捗メッセージを作る。</summary>
	public static string CreateVersionErrorProgressMessage(string? host) =>
		$"LCU:{host}=LcuVersion Get Error";

	/// <summary>取得したバージョンの進捗メッセージを作る。</summary>
	public static string CreateVersionProgressMessage(string? host, string? version) =>
		$"LCU:{host}=Version({version})";

	/// <summary>ディスク情報取得失敗のログを作る。</summary>
	public static string CreateDiskInfoErrorMessage(string? lcuName) =>
		$"{lcuName}::Don't get disk space information.";

	/// <summary>ディスク容量確認開始のログを作る。</summary>
	public static string CreateDiskSpaceCheckStartedMessage(string? lcuName) =>
		$"{lcuName}:Check Disk Space";

	/// <summary>空き容量と必要量を含む容量不足の進捗メッセージを作る。</summary>
	public static string CreateDiskSpaceErrorProgressMessage(
		string? host,
		long freeSpace,
		long requiredSpace) =>
		$"LCU:{host}=DiskSpace Error({freeSpace}<{requiredSpace})";

	/// <summary>接続失敗のログを出力順に返す。</summary>
	public static IReadOnlyList<string> CreateConnectionErrorMessages(string? lcuName) =>
		[
			$"[ERROR] {lcuName}=Access Fail",
			$"[LineInfo]:{lcuName}=Access Fail"
		];

	/// <summary>バージョン取得失敗のログを出力順に返す。</summary>
	public static IReadOnlyList<string> CreateVersionErrorMessages(string? lcuName) =>
		[
			$"[ERROR] {lcuName}=LcuVersion Get Error",
			$"[LineInfo] {lcuName};LcuVersion Get Error"
		];

	/// <summary>空き容量と必要量を含む容量不足のログを出力順に返す。</summary>
	public static IReadOnlyList<string> CreateDiskSpaceErrorMessages(
		string? lcuName,
		long freeSpace,
		long requiredSpace) =>
		[
			$"[ERROR] {lcuName}=DiskSpace Error({freeSpace}<{requiredSpace})",
			$"[LineInfo] {lcuName};DiskSpace Error({freeSpace}<{requiredSpace})"
		];

	#endregion
}
}

namespace WpfApp1_cmd.Services
{


public sealed record LineInfoLoadWaitResult(bool Succeeded, bool WasCancelled);

public sealed class LineInfoLoadWaiter
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
	private readonly Func<TimeSpan, Task> _delay;

	public LineInfoLoadWaiter()
		: this(Task.Delay)
	{
	}

	public LineInfoLoadWaiter(Func<TimeSpan, Task> delay)
	{
		ArgumentNullException.ThrowIfNull(delay);
		_delay = delay;
	}

	public async Task<LineInfoLoadWaitResult> WaitAsync(
		Task<bool> operation,
		Func<bool> isCancellationRequested,
		Action cancelOperation)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(isCancellationRequested);
		ArgumentNullException.ThrowIfNull(cancelOperation);

		bool cancellationRequested = false;
		while (!operation.IsCompleted)
		{
			if (!cancellationRequested && isCancellationRequested())
			{
				if (operation.IsCompleted)
				{
					break;
				}

				cancelOperation();
				cancellationRequested = true;
				if (operation.IsCompleted)
				{
					break;
				}
			}

			await _delay(PollInterval);
		}

		bool succeeded = await operation;
		return new LineInfoLoadWaitResult(succeeded, cancellationRequested);
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record NeximComputerRecord(int ComputerId, string Name);

public sealed record NeximLineRecord(
	string Name,
	int LineId,
	int JobId,
	int ActiveFlag,
	int LcuId,
	IReadOnlyList<NeximComputerRecord> Computers);

public sealed record NeximLcuRepositoryResult(
	IReadOnlyList<NeximLineRecord> Lines,
	string? ErrorMessage);

public sealed record NeximLcuTarget(string LineName, string LcuName, int ComputerId);

public sealed record NeximLcuDirectoryResult(
	IReadOnlyList<NeximLcuTarget> Targets,
	string? ErrorMessage);

public interface INeximLcuRepository
{
	Task<NeximLcuRepositoryResult> LoadActiveLcusAsync(
		NeximDatabaseSettings settings,
		CancellationToken cancellationToken);
}

public sealed class NeximLcuDirectoryService(INeximLcuRepository repository)
{
	public async Task<NeximLcuDirectoryResult> LoadActiveLcusAsync(
		NeximDatabaseSettings settings,
		Action<NeximLineRecord> onLineLoaded,
		Action<NeximComputerRecord> onComputerLoaded,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(onLineLoaded);
		ArgumentNullException.ThrowIfNull(onComputerLoaded);

		cancellationToken.ThrowIfCancellationRequested();
		NeximLcuRepositoryResult repositoryResult =
			await repository.LoadActiveLcusAsync(settings, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		List<NeximLcuTarget> targets = [];
		foreach (NeximLineRecord line in repositoryResult.Lines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			onLineLoaded(line);
			foreach (NeximComputerRecord computer in line.Computers)
			{
				cancellationToken.ThrowIfCancellationRequested();
				onComputerLoaded(computer);
				targets.Add(new NeximLcuTarget(line.Name, computer.Name, computer.ComputerId));
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new NeximLcuDirectoryResult(targets, repositoryResult.ErrorMessage);
	}
}
}

namespace WpfApp1_cmd.Services
{
using Oracle.ManagedDataAccess.Client;

public sealed class OracleNeximLcuRepository : INeximLcuRepository
{
	public async Task<NeximLcuRepositoryResult> LoadActiveLcusAsync(
		NeximDatabaseSettings settings,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(settings);
		cancellationToken.ThrowIfCancellationRequested();

		OracleConnectionStringBuilder connectionString = new()
		{
			UserID = settings.User,
			Password = settings.Password,
			DataSource = $"{settings.Host}/{settings.Name}"
		};
		List<NeximLineRecord> lines = [];

		try
		{
			await using OracleConnection connection = new(connectionString.ConnectionString);
			await connection.OpenAsync(cancellationToken);
			List<(string Name, int LineId, int JobId, int ActiveFlag, int LcuId)> lineRows = [];
			using (OracleCommand command = new(
				"select * from LINE WHERE JOBID=0 AND ACTIVEFLG=1",
				connection))
			await using (OracleDataReader reader =
				(OracleDataReader)await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					lineRows.Add((
						reader.GetString(2),
						reader.GetInt32(1),
						reader.GetInt32(0),
						reader.GetInt32(6),
						reader.GetInt32(18)));
				}
			}

			foreach (var line in lineRows)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int recordIndex = lines.Count;
				lines.Add(new NeximLineRecord(
					line.Name,
					line.LineId,
					line.JobId,
					line.ActiveFlag,
					line.LcuId,
					[]));

				List<NeximComputerRecord> computers = [];
				using (OracleCommand command = new(
					"select * from COMPUTER WHERE COMPUTERID=:computerId",
					connection))
				{
					command.Parameters.Add("computerId", line.LcuId);
					await using OracleDataReader reader =
						(OracleDataReader)await command.ExecuteReaderAsync(cancellationToken);
					while (await reader.ReadAsync(cancellationToken))
					{
						computers.Add(new NeximComputerRecord(
							reader.GetInt32(0),
							reader.GetString(1)));
					}
				}

				lines[recordIndex] = lines[recordIndex] with { Computers = computers };
			}

			return new NeximLcuRepositoryResult(lines, null);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return new NeximLcuRepositoryResult(lines, exception.Message);
		}
	}
}
}
