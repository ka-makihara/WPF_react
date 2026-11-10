using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuUpdatePreparationService_DiagnosticTests
{
	[Theory]
	[InlineData("LCU:HostA=Access Fail", true)]
	[InlineData("LCU:HostA=FTP information Fail", false)]
	public void CreateFailureProgressMessage_FormatsHost(
		string expectedMessage,
		bool connectionError)
	{
		string message = connectionError
			? LcuUpdatePreparationService.CreateConnectionProgressMessage("HostA")
			: LcuUpdatePreparationService.CreateFtpResponseErrorProgressMessage("HostA");

		Assert.Equal(expectedMessage, message);
	}

	[Fact]
	public void CreateProgressMessages_FormatsRemainingPreparationOutcomes()
	{
		Assert.Equal(
			"LCU:HostA=FTP user, password Fail",
			LcuUpdatePreparationService.CreateFtpCredentialsErrorProgressMessage("HostA"));
		Assert.Equal(
			"LCU:HostA=LcuVersion Get Error",
			LcuUpdatePreparationService.CreateVersionErrorProgressMessage("HostA"));
		Assert.Equal(
			"LCU:HostA=Version(2.0)",
			LcuUpdatePreparationService.CreateVersionProgressMessage("HostA", "2.0"));
		Assert.Equal(
			"LCU:HostA=DiskSpace Error(10<20)",
			LcuUpdatePreparationService.CreateDiskSpaceErrorProgressMessage("HostA", 10, 20));
		Assert.Equal(
			"LineA::Don't get disk space information.",
			LcuUpdatePreparationService.CreateDiskInfoErrorMessage("LineA"));
		Assert.Equal(
			"LineA:Check Disk Space",
			LcuUpdatePreparationService.CreateDiskSpaceCheckStartedMessage("LineA"));
	}

	[Fact]
	public void CreateConnectionErrorMessages_PreservesExistingMessagesAndOrder()
	{
		IReadOnlyList<string> messages =
			LcuUpdatePreparationService.CreateConnectionErrorMessages("LineA");

		Assert.Equal(
			[
				"[ERROR] LineA=Access Fail",
				"[LineInfo]:LineA=Access Fail"
			],
			messages);
	}

	[Fact]
	public void CreateVersionErrorMessages_PreservesExistingMessagesAndOrder()
	{
		IReadOnlyList<string> messages =
			LcuUpdatePreparationService.CreateVersionErrorMessages("LineA");

		Assert.Equal(
			[
				"[ERROR] LineA=LcuVersion Get Error",
				"[LineInfo] LineA;LcuVersion Get Error"
			],
			messages);
	}

	[Fact]
	public void CreateDiskSpaceErrorMessages_FormatsAvailableAndRequiredSpace()
	{
		IReadOnlyList<string> messages =
			LcuUpdatePreparationService.CreateDiskSpaceErrorMessages("LineA", 10, 20);

		Assert.Equal(
			[
				"[ERROR] LineA=DiskSpace Error(10<20)",
				"[LineInfo] LineA;DiskSpace Error(10<20)"
			],
			messages);
	}
}
