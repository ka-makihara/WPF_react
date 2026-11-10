using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferPreflightResultPresenterTests
{
	private readonly TransferPreflightResultPresenter _presenter = new();

	[Fact]
	public void Create_ReadyDoesNotShowDialog()
	{
		TransferPreflightPresentation presentation = Create(TransferPreflightOutcome.Ready);

		Assert.False(presentation.ShowDialog);
		Assert.Null(presentation.DialogTitle);
		Assert.Null(presentation.Message);
		Assert.Null(presentation.Detail);
	}

	[Fact]
	public void Create_NoTargetUsesConfirmationAndSelectionGuidance()
	{
		TransferPreflightPresentation presentation =
			Create(TransferPreflightOutcome.NoTargetSelected);

		Assert.True(presentation.ShowDialog);
		Assert.Equal("Confirm", presentation.DialogTitle);
		Assert.Equal("No target", presentation.Message);
		Assert.Equal("Select a target", presentation.Detail);
	}

	private TransferPreflightPresentation Create(TransferPreflightOutcome outcome) =>
		_presenter.Create(
			outcome,
			"Confirm",
			"No target",
			"Select a target");
}
