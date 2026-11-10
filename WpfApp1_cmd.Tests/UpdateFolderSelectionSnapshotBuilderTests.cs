using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateFolderSelectionSnapshotBuilderTests
{
	[Theory]
	[InlineData(true, true)]
	[InlineData(null, true)]
	[InlineData(true, null)]
	[InlineData(null, null)]
	[InlineData(false, null)]
	[InlineData(null, false)]
	public void Build_IncludesPartiallySelectedParentsButOnlyCheckedUnits(
		bool? machineSelection,
		bool? moduleSelection)
	{
		const string selectedPath = "/Selected/unit.bin";
		UpdateFolderSelectionSnapshot snapshot =
			new UpdateFolderSelectionSnapshotBuilder().Build(
				[new(machineSelection,
					[new(moduleSelection,
						[
							new(true, selectedPath, ""),
							new(false, "/Unchecked/unit.bin", ""),
							new(null, "/Indeterminate/unit.bin", "")
						])])],
				[
					new(selectedPath, ""),
					new("/Unchecked/unit.bin", ""),
					new("/Indeterminate/unit.bin", "")
				]);
		string root = Path.Combine(Path.GetTempPath(), "PartialSelection");

		List<string> folders = new UpdateFolderSelectionService().CreateSelectedFolders(
			snapshot.Machines,
			snapshot.UpdateDataFiles,
			root);

		Assert.Equal(machineSelection != false, snapshot.Machines[0].IsSelected);
		Assert.Equal(moduleSelection != false, snapshot.Machines[0].Modules[0].IsSelected);
		Assert.Equal([true, false, false],
			snapshot.Machines[0].Modules[0].Units.Select(unit => unit.IsSelected));
		if (machineSelection != false && moduleSelection != false)
		{
			Assert.Equal(root + Path.GetDirectoryName(selectedPath), Assert.Single(folders));
		}
		else
		{
			Assert.Empty(folders);
		}
	}

	[Fact]
	public void Build_MapsNullableSelectionAndPreservesFileOrder()
	{
		UpdateFolderSelectionSnapshot result =
			new UpdateFolderSelectionSnapshotBuilder().Build(
				[
					new UpdateFolderMachineSnapshot(
						true,
						[
							new UpdateFolderModuleSnapshot(
								true,
								[
									new UpdateFolderUnitSnapshot(
										true,
										"/unit.bin",
										"/fuser.bin"),
									new UpdateFolderUnitSnapshot(
										null,
										"/unselected.bin",
										"")
								])
						]),
					new UpdateFolderMachineSnapshot(false, [])
				],
				[
					new UpdateFolderFileInfo("/first.bin", null),
					new UpdateFolderFileInfo("/second.bin", "/second-fuser.bin")
				]);

		Assert.Equal(2, result.Machines.Count);
		Assert.True(result.Machines[0].IsSelected);
		Assert.True(Assert.Single(result.Machines[0].Modules).IsSelected);
		Assert.Equal(
			[
				new UpdateFolderUnit(true, "/unit.bin", "/fuser.bin"),
				new UpdateFolderUnit(false, "/unselected.bin", "")
			],
			Assert.Single(result.Machines[0].Modules).Units);
		Assert.False(result.Machines[1].IsSelected);
		Assert.Equal(
			[
				new UpdateFolderFileInfo("/first.bin", null),
				new UpdateFolderFileInfo("/second.bin", "/second-fuser.bin")
			],
			result.UpdateDataFiles);
	}

	[Fact]
	public void Build_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UpdateFolderSelectionSnapshotBuilder().Build(
				[],
				[],
				cancellation.Token));
	}
}
