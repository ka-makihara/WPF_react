namespace WpfApp1_cmd.Services;

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
