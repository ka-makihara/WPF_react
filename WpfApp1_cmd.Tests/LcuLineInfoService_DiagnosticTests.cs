using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuLineInfoService_DiagnosticTests
{
	[Fact]
	public void CreateDirectoryDiagnostics_FormatsLineAndComputerRecords()
	{
		LcuLineInfoService service = new();

		Assert.Equal(
			"LineA::LineID=1 JobID=2 ActiveFlag=1 LcuID=3",
			service.CreateDirectoryLineDiagnostic(
				new NeximLineRecord("LineA", 1, 2, 1, 3, [])).Message);
		Assert.Equal(
			"ComputerID=4 LcuName=HostA",
			service.CreateDirectoryComputerDiagnostic(
				new NeximComputerRecord(4, "HostA")).Message);
	}

	[Fact]
	public void CreateLoadMessages_FormatsStartErrorAndCancellation()
	{
		LcuLineInfoService service = new();
		IOException exception = new("offline");

		Assert.Equal("reading HostA", service.CreateLoadStartedProgressMessage("HostA"));
		Assert.Equal(
			"[ERROR] LoadLineInfo=offline",
			service.CreateLoadErrorDiagnostic(exception).Message);
		Assert.Equal(
			"HostA=offline",
			service.CreateTargetLoadProgressMessage("HostA", exception));
	}

	[Theory]
	[InlineData(LcuLineTreeProgressStage.Machine, "MachineA", "Machine=MachineA")]
	[InlineData(LcuLineTreeProgressStage.Module, "ModuleA", "Module=ModuleA")]
	public void CreateTreeProgressMessage_FormatsStage(
		LcuLineTreeProgressStage stage,
		string name,
		string expectedMessage)
	{
		string message = new LcuLineInfoService()
			.CreateTreeProgressMessage(new LcuLineTreeProgress(stage, name));

		Assert.Equal(expectedMessage, message);
	}

	[Fact]
	public void CreateDirectoryPresentation_FormatsDatabaseFailureAndRejectsDirectory()
	{
		LcuLineInfoDirectoryPresentation presentation =
			new LcuLineInfoService()
				.CreateDirectoryPresentation(new NeximLcuDirectoryResult(
					[],
					"database unavailable"));

		Assert.False(presentation.Succeeded);
		LcuLineInfoDiagnostic diagnostic = Assert.Single(presentation.Diagnostics);
		Assert.Equal(LcuLineInfoDiagnosticDestination.ApplicationLog, diagnostic.Destination);
		Assert.Equal(
			"[ERROR] NeximDB LCU list=database unavailable",
			diagnostic.Message);
	}

	[Fact]
	public void CreateDirectoryPresentation_PassesTargetsWhenDirectoryIsAvailable()
	{
		NeximLcuTarget target = new("LineA", "HostA", 12);

		LcuLineInfoDirectoryPresentation presentation =
			new LcuLineInfoService().CreateDirectoryPresentation(
				new NeximLcuDirectoryResult([target], null));

		Assert.True(presentation.Succeeded);
		Assert.Same(target, Assert.Single(presentation.Targets));
		Assert.Empty(presentation.Diagnostics);
	}

	[Fact]
	public void CreateResponsePresentation_FormatsErrorResponseAndMarksFailure()
	{
		LcuLineInfoResponsePresentation presentation =
			new LcuLineInfoService().CreateResponsePresentation(
				LcuLineInfoResponseOutcome.ErrorResponse,
				"LineA");

		Assert.False(presentation.Succeeded);
		Assert.Equal(
			[
				new LcuLineInfoDiagnostic(
					LcuLineInfoDiagnosticDestination.ApplicationLog,
					"[ERROR] LineA;get lines error"),
				new LcuLineInfoDiagnostic(
					LcuLineInfoDiagnosticDestination.ApplicationLog,
					"[LineInfo] LineA;get linesInfomation error")
			],
			presentation.Diagnostics);
	}

	[Theory]
	[InlineData(LcuLineInfoResponseOutcome.Parsed)]
	[InlineData(LcuLineInfoResponseOutcome.InvalidContent)]
	public void CreateResponsePresentation_ClassifiesParsedAndInvalidResponses(
		LcuLineInfoResponseOutcome outcome)
	{
		LcuLineInfoResponsePresentation presentation =
			new LcuLineInfoService().CreateResponsePresentation(outcome, "LineA");

		Assert.Equal(outcome == LcuLineInfoResponseOutcome.Parsed, presentation.Succeeded);
		Assert.Empty(presentation.Diagnostics);
	}

	[Fact]
	public void CreateLoadCancelledDiagnostic_UsesLegacyMessage()
	{
		LcuLineInfoDiagnostic diagnostic =
			new LcuLineInfoService().CreateLoadCancelledDiagnostic();

		Assert.Equal(LcuLineInfoDiagnosticDestination.ApplicationLog, diagnostic.Destination);
		Assert.Equal("[ERROR] LoadLineInfo=Canceled", diagnostic.Message);
	}

	[Theory]
	[InlineData(true, "[LineInfo] LineA=True")]
	[InlineData(false, "[LineInfo] LineA=False")]
	public void CreateLoadResultDiagnostic_FormatsOutcome(
		bool succeeded,
		string expectedMessage)
	{
		LcuLineInfoDiagnostic diagnostic =
			new LcuLineInfoService().CreateLoadResultDiagnostic(
				"LineA",
				succeeded);

		Assert.Equal(LcuLineInfoDiagnosticDestination.Debug, diagnostic.Destination);
		Assert.Equal(expectedMessage, diagnostic.Message);
	}

	[Fact]
	public void CreateSelectionSummaryDiagnostic_FormatsCounts()
	{
		LcuLineInfoDiagnostic diagnostic =
			new LcuLineInfoService().CreateSelectionSummaryDiagnostic(
				"LineA",
				new LcuSelectionSummary(2, 3, 5, true));

		Assert.Equal(LcuLineInfoDiagnosticDestination.ApplicationLog, diagnostic.Destination);
		Assert.Equal(
			"[LineInfo] LineA;Machine=2;Module=3;Unit=5",
			diagnostic.Message);
	}

	[Fact]
	public void CreateSkippedMachineDiagnostic_FormatsMachineAndType()
	{
		LcuLineInfoDiagnostic diagnostic =
			new LcuLineInfoService().CreateSkippedMachineDiagnostic(
				"LineA",
				"MachineA",
				"Unsupported");

		Assert.Equal(LcuLineInfoDiagnosticDestination.ApplicationLog, diagnostic.Destination);
		Assert.Equal(
			"[LineInfo] LineA;MachineA=Skip:Unsupported",
			diagnostic.Message);
	}
}
