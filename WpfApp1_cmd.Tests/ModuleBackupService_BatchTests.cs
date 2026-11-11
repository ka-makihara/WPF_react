using System.IO;
using Xunit;
using WpfApp1_cmd.Services;

public class ModuleBackupService_BatchTests
{
	[Fact]
	public async Task ExecuteAsync_CreatesPathsAndProcessesItemsInOrder()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			var service = new ModuleBackupService(() => new DateTime(2026, 10, 27, 16, 0, 0));
			var workItems = new[]
			{
				new ModuleBackupWorkItem<int>("lcu:1", "machine:1", "module-a", "192.168.0.1", 1),
				new ModuleBackupWorkItem<int>("lcu:2", "machine:2", "module-b", "192.168.0.2", 2)
			};
			List<int> processed = [];
			List<string> backupPaths = [];

			var result = await service.ExecuteBatchAsync(
				workItems,
				root,
				(item, backupPath, _) =>
				{
					processed.Add(item.Context);
					backupPaths.Add(backupPath);
					return Task.FromResult(true);
				},
				(_, _) => Task.FromResult(true),
				null,
				null,
				CancellationToken.None);

			Assert.Equal(ModuleBackupBatchOutcome.Succeeded, result.Outcome);
			Assert.Null(result.StoppedAt);
			Assert.Equal([1, 2], processed);
			Assert.Equal(
				[
					Path.Combine(root, "20261027_160000_lcu_machine_module-a"),
					Path.Combine(root, "20261027_160000_lcu_machine_module-b")
				],
				backupPaths);
			Assert.All(backupPaths, backupPath => Assert.True(Directory.Exists(backupPath)));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task ExecuteAsync_StopsAfterFirstFailedBackup()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			var workItems = new[]
			{
				new ModuleBackupWorkItem<int>("lcu", "machine", "module-a", "192.168.0.1", 1),
				new ModuleBackupWorkItem<int>("lcu", "machine", "module-b", "192.168.0.1", 2)
			};
			List<int> processed = [];

			var result = await new ModuleBackupService().ExecuteBatchAsync<int>(
				workItems,
				root,
				(item, _, _) =>
				{
					processed.Add(item.Context);
					return Task.FromResult(false);
				},
				(_, _) => Task.FromResult(true),
				null,
				null,
				CancellationToken.None);

			Assert.Equal(ModuleBackupBatchOutcome.Failed, result.Outcome);
			Assert.Equal(1, result.StoppedAt?.Context);
			Assert.Equal([1], processed);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsCancelledWhenContinuationIsDeclined()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			var workItem = new ModuleBackupWorkItem<int>(
				"lcu", "machine", "module", "192.168.0.1", 1);
			var result = await new ModuleBackupService().ExecuteBatchAsync(
				[workItem],
				root,
				(_, _, _) => Task.FromResult(true),
				(_, _) => Task.FromResult(false),
				null,
				null,
				CancellationToken.None);

			Assert.Equal(ModuleBackupBatchOutcome.Cancelled, result.Outcome);
			Assert.Same(workItem, result.StoppedAt);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsCancelledWhenBackupReturnsFalseAfterCancellation()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		try
		{
			ModuleBackupWorkItem<int> workItem =
				new("lcu", "machine", "module", "192.168.0.1", 1);
			bool completed = false;
			var result = await new ModuleBackupService().ExecuteBatchAsync(
				[workItem],
				root,
				(_, _, token) =>
				{
					Assert.Equal(cancellation.Token, token);
					cancellation.Cancel();
					return Task.FromResult(false);
				},
				(_, _) => Task.FromResult(true),
				null,
				_ => completed = true,
				cancellation.Token);

			Assert.Equal(ModuleBackupBatchOutcome.Cancelled, result.Outcome);
			Assert.Same(workItem, result.StoppedAt);
			Assert.False(completed);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotCreateModuleDirectoryWhenStartCallbackCancels()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		try
		{
			ModuleBackupWorkItem<int> workItem =
				new("lcu", "machine", "module", "192.168.0.1", 1);
			bool backupCalled = false;
			var result = await new ModuleBackupService().ExecuteBatchAsync(
				[workItem],
				root,
				(_, _, _) =>
				{
					backupCalled = true;
					return Task.FromResult(true);
				},
				(_, _) => Task.FromResult(true),
				_ => cancellation.Cancel(),
				null,
				cancellation.Token);

			Assert.Equal(ModuleBackupBatchOutcome.Cancelled, result.Outcome);
			Assert.Same(workItem, result.StoppedAt);
			Assert.False(backupCalled);
			Assert.Empty(Directory.GetDirectories(root));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task ExecuteAsync_ReturnsCancelledForEmptyWorkWhenAlreadyCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		var result = await new ModuleBackupService().ExecuteBatchAsync<int>(
			[],
			Path.GetTempPath(),
			(_, _, _) => Task.FromResult(true),
			(_, _) => Task.FromResult(true),
			null,
			null,
			cancellation.Token);

		Assert.Equal(ModuleBackupBatchOutcome.Cancelled, result.Outcome);
		Assert.Null(result.StoppedAt);
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
