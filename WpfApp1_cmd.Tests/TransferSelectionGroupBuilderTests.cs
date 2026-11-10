using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferSelectionGroupBuilderTests
{
	[Fact]
	public void Build_PreservesGroupOrderComponentsAndModes()
	{
		IReadOnlyList<TransferSelectionGroup> result =
			new TransferSelectionGroupBuilder().Build(
				[
					new TransferSelectionGroupInput(["UnitA", "UnitB"], "File"),
					new TransferSelectionGroupInput(["UnitC"], "Folder")
				]);

		Assert.Equal(2, result.Count);
		Assert.Equal(["UnitA", "UnitB"], result[0].Components);
		Assert.Equal("File", result[0].Mode);
		Assert.Equal(["UnitC"], result[1].Components);
		Assert.Equal("Folder", result[1].Mode);
	}

	[Fact]
	public void Build_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new TransferSelectionGroupBuilder().Build([], cancellation.Token));
	}
}
