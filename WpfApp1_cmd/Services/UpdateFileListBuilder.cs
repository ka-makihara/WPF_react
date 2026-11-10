namespace WpfApp1_cmd.Services;

public sealed record UpdateFileGroup(
	string Name,
	IReadOnlyList<string> NameAliases,
	IReadOnlyList<string> Components,
	string? Mode);

public sealed record UpdateFileVersion(
	string? Name,
	bool? IsSelected,
	string? Path,
	string? FuserPath,
	string? UnitGroup);

public sealed record UpdateFilePath(
	string GroupName,
	IReadOnlyList<UpdateFileEntry>? Units);

public sealed record UpdateFileEntry(string? Name, string? Path, string? FusePath);

public sealed record UpdateFileListEntry(string? Name, string? Path, string? FusePath);

public sealed record UpdateFileGroupResult(
	string GroupName,
	List<UpdateFileListEntry> Units);

public static class UpdateFileListBuilder
{
	public static int CountFiles(
		IReadOnlyList<UpdateFileGroupResult> groups,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		cancellationToken.ThrowIfCancellationRequested();

		int count = 0;
		foreach (UpdateFileGroupResult group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (UpdateFileListEntry unit in group.Units)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int fileCount =
					(string.IsNullOrEmpty(unit.Path) ? 0 : 1)
					+ (string.IsNullOrEmpty(unit.FusePath) ? 0 : 1);
				count = checked(count + fileCount);
			}
		}

		return count;
	}

	public static List<UpdateFileGroupResult> Create(
		IReadOnlyList<UpdateFileGroup> groups,
		IReadOnlyList<UpdateFileVersion> versions,
		IReadOnlyList<UpdateFilePath> updatePaths,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		ArgumentNullException.ThrowIfNull(versions);
		ArgumentNullException.ThrowIfNull(updatePaths);
		ArgumentNullException.ThrowIfNull(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, UpdateFileGroup> groupsByComponent = new(StringComparer.Ordinal);
		UpdateFileGroup? firstGroupWithNullComponent = null;
		foreach (UpdateFileGroup group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in group.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component == null)
				{
					firstGroupWithNullComponent ??= group;
					continue;
				}

				if (!groupsByComponent.TryAdd(component, group))
				{
					continue;
				}
			}
		}

		Dictionary<string, UpdateFileVersion> versionsByPath = new(StringComparer.Ordinal);
		UpdateFileVersion? firstVersionWithNullPath = null;
		Dictionary<string, int> selectedCountsByGroup = new(StringComparer.Ordinal);
		int selectedCountWithNullGroup = 0;
		foreach (UpdateFileVersion version in versions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (version.Path == null)
			{
				firstVersionWithNullPath ??= version;
			}
			else
			{
				versionsByPath.TryAdd(version.Path, version);
			}

			if (version.IsSelected == true)
			{
				if (version.UnitGroup == null)
				{
					selectedCountWithNullGroup++;
				}
				else
				{
					selectedCountsByGroup.TryGetValue(version.UnitGroup, out int count);
					selectedCountsByGroup[version.UnitGroup] = count + 1;
				}
			}
		}

		List<UpdateFileGroupResult> unitFiles = [];
		Dictionary<UpdateFileGroupResult, HashSet<(string? Name, string Path)>> addedFiles = [];
		StringComparer groupNameComparer = StringComparer.CurrentCultureIgnoreCase;
		Dictionary<string, int> firstResultIndexesByName = new(groupNameComparer);
		Dictionary<string, int> firstUpdatePathIndexesByName = new(groupNameComparer);
		for (int index = 0; index < updatePaths.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string groupName = updatePaths[index].GroupName;
			if (!string.IsNullOrEmpty(groupName))
			{
				firstUpdatePathIndexesByName.TryAdd(groupName, index);
			}
		}

		foreach (UpdateFileVersion item in versions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateFileGroup? group = item.Name == null
				? firstGroupWithNullComponent
				: groupsByComponent.GetValueOrDefault(item.Name);
			if (group == null)
			{
				continue;
			}

			int unitGroupIndex = FindFirstMatchIndex(
				group,
				firstResultIndexesByName,
				cancellationToken);
			UpdateFileGroupResult? unitGroup = unitGroupIndex >= 0
				? unitFiles[unitGroupIndex]
				: null;

			if (group.Mode == "File")
			{
				if (item.IsSelected == false)
				{
					continue;
				}

				if (unitGroup == null)
				{
					unitGroup = new UpdateFileGroupResult(group.Name, []);
					unitFiles.Add(unitGroup);
					addedFiles.Add(unitGroup, []);
					AddResultNames(
						group.Name,
						unitFiles.Count - 1,
						firstResultIndexesByName);
				}

				AddUnitFile(
					unitGroup,
					addedFiles[unitGroup],
					dataFolder,
					new UpdateFileEntry(item.Name, item.Path, item.FuserPath));
				continue;
			}

			int updatePathIndex = FindFirstMatchIndex(
				group,
				firstUpdatePathIndexesByName,
				cancellationToken);
			UpdateFilePath? updatePath = updatePathIndex >= 0
				? updatePaths[updatePathIndex]
				: null;
			if (updatePath!.Units == null || updatePath.Units.Count == 0)
			{
				continue;
			}

			foreach (UpdateFileEntry unit in updatePath.Units)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UpdateFileVersion? version = unit.Path == null
					? firstVersionWithNullPath
					: versionsByPath.GetValueOrDefault(unit.Path);
				if (version != null)
				{
					if (version.IsSelected == false)
					{
						continue;
					}

					if (unitGroup == null)
					{
						unitGroup = new UpdateFileGroupResult(group.Name, []);
						unitFiles.Add(unitGroup);
						addedFiles.Add(unitGroup, []);
						AddResultNames(
							group.Name,
							unitFiles.Count - 1,
							firstResultIndexesByName);
						AddUnitFile(unitGroup, addedFiles[unitGroup], dataFolder, unit);
					}
					else
					{
						AddUnitFile(
							unitGroup,
							addedFiles[unitGroup],
							dataFolder,
							unit with { FusePath = "" });
					}
				}
				else
				{
					int selectedCount = updatePath.GroupName == null
						? selectedCountWithNullGroup
						: selectedCountsByGroup.GetValueOrDefault(updatePath.GroupName);
					if (selectedCount != 0)
					{
						if (unitGroup == null)
						{
							unitGroup = new UpdateFileGroupResult(group.Name, []);
							unitFiles.Add(unitGroup);
							addedFiles.Add(unitGroup, []);
							AddResultNames(
								group.Name,
								unitFiles.Count - 1,
								firstResultIndexesByName);
							AddUnitFile(unitGroup, addedFiles[unitGroup], dataFolder, unit);
						}
						else
						{
							AddUnitFile(
								unitGroup,
								addedFiles[unitGroup],
								dataFolder,
								unit with { FusePath = "" });
						}
					}
				}
			}
		}

		return unitFiles;
	}

	private static int FindFirstMatchIndex(
		UpdateFileGroup group,
		IReadOnlyDictionary<string, int> indexesByName,
		CancellationToken cancellationToken)
	{
		int firstIndex = int.MaxValue;
		foreach (string alias in group.NameAliases)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!string.IsNullOrEmpty(alias)
				&& indexesByName.TryGetValue(alias, out int index))
			{
				firstIndex = Math.Min(firstIndex, index);
			}
		}

		return firstIndex == int.MaxValue ? -1 : firstIndex;
	}

	private static void AddResultNames(
		string groupName,
		int index,
		Dictionary<string, int> indexesByName)
	{
		if (!string.IsNullOrEmpty(groupName))
		{
			indexesByName.TryAdd(groupName, index);
		}
	}

	private static void AddUnitFile(
		UpdateFileGroupResult group,
		HashSet<(string? Name, string Path)> addedFiles,
		string dataFolder,
		UpdateFileEntry unit)
	{
		string path = dataFolder + unit.Path;
		if (!addedFiles.Add((unit.Name, path)))
		{
			return;
		}

		group.Units.Add(new UpdateFileListEntry(
			unit.Name,
			path,
			string.IsNullOrEmpty(unit.FusePath) ? "" : dataFolder + unit.FusePath));
	}
}
