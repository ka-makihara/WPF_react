namespace WpfApp1_cmd.Services;

public sealed class TransferPauseService
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	public async Task<bool> WaitUntilResumedAsync(
		Func<bool> isResumed,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(isResumed);

		while (true)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			if (isResumed())
			{
				return !cancellationToken.IsCancellationRequested;
			}

			try
			{
				await Task.Delay(PollInterval, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
		}
	}
}
