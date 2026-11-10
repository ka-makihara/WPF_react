namespace WpfApp1_cmd.Services;

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
