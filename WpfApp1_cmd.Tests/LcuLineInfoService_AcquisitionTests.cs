using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuLineInfoService_AcquisitionTests
{
	[Fact]
	public async Task GetLineInfoAsync_FetchesAndParsesLineInfo()
	{
		int fetchCount = 0;
		var service = new LcuLineInfoService();
		var gateway = new FakeGateway(() =>
		{
			fetchCount++;
			return Task.FromResult("<lines><machine>MachineA</machine></lines>");
		});
		using CancellationTokenSource cancellation = new();

		LcuLineInfoResponseResult<TestLineInfo> result =
			await service.GetLineInfoAsync<TestLineInfo>(
				gateway,
				data => data?.Machines != null,
				cancellation.Token);

		Assert.Equal(1, fetchCount);
		Assert.Equal(LcuLineInfoResponseOutcome.Parsed, result.Outcome);
		Assert.Equal("MachineA", Assert.Single(result.Data!.Machines!));
	}

	[Fact]
	public async Task GetLineInfoAsync_PropagatesGatewayFailures()
	{
		var service = new LcuLineInfoService();
		var gateway = new FakeGateway(() => throw new HttpRequestException("request failed"));

		await Assert.ThrowsAsync<HttpRequestException>(
			() => service.GetLineInfoAsync<TestLineInfo>(
				gateway,
				data => data?.Machines != null,
				default));
	}

	[Fact]
	public async Task GetLineInfoAsync_PassesCancellationTokenToGateway()
	{
		CancellationToken receivedToken = default;
		using CancellationTokenSource cancellation = new();
		var service = new LcuLineInfoService();
		var gateway = new FakeGateway(token =>
		{
			receivedToken = token;
			return Task.FromResult("<lines><machine>MachineA</machine></lines>");
		});

		await service.GetLineInfoAsync<TestLineInfo>(
			gateway,
			data => data?.Machines != null,
			cancellation.Token);

		Assert.Equal(cancellation.Token, receivedToken);
	}

	[Fact]
	public async Task GetLineInfoAsync_ThrowsWhenGatewayReturnsAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		int validationCount = 0;
		var service = new LcuLineInfoService();
		var gateway = new FakeGateway(() =>
		{
			cancellation.Cancel();
			return Task.FromResult("<lines><machine>MachineA</machine></lines>");
		});

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.GetLineInfoAsync<TestLineInfo>(
				gateway,
				_ =>
				{
					validationCount++;
					return true;
				},
				cancellation.Token));

		Assert.Equal(0, validationCount);
	}

	[Fact]
	public async Task GetLineInfoAsync_ThrowsWhenCancelledDuringResponseValidation()
	{
		using CancellationTokenSource cancellation = new();
		var service = new LcuLineInfoService();
		var gateway = new FakeGateway(() => Task.FromResult("<lines><machine>MachineA</machine></lines>"));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.GetLineInfoAsync<TestLineInfo>(
				gateway,
				data =>
				{
					cancellation.Cancel();
					return data?.Machines != null;
				},
				cancellation.Token));
	}

	private sealed class FakeGateway(Func<CancellationToken, Task<string>> getLinesAsync) : ILcuLineInfoGateway
	{
		public FakeGateway(Func<Task<string>> getLinesAsync)
			: this(_ => getLinesAsync())
		{
		}

		public Task<string> GetLinesAsync(CancellationToken cancellationToken) =>
			getLinesAsync(cancellationToken);
	}

	[System.Xml.Serialization.XmlRoot("lines")]
	public sealed class TestLineInfo
	{
		[System.Xml.Serialization.XmlElement("machine")]
		public List<string>? Machines { get; set; }
	}
}
