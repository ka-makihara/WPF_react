using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuLineTreeService_PlanTests
{
	[Fact]
	public void Create_SkipsUnsupportedMachineUnlessIgnoreOptionIsEnabled()
	{
		LcuMachineDescriptor[] machines =
		[
			CreateMachine("Supported", "A"),
			CreateMachine("Unsupported", "B")
		];

		LcuLineTreePlan filtered = new LcuLineTreeService().CreatePlan(
			machines,
			["A"],
			ignoreMachineType: false,
			lcuIsSelected: true);
		LcuLineTreePlan ignored = new LcuLineTreeService().CreatePlan(
			machines,
			["A"],
			ignoreMachineType: true,
			lcuIsSelected: true);

		Assert.Equal([("Unsupported", "B")], filtered.SkippedMachines);
		Assert.Equal(["Supported"], filtered.Machines.Select(machine => machine.Name));
		Assert.Equal(["Supported", "Unsupported"], ignored.Machines.Select(machine => machine.Name));
		Assert.False(Assert.Single(ignored.Machines, machine => machine.Name == "Unsupported").IsSupported);
	}

	[Fact]
	public void Create_PropagatesSelectionAndMarksUnsupportedNodesUnselected()
	{
		LcuMachineDescriptor[] machines =
		[
			CreateMachine("Supported", "A"),
			CreateMachine("Unsupported", "B")
		];

		LcuLineTreePlan plan = new LcuLineTreeService().CreatePlan(
			machines,
			["A"],
			ignoreMachineType: true,
			lcuIsSelected: null);

		LcuMachineTreePlan supported = Assert.Single(
			plan.Machines,
			machine => machine.Name == "Supported");
		LcuMachineTreePlan unsupported = Assert.Single(
			plan.Machines,
			machine => machine.Name == "Unsupported");

		Assert.Null(supported.IsSelected);
		Assert.Null(Assert.Single(Assert.Single(supported.Bases).Modules).IsSelected);
		Assert.False(unsupported.IsSelected);
		Assert.False(Assert.Single(Assert.Single(unsupported.Bases).Modules).IsSelected);
	}

	[Fact]
	public void Create_LeavesMachineSelectedWhenItHasNoBases()
	{
		LcuLineTreePlan plan = new LcuLineTreeService().CreatePlan(
			[CreateMachine("NoBase", "A") with { Bases = [] }],
			["A"],
			ignoreMachineType: false,
			lcuIsSelected: false);

		Assert.True(Assert.Single(plan.Machines).IsSelected);
	}

	[Fact]
	public void Create_MatchesSupportedMachineTypesCaseSensitively()
	{
		LcuLineTreePlan plan = new LcuLineTreeService().CreatePlan(
			[CreateMachine("Machine", "type-a")],
			["Type-A"],
			ignoreMachineType: false,
			lcuIsSelected: true);

		Assert.Empty(plan.Machines);
		Assert.Equal(("Machine", "type-a"), Assert.Single(plan.SkippedMachines));
	}

	[Fact]
	public void Create_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new LcuLineTreeService().CreatePlan(
				[CreateMachine("Machine", "A")],
				["A"],
				ignoreMachineType: false,
				lcuIsSelected: true,
				cancellation.Token));
	}

	[Fact]
	public void Create_ThrowsWhenCancelledWhileIndexingSupportedTypes()
	{
		using CancellationTokenSource cancellation = new();
		IReadOnlyCollection<string?> supportedTypes = new CancellingTypeCollection(
			["A"],
			cancellation);

		Assert.Throws<OperationCanceledException>(() =>
			new LcuLineTreeService().CreatePlan(
				[],
				supportedTypes,
				ignoreMachineType: false,
				lcuIsSelected: true,
				cancellation.Token));
	}

	private static LcuMachineDescriptor CreateMachine(string? name, string? type) =>
		new(
			name,
			type,
			"model",
			[
				new LcuBaseDescriptor(
					1,
					2,
					"127.0.0.1",
					3,
					null,
					[new LcuModuleDescriptor(4, 5, "Module")])
			]);

	private sealed class CancellingTypeCollection(
		IReadOnlyCollection<string?> items,
		CancellationTokenSource cancellation) : IReadOnlyCollection<string?>
	{
		public int Count => items.Count;

		public IEnumerator<string?> GetEnumerator()
		{
			foreach (string? item in items)
			{
				yield return item;
				cancellation.Cancel();
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
