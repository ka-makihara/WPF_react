using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ViewCacheKeyRemovalBuilderTests
{
	[Fact]
	public void Build_ReturnsKeysContainingViewNameInOriginalOrder()
	{
		IReadOnlyList<string> result = new ViewCacheKeyRemovalBuilder().Build(
			[
				"UpdateVersionView",
				"LineA;MachineA;ModuleView",
				"LineA;ModuleView;ModuleB",
				"MachineView",
				"linea;moduleview"
			],
			"ModuleView");

		Assert.Equal(
			[
				"LineA;MachineA;ModuleView",
				"LineA;ModuleView;ModuleB"
			],
			result);
	}

	[Fact]
	public void Build_UsesSubstringMatchLikeExistingCacheClear()
	{
		IReadOnlyList<string> result = new ViewCacheKeyRemovalBuilder().Build(
			["PrefixModuleViewSuffix", "OtherView"],
			"ModuleView");

		Assert.Equal(["PrefixModuleViewSuffix"], result);
	}

	[Fact]
	public void Build_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new ViewCacheKeyRemovalBuilder().Build(
				["ModuleView"],
				"ModuleView",
				cancellation.Token));
	}
}
