using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuUpdatePreparationService_PresentationTests
{
	[Theory]
	[InlineData(
		LcuUpdatePreparationOutcome.ConnectionError,
		LcuUpdatePreparationActionType.Log,
		"[ERROR] LineA=Access Fail")]
	[InlineData(
		LcuUpdatePreparationOutcome.FtpResponseError,
		LcuUpdatePreparationActionType.SetProgressMessage,
		"LCU:HostA=FTP information Fail")]
	[InlineData(
		LcuUpdatePreparationOutcome.FtpCredentialsError,
		LcuUpdatePreparationActionType.SetProgressMessage,
		"LCU:HostA=FTP user, password Fail")]
	public void Create_MapsEarlyFailuresToActions(
		LcuUpdatePreparationOutcome outcome,
		LcuUpdatePreparationActionType firstActionType,
		string firstMessage)
	{
		LcuUpdatePreparationPresentation presentation = LcuUpdatePreparationService.CreatePresentation(
			new LcuUpdatePreparationResult(outcome),
			"LineA",
			"HostA",
			100);

		Assert.False(presentation.CanContinue);
		Assert.Equal(firstActionType, presentation.Actions[0].Type);
		Assert.Equal(firstMessage, presentation.Actions[0].Message);
		Assert.Contains(
			presentation.Actions,
			action => action.Type == LcuUpdatePreparationActionType.SetLcuErrorCode);
	}

	[Fact]
	public void Create_VersionFailureAppliesAccountBeforeLoggingAndStopping()
	{
		LcuUpdatePreparationPresentation presentation = LcuUpdatePreparationService.CreatePresentation(
			new LcuUpdatePreparationResult(
				LcuUpdatePreparationOutcome.VersionError,
				new LcuFtpAccountResult(LcuFtpAccountStatus.Available, "user", "password")),
			"LineA",
			"HostA",
			100);

		Assert.False(presentation.CanContinue);
		Assert.Equal(
			[
				LcuUpdatePreparationActionType.ApplyFtpAccount,
				LcuUpdatePreparationActionType.Log,
				LcuUpdatePreparationActionType.Log,
				LcuUpdatePreparationActionType.SetProgressMessage,
				LcuUpdatePreparationActionType.SetLcuErrorCode
			],
			presentation.Actions.Select(action => action.Type));
		Assert.Equal(
			[
				"[ERROR] LineA=LcuVersion Get Error",
				"[LineInfo] LineA;LcuVersion Get Error"
			],
			presentation.Actions
				.Where(action => action.Type == LcuUpdatePreparationActionType.Log)
				.Select(action => action.Message));
	}

	[Fact]
	public void Create_DiskInfoFailureAppliesVersionBeforeSettingFailureState()
	{
		LcuUpdatePreparationPresentation presentation = LcuUpdatePreparationService.CreatePresentation(
			new LcuUpdatePreparationResult(
				LcuUpdatePreparationOutcome.DiskInfoError,
				new LcuFtpAccountResult(LcuFtpAccountStatus.Available, "user", "password"),
				new LcuVersionResult(true, "2.3"),
				new LcuDiskSpaceResult(false, 0)),
			"LineA",
			"HostA",
			100);

		Assert.False(presentation.CanContinue);
		Assert.Equal(
			[
				LcuUpdatePreparationActionType.ApplyFtpAccount,
				LcuUpdatePreparationActionType.ApplyVersion,
				LcuUpdatePreparationActionType.SetProgressMessage,
				LcuUpdatePreparationActionType.SetGlobalErrorCode,
				LcuUpdatePreparationActionType.SetLcuErrorCode,
				LcuUpdatePreparationActionType.MarkDiskInfoError,
				LcuUpdatePreparationActionType.Log
			],
			presentation.Actions.Select(action => action.Type));
	}

	[Fact]
	public void Create_ReadyWithCachedAccountDoesNotApplyUnavailableInformation()
	{
		LcuUpdatePreparationPresentation presentation = LcuUpdatePreparationService.CreatePresentation(
			new LcuUpdatePreparationResult(LcuUpdatePreparationOutcome.Ready),
			"LineA",
			"HostA",
			100);

		Assert.True(presentation.CanContinue);
		Assert.Empty(presentation.Actions);
	}

	[Fact]
	public void Create_InsufficientDiskSpaceKeepsContinuationAndActionOrder()
	{
		LcuUpdatePreparationPresentation presentation = LcuUpdatePreparationService.CreatePresentation(
			new LcuUpdatePreparationResult(
				LcuUpdatePreparationOutcome.Ready,
				new LcuFtpAccountResult(LcuFtpAccountStatus.Available, "user", "password"),
				new LcuVersionResult(true, "2.3"),
				new LcuDiskSpaceResult(true, 10),
				IsDiskSpaceInsufficient: true),
			"LineA",
			"HostA",
			20);

		Assert.True(presentation.CanContinue);
		Assert.Equal(
			[
				LcuUpdatePreparationActionType.ApplyFtpAccount,
				LcuUpdatePreparationActionType.ApplyVersion,
				LcuUpdatePreparationActionType.SetProgressMessage,
				LcuUpdatePreparationActionType.ApplyDiskSpace,
				LcuUpdatePreparationActionType.SetLcuErrorCode,
				LcuUpdatePreparationActionType.MarkDiskSpaceInsufficient,
				LcuUpdatePreparationActionType.Log,
				LcuUpdatePreparationActionType.Log,
				LcuUpdatePreparationActionType.SetProgressMessage,
				LcuUpdatePreparationActionType.SetDiskSpaceErrorTooltip
			],
			presentation.Actions.Select(action => action.Type));
		Assert.Equal(
			[
				"[ERROR] LineA=DiskSpace Error(10<20)",
				"[LineInfo] LineA;DiskSpace Error(10<20)"
			],
			presentation.Actions
				.Where(action => action.Type == LcuUpdatePreparationActionType.Log)
				.Select(action => action.Message));
	}

	[Fact]
	public void Create_RejectsMissingRequiredResultData()
	{
		Assert.Throws<InvalidOperationException>(() => LcuUpdatePreparationService.CreatePresentation(
			new LcuUpdatePreparationResult(LcuUpdatePreparationOutcome.DiskInfoError),
			"LineA",
			"HostA",
			100));
	}
}
