namespace WpfApp1_cmd.Services;

public sealed record TransferSelectionGroupInput(
	IReadOnlyList<string> Components,
	string? Mode);

public sealed class TransferSelectionGroupBuilder
{
	public IReadOnlyList<TransferSelectionGroup> Build(
		IReadOnlyList<TransferSelectionGroupInput> groups,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		cancellationToken.ThrowIfCancellationRequested();

		TransferSelectionGroup[] result = new TransferSelectionGroup[groups.Count];
		for (int index = 0; index < groups.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TransferSelectionGroupInput group = groups[index];
			string[] components = new string[group.Components.Count];
			for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				components[componentIndex] = group.Components[componentIndex];
			}

			result[index] = new TransferSelectionGroup(components, group.Mode);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}
}
