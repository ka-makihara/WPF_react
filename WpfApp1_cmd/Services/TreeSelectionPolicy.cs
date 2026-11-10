namespace WpfApp1_cmd.Services;

public sealed record TreeSelectionDecision(
	bool? Selection,
	bool PropagateToChildren);

public sealed class TreeSelectionPolicy
{
	public TreeSelectionDecision Evaluate(ItemStatus status, bool? requestedSelection) =>
		status == ItemStatus.NOT_SUPPORTED
			? new(false, false)
			: new(requestedSelection, true);
}
