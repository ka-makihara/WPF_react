using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleUpdateService_DecideUpdateInfoSourceTests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	public void Decide_UsesMachineInfoAndPreservesVersionOption(
		bool isAdministrator,
		bool ignoreVersion)
	{
		ModuleUpdateInfoDecision result = new ModuleUpdateService().DecideUpdateInfoSource(
			hasMachineUpdateInfo: true,
			isAdministrator,
			ignoreVersion);

		Assert.Equal(ModuleUpdateInfoSource.Machine, result.Source);
		Assert.Equal(ignoreVersion, result.IgnoreVersion);
	}

	[Fact]
	public void Decide_AdministratorFallsBackToUpdateDataAndIgnoresVersion()
	{
		ModuleUpdateInfoDecision result = new ModuleUpdateService().DecideUpdateInfoSource(
			hasMachineUpdateInfo: false,
			isAdministrator: true,
			ignoreVersion: false);

		Assert.Equal(ModuleUpdateInfoSource.UpdateData, result.Source);
		Assert.True(result.IgnoreVersion);
	}

	[Fact]
	public void Decide_UserWithoutMachineInfoReportsUnavailableAndPreservesOption()
	{
		ModuleUpdateInfoDecision result = new ModuleUpdateService().DecideUpdateInfoSource(
			hasMachineUpdateInfo: false,
			isAdministrator: false,
			ignoreVersion: true);

		Assert.Equal(ModuleUpdateInfoSource.Unavailable, result.Source);
		Assert.True(result.IgnoreVersion);
	}
}
