using Xunit;
using WpfApp1_cmd.Services;

public class ModuleUpdateService_TransferBatchesTests
{
	[Theory]
	[InlineData(0, 1)]
	[InlineData(10, 1)]
	[InlineData(11, 2)]
	[InlineData(20, 2)]
	[InlineData(21, 3)]
	public async Task TransferAsync_SplitsFilesIntoMaximumSizedBatches(int fileCount, int expectedBatchCount)
	{
		var service = new ModuleUpdateService();
		var machineFiles = Enumerable.Range(0, fileCount).Select(index => $"machine-{index}").ToArray();
		var lcuFiles = Enumerable.Range(0, fileCount).Select(index => $"lcu-{index}").ToArray();
		List<int> batchSizes = [];
		List<int> transferredCounts = [];

		ModuleFileTransferBatchResult result = await service.TransferBatchesAsync(
			machineFiles,
			lcuFiles,
			(machineBatch, lcuBatch, _) =>
			{
				Assert.Equal(machineBatch.Select(file => file.Replace("machine-", "lcu-")), lcuBatch);
				batchSizes.Add(machineBatch.Count);
				return Task.FromResult(true);
			},
			transferredCounts.Add,
			CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(fileCount, result.TransferredFileCount);
		Assert.Equal(expectedBatchCount, batchSizes.Count);
		Assert.All(batchSizes, size => Assert.InRange(size, 0, ModuleUpdateService.MaximumFilesPerBatch));
		Assert.Equal(batchSizes, transferredCounts);
	}

	[Fact]
	public async Task TransferAsync_StopsAfterFailedBatchAndKeepsPriorSuccessCount()
	{
		var service = new ModuleUpdateService();
		var files = Enumerable.Range(0, 25).Select(index => $"file-{index}").ToArray();
		int sendCount = 0;
		List<int> successfulBatches = [];

		ModuleFileTransferBatchResult result = await service.TransferBatchesAsync(
			files,
			files,
			(_, _, _) => Task.FromResult(++sendCount < 2),
			successfulBatches.Add,
			CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal(10, result.TransferredFileCount);
		Assert.Equal(2, sendCount);
		Assert.Equal([10], successfulBatches);
	}

	[Fact]
	public async Task TransferAsync_PropagatesCancellationWhenBatchReturnsFalseAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		int completedBatchCount = 0;
		var service = new ModuleUpdateService();

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.TransferBatchesAsync(
				["machine-file"],
				["lcu-file"],
				(_, _, token) =>
				{
					Assert.Equal(cancellation.Token, token);
					cancellation.Cancel();
					return Task.FromResult(false);
				},
				_ => completedBatchCount++,
				cancellation.Token));

		Assert.Equal(0, completedBatchCount);
	}

	[Fact]
	public async Task TransferAsync_StopsWhenCancelledWhileBuildingBatch()
	{
		using CancellationTokenSource cancellation = new();
		int sendCount = 0;
		IReadOnlyList<string> machineFiles =
			new CancellingReadOnlyList<string>(["machine-file"], cancellation);

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			new ModuleUpdateService().TransferBatchesAsync(
				machineFiles,
				["lcu-file"],
				(_, _, _) =>
				{
					sendCount++;
					return Task.FromResult(true);
				},
				_ => { },
				cancellation.Token));

		Assert.Equal(0, sendCount);
	}

	[Fact]
	public async Task TransferAsync_RejectsUnequalFileLists()
	{
		var service = new ModuleUpdateService();

		await Assert.ThrowsAsync<ArgumentException>(() => service.TransferBatchesAsync(
			["machine-file"],
			Array.Empty<string>(),
			(_, _, _) => Task.FromResult(true),
			_ => { },
			CancellationToken.None));
	}

	private sealed class CancellingReadOnlyList<T>(
		IReadOnlyList<T> items,
		CancellationTokenSource cancellation) : IReadOnlyList<T>
	{
		public int Count => items.Count;

		public T this[int index]
		{
			get
			{
				T item = items[index];
				cancellation.Cancel();
				return item;
			}
		}

		public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
