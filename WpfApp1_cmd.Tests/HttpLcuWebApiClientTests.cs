using System.Net;
using System.Net.Http;
using System.Text;
using WpfLcuCtrlLib;
using Xunit;

namespace WpfApp1_cmd.Tests;

public class HttpLcuWebApiClientTests
{
	[Fact]
	public async Task SendCommandAsync_PostsJsonToLcuCommandEndpoint()
	{
		HttpRequestMessage? capturedRequest = null;
		string? capturedPayload = null;
		using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, _) =>
		{
			capturedRequest = request;
			capturedPayload = await request.Content!.ReadAsStringAsync();
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"result\":\"ok\"}")
			};
		}));
		var api = new HttpLcuWebApiClient("lcu.example:9000", httpClient);

		string response = await api.SendCommandAsync("{\"cmd\":\"test\"}", CancellationToken.None);

		Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
		Assert.Equal("http://lcu.example:9000/LCUWeb/api/lcuCommand", capturedRequest.RequestUri!.ToString());
		Assert.Equal("{\"cmd\":\"test\"}", capturedPayload);
		Assert.Equal("{\"result\":\"ok\"}", response);
		capturedRequest.Dispose();
	}

	[Fact]
	public async Task GetAsync_ReturnsResponseBodyForAnErrorStatus()
	{
		using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
			{
				Content = new StringContent("LCU error")
			})));
		var api = new HttpLcuWebApiClient("lcu.example", httpClient);

		string response = await api.GetAsync("deviceInfo", CancellationToken.None);

		Assert.Equal("LCU error", response);
	}

	[Fact]
	public async Task SendCommandAsync_LogsRequestAndResponseWithoutLoggingPayload()
	{
		List<string> logEntries = [];
		using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"result\":\"ok\"}")
			})));
		var api = new HttpLcuWebApiClient("lcu.example:9000", httpClient, logEntries.Add);
		const string payload = """{"cmd":"private-command-value"}""";

		await api.SendCommandAsync(payload, CancellationToken.None);

		Assert.Equal(2, logEntries.Count);
		Assert.Contains("HTTP POST http://lcu.example:9000/LCUWeb/api/lcuCommand started", logEntries[0]);
		Assert.Contains("payload:", logEntries[0]);
		Assert.Contains("completed: 200 OK", logEntries[1]);
		Assert.DoesNotContain("private-command-value", string.Join(Environment.NewLine, logEntries));
	}

	[Fact]
	public async Task SendCommandAsync_PropagatesCancellation()
	{
		using var httpClient = new HttpClient(new StubHttpMessageHandler(async (_, cancellationToken) =>
		{
			await Task.Delay(Timeout.Infinite, cancellationToken);
			return new HttpResponseMessage(HttpStatusCode.OK);
		}));
		var api = new HttpLcuWebApiClient("lcu.example", httpClient);
		using var cancellation = new CancellationTokenSource();

		Task<string> request = api.SendCommandAsync("{}", cancellation.Token);
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
	}

	private sealed class StubHttpMessageHandler(
		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
		: HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) =>
			send(request, cancellationToken);
	}
}
