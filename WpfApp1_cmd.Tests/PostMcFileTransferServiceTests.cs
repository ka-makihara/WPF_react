using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class PostMcFileTransferServiceTests
{
	[Fact]
	public async Task SendAsync_SendsRequestAndReturnsSuccess()
	{
		var gateway = new FakeGateway(
			"""{"ftp":{"data":[{"errorCode":"","errorMessage":""}]}}""");
		var request = CreateRequest();

		PostMcFileTransferResult result = await new PostMcFileTransferService().SendAsync(
			request,
			gateway,
			CancellationToken.None);

		Assert.Equal(PostMcFileTransferOutcome.Succeeded, result.Outcome);
		Assert.Same(request, gateway.Request);
	}

	[Fact]
	public async Task SendAsync_DoesNotCallGatewayWhenCredentialsAreMissing()
	{
		var gateway = new FakeGateway("");
		PostMcFileTransferRequest request = CreateRequest() with { Password = "" };

		PostMcFileTransferResult result = await new PostMcFileTransferService().SendAsync(
			request,
			gateway,
			CancellationToken.None);

		Assert.Equal(PostMcFileTransferOutcome.CredentialsMissing, result.Outcome);
		Assert.Null(gateway.Request);
	}

	[Fact]
	public async Task SendAsync_ThrowsWhenGatewayReturnsAnErrorResponseAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeGateway(
			"Internal Server Error",
			() => cancellation.Cancel());

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new PostMcFileTransferService().SendAsync(
				CreateRequest(),
				gateway,
				cancellation.Token));
	}

	[Fact]
	public async Task SendAsync_DoesNotCallGatewayWhenAlreadyCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		var gateway = new FakeGateway("");

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new PostMcFileTransferService().SendAsync(
				CreateRequest(),
				gateway,
				cancellation.Token));

		Assert.Null(gateway.Request);
	}

	[Theory]
	[InlineData("", PostMcFileTransferOutcome.ConnectionFailed)]
	[InlineData("Internal Server Error", PostMcFileTransferOutcome.ConnectionFailed)]
	[InlineData("""{"ftp":{"data":null}}""", PostMcFileTransferOutcome.ResponseErrorDataMissing)]
	[InlineData("""{"ftp":{"data":[{"errorCode":"E42","errorMessage":"failed"}]}}""", PostMcFileTransferOutcome.TransferFailed)]
	public async Task SendAsync_MapsFailureResponses(string response, PostMcFileTransferOutcome expected)
	{
		var gateway = new FakeGateway(response);

		PostMcFileTransferResult result = await new PostMcFileTransferService().SendAsync(
			CreateRequest(),
			gateway,
			CancellationToken.None);

		Assert.Equal(expected, result.Outcome);
	}

	private static PostMcFileTransferRequest CreateRequest() =>
		new("MachineA", 2, "user", "password", [@"\Peripheral\file.bin"], ["/MCFiles/file.bin"]);

	private sealed class FakeGateway(string response, Action? onSend = null) : IPostMcFileTransferGateway
	{
		public PostMcFileTransferRequest? Request { get; private set; }

		public Task<string> SendAsync(
			PostMcFileTransferRequest request,
			CancellationToken cancellationToken)
		{
			Request = request;
			onSend?.Invoke();
			return Task.FromResult(response);
		}
	}
}
