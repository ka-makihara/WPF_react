using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferStartConfirmationPresenterTests
{
	[Fact]
	public void Create_FormatsModuleCountAndQuestion()
	{
		TransferStartConfirmationPresentation presentation =
			new TransferStartConfirmationPresenter().Create(
				3,
				"Confirm",
				"Start transfer?");

		Assert.Equal("Confirm", presentation.DialogTitle);
		Assert.Equal("3 Module\nStart transfer?", presentation.DialogText);
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData("CANCEL", false)]
	[InlineData("OK", true)]
	[InlineData("cancel", true)]
	public void ShouldStart_PreservesDialogResultRules(string? result, bool expected)
	{
		Assert.Equal(
			expected,
			new TransferStartConfirmationPresenter().ShouldStart(result));
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData("CANCEL", false)]
	[InlineData("OK", true)]
	[InlineData("cancel", true)]
	public void ShouldStopTransfer_PreservesDialogResultRules(string? result, bool expected)
	{
		Assert.Equal(
			expected,
			new TransferStartConfirmationPresenter().ShouldStopTransfer(result));
	}

	[Fact]
	public void Create_RejectsNegativeModuleCount()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new TransferStartConfirmationPresenter().Create(-1L, "Confirm", "Question"));
	}
}
