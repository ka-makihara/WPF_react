namespace WpfApp1_cmd.Services;

public sealed record TransferSelectionUnitInput(
	bool? IsSelected,
	string? Name,
	string Path,
	string? FuserPath);

public sealed record TransferSelectionModuleInput(
	bool? IsSelected,
	IReadOnlyList<TransferSelectionUnitInput> Units);

public sealed record TransferSelectionMachineInput(
	bool? IsSelected,
	IReadOnlyList<TransferSelectionModuleInput> Modules);

public sealed record TransferSelectionLcuInput(
	bool? IsSelected,
	IReadOnlyList<TransferSelectionMachineInput> Machines);

public sealed class TransferSelectionTreeBuilder
{
	public IReadOnlyList<TransferSelectionLcu> Build(
		IReadOnlyList<TransferSelectionLcuInput> lcus,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		cancellationToken.ThrowIfCancellationRequested();

		TransferSelectionLcu[] result = new TransferSelectionLcu[lcus.Count];
		for (int lcuIndex = 0; lcuIndex < lcus.Count; lcuIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TransferSelectionLcuInput lcu = lcus[lcuIndex];
			TransferSelectionMachine[] machines =
				new TransferSelectionMachine[lcu.Machines.Count];
			for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				TransferSelectionMachineInput machine = lcu.Machines[machineIndex];
				TransferSelectionModule[] modules =
					new TransferSelectionModule[machine.Modules.Count];
				for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					TransferSelectionModuleInput module = machine.Modules[moduleIndex];
					TransferSelectionUnit[] units =
						new TransferSelectionUnit[module.Units.Count];
					for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
					{
						cancellationToken.ThrowIfCancellationRequested();
						TransferSelectionUnitInput unit = module.Units[unitIndex];
						units[unitIndex] = new TransferSelectionUnit(
							unit.IsSelected == true,
							unit.Name,
							unit.Path,
							unit.FuserPath);
					}

					modules[moduleIndex] = new TransferSelectionModule(
						module.IsSelected != false,
						units);
				}

				machines[machineIndex] = new TransferSelectionMachine(
					machine.IsSelected != false,
					modules);
			}

			result[lcuIndex] = new TransferSelectionLcu(
				lcu.IsSelected != false,
				machines);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}
}
