using System.Net.NetworkInformation;
using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuReachabilityServiceTests
{
	[Fact]
	public async Task IsReachableAsync_RetriesUntilPingSucceeds()
	{
		int pingCount = 0;
		int delayCount = 0;
		var service = new LcuReachabilityService(
			(_, _) => Task.FromResult(
				++pingCount == 2 ? IPStatus.Success : IPStatus.TimedOut),
			(_, _) =>
			{
				delayCount++;
				return Task.CompletedTask;
			});

		bool reachable = await service.IsReachableAsync("lcu-host", 3);

		Assert.True(reachable);
		Assert.Equal(2, pingCount);
		Assert.Equal(1, delayCount);
	}

	[Fact]
	public async Task IsReachableAsync_ReturnsFalseAfterAllAttemptsFail()
	{
		int pingCount = 0;
		int delayCount = 0;
		var service = new LcuReachabilityService(
			(_, _) =>
			{
				pingCount++;
				return Task.FromResult(IPStatus.TimedOut);
			},
			(_, _) =>
			{
				delayCount++;
				return Task.CompletedTask;
			});

		bool reachable = await service.IsReachableAsync("lcu-host", 3);

		Assert.False(reachable);
		Assert.Equal(3, pingCount);
		Assert.Equal(3, delayCount);
	}

	[Fact]
	public async Task IsReachableAsync_ReportsPingExceptionAndReturnsFalse()
	{
		string? reportedMessage = null;
		var service = new LcuReachabilityService(
			(_, _) => throw new PingException("ping failed"),
			(_, _) => Task.CompletedTask);

		bool reachable = await service.IsReachableAsync(
			"lcu-host",
			3,
			message => reportedMessage = message);

		Assert.False(reachable);
		Assert.Equal("ping failed", reportedMessage);
	}

	[Fact]
	public async Task IsReachableAsync_PropagatesCancellationInsteadOfReportingPingFailure()
	{
		using CancellationTokenSource cancellation = new();
		bool pingErrorReported = false;
		var service = new LcuReachabilityService(
			(_, _) =>
			{
				cancellation.Cancel();
				throw new PingException("ping cancelled");
			},
			(_, _) => Task.CompletedTask);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.IsReachableAsync(
				"lcu-host",
				3,
				_ => pingErrorReported = true,
				cancellation.Token));

		Assert.False(pingErrorReported);
	}

	[Fact]
	public async Task IsReachableAsync_PropagatesUnexpectedExceptions()
	{
		var service = new LcuReachabilityService(
			(_, _) => throw new InvalidOperationException("unexpected"),
			(_, _) => Task.CompletedTask);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.IsReachableAsync("lcu-host", 3));
	}

	[Fact]
	public async Task IsReachableAsync_PassesCancellationTokenToPingAndDelay()
	{
		using CancellationTokenSource cancellation = new();
		CancellationToken? pingToken = null;
		CancellationToken? delayToken = null;
		var service = new LcuReachabilityService(
			(_, token) =>
			{
				pingToken = token;
				return Task.FromResult(IPStatus.TimedOut);
			},
			(_, token) =>
			{
				delayToken = token;
				return Task.CompletedTask;
			});

		await service.IsReachableAsync(
			"lcu-host",
			1,
			cancellationToken: cancellation.Token);

		Assert.Equal(cancellation.Token, pingToken);
		Assert.Equal(cancellation.Token, delayToken);
	}

	[Fact]
	public async Task IsReachableAsync_ThrowsWhenAlreadyCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		int pingCount = 0;
		var service = new LcuReachabilityService(
			(_, _) =>
			{
				pingCount++;
				return Task.FromResult(IPStatus.Success);
			},
			(_, _) => Task.CompletedTask);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => service.IsReachableAsync(
				"lcu-host",
				3,
				cancellationToken: cancellation.Token));

		Assert.Equal(0, pingCount);
	}
}
