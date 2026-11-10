using System.IO;

namespace WpfApp1_cmd.Services;

public sealed record UpdateFolderFileInfo(string? Path, string? FusePath);

public sealed record UpdateFolderUnit(bool IsSelected, string Path, string FusePath);

public sealed record UpdateFolderModule(bool IsSelected, IReadOnlyList<UpdateFolderUnit> Units);

public sealed record UpdateFolderMachine(bool IsSelected, IReadOnlyList<UpdateFolderModule> Modules);

public sealed class UpdateFolderSelectionService
{
	public List<string> CreateAllFolders(
		IReadOnlyList<string> updateDataPaths,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(updateDataPaths);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		List<string> folders = [];
		HashSet<string> uniqueFolders = new(StringComparer.Ordinal);
		foreach (string updateDataPath in updateDataPaths)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string localPath = dataFolder + updateDataPath;
			if (!Path.Exists(localPath))
			{
				continue;
			}

			if ((File.GetAttributes(localPath) & FileAttributes.Directory) == FileAttributes.Directory)
			{
				AddUniqueFolder(folders, uniqueFolders, localPath);
			}
			else
			{
				string? directory = Path.GetDirectoryName(localPath);
				if (directory != null)
				{
					AddUniqueFolder(folders, uniqueFolders, directory);
				}
			}
		}

		return folders;
	}

	public List<string> CreateSelectedFolders(
		IReadOnlyList<UpdateFolderMachine> machines,
		IReadOnlyList<UpdateFolderFileInfo> updateDataFiles,
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(updateDataFiles);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		HashSet<string> updateDataPaths = new(StringComparer.Ordinal);
		foreach (UpdateFolderFileInfo file in updateDataFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (file.Path != null)
			{
				updateDataPaths.Add(file.Path);
			}
			if (file.FusePath != null)
			{
				updateDataPaths.Add(file.FusePath);
			}
		}

		List<string> folders = [];
		HashSet<string> uniqueFolders = new(StringComparer.Ordinal);
		foreach (UpdateFolderMachine machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!machine.IsSelected)
			{
				continue;
			}

			foreach (UpdateFolderModule module in machine.Modules)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!module.IsSelected)
				{
					continue;
				}

				if (module.Units.Count == 0)
				{
					List<string> selectedUpdateDataPaths = [];
					HashSet<string> uniquePaths = new(StringComparer.Ordinal);
					foreach (UpdateFolderFileInfo file in updateDataFiles)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (file.Path != null && uniquePaths.Add(file.Path))
						{
							selectedUpdateDataPaths.Add(file.Path);
						}
					}

					foreach (string folder in CreateAllFolders(
						selectedUpdateDataPaths,
						dataFolder,
						cancellationToken))
					{
						cancellationToken.ThrowIfCancellationRequested();
						AddUniqueFolder(folders, uniqueFolders, folder);
					}

					return folders;
				}

				foreach (UpdateFolderUnit unit in module.Units)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!unit.IsSelected)
					{
						continue;
					}

					if (!string.IsNullOrEmpty(unit.Path) && updateDataPaths.Contains(unit.Path))
					{
						AddUniqueFolder(
							folders,
							uniqueFolders,
							dataFolder + Path.GetDirectoryName(unit.Path));
					}
					if (!string.IsNullOrEmpty(unit.FusePath) && updateDataPaths.Contains(unit.FusePath))
					{
						AddUniqueFolder(
							folders,
							uniqueFolders,
							dataFolder + Path.GetDirectoryName(unit.FusePath));
					}
				}
			}
		}

		return folders;
	}

	private static void AddUniqueFolder(
		List<string> folders,
		HashSet<string> uniqueFolders,
		string folder)
	{
		if (uniqueFolders.Add(folder))
		{
			folders.Add(folder);
		}
	}
}
