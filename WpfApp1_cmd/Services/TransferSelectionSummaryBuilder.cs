using System.IO;

namespace WpfApp1_cmd.Services;

public sealed record TransferSelectionUnit(
	bool IsSelected,
	string? Name,
	string Path,
	string? FuserPath);

public sealed record TransferSelectionModule(
	bool IsSelected,
	IReadOnlyList<TransferSelectionUnit> Units);

public sealed record TransferSelectionMachine(
	bool IsSelected,
	IReadOnlyList<TransferSelectionModule> Modules);

public sealed record TransferSelectionLcu(
	bool IsSelected,
	IReadOnlyList<TransferSelectionMachine> Machines);

public sealed record TransferSelectionGroup(
	IReadOnlyList<string> Components,
	string? Mode);

public sealed record TransferSelectionSummary(
	long LcuCount,
	long MachineCount,
	long ModuleCount,
	long UnitCount,
	long FileCount,
	IReadOnlyList<string> Paths);

public sealed class TransferSelectionSummaryBuilder
{
	public long CalculateFtpTransferSize(
		long updateDataSize,
		IReadOnlyList<TransferSelectionLcu> lcus)
	{
		ArgumentNullException.ThrowIfNull(lcus);

		int selectedLcuCount = 0;
		foreach (TransferSelectionLcu lcu in lcus)
		{
			if (lcu.IsSelected)
			{
				selectedLcuCount++;
			}
		}

		return CalculateFtpTransferSize(updateDataSize, selectedLcuCount);
	}

	public long CalculateFtpTransferSize(long updateDataSize, int selectedLcuCount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(selectedLcuCount);
		return updateDataSize * selectedLcuCount;
	}

	public int CountSelectedUnits(
		IReadOnlyList<TransferSelectionLcu> lcus,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		cancellationToken.ThrowIfCancellationRequested();

		int count = 0;
		foreach (TransferSelectionLcu lcu in lcus)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!lcu.IsSelected)
			{
				continue;
			}

			foreach (TransferSelectionMachine machine in lcu.Machines)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!machine.IsSelected)
				{
					continue;
				}

				foreach (TransferSelectionModule module in machine.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!module.IsSelected)
					{
						continue;
					}

					foreach (TransferSelectionUnit unit in module.Units)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (unit.IsSelected)
						{
							count++;
						}
					}
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return count;
	}

	public TransferSelectionSummary Build(
		IReadOnlyList<TransferSelectionLcu> lcus,
		IReadOnlyList<TransferSelectionGroup> groups,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		ArgumentNullException.ThrowIfNull(groups);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, TransferSelectionGroup> groupsByComponent = new(StringComparer.Ordinal);
		TransferSelectionGroup? firstGroupWithNullComponent = null;
		foreach (TransferSelectionGroup group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in group.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component == null)
				{
					firstGroupWithNullComponent ??= group;
				}
				else
				{
					groupsByComponent.TryAdd(component, group);
				}
			}
		}

		long lcuCount = 0;
		long machineCount = 0;
		long moduleCount = 0;
		long unitCount = 0;
		long fileCount = 0;
		List<string> paths = [];
		HashSet<string> uniquePaths = new(StringComparer.Ordinal);
		Dictionary<string, string[]> folderFilesByDirectory =
			new(StringComparer.OrdinalIgnoreCase);

		foreach (TransferSelectionLcu lcu in lcus)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!lcu.IsSelected)
			{
				continue;
			}
			lcuCount++;

			foreach (TransferSelectionMachine machine in lcu.Machines)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!machine.IsSelected)
				{
					continue;
				}
				machineCount++;

				foreach (TransferSelectionModule module in machine.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!module.IsSelected)
					{
						continue;
					}
					moduleCount++;

					foreach (TransferSelectionUnit unit in module.Units)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (!unit.IsSelected)
						{
							continue;
						}
						unitCount++;

						TransferSelectionGroup? group = unit.Name == null
							? firstGroupWithNullComponent
							: groupsByComponent.GetValueOrDefault(unit.Name);
						if (group?.Mode == "Folder")
						{
							string sourcePath = (dataFolder + unit.Path).Replace("/", "\\");
							string directory = Path.GetDirectoryName(sourcePath) ?? ".\\";
							if (!folderFilesByDirectory.TryGetValue(directory, out string[]? folderFiles))
							{
								List<string> files = [];
								foreach (string file in Directory.EnumerateFiles(
									directory,
									"*",
									SearchOption.AllDirectories))
								{
									cancellationToken.ThrowIfCancellationRequested();
									files.Add(file);
								}

								cancellationToken.ThrowIfCancellationRequested();
								folderFiles = files.ToArray();
								folderFilesByDirectory.Add(directory, folderFiles);
							}

							foreach (string file in folderFiles)
							{
								cancellationToken.ThrowIfCancellationRequested();
								if (uniquePaths.Add(file))
								{
									fileCount++;
									paths.Add(file);
								}
							}
							continue;
						}

						string unitPath = dataFolder + unit.Path.Replace("/", "\\");
						if (uniquePaths.Add(unitPath))
						{
							paths.Add(unitPath);
						}
						fileCount++;
						if (!string.IsNullOrEmpty(unit.FuserPath))
						{
							string fuserPath = dataFolder + unit.FuserPath.Replace("/", "\\");
							if (uniquePaths.Add(fuserPath))
							{
								paths.Add(fuserPath);
							}
							fileCount++;
						}
					}
				}
			}
		}

		string[] resultPaths = new string[paths.Count];
		for (int index = 0; index < paths.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			resultPaths[index] = paths[index];
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(
			lcuCount,
			machineCount,
			moduleCount,
			unitCount,
			fileCount,
			resultPaths);
	}
}
