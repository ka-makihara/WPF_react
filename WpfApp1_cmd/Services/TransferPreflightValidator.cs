namespace WpfApp1_cmd.Services;

public enum TransferPreflightOutcome
{
	Ready,
	NoTargetSelected
}

public sealed class TransferPreflightValidator
{
	public TransferPreflightOutcome Validate(
		TransferSelectionSummary selection)
	{
		ArgumentNullException.ThrowIfNull(selection);

		if (selection.LcuCount == 0
			&& selection.MachineCount == 0
			&& selection.ModuleCount == 0)
		{
			return TransferPreflightOutcome.NoTargetSelected;
		}

		return TransferPreflightOutcome.Ready;
	}
}
