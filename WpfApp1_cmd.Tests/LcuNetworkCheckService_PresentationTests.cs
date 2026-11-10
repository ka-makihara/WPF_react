using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuNetworkCheckService_PresentationTests
{
	[Fact]
	public void Create_UnreachableShowsErrorDialogAndConnectionCode()
	{
		LcuNetworkCheckPresentation presentation = LcuNetworkCheckService.CreatePresentation(
			new LcuNetworkCheckResult(LcuNetworkCheckOutcome.Unreachable),
			"Error",
			"Information",
			"Connection failed",
			null);

		Assert.True(presentation.ShowDialog);
		Assert.Equal(ErrorCode.LCU_CONNECT_ERROR, presentation.ErrorCode);
		Assert.Equal("Error", presentation.DialogTitle);
		Assert.Equal(
			$"ErrorCode=0x{(int)ErrorCode.LCU_CONNECT_ERROR:X}",
			presentation.Message);
		Assert.Equal("Connection failed", presentation.Detail);
		Assert.True(presentation.IsError);
	}

	[Fact]
	public void Create_VersionUnavailablePreservesNoDialogBehavior()
	{
		LcuNetworkCheckPresentation presentation = LcuNetworkCheckService.CreatePresentation(
			new LcuNetworkCheckResult(LcuNetworkCheckOutcome.VersionUnavailable),
			"Error",
			"Information",
			"Connection failed",
			null);

		Assert.False(presentation.ShowDialog);
		Assert.Null(presentation.ErrorCode);
		Assert.Null(presentation.DialogTitle);
		Assert.Null(presentation.Message);
		Assert.Null(presentation.Detail);
		Assert.False(presentation.IsError);
	}

	[Fact]
	public void Create_AvailableShowsVersionInformation()
	{
		LcuNetworkCheckPresentation presentation = LcuNetworkCheckService.CreatePresentation(
			new LcuNetworkCheckResult(LcuNetworkCheckOutcome.Available, "2.3"),
			"Error",
			"Information",
			"Connection failed",
			"2.3");

		Assert.True(presentation.ShowDialog);
		Assert.Null(presentation.ErrorCode);
		Assert.Equal("Information", presentation.DialogTitle);
		Assert.Equal("Access OK", presentation.Message);
		Assert.Equal("Version:2.3", presentation.Detail);
		Assert.False(presentation.IsError);
	}
}
