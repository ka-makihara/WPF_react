using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferSelectionTreeBuilderTests
{
	[Fact]
	public void Build_PreservesHierarchyAndSelectionDefaults()
	{
		IReadOnlyList<TransferSelectionLcu> result = new TransferSelectionTreeBuilder().Build(
			[
				new TransferSelectionLcuInput(
					null,
					[
						new TransferSelectionMachineInput(
							false,
							[
								new TransferSelectionModuleInput(
									null,
									[
										new TransferSelectionUnitInput(
											true,
											"UnitA",
											"/a.bin",
											"/fuser.bin"),
										new TransferSelectionUnitInput(
											null,
											"UnitB",
											"/b.bin",
											null)
									])
							])
					])
			]);

		TransferSelectionLcu lcu = Assert.Single(result);
		Assert.True(lcu.IsSelected);
		TransferSelectionMachine machine = Assert.Single(lcu.Machines);
		Assert.False(machine.IsSelected);
		TransferSelectionModule module = Assert.Single(machine.Modules);
		Assert.True(module.IsSelected);
		Assert.Equal(
			[
				new TransferSelectionUnit(true, "UnitA", "/a.bin", "/fuser.bin"),
				new TransferSelectionUnit(false, "UnitB", "/b.bin", null)
			],
			module.Units);
	}

	[Fact]
	public void Build_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new TransferSelectionTreeBuilder().Build([], cancellation.Token));
	}
}
