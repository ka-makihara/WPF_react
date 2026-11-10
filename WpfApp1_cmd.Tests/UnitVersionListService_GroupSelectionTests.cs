using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_GroupSelectionTests
{
	[Fact]
	public void GetGroupsToDeselect_ReturnsOnlyIndexesInGroupsContainingFalse()
	{
		UnitGroupSelection[] units =
		[
			new("GroupA", true),
			new("GroupA", false),
			new("GroupB", true),
			new("GroupB", null),
			new("GroupC", null)
		];

		IReadOnlyList<int> result =
			new UnitVersionListService().GetGroupsToDeselect(units);

		Assert.Equal([0, 1], result);
	}

	[Fact]
	public void GetGroupsToDeselect_PreservesGroupsWithoutFalseSelections()
	{
		UnitGroupSelection[] units =
		[
			new("GroupA", true),
			new("GroupA", null),
			new("GroupB", null)
		];

		IReadOnlyList<int> result =
			new UnitVersionListService().GetGroupsToDeselect(units);

		Assert.Empty(result);
	}

	[Fact]
	public void GetGroupsToDeselect_PreservesFirstGroupOccurrenceOrder()
	{
		UnitGroupSelection[] units =
		[
			new("GroupA", true),
			new("GroupB", false),
			new("GroupA", false),
			new("GroupB", true)
		];

		IReadOnlyList<int> result =
			new UnitVersionListService().GetGroupsToDeselect(units);

		Assert.Equal([0, 2, 1, 3], result);
	}

	[Fact]
	public void GetGroupsToDeselect_ThrowsWhenCancelledDuringGrouping()
	{
		using CancellationTokenSource cancellation = new();
		IReadOnlyList<UnitGroupSelection> units = new CancellingUnitSelectionList(
			[new("GroupA", false)],
			cancellation);

		Assert.Throws<OperationCanceledException>(() =>
			new UnitVersionListService().GetGroupsToDeselect(
				units,
				cancellation.Token));
	}

	private sealed class CancellingUnitSelectionList(
		IReadOnlyList<UnitGroupSelection> items,
		CancellationTokenSource cancellation) : IReadOnlyList<UnitGroupSelection>
	{
		public int Count => items.Count;

		public UnitGroupSelection this[int index]
		{
			get
			{
				UnitGroupSelection item = items[index];
				cancellation.Cancel();
				return item;
			}
		}

		public IEnumerator<UnitGroupSelection> GetEnumerator() => items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
