using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_PrepareTests
{
	[Fact]
	public void Prepare_ThrowsWhenCancellationIsRequestedBeforePreparation()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		UnitVersionPreparationRequest request = new(
			[],
			[],
			[],
			[],
			"MachineA",
			Path.GetTempPath(),
			IgnoreVersion: false,
			IsModuleSelected: true,
			IsBackupData: false,
			IsBaseUpdateInfo: false,
			UnifyGroupSelection: false,
			NotUpdateAttribute: 2);

		Assert.Throws<OperationCanceledException>(() =>
			CreateService().Prepare(request, cancellation.Token));
	}

	[Fact]
	public void Prepare_SelectsCandidatesBuildsUnitsAndUnifiesGroups()
	{
		string root = Path.Combine(Path.GetTempPath(), $"UnitVersionPreparation-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "unit-b.bin"), "payload");

		try
		{
			UnitVersionPreparationResult result = CreateService().Prepare(
				new UnitVersionPreparationRequest(
					[
						new UpdateVersionCandidate("UnitA", "1.0"),
						new UpdateVersionCandidate("UnitB", "2.0")
					],
					[
						new UnitVersionDefinition("UnitA", "unit-a.bin", null, "0", "1.0"),
						new UnitVersionDefinition("UnitB", "unit-b.bin", null, "0", "1.5")
					],
					[new UnitGroupSelection("GroupA", false)],
					[new UnitVersionGroupDefinition(
						["UnitA", "UnitB"],
						new Dictionary<string, string> { ["MachineA"] = "GroupA" })],
					"MachineA",
					root + Path.DirectorySeparatorChar,
					IgnoreVersion: false,
					IsModuleSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					UnifyGroupSelection: true,
					NotUpdateAttribute: 2));

			Assert.Single(result.SkippedSameVersion);
			Assert.Equal("UnitA", result.SkippedSameVersion[0].Name);
			UnitVersionPreparationItem item = Assert.Single(result.Items);
			Assert.Equal("UnitB", item.UnitName);
			Assert.Equal(UnitVersionBuildOutcome.Created, item.BuildResult.Outcome);
			Assert.Equal("GroupA", item.BuildResult.UnitGroup);
			Assert.Equal([0], result.ExistingUnitsToDeselect);
			Assert.Equal([0], result.NewItemsToDeselect);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Prepare_DoesNotUnifyGroupsWhenNotRequested()
	{
		string root = Path.Combine(Path.GetTempPath(), $"UnitVersionPreparation-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "unit.bin"), "payload");

		try
		{
			UnitVersionPreparationResult result = CreateService().Prepare(
				new UnitVersionPreparationRequest(
					[new UpdateVersionCandidate("UnitA", "2.0")],
					[new UnitVersionDefinition("UnitA", "unit.bin", null, "0", "1.0")],
					[],
					[],
					"MachineA",
					root + Path.DirectorySeparatorChar,
					IgnoreVersion: false,
					IsModuleSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					UnifyGroupSelection: false,
					NotUpdateAttribute: 2));

			Assert.Empty(result.ExistingUnitsToDeselect);
			Assert.Empty(result.NewItemsToDeselect);
			Assert.Equal(UnitVersionBuildOutcome.Created, Assert.Single(result.Items).BuildResult.Outcome);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Prepare_UsesFirstMachineDefinitionWhenNamesAreDuplicated()
	{
		string root = Path.Combine(Path.GetTempPath(), $"UnitVersionPreparation-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "first.bin"), "first");
		File.WriteAllText(Path.Combine(root, "second.bin"), "second");

		try
		{
			UnitVersionPreparationResult result = CreateService().Prepare(
				new UnitVersionPreparationRequest(
					[new UpdateVersionCandidate("UnitA", "2.0")],
					[
						new UnitVersionDefinition("UnitA", "first.bin", null, "0", "1.0"),
						new UnitVersionDefinition("UnitA", "second.bin", null, "0", "1.5")
					],
					[],
					[],
					"MachineA",
					root + Path.DirectorySeparatorChar,
					IgnoreVersion: false,
					IsModuleSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					UnifyGroupSelection: false,
					NotUpdateAttribute: 2));

			UnitVersionPreparationItem item = Assert.Single(result.Items);
			Assert.Equal("first.bin", item.SourcePath);
			Assert.Equal("1.0", item.BuildResult.CurrentVersion);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Prepare_MapsDeselectIndexToCreatedItemWhenEarlierItemIsInvalid()
	{
		string root = Path.Combine(Path.GetTempPath(), $"UnitVersionPreparation-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "invalid.bin"), "payload");
		File.WriteAllText(Path.Combine(root, "valid.bin"), "payload");

		try
		{
			UnitVersionPreparationResult result = CreateService().Prepare(
				new UnitVersionPreparationRequest(
					[
						new UpdateVersionCandidate("InvalidUnit", "2.0"),
						new UpdateVersionCandidate("ValidUnit", "2.0")
					],
					[
						new UnitVersionDefinition("InvalidUnit", "invalid.bin", null, "invalid", "1.0"),
						new UnitVersionDefinition("ValidUnit", "valid.bin", null, "0", "1.0")
					],
					[new UnitGroupSelection("GroupA", false)],
					[
						new UnitVersionGroupDefinition(
							["InvalidUnit", "ValidUnit"],
							new Dictionary<string, string> { ["MachineA"] = "GroupA" })
					],
					"MachineA",
					root + Path.DirectorySeparatorChar,
					IgnoreVersion: false,
					IsModuleSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					UnifyGroupSelection: true,
					NotUpdateAttribute: 2));

			Assert.Equal(
				[UnitVersionBuildOutcome.InvalidAttribute, UnitVersionBuildOutcome.Created],
				result.Items.Select(item => item.BuildResult.Outcome));
			Assert.Equal([0], result.ExistingUnitsToDeselect);
			Assert.Equal([1], result.NewItemsToDeselect);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static UnitVersionListService CreateService() => new();
}
