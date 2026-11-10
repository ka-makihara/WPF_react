using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleUpdateService_ProgressTests
{
	[Theory]
	[InlineData(0, 10, 250)]
	[InlineData(5, 10, 625)]
	[InlineData(10, 10, 1000)]
	public void Create_ReturnsCalculatedProgressAndLegacyDebugMessage(
		long transferred,
		long total,
		double expectedProgress)
	{
		ModuleTransferProgressPresentation presentation =
			new ModuleUpdateService(new TransferProgressCalculator()).CreateProgress(
				transferred,
				total,
				4,
				7);

		Assert.Equal(expectedProgress, presentation.Progress);
		Assert.Equal(
			$"Progress={expectedProgress} Transfered=4 cnt=7",
			presentation.DebugMessage);
	}
}
