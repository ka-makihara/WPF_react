using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleBackupService_UpdatePathTests
{
	[Fact]
	public void Build_IncludesPathsAndNonEmptyFuserPathsInSectionOrder()
	{
		IReadOnlyList<string> paths = new ModuleBackupService().BuildUpdatePaths(
			[
				new UpdateCommonPathEntry("UnitA", "/a.bin", "/fuser/a.bin"),
				new UpdateCommonPathEntry("UnitB", "/b.bin", ""),
				new UpdateCommonPathEntry("UnitC", "", "/fuser/c.bin")
			]);

		Assert.Equal(
			["/a.bin", "/fuser/a.bin", "/b.bin", "", "/fuser/c.bin"],
			paths);
	}

	[Fact]
	public void Build_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleBackupService().BuildUpdatePaths([], cancellation.Token));
	}
}
