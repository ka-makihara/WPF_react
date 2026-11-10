namespace WpfApp1_cmd.Services;

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
