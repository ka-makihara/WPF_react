using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuInformationServiceTests
{
	[Fact]
	public async Task GetFtpAccountAsync_NormalizesDomainUsername()
	{
		var service = new LcuInformationService(new FakeGateway
		{
			FtpAccount = new("domain\\operator", "secret")
		});

		LcuFtpAccountResult result = await service.GetFtpAccountAsync(default);

		Assert.Equal(LcuFtpAccountStatus.Available, result.Status);
		Assert.Equal("operator", result.Username);
		Assert.Equal("secret", result.Password);
	}

	[Fact]
	public async Task GetFtpAccountAsync_ReportsMissingResponseAndCredentials()
	{
		var noResponseService = new LcuInformationService(new FakeGateway());
		var missingCredentialsService = new LcuInformationService(new FakeGateway
		{
			FtpAccount = new("operator", null)
		});

		LcuFtpAccountResult noResponse =
			await noResponseService.GetFtpAccountAsync(default);
		LcuFtpAccountResult missingCredentials =
			await missingCredentialsService.GetFtpAccountAsync(default);

		Assert.Equal(LcuFtpAccountStatus.ResponseError, noResponse.Status);
		Assert.Equal(LcuFtpAccountStatus.CredentialsError, missingCredentials.Status);
	}

	[Fact]
	public async Task GetFtpAccountAsync_ThrowsWhenGatewayReturnsNullAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		var service = new LcuInformationService(new FakeGateway
		{
			OnGetFtpAccount = () => cancellation.Cancel()
		});

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.GetFtpAccountAsync(cancellation.Token));
	}

	[Fact]
	public async Task GetVersionAsync_ReturnsTheCommunicationServerVersion()
	{
		var service = new LcuInformationService(new FakeGateway
		{
			Versions =
			[
				new("Other Service", "1.0"),
				new("Fuji LCU Communication Server Service", "2.3")
			]
		});

		LcuVersionResult result = await service.GetVersionAsync(default);

		Assert.True(result.IsAvailable);
		Assert.Equal("2.3", result.Version);
	}

	[Fact]
	public async Task GetVersionAsync_ThrowsWhenGatewayReturnsNullAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		var service = new LcuInformationService(new FakeGateway
		{
			OnGetVersion = () => cancellation.Cancel()
		});

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.GetVersionAsync(cancellation.Token));
	}

	[Theory]
	[MemberData(nameof(InvalidVersionResponses))]
	public async Task GetVersionAsync_ReturnsUnavailableWhenRequiredVersionIsMissing(
		IReadOnlyList<LcuVersionEntry>? versions)
	{
		var service = new LcuInformationService(new FakeGateway
		{
			Versions = versions
		});

		LcuVersionResult result = await service.GetVersionAsync(default);

		Assert.False(result.IsAvailable);
		Assert.Null(result.Version);
	}

	[Fact]
	public async Task GetDiskSpaceAsync_ParsesDDriveFreeSpace()
	{
		var service = new LcuInformationService(new FakeGateway
		{
			Drives =
			[
				new("C", "100"),
				new("D", "2048")
			]
		});

		LcuDiskSpaceResult result = await service.GetDiskSpaceAsync(default);

		Assert.True(result.IsAvailable);
		Assert.Equal(2048, result.FreeSpace);
	}

	[Fact]
	public async Task GetDiskSpaceAsync_ThrowsWhenGatewayReturnsNullAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		var service = new LcuInformationService(new FakeGateway
		{
			OnGetDiskInfo = () => cancellation.Cancel()
		});

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.GetDiskSpaceAsync(cancellation.Token));
	}

	[Theory]
	[MemberData(nameof(InvalidDiskResponses))]
	public async Task GetDiskSpaceAsync_ReturnsUnavailableForMissingOrInvalidDDrive(
		IReadOnlyList<LcuDiskDrive>? drives)
	{
		var service = new LcuInformationService(new FakeGateway
		{
			Drives = drives
		});

		LcuDiskSpaceResult result = await service.GetDiskSpaceAsync(default);

		Assert.False(result.IsAvailable);
		Assert.Equal(0, result.FreeSpace);
	}

	public static TheoryData<IReadOnlyList<LcuVersionEntry>?> InvalidVersionResponses
	{
		get
		{
			TheoryData<IReadOnlyList<LcuVersionEntry>?> data = new();
			data.Add(null);
			data.Add(Array.Empty<LcuVersionEntry>());
			data.Add(new[] { new LcuVersionEntry("Other Service", "1.0") });
			data.Add(new[] { new LcuVersionEntry("Fuji LCU Communication Server Service", null) });
			return data;
		}
	}

	public static TheoryData<IReadOnlyList<LcuDiskDrive>?> InvalidDiskResponses
	{
		get
		{
			TheoryData<IReadOnlyList<LcuDiskDrive>?> data = new();
			data.Add(null);
			data.Add(Array.Empty<LcuDiskDrive>());
			data.Add(new[] { new LcuDiskDrive("C", "100") });
			data.Add(new[] { new LcuDiskDrive("D", null) });
			data.Add(new[] { new LcuDiskDrive("D", "not-a-number") });
			return data;
		}
	}

	private sealed class FakeGateway : ILcuInformationGateway
	{
		public LcuFtpAccount? FtpAccount { get; init; }
		public IReadOnlyList<LcuVersionEntry>? Versions { get; init; }
		public IReadOnlyList<LcuDiskDrive>? Drives { get; init; }
		public Action? OnGetFtpAccount { get; init; }
		public Action? OnGetVersion { get; init; }
		public Action? OnGetDiskInfo { get; init; }

		public Task<LcuFtpAccount?> GetFtpAccountAsync(CancellationToken cancellationToken)
		{
			OnGetFtpAccount?.Invoke();
			return Task.FromResult(FtpAccount);
		}

		public Task<IReadOnlyList<LcuVersionEntry>?> GetVersionAsync(
			CancellationToken cancellationToken)
		{
			OnGetVersion?.Invoke();
			return Task.FromResult(Versions);
		}

		public Task<IReadOnlyList<LcuDiskDrive>?> GetDiskInfoAsync(
			CancellationToken cancellationToken)
		{
			OnGetDiskInfo?.Invoke();
			return Task.FromResult(Drives);
		}
	}
}
