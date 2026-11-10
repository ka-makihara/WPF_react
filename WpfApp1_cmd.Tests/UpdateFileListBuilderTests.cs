using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public class UpdateFileListBuilderTests
{
	private const string DataFolder = @"C:\Updates\";

	[Fact]
	public void Create_FileModeIncludesSelectedFilesAndFirmwarePathsOnly()
	{
		var groups = new[]
		{
			new UpdateFileGroup("Control", ["Control"], ["Main", "Aux"], "File")
		};
		var versions = new[]
		{
			new UpdateFileVersion("Main", true, @"\Main.bin", @"\Main.fus", "Control"),
			new UpdateFileVersion("Aux", false, @"\Aux.bin", "", "Control")
		};

		var result = UpdateFileListBuilder.Create(groups, versions, [], DataFolder);

		var group = Assert.Single(result);
		Assert.Equal("Control", group.GroupName);
		var file = Assert.Single(group.Units);
		Assert.Equal("Main", file.Name);
		Assert.Equal(DataFolder + @"\Main.bin", file.Path);
		Assert.Equal(DataFolder + @"\Main.fus", file.FusePath);
	}

	[Fact]
	public void Create_FileModeDoesNotAddTheSameFileTwice()
	{
		var groups = new[]
		{
			new UpdateFileGroup("Control", ["Control"], ["Main"], "File")
		};
		var versions = new[]
		{
			new UpdateFileVersion("Main", true, @"\Main.bin", "", "Control"),
			new UpdateFileVersion("Main", true, @"\Main.bin", "", "Control")
		};

		var result = UpdateFileListBuilder.Create(groups, versions, [], DataFolder);

		var group = Assert.Single(result);
		Assert.Single(group.Units);
	}

	[Fact]
	public void Create_FolderModeIncludesSelectedKnownFilesAndUnlistedFilesInSelectedGroup()
	{
		var groups = new[]
		{
			new UpdateFileGroup("Control", ["Control", "FolderAlias"], ["Main"], "Folder")
		};
		var versions = new[]
		{
			new UpdateFileVersion("Main", true, @"\Folder\Main.bin", "", "FolderAlias"),
			new UpdateFileVersion("Other", false, @"\Folder\Other.bin", "", "FolderAlias")
		};
		var updatePaths = new[]
		{
			new UpdateFilePath("FolderAlias",
			[
				new UpdateFileEntry("Main", @"\Folder\Main.bin", ""),
				new UpdateFileEntry("Control", @"\Folder\Unlisted.bin", "")
			])
		};

		var result = UpdateFileListBuilder.Create(groups, versions, updatePaths, DataFolder);

		var group = Assert.Single(result);
		Assert.Collection(
			group.Units,
			file => Assert.Equal(DataFolder + @"\Folder\Main.bin", file.Path),
			file => Assert.Equal(DataFolder + @"\Folder\Unlisted.bin", file.Path));
	}

	[Fact]
	public void Create_DoesNotAddUnlistedFolderFilesWhenNoVersionInGroupIsSelected()
	{
		var groups = new[]
		{
			new UpdateFileGroup("Control", ["Control", "FolderAlias"], ["Main"], "Folder")
		};
		var versions = new[]
		{
			new UpdateFileVersion("Main", false, @"\Folder\Main.bin", "", "FolderAlias")
		};
		var updatePaths = new[]
		{
			new UpdateFilePath("FolderAlias",
			[
				new UpdateFileEntry("Main", @"\Folder\Main.bin", ""),
				new UpdateFileEntry("Control", @"\Folder\Unlisted.bin", "")
			])
		};

		var result = UpdateFileListBuilder.Create(groups, versions, updatePaths, DataFolder);

		Assert.Empty(result);
	}

	[Fact]
	public void Create_IgnoresVersionsWithoutAConfiguredGroup()
	{
		var versions = new[]
		{
			new UpdateFileVersion("Unmapped", true, @"\Unmapped.bin", "", null)
		};

		var result = UpdateFileListBuilder.Create([], versions, [], DataFolder);

		Assert.Empty(result);
	}

	[Fact]
	public void Create_UsesFirstConfiguredGroupForDuplicateComponents()
	{
		UpdateFileGroup[] groups =
		[
			new("First", ["First"], ["Unit"], "File"),
			new("Second", ["Second"], ["Unit"], "File")
		];
		UpdateFileVersion[] versions =
		[
			new("Unit", true, @"\Unit.bin", "", "First")
		];

		List<UpdateFileGroupResult> result =
			UpdateFileListBuilder.Create(groups, versions, [], DataFolder);

		Assert.Equal("First", Assert.Single(result).GroupName);
	}

	[Fact]
	public void Create_ReusesFirstResultMatchingAnyConfiguredAlias()
	{
		UpdateFileGroup[] groups =
		[
			new("First", ["First"], ["UnitA"], "File"),
			new("Second", ["Second", "First"], ["UnitB"], "File")
		];
		UpdateFileVersion[] versions =
		[
			new("UnitA", true, @"\UnitA.bin", "", null),
			new("UnitB", true, @"\UnitB.bin", "", null)
		];

		List<UpdateFileGroupResult> result =
			UpdateFileListBuilder.Create(groups, versions, [], DataFolder);

		UpdateFileGroupResult group = Assert.Single(result);
		Assert.Equal("First", group.GroupName);
		Assert.Equal(
			[DataFolder + @"\UnitA.bin", DataFolder + @"\UnitB.bin"],
			group.Units.Select(unit => unit.Path));
	}

	[Fact]
	public void Create_UsesFirstUpdatePathByListOrderAcrossAliases()
	{
		UpdateFileGroup[] groups =
		[
			new("Folder", ["AliasA", "AliasB"], ["UnitA", "UnitB"], "Folder")
		];
		UpdateFileVersion[] versions =
		[
			new("UnitA", true, @"\Folder\A.bin", "", "AliasA"),
			new("UnitB", true, @"\Folder\B.bin", "", "AliasB")
		];
		UpdateFilePath[] paths =
		[
			new("AliasB", [new UpdateFileEntry("UnitB", @"\Folder\B.bin", "")]),
			new("AliasA", [new UpdateFileEntry("UnitA", @"\Folder\A.bin", "")])
		];

		List<UpdateFileGroupResult> result =
			UpdateFileListBuilder.Create(groups, versions, paths, DataFolder);

		Assert.Equal(2, result.Count);
		Assert.All(
			result,
			group => Assert.Equal(
				[DataFolder + @"\Folder\B.bin"],
				group.Units.Select(unit => unit.Path)));
	}

	[Fact]
	public void Create_ThrowsWhenCancelledBeforeBuilding()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			UpdateFileListBuilder.Create([], [], [], DataFolder, cancellation.Token));
	}

	[Fact]
	public void CountFiles_CountsPrimaryAndFuserPathsOnlyWhenPresent()
	{
		UpdateFileGroupResult[] groups =
		[
			new("GroupA",
			[
				new UpdateFileListEntry("UnitA", @"C:\Data\unit.bin", @"C:\Data\fuser.bin"),
				new UpdateFileListEntry("UnitB", @"C:\Data\other.bin", ""),
				new UpdateFileListEntry("UnitC", null, null)
			])
		];

		Assert.Equal(3, UpdateFileListBuilder.CountFiles(groups));
	}

	[Fact]
	public void CountFiles_ThrowsWhenCancelledBeforeCounting()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			UpdateFileListBuilder.CountFiles([], cancellation.Token));
	}
}
