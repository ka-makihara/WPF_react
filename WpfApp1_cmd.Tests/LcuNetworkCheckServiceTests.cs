using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuNetworkCheckServiceTests
{
	[Fact]
	public async Task CheckAsync_ReturnsUnreachableWithoutRequestingVersion()
	{
		int versionRequestCount = 0;
		LcuNetworkCheckService service = new(new FakeReachabilityService(
			_ => Task.FromResult(false)));

		LcuNetworkCheckResult result = await service.CheckAsync(
			"lcu-host",
			new FakeInformationGateway(_ =>
			{
				versionRequestCount++;
				return Task.FromResult<IReadOnlyList<LcuVersionEntry>?>([]);
			}));

		Assert.Equal(LcuNetworkCheckOutcome.Unreachable, result.Outcome);
		Assert.Equal(0, versionRequestCount);
	}

	[Fact]
	public async Task CheckAsync_ReturnsVersionUnavailableWhenVersionIsMissing()
	{
		LcuNetworkCheckService service = new(new FakeReachabilityService(
			_ => Task.FromResult(true)));

		LcuNetworkCheckResult result = await service.CheckAsync(
			"lcu-host",
			new FakeInformationGateway(_ =>
				Task.FromResult<IReadOnlyList<LcuVersionEntry>?>([])));

		Assert.Equal(LcuNetworkCheckOutcome.VersionUnavailable, result.Outcome);
		Assert.Null(result.Version);
	}

	[Fact]
	public async Task CheckAsync_ReturnsVersionWhenReachable()
	{
		LcuNetworkCheckService service = new(new FakeReachabilityService(
			_ => Task.FromResult(true)));

		LcuNetworkCheckResult result = await service.CheckAsync(
			"lcu-host",
			new FakeInformationGateway(_ =>
				Task.FromResult<IReadOnlyList<LcuVersionEntry>?>([
					new("Fuji LCU Communication Server Service", "1.2.3")
				])));

		Assert.Equal(LcuNetworkCheckOutcome.Available, result.Outcome);
		Assert.Equal("1.2.3", result.Version);
	}

	[Fact]
	public async Task CheckAsync_StopsBeforeVersionRequestWhenCancelledDuringPing()
	{
		using CancellationTokenSource cancellation = new();
		int versionRequestCount = 0;
		LcuNetworkCheckService service = new(new FakeReachabilityService(
			_ =>
			{
				cancellation.Cancel();
				return Task.FromResult(true);
			}));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.CheckAsync(
				"lcu-host",
				new FakeInformationGateway(_ =>
				{
					versionRequestCount++;
					return Task.FromResult<IReadOnlyList<LcuVersionEntry>?>([]);
				}),
				cancellationToken: cancellation.Token));

		Assert.Equal(0, versionRequestCount);
	}

	[Fact]
	public async Task CheckAsync_PropagatesCancellationFromVersionRequest()
	{
		using CancellationTokenSource cancellation = new();
		CancellationToken gatewayToken = default;
		LcuNetworkCheckService service = new(new FakeReachabilityService(
			_ => Task.FromResult(true)));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.CheckAsync(
				"lcu-host",
				new FakeInformationGateway(token =>
				{
					gatewayToken = token;
					cancellation.Cancel();
					return Task.FromResult<IReadOnlyList<LcuVersionEntry>?>([]);
				}),
				cancellationToken: cancellation.Token));

		Assert.Equal(cancellation.Token, gatewayToken);
	}

	private sealed class FakeReachabilityService(
		Func<CancellationToken, Task<bool>> isReachableAsync) : ILcuReachabilityService
	{
		public Task<bool> IsReachableAsync(
			string host,
			int attempts,
			Action<string>? onPingError = null,
			CancellationToken cancellationToken = default) =>
			isReachableAsync(cancellationToken);
	}

	private sealed class FakeInformationGateway(
		Func<CancellationToken, Task<IReadOnlyList<LcuVersionEntry>?>> getVersionAsync) : ILcuInformationGateway
	{
		public Task<LcuFtpAccount?> GetFtpAccountAsync(CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public Task<IReadOnlyList<LcuVersionEntry>?> GetVersionAsync(
			CancellationToken cancellationToken) => getVersionAsync(cancellationToken);

		public Task<IReadOnlyList<LcuDiskDrive>?> GetDiskInfoAsync(CancellationToken cancellationToken) =>
			throw new NotSupportedException();
	}
}
