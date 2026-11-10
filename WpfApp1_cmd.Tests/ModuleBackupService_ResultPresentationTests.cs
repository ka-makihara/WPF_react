using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleBackupService_ResultPresentationTests
{
	private readonly ModuleBackupService _presenter = new();

	[Fact]
	public void Create_SucceededDoesNotShowDialog()
	{
		BackupResultPresentation presentation = Create(
			BackupOutcome.Succeeded,
			"unused");

		Assert.False(presentation.ShowDialog);
		Assert.Null(presentation.DialogTitle);
		Assert.Null(presentation.Message);
		Assert.Null(presentation.Detail);
	}

	[Fact]
	public void Create_CancelledShowsCautionMessageAndRetainsFailureDetail()
	{
		BackupResultPresentation presentation = Create(
			BackupOutcome.Cancelled,
			"Backup stopped");

		Assert.True(presentation.ShowDialog);
		Assert.Equal("Error", presentation.DialogTitle);
		Assert.Equal("Backup canceled", presentation.Message);
		Assert.Equal("Backup stopped", presentation.Detail);
		Assert.True(presentation.IsCancelled);
	}

	[Fact]
	public void Create_FailedUsesRequiredMessageAndFailureDetail()
	{
		BackupResultPresentation presentation = Create(
			BackupOutcome.Failed,
			"Disk error");

		Assert.True(presentation.ShowDialog);
		Assert.Equal("Backup required", presentation.Message);
		Assert.Equal("Disk error", presentation.Detail);
		Assert.False(presentation.IsCancelled);
	}

	[Fact]
	public void Create_UsesRequiredMessageWhenFailureDetailIsMissing()
	{
		BackupResultPresentation presentation = Create(
			BackupOutcome.Failed,
			null);

		Assert.Equal("Backup required", presentation.Detail);
	}

	private BackupResultPresentation Create(BackupOutcome outcome, string? failureMessage) =>
		_presenter.CreateResultPresentation(
			outcome,
			"Error",
			"Backup canceled",
			"Backup required",
			failureMessage);
}
