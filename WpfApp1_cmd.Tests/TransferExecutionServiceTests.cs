using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferExecutionServiceTests
{
	[Theory]
	[InlineData("success", TransferExecutionFailure.None)]
	[InlineData("load", TransferExecutionFailure.UpdateInfoLoad)]
	[InlineData("module1", TransferExecutionFailure.ModuleTransfer)]
	[InlineData("module2", TransferExecutionFailure.ModuleTransfer)]
	[InlineData("cleanup", TransferExecutionFailure.ModuleTransfer)]
	[InlineData("cancel", TransferExecutionFailure.Cancelled)]
	public async Task ExecuteAsync_SupportsPerModuleStagingAndCleanupWithoutBulkUpload(
		string scenario, TransferExecutionFailure expectedFailure)
	{
		using CancellationTokenSource cancellation = new();
		List<string> calls = [];
		TransferLcuWork MakeLcu(string name, bool? selected) =>
			new(name, selected, true, true, null,
			Enumerable.Range(1, 2).Select(index =>
				new TransferMachineWork($"Machine{index}", true, true,
				[
					new TransferModuleWork($"Module{index}", true, true, _ =>
					{
						calls.Add($"{name}:upload{index}");
						calls.Add($"{name}:module{index}");
						if (scenario == "cancel")
							cancellation.Cancel();
						if (scenario == $"module{index}" || cancellation.IsCancellationRequested)
							return Task.FromResult(false);
						calls.Add($"{name}:cleanup{index}");
						return Task.FromResult(scenario != "cleanup");
					})
				])).ToArray());

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[MakeLcu("first", null), MakeLcu("second", true), MakeLcu("skipped", false)],
			_ => Task.FromResult(scenario == "load" ? 0L : 1L),
			_ => { }, _ => { }, (_, _) => { }, (_, _, _) => { },
			(_, _, _) => { }, (_, _, _) => { }, cancellation.Token);

		Assert.Equal(expectedFailure, result.Failure);
		Assert.Equal(scenario == "success", result.IsSuccess);
		Assert.DoesNotContain(calls, call => call.StartsWith("skipped:"));
		if (scenario == "success")
		{
			Assert.Equal(
				["first:upload1", "first:module1", "first:cleanup1",
				 "first:upload2", "first:module2", "first:cleanup2",
				 "second:upload1", "second:module1", "second:cleanup1",
				 "second:upload2", "second:module2", "second:cleanup2"],
				calls);
		}
		else if (scenario == "cleanup")
		{
			Assert.Equal("first:cleanup1", calls.Last());
		}
		else
		{
			Assert.DoesNotContain(calls, call => call == "first:cleanup2");
			if (scenario != "module2")
				Assert.DoesNotContain(calls, call => call.Contains(":cleanup"));
		}
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsCancelledWhenTokenIsCancelledDuringUpload()
	{
		using CancellationTokenSource cancellation = new();
		int moduleLoadCalls = 0;
		TransferLcuWork lcu = CreateLcu(
			"LCU",
			true,
			token =>
			{
				Assert.Equal(cancellation.Token, token);
				cancellation.Cancel();
				return Task.FromCanceled<bool>(cancellation.Token);
			},
			[]);

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[lcu],
			token =>
			{
				Assert.Equal(cancellation.Token, token);
				moduleLoadCalls++;
				return Task.FromResult(1L);
			},
			_ => { },
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			cancellation.Token);

		Assert.False(result.IsSuccess);
		Assert.Equal(TransferExecutionFailure.Cancelled, result.Failure);
		Assert.Equal(0, moduleLoadCalls);
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsCancelledWhenUploadReportsFalseAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		TransferLcuWork lcu = CreateLcu(
			"LCU",
			true,
			token =>
			{
				Assert.Equal(cancellation.Token, token);
				cancellation.Cancel();
				return Task.FromResult(false);
			},
			[]);

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[lcu],
			_ => Task.FromResult(1L),
			_ => { },
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			cancellation.Token);

		Assert.False(result.IsSuccess);
		Assert.Equal(TransferExecutionFailure.Cancelled, result.Failure);
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotLoadModulesWhenAlreadyCancelledWithNoLcus()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		bool moduleLoadCalled = false;

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[],
			_ =>
			{
				moduleLoadCalled = true;
				return Task.FromResult(1L);
			},
			_ => { },
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			cancellation.Token);

		Assert.False(result.IsSuccess);
		Assert.Equal(TransferExecutionFailure.Cancelled, result.Failure);
		Assert.False(moduleLoadCalled);
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsCancelledWhileScanningUnselectedLcus()
	{
		using CancellationTokenSource cancellation = new();
		int moduleLoadCalls = 0;
		TransferLcuWork[] lcus =
		[
			CreateLcu("UnselectedA", false, _ => Task.FromResult(true), []),
			CreateLcu("UnselectedB", false, _ => Task.FromResult(true), [])
		];

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			new CancellingReadOnlyList<TransferLcuWork>(lcus, cancellation),
			_ =>
			{
				moduleLoadCalls++;
				return Task.FromResult(1L);
			},
			_ => { },
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			cancellation.Token);

		Assert.False(result.IsSuccess);
		Assert.Equal(TransferExecutionFailure.Cancelled, result.Failure);
		Assert.Equal(0, moduleLoadCalls);
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsCancelledWhenLoadCallbackCancelsWithNoLcus()
	{
		using CancellationTokenSource cancellation = new();

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[],
			token =>
			{
				Assert.Equal(cancellation.Token, token);
				return Task.FromResult(1L);
			},
			_ => cancellation.Cancel(),
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			cancellation.Token);

		Assert.False(result.IsSuccess);
		Assert.Equal(TransferExecutionFailure.Cancelled, result.Failure);
	}

	[Fact]
	public async Task ExecuteAsync_RunsUploadsLoadThenEligibleModulesInOrder()
	{
		List<string> calls = [];
		TransferLcuWork selected = CreateLcu("Selected", true, token =>
		{
			Assert.Equal(CancellationToken.None, token);
			calls.Add("lcu-upload");
			return Task.FromResult(true);
		},
		[
			new TransferMachineWork("Machine", true, true,
			[
				new TransferModuleWork("Module", true, true, token =>
				{
					Assert.Equal(CancellationToken.None, token);
					calls.Add("module-transfer");
					return Task.FromResult(true);
				})
			])
		]);
		TransferLcuWork unselected = CreateLcu("Unselected", false, _ =>
		{
			calls.Add("unexpected-lcu-upload");
			return Task.FromResult(true);
		}, []);

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[selected, unselected],
			token =>
			{
				Assert.Equal(CancellationToken.None, token);
				calls.Add("load");
				return Task.FromResult(1L);
			},
			count => calls.Add($"loaded:{count}"),
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { });

		Assert.True(result.IsSuccess);
		Assert.Equal(
			["lcu-upload", "load", "loaded:1", "module-transfer"],
			calls);
	}

	[Fact]
	public async Task ExecuteAsync_StopsAfterUploadFailureWithoutLoadingModules()
	{
		int loadCalls = 0;
		TransferLcuWork lcu = CreateLcu("LCU", true, _ => Task.FromResult(false), []);

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[lcu],
			_ =>
			{
				loadCalls++;
				return Task.FromResult(1L);
			},
			_ => { },
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { });

		Assert.False(result.IsSuccess);
		Assert.Equal(TransferExecutionFailure.LcuUpload, result.Failure);
		Assert.Equal(0, loadCalls);
	}

	[Fact]
	public async Task ExecuteAsync_StopsAfterModuleLoadFailure()
	{
		int moduleTransfers = 0;
		TransferLcuWork lcu = CreateLcu(
			"LCU",
			true,
			_ => Task.FromResult(true),
			[
				new TransferMachineWork("Machine", true, true,
				[
					new TransferModuleWork("Module", true, true, _ =>
					{
						moduleTransfers++;
						return Task.FromResult(true);
					})
				])
			]);

		TransferExecutionResult result = await new TransferExecutionService().ExecuteAsync(
			[lcu],
			_ => Task.FromResult(0L),
			_ => { },
			_ => { },
			(_, _) => { },
			(_, _, _) => { },
			(_, _, _) => { },
			(_, _, _) => { });

		Assert.False(result.IsSuccess);
		Assert.Equal(TransferExecutionFailure.UpdateInfoLoad, result.Failure);
		Assert.Equal(0, moduleTransfers);
	}

	private static TransferLcuWork CreateLcu(
		string name,
		bool? selected,
		Func<CancellationToken, Task<bool>> upload,
		IReadOnlyList<TransferMachineWork> machines) =>
		new(name, selected, true, true, upload, machines);

	private sealed class CancellingReadOnlyList<T>(
		IReadOnlyList<T> items,
		CancellationTokenSource cancellation) : IReadOnlyList<T>
	{
		public int Count => items.Count;

		public T this[int index] => items[index];

		public IEnumerator<T> GetEnumerator()
		{
			foreach (T item in items)
			{
				yield return item;
				cancellation.Cancel();
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
