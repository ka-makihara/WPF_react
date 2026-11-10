using Xunit;
using WpfApp1_cmd.Services;

public class ModuleUpdateService_TransferPlanTests
{
	[Fact]
	public void Create_BuildsMachineAndLcuPathsFromUnitAndFuserFiles()
	{
		var plans = ModuleUpdateService.CreateTransferPlans(
			[
				new UpdateFileGroupResult(
					"Control",
					[
						new UpdateFileListEntry("UnitA", @"C:\Updates\Fuji\UnitA.bin", @"C:\Updates\Fuji\UnitA.fus"),
						new UpdateFileListEntry("UnitB", @"C:\Updates\Fuji\UnitB.bin", "")
					])
			],
			@"C:\Updates",
			"/MCFiles");

		var plan = Assert.Single(plans);
		Assert.Equal("Control", plan.GroupName);
		Assert.Equal(
			[
				@"\Fuji\UnitA.bin",
				@"\Fuji\UnitA.fus",
				@"\Fuji\UnitB.bin"
			],
			plan.MachineFiles);
		Assert.Equal(
			[
				@"/MCFiles\Fuji\UnitA.bin",
				@"/MCFiles\Fuji\UnitA.fus",
				@"/MCFiles\Fuji\UnitB.bin"
			],
			plan.LcuFiles);
		Assert.Equal(2, plan.Units.Count);
	}

	[Fact]
	public void Create_RejectsUnitsWithoutTransferPath()
	{
		Assert.Throws<ArgumentException>(() => ModuleUpdateService.CreateTransferPlans(
			[
				new UpdateFileGroupResult(
					"Control",
					[new UpdateFileListEntry("UnitA", null, "")])
			],
			@"C:\Updates",
			"/MCFiles"));
	}

	[Fact]
	public void Create_ThrowsWhenCancelledBeforeBuildingPlans()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			ModuleUpdateService.CreateTransferPlans(
				[],
				@"C:\Updates",
				"/MCFiles",
				cancellation.Token));
	}

	[Fact]
	public void Create_ThrowsWhenCancelledAfterReadingLastGroup()
	{
		using CancellationTokenSource cancellation = new();
		IReadOnlyList<UpdateFileGroupResult> groups = new CancellingGroupList(
			[new UpdateFileGroupResult(
				"Control",
				[new UpdateFileListEntry("UnitA", @"C:\Updates\Fuji\UnitA.bin", "")])],
			cancellation);

		Assert.Throws<OperationCanceledException>(() =>
			ModuleUpdateService.CreateTransferPlans(
				groups,
				@"C:\Updates",
				"/MCFiles",
				cancellation.Token));
	}

	private sealed class CancellingGroupList(
		IReadOnlyList<UpdateFileGroupResult> items,
		CancellationTokenSource cancellation) : IReadOnlyList<UpdateFileGroupResult>
	{
		public int Count => items.Count;

		public UpdateFileGroupResult this[int index] => items[index];

		public IEnumerator<UpdateFileGroupResult> GetEnumerator()
		{
			foreach (UpdateFileGroupResult item in items)
			{
				yield return item;
				cancellation.Cancel();
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
