using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LineInfoLoadWaiterTests
{
	[Fact]
	public async Task WaitAsync_ReturnsAfterOperationCompletes()
	{
		int delayCount = 0;
		var operation = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		var waiter = new LineInfoLoadWaiter(_ =>
		{
			delayCount++;
			operation.SetResult(true);
			return Task.CompletedTask;
		});

		LineInfoLoadWaitResult result = await waiter.WaitAsync(
			operation.Task,
			() => false,
			() => Assert.Fail("Unexpected cancellation"));

		Assert.Equal(new LineInfoLoadWaitResult(true, false), result);
		Assert.Equal(1, delayCount);
	}

	[Fact]
	public async Task WaitAsync_CancelsOperationWhenCancellationIsRequested()
	{
		bool cancellationRequested = false;
		int cancellationCount = 0;
		int delayCount = 0;
		var operation = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		var waiter = new LineInfoLoadWaiter(_ =>
		{
			delayCount++;
			if (delayCount == 1)
			{
				cancellationRequested = true;
			}
			else
			{
				operation.SetResult(false);
			}
			return Task.CompletedTask;
		});

		LineInfoLoadWaitResult result = await waiter.WaitAsync(
			operation.Task,
			() => cancellationRequested,
			() =>
			{
				cancellationCount++;
			});

		Assert.Equal(new LineInfoLoadWaitResult(false, true), result);
		Assert.Equal(1, cancellationCount);
		Assert.Equal(2, delayCount);
	}

	[Fact]
	public async Task WaitAsync_DoesNotCancelOperationThatCompletesDuringCancellationCheck()
	{
		int cancellationCount = 0;
		int delayCount = 0;
		var operation = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		var waiter = new LineInfoLoadWaiter(_ =>
		{
			delayCount++;
			return Task.CompletedTask;
		});

		LineInfoLoadWaitResult result = await waiter.WaitAsync(
			operation.Task,
			() =>
			{
				operation.SetResult(true);
				return true;
			},
			() => cancellationCount++);

		Assert.Equal(new LineInfoLoadWaitResult(true, false), result);
		Assert.Equal(0, cancellationCount);
		Assert.Equal(0, delayCount);
	}

	[Fact]
	public async Task WaitAsync_ReturnsImmediatelyWhenCancelCallbackCompletesOperation()
	{
		int delayCount = 0;
		var operation = new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		var waiter = new LineInfoLoadWaiter(_ =>
		{
			delayCount++;
			return Task.CompletedTask;
		});

		LineInfoLoadWaitResult result = await waiter.WaitAsync(
			operation.Task,
			() => true,
			() => operation.SetResult(false));

		Assert.Equal(new LineInfoLoadWaitResult(false, true), result);
		Assert.Equal(0, delayCount);
	}

	[Fact]
	public async Task WaitAsync_PreservesAnUnsuccessfulOperationResult()
	{
		var waiter = new LineInfoLoadWaiter(_ => Task.CompletedTask);

		LineInfoLoadWaitResult result = await waiter.WaitAsync(
			Task.FromResult(false),
			() => false,
			() => Assert.Fail("Unexpected cancellation"));

		Assert.Equal(new LineInfoLoadWaitResult(false, false), result);
	}

	[Fact]
	public async Task WaitAsync_PropagatesOperationFailures()
	{
		var failure = new InvalidOperationException("load failed");
		var waiter = new LineInfoLoadWaiter(_ => Task.CompletedTask);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => waiter.WaitAsync(
				Task.FromException<bool>(failure),
				() => false,
				() => Assert.Fail("Unexpected cancellation")));
	}
}
