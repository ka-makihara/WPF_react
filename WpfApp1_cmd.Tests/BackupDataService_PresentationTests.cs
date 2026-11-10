using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class BackupDataService_PresentationTests
{
	private readonly BackupDataService _presenter = new();

	[Fact]
	public void Create_MissingCandidatePreservesErrorTextAndLog()
	{
		BackupDataCandidatePresentation presentation = Create(null);

		Assert.Equal(
			BackupDataCandidatePresentationOutcome.MissingUpdateCommon,
			presentation.Outcome);
		Assert.Equal(ErrorCode.UPDATE_UNDEFINED_ERROR, presentation.ErrorCode);
		Assert.Equal(
			"[ERROR] Path=selected Undefined UpdateCommon.inf",
			presentation.LogMessage);
		Assert.Equal("ErrorCode=0x E000002", presentation.DialogMessage);
		Assert.Equal("UpdateCommon.inf not exist.", presentation.DialogInformation);
		Assert.True(presentation.IsError);
	}

	[Fact]
	public void Create_MetadataErrorIncludesErrorAndMismatchPresentation()
	{
		BackupDataCandidatePresentation presentation = Create(
			Candidate(
				BackupDataCandidateOutcome.MetadataError,
				new(true, true, null, null, null, null, "Malformed")));

		Assert.Equal(BackupDataCandidatePresentationOutcome.MetadataError, presentation.Outcome);
		Assert.Equal(ErrorCode.BACKUP_DATA_MISSMATCH, presentation.ErrorCode);
		Assert.Equal("[ERROR] Backup data read failed:Malformed", presentation.LogMessage);
		Assert.Equal("ErrorCode=0xE000020", presentation.DialogMessage);
		Assert.Equal("Backup mismatch", presentation.DialogDetail);
		Assert.Equal("Malformed", presentation.DialogInformation);
	}

	[Fact]
	public void Create_TargetMismatchFormatsTargetAndNoLog()
	{
		BackupDataCandidatePresentation presentation = Create(
			Candidate(
				BackupDataCandidateOutcome.TargetMismatch,
				new(true, true, "Line", "Machine", "Module", null, null)));

		Assert.Equal(BackupDataCandidatePresentationOutcome.TargetMismatch, presentation.Outcome);
		Assert.Equal("Backup mismatch", presentation.DialogDetail);
		Assert.Equal("Line-Machine-Module", presentation.DialogInformation);
		Assert.Null(presentation.LogMessage);
	}

	[Fact]
	public void Create_BackupDataRequiresConfirmation()
	{
		BackupDataCandidatePresentation presentation = Create(
			Candidate(
				BackupDataCandidateOutcome.BackupData,
				new(true, true, "Line", "Machine", "Module", null, null)));

		Assert.Equal(
			BackupDataCandidatePresentationOutcome.BackupDataConfirmationRequired,
			presentation.Outcome);
		Assert.Equal("Information", presentation.DialogTitle);
		Assert.Equal("Line=Machine=Module", presentation.DialogMessage);
		Assert.Equal("Backup Data", presentation.DialogDetail);
		Assert.Equal("Load Backup Data ?", presentation.DialogInformation);
		Assert.False(presentation.IsError);
	}

	[Theory]
	[InlineData("CANCEL", false)]
	[InlineData("cancel", true)]
	[InlineData("OK", true)]
	[InlineData(null, true)]
	public void ShouldLoadBackupData_PreservesCancelRule(string? result, bool expected) =>
		Assert.Equal(expected, _presenter.ShouldLoadBackupData(result));

	private BackupDataCandidatePresentation Create(BackupDataCandidate? candidate) =>
		_presenter.CreatePresentation(
			candidate,
			"selected",
			"Error",
			"Information",
			"Update missing",
			"Backup mismatch");

	private static BackupDataCandidate Candidate(
		BackupDataCandidateOutcome outcome,
		BackupDataSelectionResult metadata) =>
		new(
			"UpdateCommon.inf",
			"data",
			metadata,
			null,
			outcome);
}
