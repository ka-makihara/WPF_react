using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class PostMcFileTransferResultPresenterTests
{
	[Fact]
	public void Create_ReportsSuccessWithoutErrorsOrLogs()
	{
		PostMcFileTransferPresentation presentation = Create(
			PostMcFileTransferOutcome.Succeeded);

		Assert.True(presentation.Succeeded);
		Assert.Null(presentation.ErrorCode);
		Assert.False(presentation.CountTransferError);
		Assert.Empty(presentation.LogMessages);
	}

	[Theory]
	[InlineData(
		PostMcFileTransferOutcome.CredentialsMissing,
		ErrorCode.FTP_ACCOUNT_ERROR,
		false,
		"[ERROR] LcuA;MachineA;ModuleA=FTP account error.")]
	[InlineData(
		PostMcFileTransferOutcome.ConnectionFailed,
		ErrorCode.LCU_CONNECT_ERROR,
		false,
		"[ERROR] LcuA;MachineA;ModuleA=WebAPI(PostMcFile) Error")]
	[InlineData(
		PostMcFileTransferOutcome.ResponseErrorDataMissing,
		ErrorCode.FTP_TRANSFER_ERROR,
		true,
		"[ERROR] LcuA;MachineA;ModuleA=WebAPI(PostMcFile) Error:No Error Data")]
	public void Create_MapsFailuresToErrorStateAndMessages(
		PostMcFileTransferOutcome outcome,
		ErrorCode expectedErrorCode,
		bool expectedCountTransferError,
		string expectedLogMessage)
	{
		PostMcFileTransferPresentation presentation = Create(outcome);

		Assert.False(presentation.Succeeded);
		Assert.Equal(expectedErrorCode, presentation.ErrorCode);
		Assert.Equal(expectedCountTransferError, presentation.CountTransferError);
		Assert.Equal([expectedLogMessage], presentation.LogMessages);
	}

	[Fact]
	public void Create_ReportsTransferFailureDetailsInOriginalOrder()
	{
		PostMcFileTransferPresentation presentation = Create(
			PostMcFileTransferOutcome.TransferFailed,
			errorCode: "E42",
			errorMessage: "failed");

		Assert.Equal(ErrorCode.FTP_TRANSFER_ERROR, presentation.ErrorCode);
		Assert.True(presentation.CountTransferError);
		Assert.Equal(
			[
				"[ERROR] failed errCode=E42",
				"[Transfer] LcuA;MachineA;ModuleA;GroupA=NG:failed"
			],
			presentation.LogMessages);
	}

	private static PostMcFileTransferPresentation Create(
		PostMcFileTransferOutcome outcome,
		string? errorCode = null,
		string? errorMessage = null)
	{
		return new PostMcFileTransferResultPresenter().Create(
			new PostMcFileTransferResult(outcome, errorCode, errorMessage),
			"LcuA",
			"MachineA",
			"ModuleA",
			"GroupA",
			"FTP account error.");
	}
}
