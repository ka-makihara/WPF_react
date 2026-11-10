using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuLineTreeService_SelectionTests
{
	[Fact]
	public void Create_CountsSelectedAndIndeterminateItemsAtEachLevel()
	{
		LcuMachineSelection[] machines =
		[
			new(
				true,
				[
					new(true, [true, null, false]),
					new(false, [true])
				]),
			new(null, [new(null, [null])]),
			new(false, [new(true, [true])])
		];

		LcuSelectionSummary summary = new LcuLineTreeService().CreateSelectionSummary(machines);

		Assert.Equal(2, summary.MachineCount);
		Assert.Equal(3, summary.ModuleCount);
		Assert.Equal(5, summary.UnitCount);
		Assert.Null(summary.SelectionState);
	}

	[Fact]
	public void Create_ReturnsZeroCountsForAnEmptyTree()
	{
		LcuSelectionSummary summary =
			new LcuLineTreeService().CreateSelectionSummary([]);

		Assert.Equal(new LcuSelectionSummary(0, 0, 0, false), summary);
	}

	[Fact]
	public void Create_ThrowsWhenCancelledBeforeCounting()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new LcuLineTreeService().CreateSelectionSummary([], cancellation.Token));
	}

	[Fact]
	public void Create_ThrowsWhenCancelledWhileEnumeratingMachines()
	{
		using CancellationTokenSource cancellation = new();
		IReadOnlyList<LcuMachineSelection> machines = new CancellingMachineList(
			[new(true, [])],
			cancellation);

		Assert.Throws<OperationCanceledException>(() =>
			new LcuLineTreeService().CreateSelectionSummary(machines, cancellation.Token));
	}

	[Theory]
	[InlineData(true, true, true)]
	[InlineData(false, false, false)]
	[InlineData(true, false, null)]
	[InlineData(null, null, null)]
	public void Create_CalculatesParentSelectionState(
		bool? firstSelection,
		bool? secondSelection,
		bool? expectedSelection)
	{
		LcuMachineSelection[] machines =
		[
			new(firstSelection, []),
			new(secondSelection, [])
		];

		LcuSelectionSummary summary = new LcuLineTreeService().CreateSelectionSummary(machines);

		Assert.Equal(expectedSelection, summary.SelectionState);
	}

	private sealed class CancellingMachineList(
		IReadOnlyList<LcuMachineSelection> items,
		CancellationTokenSource cancellation) : IReadOnlyList<LcuMachineSelection>
	{
		public int Count => items.Count;

		public LcuMachineSelection this[int index] => items[index];

		public IEnumerator<LcuMachineSelection> GetEnumerator()
		{
			foreach (LcuMachineSelection item in items)
			{
				yield return item;
				cancellation.Cancel();
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
