namespace WpfApp1_cmd.Services;

public sealed record LineInfoLoadWaitResult(bool Succeeded, bool WasCancelled);

public sealed class LineInfoLoadWaiter
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
	private readonly Func<TimeSpan, Task> _delay;

	public LineInfoLoadWaiter()
		: this(Task.Delay)
	{
	}

	public LineInfoLoadWaiter(Func<TimeSpan, Task> delay)
	{
		ArgumentNullException.ThrowIfNull(delay);
		_delay = delay;
	}

	public async Task<LineInfoLoadWaitResult> WaitAsync(
		Task<bool> operation,
		Func<bool> isCancellationRequested,
		Action cancelOperation)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(isCancellationRequested);
		ArgumentNullException.ThrowIfNull(cancelOperation);

		bool cancellationRequested = false;
		while (!operation.IsCompleted)
		{
			if (!cancellationRequested && isCancellationRequested())
			{
				if (operation.IsCompleted)
				{
					break;
				}

				cancelOperation();
				cancellationRequested = true;
				if (operation.IsCompleted)
				{
					break;
				}
			}

			await _delay(PollInterval);
		}

		bool succeeded = await operation;
		return new LineInfoLoadWaitResult(succeeded, cancellationRequested);
	}
}
