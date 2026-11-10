using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleUpdateService_TransferPreparationTests
{
	[Fact]
	public void Build_PreparesTransferPlansAndPreservesVersionData()
	{
		UpdateFileGroupResult[] groups =
		[
			new("GroupA",
			[
				new UpdateFileListEntry("UnitA", @"C:\Updates\Fuji\UnitA.bin", @"C:\Updates\Fuji\UnitA.fus")
			])
		];
		ModuleUpdateVersion[] versions =
		[
			new("UnitA", @"\Fuji\UnitA.bin", @"\Fuji\UnitA.fus", "1.0", "2.0")
		];

		ModuleUpdateTransferPreparation result =
			new ModuleUpdateService().BuildTransferPreparation(
				groups,
				versions,
				@"C:\Updates",
				"/MCFiles");

		ModuleFileTransferPlan plan = Assert.Single(result.Plans);
		Assert.Equal("GroupA", plan.GroupName);
		Assert.Equal(
			[@"\Fuji\UnitA.bin", @"\Fuji\UnitA.fus"],
			plan.MachineFiles);
		Assert.Equal(
			[@"/MCFiles\Fuji\UnitA.bin", @"/MCFiles\Fuji\UnitA.fus"],
			plan.LcuFiles);
		Assert.Equal(versions, result.Versions);
		Assert.NotSame(versions, result.Versions);
	}

	[Fact]
	public void Build_RejectsNullInputs()
	{
		var builder = new ModuleUpdateService();

		Assert.Throws<ArgumentNullException>(() =>
			builder.BuildTransferPreparation(null!, [], @"C:\Updates", "/MCFiles"));
		Assert.Throws<ArgumentNullException>(() =>
			builder.BuildTransferPreparation([], null!, @"C:\Updates", "/MCFiles"));
	}

	[Fact]
	public void Build_ThrowsWhenCancelledWhileCopyingVersionSnapshot()
	{
		using CancellationTokenSource cancellation = new();
		IReadOnlyList<ModuleUpdateVersion> versions = new CancellingVersionList(
			[new ModuleUpdateVersion("UnitA", null, null, "1.0", "2.0")],
			cancellation);

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleUpdateService().BuildTransferPreparation(
				[],
				versions,
				@"C:\Updates",
				"/MCFiles",
				cancellation.Token));
	}

	private sealed class CancellingVersionList(
		IReadOnlyList<ModuleUpdateVersion> items,
		CancellationTokenSource cancellation) : IReadOnlyList<ModuleUpdateVersion>
	{
		public int Count => items.Count;

		public ModuleUpdateVersion this[int index]
		{
			get
			{
				ModuleUpdateVersion item = items[index];
				cancellation.Cancel();
				return item;
			}
		}

		public IEnumerator<ModuleUpdateVersion> GetEnumerator() => items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
