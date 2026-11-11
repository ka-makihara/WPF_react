using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_DiagnosticsTests
{
	[Fact]
	public void CreateCreatedVersionMessage_FormatsVersionTransition()
	{
		string message = new UnitVersionListService()
			.CreateCreatedVersionMessage("ModuleA", "UnitA", "1.0", "2.0");

		Assert.Equal("[UnitVersion] ModuleA:UnitA=1.0=>2.0", message);
	}

	[Fact]
	public void CreateDiagnostics_FormatsSupportedOutcomesInStableOrder()
	{
		UnitVersionPreparationProjection projection = new(
			[new UpdateVersionCandidate("Same", "1.0")],
			[
				CreateItem("Missing", UnitVersionBuildOutcome.UpdateFileNotFound),
				CreateItem("Directory", UnitVersionBuildOutcome.UpdatePathIsDirectory),
				CreateItem("Invalid", UnitVersionBuildOutcome.InvalidAttribute, attribute: "bad")
			],
			[],
			[("FuserUnit", "/old/fuser.bin")],
			[])
		{
			SkippedNotOnMachine = [new UpdateVersionCandidate("NotOnMachine", "2.0")]
		};

		IReadOnlyList<UnitVersionPreparationDiagnostic> diagnostics =
			new UnitVersionListService().CreateDiagnostics(
				projection,
				"LineA",
				"MachineA",
				"ModuleA");

		Assert.Equal(
			[
				new UnitVersionPreparationDiagnostic(
					UnitVersionPreparationDiagnosticPhase.Preparation,
					UnitVersionDiagnosticDestination.ApplicationLog,
					"[INFO] LineA;MachineA;ModuleA:NotOnMachine=skip (not in machine UpdateCommon.inf; not required)"),
				new UnitVersionPreparationDiagnostic(
					UnitVersionPreparationDiagnosticPhase.Preparation,
					UnitVersionDiagnosticDestination.ApplicationLog,
					"[INFO] LineA;MachineA;ModuleA:Same=sameVersion"),
				new UnitVersionPreparationDiagnostic(
					UnitVersionPreparationDiagnosticPhase.Preparation,
					UnitVersionDiagnosticDestination.ApplicationLog,
					"[ERROR] ModuleA:Invalid has invalid Attribute=bad"),
				new UnitVersionPreparationDiagnostic(
					UnitVersionPreparationDiagnosticPhase.IniUpdate,
					UnitVersionDiagnosticDestination.Debug,
					"[UnitVersion] ModuleA:FuserUnit=/old/fuser.bin FuserPath Not Found")
			],
			diagnostics);
	}

	[Fact]
	public void CreateDiagnostics_DoesNotWarnForMissingMachinePath()
	{
		UnitVersionPreparationProjection projection = new(
			[],
			[],
			[
				new UnitVersionPreparedDefinition(
					"Missing", false, 0, "/Missing.bin", "", "1.0", "2.0", 0, "GroupA",
					IsUpdateFileMissing: true),
				new UnitVersionPreparedDefinition(
					"Present", true, 0, "/Present.bin", "", "1.0", "2.0", 10, "GroupA")
			],
			[],
			[]);

		IReadOnlyList<UnitVersionPreparationDiagnostic> diagnostics =
			new UnitVersionListService().CreateDiagnostics(
				projection,
				"LineA",
				"MachineA",
				"ModuleA");

		Assert.Empty(diagnostics);
	}

	[Fact]
	public void CreateDiagnostics_DoesNotWarnForMissingMachinePathInTestMode()
	{
		UnitVersionPreparationProjection projection = new(
			[],
			[],
			[
				new UnitVersionPreparedDefinition(
					"Missing", false, 0, "/Missing.bin", "", "1.0", "2.0", 0, "GroupA",
					IsUpdateFileMissing: true)
			],
			[],
			[]);

		IReadOnlyList<UnitVersionPreparationDiagnostic> diagnostics =
			new UnitVersionListService().CreateDiagnostics(
				projection,
				"LineA",
				"MachineA",
				"ModuleA",
				missingUpdateFileSelectable: true);

		Assert.Empty(diagnostics);
	}

	[Fact]
	public void CreateDiagnostics_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UnitVersionListService().CreateDiagnostics(
				new UnitVersionPreparationProjection([], [], [], [], []),
				"LineA",
				"MachineA",
				"ModuleA",
				cancellation.Token));
	}

	private static UnitVersionPreparationItem CreateItem(
		string name,
		UnitVersionBuildOutcome outcome,
		string? attribute = "2") =>
		new(
			name,
			$"/{name}.bin",
			"2.0",
			attribute,
			new UnitVersionBuildResult(outcome));
}
