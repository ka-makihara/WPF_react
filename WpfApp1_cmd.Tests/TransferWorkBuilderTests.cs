using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferWorkBuilderTests
{
	[Theory]
	[InlineData(null, null, 1)]
	[InlineData(null, true, 1)]
	[InlineData(true, null, 1)]
	[InlineData(true, true, 1)]
	[InlineData(false, null, 0)]
	[InlineData(null, false, 0)]
	public async Task Build_ExecutesPartiallySelectedParentsButSkipsUncheckedParents(
		bool? machineSelection,
		bool? moduleSelection,
		int expectedTransfers)
	{
		int uploads = 0;
		int transfers = 0;
		TransferLcuNode<string, string, string>[] nodes =
		[
			new("Line", null, true, true, "lcu",
				[new("Machine", true, machineSelection, "machine",
					[new("Module", moduleSelection, true, "module")])])
		];
		IReadOnlyList<TransferLcuWork> work = new TransferWorkBuilder().Build(
			nodes,
			(_, _) =>
			{
				uploads++;
				return Task.FromResult(true);
			},
			(_, _, _, _) =>
			{
				transfers++;
				return Task.FromResult(true);
			});

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			work,
			_ => Task.FromResult(1L),
			_ => { },
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { });

		Assert.True(result.IsSuccess);
		Assert.Equal(1, uploads);
		Assert.Equal(expectedTransfers, transfers);
	}

	[Fact]
	public async Task Build_PreservesHierarchyFlagsAndBindsOperationsToTheirContexts()
	{
		var builder = new TransferWorkBuilder();
		using CancellationTokenSource cancellation = new();
		TransferLcuNode<string, string, string>[] nodes =
		[
			new(
				"LineA",
				null,
				true,
				true,
				"lcu-context",
				[
					new(
						"MachineA",
						true,
						false,
						"machine-context",
						[
							new("ModuleA", true, true, "module-context")
						])
				])
		];
		List<string> calls = [];

		IReadOnlyList<TransferLcuWork> work = builder.Build(
			nodes,
			(context, token) =>
			{
				Assert.Equal(cancellation.Token, token);
				calls.Add($"upload:{context}");
				return Task.FromResult(true);
			},
			(lcu, machine, module, token) =>
			{
				Assert.Equal(cancellation.Token, token);
				calls.Add($"transfer:{lcu}:{machine}:{module}");
				return Task.FromResult(true);
			});

		TransferLcuWork lcuWork = Assert.Single(work);
		Assert.Equal("LineA", lcuWork.Name);
		Assert.Null(lcuWork.IsSelected);
		Assert.True(lcuWork.HasChildren);
		Assert.True(lcuWork.HasController);
		Assert.NotNull(lcuWork.UploadFiles);
		Assert.True(await lcuWork.UploadFiles(cancellation.Token));
		TransferMachineWork machineWork = Assert.Single(lcuWork.Machines);
		Assert.Equal("MachineA", machineWork.Name);
		Assert.False(machineWork.IsSelected);
		TransferModuleWork moduleWork = Assert.Single(machineWork.Modules);
		Assert.Equal("ModuleA", moduleWork.Name);
		Assert.True(moduleWork.IsSelected);
		Assert.True(moduleWork.HasUnitVersions);
		Assert.True(await moduleWork.Transfer(cancellation.Token));
		Assert.Equal(
			["upload:lcu-context", "transfer:lcu-context:machine-context:module-context"],
			calls);
	}

	[Fact]
	public void Build_RejectsNullInputs()
	{
		var builder = new TransferWorkBuilder();

		Assert.Throws<ArgumentNullException>(() =>
			builder.Build<string, string, string>(
				null!,
				(_, _) => Task.FromResult(true),
				(_, _, _, _) => Task.FromResult(true)));
		Assert.Empty(builder.Build<string, string, string>(
			[], null, (_, _, _, _) => Task.FromResult(true)));
	}

	[Fact]
	public void Build_ThrowsWhenCancelledBeforeBuildingWork()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new TransferWorkBuilder().Build<string, string, string>(
				[],
				(_, _) => Task.FromResult(true),
				(_, _, _, _) => Task.FromResult(true),
				cancellation.Token));
	}

	[Fact]
	public void Build_ThrowsWhenCancelledWhileEnumeratingMachines()
	{
		using CancellationTokenSource cancellation = new();
		TransferLcuNode<string, string, string>[] nodes =
		[
			new(
				"LineA",
				true,
				true,
				true,
				"lcu",
				new CancellingReadOnlyList<TransferMachineNode<string, string>>(
					[],
					cancellation))
		];

		Assert.Throws<OperationCanceledException>(() =>
			new TransferWorkBuilder().Build(
				nodes,
				(_, _) => Task.FromResult(true),
				(_, _, _, _) => Task.FromResult(true),
				cancellation.Token));
	}

	private sealed class CancellingReadOnlyList<T>(
		IReadOnlyList<T> items,
		CancellationTokenSource cancellation) : IReadOnlyList<T>
	{
		public int Count
		{
			get
			{
				cancellation.Cancel();
				return items.Count;
			}
		}

		public T this[int index] => items[index];

		public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
