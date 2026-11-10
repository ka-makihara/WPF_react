namespace WpfApp1_cmd.Services;

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
