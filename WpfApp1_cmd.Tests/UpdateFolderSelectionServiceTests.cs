using System.IO;
using WpfApp1_cmd.Services;
using Xunit;

public sealed class UpdateFolderSelectionServiceTests
{
	[Fact]
	public void CreateSelectedFolders_UsesSelectedUnitFoldersAndDeduplicates()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			List<UpdateFolderMachine> machines =
			[
				new(
					true,
					[
						new(
							true,
							[
								new(true, @"\Fuji\Peripheral\UnitA\main.bin", @"\Fuji\Peripheral\UnitA\boot.bin"),
								new(false, @"\Fuji\Peripheral\UnitB\main.bin", "")
							])
					])
			];
			List<UpdateFolderFileInfo> files =
			[
				new(@"\Fuji\Peripheral\UnitA\main.bin", @"\Fuji\Peripheral\UnitA\boot.bin"),
				new(@"\Fuji\Peripheral\UnitB\main.bin", null)
			];

			List<string> folders = new UpdateFolderSelectionService().CreateSelectedFolders(
				machines,
				files,
				root);

			Assert.Equal([root + @"\Fuji\Peripheral\UnitA"], folders);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void CreateSelectedFolders_UsesAllExistingDataWhenModuleHasNoUnits()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string updateFolder = root + @"\Fuji\Peripheral";
			Directory.CreateDirectory(updateFolder);
			File.WriteAllText(Path.Combine(updateFolder, "main.bin"), "data");
			List<UpdateFolderMachine> machines =
			[
				new(true, [new(true, [])])
			];
			List<UpdateFolderFileInfo> files =
			[
				new(@"\Fuji\Peripheral\main.bin", null),
				new(@"\Fuji\Peripheral\missing.bin", null)
			];

			List<string> folders = new UpdateFolderSelectionService().CreateSelectedFolders(
				machines,
				files,
				root);

			Assert.Equal([updateFolder], folders);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void CreateSelectedFolders_MatchesPathsWithOrdinalCaseSensitivity()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			List<string> folders = new UpdateFolderSelectionService().CreateSelectedFolders(
				[
					new(true,
					[
						new(true,
						[
							new(true, @"\Fuji\Peripheral\unit.bin", "")
						])
					])
				],
				[new UpdateFolderFileInfo(@"\Fuji\Peripheral\UNIT.bin", null)],
				root);

			Assert.Empty(folders);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void CreateSelectedFolders_ThrowsWhenCancelledBeforeProcessing()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UpdateFolderSelectionService().CreateSelectedFolders(
				[],
				[],
				Path.GetTempPath(),
				cancellation.Token));
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
