namespace WpfApp1_cmd.Services;

public sealed record TreeSelectionUnitInput(string? Name, bool? IsSelected);

public sealed record TreeSelectionModuleInput(
	string? Name,
	bool? IsSelected,
	IReadOnlyList<TreeSelectionUnitInput> Units);

public sealed record TreeSelectionMachineInput(
	string? Name,
	bool? IsSelected,
	IReadOnlyList<TreeSelectionModuleInput> Modules);

public sealed record TreeSelectionLcuInput(
	string? Name,
	bool? IsSelected,
	IReadOnlyList<TreeSelectionMachineInput> Machines);

public sealed class TreeSelectionDiagnosticBuilder
{
	public IReadOnlyList<string> Build(IReadOnlyList<TreeSelectionLcuInput> lcus)
	{
		ArgumentNullException.ThrowIfNull(lcus);

		List<string> messages = [];
		foreach (TreeSelectionLcuInput lcu in lcus)
		{
			ArgumentNullException.ThrowIfNull(lcu);
			messages.Add($"{lcu.Name}:{FormatSelection(lcu.IsSelected)}");
			foreach (TreeSelectionMachineInput machine in lcu.Machines)
			{
				ArgumentNullException.ThrowIfNull(machine);
				messages.Add($"    {machine.Name}:{FormatSelection(machine.IsSelected)}");
				foreach (TreeSelectionModuleInput module in machine.Modules)
				{
					ArgumentNullException.ThrowIfNull(module);
					messages.Add($"       {module.Name}:{FormatSelection(module.IsSelected)}");
					foreach (TreeSelectionUnitInput unit in module.Units)
					{
						ArgumentNullException.ThrowIfNull(unit);
						messages.Add($"          {unit.Name}:{FormatSelection(unit.IsSelected)}");
					}
				}
			}
		}

		return messages;
	}

	private static string FormatSelection(bool? value) =>
		value switch
		{
			true => "True",
			false => "False",
			null => "Null"
		};
}
