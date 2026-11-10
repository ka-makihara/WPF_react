using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuInformationService_DiskSpacePresentationTests
{
	[Fact]
	public void Create_AvailableSpaceFormatsMegabytesAndLog()
	{
		LcuDiskSpaceCheckPresentation presentation = LcuInformationService.CreateDiskSpacePresentation(
			new LcuDiskSpaceResult(true, 5L * 1024 * 1024 + 900_000),
			"LineA",
			ErrorCode.OK,
			"Error",
			"Confirm",
			"Disk information failed");

		Assert.True(presentation.IsAvailable);
		Assert.False(presentation.IsError);
		Assert.Equal("Confirm", presentation.DialogTitle);
		Assert.Equal("LineA", presentation.DialogMessage);
		Assert.Equal("DiskSpace=5 MB", presentation.DialogDetail);
		Assert.Equal(["LineA::DiskSpace=6142880"], presentation.LogMessages);
	}

	[Fact]
	public void Create_UnavailableSpaceFormatsErrorDialogWithoutSuccessLog()
	{
		LcuDiskSpaceCheckPresentation presentation = LcuInformationService.CreateDiskSpacePresentation(
			new LcuDiskSpaceResult(false, 0),
			"LineA",
			ErrorCode.LCU_DISK_INFO_ERROR,
			"Error",
			"Confirm",
			"Disk information failed");

		Assert.False(presentation.IsAvailable);
		Assert.True(presentation.IsError);
		Assert.Equal("Error", presentation.DialogTitle);
		Assert.Equal(
			$"ErrorCode=0x{(int)ErrorCode.LCU_DISK_INFO_ERROR:X}",
			presentation.DialogMessage);
		Assert.Equal("Disk information failed", presentation.DialogDetail);
		Assert.Empty(presentation.LogMessages);
	}
}
