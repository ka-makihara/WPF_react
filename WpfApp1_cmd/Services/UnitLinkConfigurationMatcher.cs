namespace WpfApp1_cmd.Services;

public static class UnitLinkConfigurationMatcher
{
	public static bool HasSameName(
		IReadOnlyDictionary<string, object> existing,
		IReadOnlyDictionary<string, string> configured)
	{
		ArgumentNullException.ThrowIfNull(existing);
		ArgumentNullException.ThrowIfNull(configured);

		if (existing.Count != configured.Count)
		{
			return false;
		}

		return existing.All(pair =>
			configured.TryGetValue(pair.Key, out string? value) &&
			string.Equals(pair.Value.ToString(), value, StringComparison.Ordinal));
	}
}
