using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class OperationProgressMonitorTests
{
	[Fact]
	public async Task WaitAsync_ReturnsWhenOperationCompletes()
	{
		int confirmationCount = 0;

		bool cancelled = await new OperationProgressMonitor().WaitAsync(
			Task.CompletedTask,
			() => true,
			() => true,
			() => Task.CompletedTask,
			() =>
			{
				confirmationCount++;
				return Task.FromResult(true);
			},
			() => Task.CompletedTask,
			() => { });

		Assert.False(cancelled);
		Assert.Equal(0, confirmationCount);
	}

	[Fact]
	public async Task WaitAsync_ConfirmsCancellationAndInvokesCallback()
	{
		int pauseCount = 0;
		int confirmationCount = 0;
		int cancellationCount = 0;

		var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		bool cancelled = await new OperationProgressMonitor().WaitAsync(
			operation.Task,
			() => true,
			() => true,
			() =>
			{
				pauseCount++;
				return Task.CompletedTask;
			},
			() =>
			{
				confirmationCount++;
				return Task.FromResult(true);
			},
			() => Task.CompletedTask,
			() => cancellationCount++);

		Assert.True(cancelled);
		Assert.Equal(1, pauseCount);
		Assert.Equal(1, confirmationCount);
		Assert.Equal(1, cancellationCount);
	}

	[Fact]
	public async Task WaitAsync_SkipsConfirmationWhenOperationCompletesDuringPause()
	{
		var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		int confirmationCount = 0;
		int cancellationCount = 0;

		bool cancelled = await new OperationProgressMonitor().WaitAsync(
			operation.Task,
			() => true,
			() => true,
			() =>
			{
				operation.SetResult();
				return Task.CompletedTask;
			},
			() =>
			{
				confirmationCount++;
				return Task.FromResult(true);
			},
			() => Task.CompletedTask,
			() => cancellationCount++);

		Assert.False(cancelled);
		Assert.Equal(0, confirmationCount);
		Assert.Equal(0, cancellationCount);
	}

	[Fact]
	public async Task WaitAsync_DoesNotReportCancellationWhenOperationCompletesDuringConfirmation()
	{
		var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		int cancellationCount = 0;

		bool cancelled = await new OperationProgressMonitor().WaitAsync(
			operation.Task,
			() => true,
			() => true,
			() => Task.CompletedTask,
			() =>
			{
				operation.SetResult();
				return Task.FromResult(true);
			},
			() => Task.CompletedTask,
			() => cancellationCount++);

		Assert.False(cancelled);
		Assert.Equal(0, cancellationCount);
	}

	[Fact]
	public async Task WaitAsync_DoesNotResumeWhenOperationCompletesAfterCancellationIsDeclined()
	{
		var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		int resumeCount = 0;

		bool cancelled = await new OperationProgressMonitor().WaitAsync(
			operation.Task,
			() => true,
			() => true,
			() => Task.CompletedTask,
			() =>
			{
				operation.SetResult();
				return Task.FromResult(false);
			},
			() =>
			{
				resumeCount++;
				return Task.CompletedTask;
			},
			() => { });

		Assert.False(cancelled);
		Assert.Equal(0, resumeCount);
	}

	[Fact]
	public async Task WaitAsync_ResumesWhenCancellationIsDeclined()
	{
		var operation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		bool cancellationRequested = true;
		int resumeCount = 0;

		bool cancelled = await new OperationProgressMonitor().WaitAsync(
			operation.Task,
			() => true,
			() => cancellationRequested,
			() => Task.CompletedTask,
			() => Task.FromResult(false),
			() =>
			{
				resumeCount++;
				cancellationRequested = false;
				operation.SetResult();
				return Task.CompletedTask;
			},
			() => { });

		Assert.False(cancelled);
		Assert.Equal(1, resumeCount);
	}
}
