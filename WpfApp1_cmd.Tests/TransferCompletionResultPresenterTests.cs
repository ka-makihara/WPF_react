using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferCompletionResultPresenterTests
{
	private readonly TransferCompletionResultPresenter _presenter = new();

	[Fact]
	public void Create_FormatsTransferCountsAndRetainsDialogText()
	{
		TransferCompletionResultPresentation presentation = _presenter.Create(
			3,
			2,
			"Information",
			"Success",
			"Fail",
			"Transfer details");

		Assert.Equal("Information", presentation.DialogTitle);
		Assert.Equal("Success=3", presentation.SuccessMessage);
		Assert.Equal("Fail=2", presentation.FailureMessage);
		Assert.Equal("Transfer details", presentation.InformationMessage);
	}

	[Theory]
	[InlineData("YES", true)]
	[InlineData("NO", false)]
	[InlineData("yes", false)]
	public void ShouldOpenResult_OnlyForExactYes(string result, bool expected)
	{
		Assert.Equal(expected, _presenter.ShouldOpenResult(result));
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(0, -1)]
	public void Create_RejectsNegativeCounts(int successCount, int failureCount)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => _presenter.Create(
			successCount,
			failureCount,
			"Information",
			"Success",
			"Fail",
			"Transfer details"));
	}
}
