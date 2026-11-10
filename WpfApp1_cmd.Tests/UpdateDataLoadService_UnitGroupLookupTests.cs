using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_UnitGroupLookupTests
{
	[Fact]
	public void Build_UsesFirstGroupAndOrdinalComponentMatching()
	{
		IReadOnlyDictionary<string, string> groups =
			new UpdateDataLoadService().BuildUnitGroupLookup(
			[
				new(["UnitA", "unita"], "First", ["First"], null),
				new(["UnitA"], "Second", ["Second"], null)
			]);

		Assert.Equal("First", groups["UnitA"]);
		Assert.Equal("First", groups["unita"]);
		Assert.False(groups.ContainsKey("UNITA"));
	}

	[Fact]
	public void Build_ThrowsWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UpdateDataLoadService().BuildUnitGroupLookup([], cancellation.Token));
	}
}
