using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_ProjectTests
{
	[Fact]
	public void Project_FiltersResultsAndAppliesDeselectionWithoutChangingOrder()
	{
		UnitVersionPreparationResult preparation = new(
			[new UpdateVersionCandidate("Skipped", "1.0")],
			[
				CreateItem("Missing", UnitVersionBuildOutcome.UpdateFileNotFound),
				CreateItem("Directory", UnitVersionBuildOutcome.UpdatePathIsDirectory),
				CreateItem("Invalid", UnitVersionBuildOutcome.InvalidAttribute),
				CreateItem("First", UnitVersionBuildOutcome.Created, isSelected: true),
				CreateItem("Second", UnitVersionBuildOutcome.Created, isSelected: true)
			],
			[2],
			[4]);

		UnitVersionPreparationProjection result =
			new UnitVersionListService().Project(
				preparation,
				new Dictionary<string, string>());

		Assert.Equal("Skipped", Assert.Single(result.SkippedSameVersion).Name);
		Assert.Equal(
			["Missing", "Directory", "Invalid"],
			result.ItemsNotCreated.Select(item => item.UnitName));
		Assert.Equal(["First", "Second"], result.ItemsToCreate.Select(item => item.Name));
		Assert.True(result.ItemsToCreate[0].IsSelected);
		Assert.False(result.ItemsToCreate[1].IsSelected);
		Assert.Equal("/First.bin", result.ItemsToCreate[0].Path);
		Assert.Equal("2.0", result.ItemsToCreate[0].NewVersion);
		Assert.Equal([2], result.ExistingUnitsToDeselect);
	}

	[Fact]
	public void Project_RetainsOriginalFuserPathForClearOperations()
	{
		UnitVersionPreparationResult preparation = new(
			[],
			[CreateItem(
				"UnitA",
				UnitVersionBuildOutcome.Created,
				clearFuserPath: true)],
			[],
			[]);

		UnitVersionPreparationProjection result =
			new UnitVersionListService().Project(
				preparation,
				new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["UnitA"] = "/old/fuser.bin"
				});

		Assert.Equal(
			("UnitA", "/old/fuser.bin"),
			Assert.Single(result.FuserPathsToClear));
	}

	[Fact]
	public void Project_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UnitVersionListService().Project(
				new UnitVersionPreparationResult([], [], [], []),
				new Dictionary<string, string>(),
				cancellation.Token));
	}

	private static UnitVersionPreparationItem CreateItem(
		string name,
		UnitVersionBuildOutcome outcome,
		bool isSelected = false,
		bool clearFuserPath = false) =>
		new(
			name,
			$"/{name}.bin",
			"2.0",
			"0",
			new UnitVersionBuildResult(
				outcome,
				Path: $"/{name}.bin",
				FuserPath: clearFuserPath ? "" : null,
				ClearFuserPath: clearFuserPath,
				CurrentVersion: "1.0",
				NewVersion: "2.0",
				IsSelected: isSelected));
}
