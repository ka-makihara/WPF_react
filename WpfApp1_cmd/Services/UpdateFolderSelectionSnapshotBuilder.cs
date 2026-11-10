namespace WpfApp1_cmd.Services;

public sealed record UpdateFolderUnitSnapshot(
	bool? IsSelected,
	string Path,
	string FusePath);

public sealed record UpdateFolderModuleSnapshot(
	bool? IsSelected,
	IReadOnlyList<UpdateFolderUnitSnapshot> Units);

public sealed record UpdateFolderMachineSnapshot(
	bool? IsSelected,
	IReadOnlyList<UpdateFolderModuleSnapshot> Modules);

public sealed record UpdateFolderSelectionSnapshot(
	IReadOnlyList<UpdateFolderMachine> Machines,
	IReadOnlyList<UpdateFolderFileInfo> UpdateDataFiles);

public sealed class UpdateFolderSelectionSnapshotBuilder
{
	public UpdateFolderSelectionSnapshot Build(
		IReadOnlyList<UpdateFolderMachineSnapshot> machines,
		IReadOnlyList<UpdateFolderFileInfo> updateDataFiles,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(updateDataFiles);
		cancellationToken.ThrowIfCancellationRequested();

		UpdateFolderMachine[] result = new UpdateFolderMachine[machines.Count];
		for (int machineIndex = 0; machineIndex < machines.Count; machineIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateFolderMachineSnapshot machine = machines[machineIndex];
			UpdateFolderModule[] modules = new UpdateFolderModule[machine.Modules.Count];
			for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UpdateFolderModuleSnapshot module = machine.Modules[moduleIndex];
				UpdateFolderUnit[] units = new UpdateFolderUnit[module.Units.Count];
				for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					UpdateFolderUnitSnapshot unit = module.Units[unitIndex];
					units[unitIndex] = new UpdateFolderUnit(
						unit.IsSelected == true,
						unit.Path,
						unit.FusePath);
				}

				modules[moduleIndex] = new UpdateFolderModule(
					module.IsSelected != false,
					units);
			}

			result[machineIndex] = new UpdateFolderMachine(
				machine.IsSelected != false,
				modules);
		}

		UpdateFolderFileInfo[] fileResults = new UpdateFolderFileInfo[updateDataFiles.Count];
		for (int index = 0; index < fileResults.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateFolderFileInfo file = updateDataFiles[index];
			fileResults[index] = new UpdateFolderFileInfo(file.Path, file.FusePath);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(result, fileResults);
	}
}
