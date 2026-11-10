using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateInspectionDiagnosticServiceTests
{
	[Fact]
	public void CreateTransferredFilePresenceDiagnostics_FormatsUnitAndFuserPathsInOrder()
	{
		IReadOnlyList<InspectionDiagnostic> diagnostics =
			new UpdateInspectionDiagnosticService().CreateTransferredFilePresenceDiagnostics(
				[
					new TransferredFilePresence("UnitA", "/unit.bin", false, true),
					new TransferredFilePresence("UnitA", "/fuser.bin", true, false)
				]);

		Assert.Equal(
			[
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.Debug,
					"[INSPECTION] UnitA=/unit.bin is found in LCU.",
					false),
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.Debug,
					"[INSPECTION] /fuser.bin is not found in LCU.",
					false)
			],
			diagnostics);
	}

	[Fact]
	public void CreateModuleDiagnostics_FormatsResultsAndReportsOverallMatch()
	{
		ModuleInspectionDiagnostics diagnostics =
			new UpdateInspectionDiagnosticService().CreateModuleDiagnostics(
				"LineA",
				"MachineA",
				"ModuleA",
				[
					new ModuleFileInspectionResult("UnitA", "/a.bin", true),
					new ModuleFileInspectionResult("UnitB", "/b.bin", false)
				]);

		Assert.False(diagnostics.AllFilesMatch);
		Assert.Equal(
			[
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.ApplicationLog,
					"[INSPECTION] LineA;MachineA;ModuleA;UnitA;/a.bin=OK:Inspection Success",
					false),
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.ApplicationLog,
					"[INSPECTION] LineA;MachineA;ModuleA;UnitB=NG:DiffFile Inspection Error(/b.bin)",
					true)
			],
			diagnostics.Entries);
	}

	[Fact]
	public void CreateUpdateCommonDiagnostics_FormatsEntriesInOriginalOrder()
	{
		IReadOnlyList<InspectionDiagnostic> diagnostics =
			new UpdateInspectionDiagnosticService().CreateUpdateCommonDiagnostics(
				new UpdateCommonVersionInspectionResult(
					true,
					null,
					[
						new UpdateCommonVersionEntry(
							"UnitA",
							"2.0",
							"1.0",
							UpdateCommonVersionStatus.Mismatched),
						new UpdateCommonVersionEntry(
							"UnitB",
							"3.0",
							"3.0",
							UpdateCommonVersionStatus.Matched),
						new UpdateCommonVersionEntry(
							"UnitC",
							"4.0",
							null,
							UpdateCommonVersionStatus.UnitNotFound)
					]),
				"/UpdateCommon.inf");

		Assert.Equal(
			[
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.Debug,
					"Section=UnitA, Version=2.0",
					false),
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.ApplicationLog,
					"[ERROR] UnitA version mismatch: 1.0 != 2.0",
					true),
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.Debug,
					"Section=UnitB, Version=3.0",
					false),
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.ApplicationLog,
					"[INFO] UnitB=sameVersion (3.0 == 3.0)",
					false),
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.Debug,
					"Section=UnitC, Version=4.0",
					false),
				new InspectionDiagnostic(
					InspectionDiagnosticDestination.ApplicationLog,
					"[INFO] UnitC=skip (not in inspection targets)",
					false)
			],
			diagnostics);
	}

	[Fact]
	public void CreateUpdateCommonDiagnostics_ReportsMissingFileAndReadError()
	{
		UpdateInspectionDiagnosticService service = new();

		Assert.Equal(
			new InspectionDiagnostic(
				InspectionDiagnosticDestination.ApplicationLog,
				"[ERROR] UpdateCommon.inf is not exists: /missing.inf",
				true),
			Assert.Single(service.CreateUpdateCommonDiagnostics(
				new UpdateCommonVersionInspectionResult(false, null, []),
				"/missing.inf")));
		Assert.Equal(
			new InspectionDiagnostic(
				InspectionDiagnosticDestination.ApplicationLog,
				"iniファイル読み込みエラー: denied",
				true),
			Assert.Single(service.CreateUpdateCommonDiagnostics(
				new UpdateCommonVersionInspectionResult(true, "denied", []),
				"/read-error.inf")));
	}

	[Fact]
	public void CreateDiagnostics_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		UpdateInspectionDiagnosticService service = new();

		Assert.Throws<OperationCanceledException>(() =>
			service.CreateModuleDiagnostics("L", "M", "U", [], cancellation.Token));
		Assert.Throws<OperationCanceledException>(() =>
			service.CreateUpdateCommonDiagnostics(
				new UpdateCommonVersionInspectionResult(false, null, []),
				"path",
				cancellation.Token));
	}
}
