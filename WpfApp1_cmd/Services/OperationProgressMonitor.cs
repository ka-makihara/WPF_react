namespace WpfApp1_cmd.Services;

public sealed class OperationProgressMonitor
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	public async Task<bool> WaitAsync(
		Task operation,
		Func<bool> isRunning,
		Func<bool> isCancelRequested,
		Func<Task> pauseForConfirmation,
		Func<Task<bool>> confirmCancellation,
		Func<Task> resumeAfterDeclinedCancellation,
		Action onCancellationConfirmed)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(isRunning);
		ArgumentNullException.ThrowIfNull(isCancelRequested);
		ArgumentNullException.ThrowIfNull(pauseForConfirmation);
		ArgumentNullException.ThrowIfNull(confirmCancellation);
		ArgumentNullException.ThrowIfNull(resumeAfterDeclinedCancellation);
		ArgumentNullException.ThrowIfNull(onCancellationConfirmed);

		while (!operation.IsCompleted && isRunning())
		{
			if (isCancelRequested())
			{
				await pauseForConfirmation();
				if (operation.IsCompleted)
				{
					return false;
				}

				if (await confirmCancellation())
				{
					if (operation.IsCompleted)
					{
						return false;
					}

					onCancellationConfirmed();
					return true;
				}

				if (operation.IsCompleted)
				{
					return false;
				}

				await resumeAfterDeclinedCancellation();
				if (operation.IsCompleted)
				{
					return false;
				}
			}

			await Task.Delay(PollInterval);
		}

		return false;
	}
}
