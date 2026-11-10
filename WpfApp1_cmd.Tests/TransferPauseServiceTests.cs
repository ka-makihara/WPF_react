using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferPauseServiceTests
{
	[Fact]
	public async Task WaitUntilResumedAsync_ReturnsImmediatelyWhenNotPaused()
	{
		bool resumed = await new TransferPauseService().WaitUntilResumedAsync(
			() => true,
			CancellationToken.None);

		Assert.True(resumed);
	}

	[Fact]
	public async Task WaitUntilResumedAsync_ReturnsFalseWhenAlreadyCancelledAndResumed()
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		bool resumed = await new TransferPauseService().WaitUntilResumedAsync(
			() => true,
			cancellation.Token);

		Assert.False(resumed);
	}

	[Fact]
	public async Task WaitUntilResumedAsync_ReturnsFalseWhenResumeCheckCancelsToken()
	{
		using var cancellation = new CancellationTokenSource();

		bool resumed = await new TransferPauseService().WaitUntilResumedAsync(
			() =>
			{
				cancellation.Cancel();
				return true;
			},
			cancellation.Token);

		Assert.False(resumed);
	}

	[Fact]
	public async Task WaitUntilResumedAsync_ReturnsFalseWhenCancelledWhilePaused()
	{
		using var cancellation = new CancellationTokenSource();
		Task<bool> wait = new TransferPauseService().WaitUntilResumedAsync(
			() => false,
			cancellation.Token);

		cancellation.Cancel();

		Assert.False(await wait);
	}

	[Fact]
	public async Task WaitUntilResumedAsync_ReturnsTrueAfterResuming()
	{
		bool isResumed = false;
		Task<bool> wait = new TransferPauseService().WaitUntilResumedAsync(
			() => isResumed,
			CancellationToken.None);

		Assert.False(wait.IsCompleted);
		isResumed = true;

		Assert.True(await wait);
	}
}
