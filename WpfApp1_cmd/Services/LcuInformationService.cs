namespace WpfApp1_cmd.Services;

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
