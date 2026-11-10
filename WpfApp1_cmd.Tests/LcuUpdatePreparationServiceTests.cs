using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuUpdatePreparationServiceTests
{
	[Fact]
	public async Task PrepareAsync_StopsBeforeLoadingInformationWhenUnreachable()
	{
		var gateway = new FakeGateway();
		var service = CreateService(gateway, false);

		LcuUpdatePreparationResult result =
			await service.PrepareAsync("lcu-host", false, 1024, default);

		Assert.Equal(LcuUpdatePreparationOutcome.ConnectionError, result.Outcome);
		Assert.Empty(gateway.Calls);
	}

	[Fact]
	public async Task PrepareAsync_ThrowsWhenReachabilityReturnsFalseAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeGateway();
		var service = new LcuUpdatePreparationService(
			new FakeReachabilityService(
				false,
				() => cancellation.Cancel()),
			new LcuInformationService(gateway));

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.PrepareAsync(
				"lcu-host",
				false,
				1024,
				cancellation.Token));

		Assert.Empty(gateway.Calls);
	}

	[Fact]
	public async Task PrepareAsync_LoadsAccountThenVersionThenDiskSpace()
	{
		var gateway = new FakeGateway
		{
			FtpAccount = new("operator", "secret"),
			Versions = [new("Fuji LCU Communication Server Service", "2.3")],
			Drives = [new("D", "2048")]
		};
		var service = CreateService(gateway, true);
		bool diskCheckStarted = false;

		LcuUpdatePreparationResult result = await service.PrepareAsync(
			"lcu-host",
			false,
			1024,
			default,
			onDiskSpaceCheckStarted: () => diskCheckStarted = true);

		Assert.Equal(LcuUpdatePreparationOutcome.Ready, result.Outcome);
		Assert.Equal(["ftp", "version", "disk"], gateway.Calls);
		Assert.True(diskCheckStarted);
		Assert.Equal("operator", result.FtpAccount?.Username);
		Assert.Equal("2.3", result.Version?.Version);
		Assert.Equal(2048, result.DiskSpace?.FreeSpace);
	}

	[Fact]
	public async Task PrepareAsync_SkipsAccountVersionAndDiskWhenAccountIsCached()
	{
		var gateway = new FakeGateway();
		var service = CreateService(gateway, true);

		LcuUpdatePreparationResult result =
			await service.PrepareAsync("lcu-host", true, 1024, default);

		Assert.Equal(LcuUpdatePreparationOutcome.Ready, result.Outcome);
		Assert.Empty(gateway.Calls);
		Assert.Null(result.FtpAccount);
		Assert.Null(result.Version);
		Assert.Null(result.DiskSpace);
	}

	[Fact]
	public async Task PrepareAsync_StopsAfterAccountFailure()
	{
		var gateway = new FakeGateway();
		var service = CreateService(gateway, true);

		LcuUpdatePreparationResult result =
			await service.PrepareAsync("lcu-host", false, 1024, default);

		Assert.Equal(LcuUpdatePreparationOutcome.FtpResponseError, result.Outcome);
		Assert.Equal(["ftp"], gateway.Calls);
	}

	[Fact]
	public async Task PrepareAsync_ReportsDiskInfoFailureSeparatelyFromInsufficientSpace()
	{
		var gateway = new FakeGateway
		{
			FtpAccount = new("operator", "secret"),
			Versions = [new("Fuji LCU Communication Server Service", "2.3")]
		};
		var service = CreateService(gateway, true);

		LcuUpdatePreparationResult result =
			await service.PrepareAsync("lcu-host", false, 1024, default);

		Assert.Equal(LcuUpdatePreparationOutcome.DiskInfoError, result.Outcome);
		Assert.False(result.DiskSpace!.IsAvailable);
		Assert.False(result.IsDiskSpaceInsufficient);
		Assert.Equal(["ftp", "version", "disk"], gateway.Calls);
	}

	[Fact]
	public async Task PrepareAsync_PassesCancellationTokenToDiskInfoRequest()
	{
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeGateway
		{
			FtpAccount = new("operator", "secret"),
			Versions = [new("Fuji LCU Communication Server Service", "2.3")],
			Drives = [new("D", "2048")]
		};
		var service = CreateService(gateway, true);

		await service.PrepareAsync(
			"lcu-host",
			false,
			1024,
			cancellation.Token);

		Assert.Equal(cancellation.Token, gateway.DiskInfoCancellationToken);
	}

	[Fact]
	public async Task PrepareAsync_PassesCancellationTokenToReachabilityCheck()
	{
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeGateway();
		var reachability = new FakeReachabilityService(true);
		var service = new LcuUpdatePreparationService(
			reachability,
			new LcuInformationService(gateway));

		await service.PrepareAsync(
			"lcu-host",
			false,
			1024,
			cancellation.Token);

		Assert.Equal(cancellation.Token, reachability.CancellationToken);
	}

	[Theory]
	[InlineData(1023, 1024, true)]
	[InlineData(1024, 1024, false)]
	[InlineData(1025, 1024, false)]
	public async Task PrepareAsync_ComparesFreeSpaceWithRequiredSize(
		long freeSpace,
		long requiredSpace,
		bool expectedInsufficient)
	{
		var gateway = new FakeGateway
		{
			FtpAccount = new("operator", "secret"),
			Versions = [new("Fuji LCU Communication Server Service", "2.3")],
			Drives = [new("D", freeSpace.ToString())]
		};
		var service = CreateService(gateway, true);

		LcuUpdatePreparationResult result =
			await service.PrepareAsync("lcu-host", false, requiredSpace, default);

		Assert.Equal(expectedInsufficient, result.IsDiskSpaceInsufficient);
	}

	[Fact]
	public async Task PrepareAsync_RejectsNegativeRequiredSpace()
	{
		var service = CreateService(new FakeGateway(), true);

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => service.PrepareAsync("lcu-host", false, -1, default));
	}

	private static LcuUpdatePreparationService CreateService(
		FakeGateway gateway,
		bool isReachable)
	{
		return new LcuUpdatePreparationService(
			new FakeReachabilityService(isReachable),
			new LcuInformationService(gateway));
	}

	private sealed class FakeReachabilityService(
		bool isReachable,
		Action? onCheck = null) : ILcuReachabilityService
	{
		public CancellationToken CancellationToken { get; private set; }

		public Task<bool> IsReachableAsync(
			string host,
			int attempts,
			Action<string>? onPingError = null,
			CancellationToken cancellationToken = default)
		{
			CancellationToken = cancellationToken;
			onCheck?.Invoke();
			return Task.FromResult(isReachable);
		}
	}

	private sealed class FakeGateway : ILcuInformationGateway
	{
		public List<string> Calls { get; } = [];
		public LcuFtpAccount? FtpAccount { get; init; }
		public IReadOnlyList<LcuVersionEntry>? Versions { get; init; }
		public IReadOnlyList<LcuDiskDrive>? Drives { get; init; }
		public CancellationToken DiskInfoCancellationToken { get; private set; }

		public Task<LcuFtpAccount?> GetFtpAccountAsync(CancellationToken cancellationToken)
		{
			Calls.Add("ftp");
			return Task.FromResult(FtpAccount);
		}

		public Task<IReadOnlyList<LcuVersionEntry>?> GetVersionAsync(
			CancellationToken cancellationToken)
		{
			Calls.Add("version");
			return Task.FromResult(Versions);
		}

		public Task<IReadOnlyList<LcuDiskDrive>?> GetDiskInfoAsync(
			CancellationToken cancellationToken)
		{
			Calls.Add("disk");
			DiskInfoCancellationToken = cancellationToken;
			return Task.FromResult(Drives);
		}
	}
}
