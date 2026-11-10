using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuFileUploadResultPresenterTests
{
	[Fact]
	public void Create_ReturnsCompletionMessageForSuccess()
	{
		LcuFileUploadPresentation result = new LcuFileUploadResultPresenter().Create(
			new LcuUnitFileUploadResult(LcuUnitFileUploadOutcome.Succeeded, []),
			"LineA");

		Assert.True(result.Succeeded);
		Assert.False(result.Cancelled);
		Assert.Equal(["[Transfer] LineA=UploadFiles End"], result.LogMessages);
	}

	[Fact]
	public void Create_ReturnsCancellationMessageWithoutFailure()
	{
		LcuFileUploadPresentation result = new LcuFileUploadResultPresenter().Create(
			new LcuUnitFileUploadResult(LcuUnitFileUploadOutcome.Cancelled, []),
			"LineA");

		Assert.False(result.Succeeded);
		Assert.True(result.Cancelled);
		Assert.Equal(["[Transfer] LineA=UploadFiles Cancelled"], result.LogMessages);
	}

	[Fact]
	public void Create_ReportsExceptionAndGenericFailureInOrder()
	{
		LcuFileUploadPresentation result = new LcuFileUploadResultPresenter().Create(
			new LcuUnitFileUploadResult(
				LcuUnitFileUploadOutcome.Failed,
				[],
				new IOException("connection failed")),
			"LineA");

		Assert.False(result.Succeeded);
		Assert.False(result.Cancelled);
		Assert.Equal(
			[
				"[Transfer] LineA=UploadFilesWithFolder Error(connection failed)",
				"[Transfer] LineA=UploadFilesWithFolder Error"
			],
			result.LogMessages);
	}

	[Fact]
	public void Create_ReportsGenericFailureWhenNoExceptionIsAvailable()
	{
		LcuFileUploadPresentation result = new LcuFileUploadResultPresenter().Create(
			new LcuUnitFileUploadResult(LcuUnitFileUploadOutcome.Failed, []),
			"LineA");

		Assert.Equal(
			["[Transfer] LineA=UploadFilesWithFolder Error"],
			result.LogMessages);
	}

	[Fact]
	public void Create_CanOmitOutcomeMessagesForLegacyCaller()
	{
		LcuFileUploadResultPresenter presenter = new();

		Assert.Empty(presenter.Create(
			new LcuUnitFileUploadResult(LcuUnitFileUploadOutcome.Succeeded, []),
			"LineA",
			includeOutcomeMessages: false).LogMessages);
		Assert.Empty(presenter.Create(
			new LcuUnitFileUploadResult(LcuUnitFileUploadOutcome.Cancelled, []),
			"LineA",
			includeOutcomeMessages: false).LogMessages);
		Assert.Empty(presenter.Create(
			new LcuUnitFileUploadResult(LcuUnitFileUploadOutcome.Failed, []),
			"LineA",
			includeOutcomeMessages: false).LogMessages);
	}

	[Fact]
	public void Create_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new LcuFileUploadResultPresenter().Create(
				new LcuUnitFileUploadResult(LcuUnitFileUploadOutcome.Succeeded, []),
				"LineA",
				cancellation.Token));
	}
}
