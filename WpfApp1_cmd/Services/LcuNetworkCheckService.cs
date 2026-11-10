namespace WpfApp1_cmd.Services;

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
