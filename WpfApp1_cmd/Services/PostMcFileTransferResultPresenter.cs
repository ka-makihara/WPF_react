namespace WpfApp1_cmd.Services;

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
