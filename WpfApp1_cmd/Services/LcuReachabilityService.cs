using System.Net.NetworkInformation;

namespace WpfApp1_cmd.Services;

public interface ILcuReachabilityService
{
	Task<bool> IsReachableAsync(
		string host,
		int attempts,
		Action<string>? onPingError = null,
		CancellationToken cancellationToken = default);
}

public sealed class LcuReachabilityService : ILcuReachabilityService
{
	private readonly Func<string, CancellationToken, Task<IPStatus>> _sendPing;
	private readonly Func<TimeSpan, CancellationToken, Task> _delay;

	public LcuReachabilityService()
		: this(
			async (host, cancellationToken) =>
			{
				using Ping ping = new();
				PingReply reply = await ping.SendPingAsync(
					host,
					TimeSpan.FromSeconds(5),
					new byte[32],
					new PingOptions(),
					cancellationToken);
				return reply.Status;
			},
			(delay, cancellationToken) => Task.Delay(delay, cancellationToken))
	{
	}

	public LcuReachabilityService(
		Func<string, CancellationToken, Task<IPStatus>> sendPing,
		Func<TimeSpan, CancellationToken, Task> delay)
	{
		ArgumentNullException.ThrowIfNull(sendPing);
		ArgumentNullException.ThrowIfNull(delay);
		_sendPing = sendPing;
		_delay = delay;
	}

	public async Task<bool> IsReachableAsync(
		string host,
		int attempts,
		Action<string>? onPingError = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);

		try
		{
			for (int attempt = 0; attempt < attempts; attempt++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				IPStatus status = await _sendPing(host, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (status == IPStatus.Success)
				{
					return true;
				}

				await _delay(TimeSpan.FromMilliseconds(100), cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
			}

			return false;
		}
		catch (PingException exception)
		{
			cancellationToken.ThrowIfCancellationRequested();
			onPingError?.Invoke(exception.Message);
			return false;
		}
	}
}
