namespace WpfApp1_cmd.Services;

public sealed record PostMcFileTransferRequest(
	string MachineName,
	int ModulePosition,
	string User,
	string Password,
	IReadOnlyList<string> MachineFiles,
	IReadOnlyList<string> LcuFiles);

public enum PostMcFileTransferOutcome
{
	Succeeded,
	CredentialsMissing,
	ConnectionFailed,
	ResponseErrorDataMissing,
	TransferFailed
}

public sealed record PostMcFileTransferResult(
	PostMcFileTransferOutcome Outcome,
	string? ErrorCode = null,
	string? ErrorMessage = null)
{
	public bool IsSuccess => Outcome == PostMcFileTransferOutcome.Succeeded;
}

public interface IPostMcFileTransferGateway
{
	Task<string> SendAsync(
		PostMcFileTransferRequest request,
		CancellationToken cancellationToken);
}

public sealed class PostMcFileTransferService
{
	public async Task<PostMcFileTransferResult> SendAsync(
		PostMcFileTransferRequest request,
		IPostMcFileTransferGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentNullException.ThrowIfNull(request.MachineFiles);
		ArgumentNullException.ThrowIfNull(request.LcuFiles);

		if (string.IsNullOrEmpty(request.User) || string.IsNullOrEmpty(request.Password))
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.CredentialsMissing);
		}

		cancellationToken.ThrowIfCancellationRequested();
		string response = await gateway.SendAsync(request, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (response.Length == 0 || response == "Internal Server Error")
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.ConnectionFailed);
		}

		PostMcFileResponse parsedResponse = PostMcFileResponseParser.Parse(response);
		if (!parsedResponse.HasError)
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.Succeeded);
		}

		if (!parsedResponse.HasErrorData)
		{
			return new PostMcFileTransferResult(PostMcFileTransferOutcome.ResponseErrorDataMissing);
		}

		return new PostMcFileTransferResult(
			PostMcFileTransferOutcome.TransferFailed,
			parsedResponse.ErrorCode,
			parsedResponse.ErrorMessage);
	}
}
