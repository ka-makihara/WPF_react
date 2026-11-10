using System.Collections.Concurrent;

namespace WpfApp1_cmd.Services;

public static class DialogHostShowCoordinator
{
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> DialogLocks =
		new(StringComparer.Ordinal);

	public static async Task<object?> ShowAsync(
		string identifier,
		Func<Task<object?>> showDialog)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
		ArgumentNullException.ThrowIfNull(showDialog);

		SemaphoreSlim dialogLock = DialogLocks.GetOrAdd(
			identifier,
			static _ => new SemaphoreSlim(1, 1));
		await dialogLock.WaitAsync();
		try
		{
			return await showDialog();
		}
		finally
		{
			dialogLock.Release();
		}
	}
}
