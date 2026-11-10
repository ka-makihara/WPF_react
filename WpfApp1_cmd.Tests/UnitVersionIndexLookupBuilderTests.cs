using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionIndexLookupBuilderTests
{
	[Fact]
	public void Build_ResolvesFirstExactAndNullNameIndices()
	{
		UnitVersionIndexLookup lookup = new UnitVersionIndexLookupBuilder()
			.Build(["UnitA", null, "UnitA", "unita"]);

		Assert.Equal(0, lookup.FindIndex("UnitA"));
		Assert.Equal(1, lookup.FindIndex(null));
		Assert.Equal(3, lookup.FindIndex("unita"));
		Assert.Null(lookup.FindIndex("Missing"));
	}

	[Fact]
	public void Build_ThrowsWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UnitVersionIndexLookupBuilder().Build(["UnitA"], cancellation.Token));
	}
}
