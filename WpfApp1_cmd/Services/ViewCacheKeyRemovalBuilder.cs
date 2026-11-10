namespace WpfApp1_cmd.Services;

public sealed class ViewCacheKeyRemovalBuilder
{
	public IReadOnlyList<string> Build(
		IEnumerable<string> keys,
		string viewName,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(keys);
		ArgumentNullException.ThrowIfNull(viewName);
		cancellationToken.ThrowIfCancellationRequested();

		List<string> keysToRemove = [];
		foreach (string key in keys)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (key.Contains(viewName))
			{
				keysToRemove.Add(key);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return keysToRemove;
	}
}
