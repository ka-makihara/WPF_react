namespace WpfApp1_cmd.Services;

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
