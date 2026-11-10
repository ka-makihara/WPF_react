namespace WpfApp1_cmd.Services;

public sealed class UnitVersionIndexLookup
{
	private readonly IReadOnlyDictionary<string, int> _indicesByName;
	private readonly int? _indexWithoutName;

	internal UnitVersionIndexLookup(
		IReadOnlyDictionary<string, int> indicesByName,
		int? indexWithoutName)
	{
		_indicesByName = indicesByName;
		_indexWithoutName = indexWithoutName;
	}

	public int? FindIndex(string? name) =>
		name == null
			? _indexWithoutName
			: _indicesByName.TryGetValue(name, out int index) ? index : null;
}

public sealed class UnitVersionIndexLookupBuilder
{
	public UnitVersionIndexLookup Build(
		IReadOnlyList<string?> names,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(names);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, int> indicesByName = new(StringComparer.Ordinal);
		int? indexWithoutName = null;
		for (int index = 0; index < names.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string? name = names[index];
			if (name == null)
			{
				indexWithoutName ??= index;
			}
			else
			{
				indicesByName.TryAdd(name, index);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(indicesByName, indexWithoutName);
	}
}
