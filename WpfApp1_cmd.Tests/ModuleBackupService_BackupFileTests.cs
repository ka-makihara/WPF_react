using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public class ModuleBackupService_BackupFileTests
{
	[Fact]
	public void Create_IncludesUpdateCommonPathsFuserPathsAndMetadataFile()
	{
		var result = ModuleBackupService.CreateBackupFiles(
			[
				"/Fuji/System3/Program/Peripheral/Unit/file.bin",
				"/Fuji/System3/Program/Peripheral/Unit/file.fus"
			],
			"/Fuji/System3/Program/Peripheral/UpdateCommon.inf");

		Assert.Collection(
			result,
			file =>
			{
				Assert.Equal("Fuji/System3/Program/Peripheral/Unit/file.bin", file.RelativePath);
				Assert.Equal("Fuji/System3/Program/Peripheral/Unit", file.RelativeDirectory);
			},
			file =>
			{
				Assert.Equal("Fuji/System3/Program/Peripheral/Unit/file.fus", file.RelativePath);
				Assert.Equal("Fuji/System3/Program/Peripheral/Unit", file.RelativeDirectory);
			},
			file =>
			{
				Assert.Equal("Fuji/System3/Program/Peripheral/UpdateCommon.inf", file.RelativePath);
				Assert.Equal("Fuji/System3/Program/Peripheral", file.RelativeDirectory);
			});
	}

	[Fact]
	public void Create_NormalizesSeparatorsAndRemovesDuplicatePathsWithoutCaseSensitivity()
	{
		var result = ModuleBackupService.CreateBackupFiles(
			[
				@"\Fuji\System3\Program\Peripheral\unit.bin",
				"/Fuji/System3/Program/Peripheral/UNIT.BIN"
			],
			@"\Fuji\System3\Program\Peripheral\UpdateCommon.inf");

		Assert.Collection(
			result,
			file => Assert.Equal("Fuji/System3/Program/Peripheral/unit.bin", file.RelativePath),
			file => Assert.Equal("Fuji/System3/Program/Peripheral/UpdateCommon.inf", file.RelativePath));
	}

	[Fact]
	public void Create_ThrowsWhenCancelledBeforeBuildingPlan()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			ModuleBackupService.CreateBackupFiles([], "UpdateCommon.inf", cancellation.Token));
	}
}
