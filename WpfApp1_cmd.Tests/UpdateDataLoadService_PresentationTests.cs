using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_PresentationTests
{
	[Fact]
	public void Create_LoadedResultIncludesSizeAndReadMessage()
	{
		UpdateDataLoadPresentation presentation = Create(
			new(true, [], 123, null, [], []));

		Assert.Equal(UpdateDataLoadPresentationOutcome.Loaded, presentation.Outcome);
		Assert.Equal(123, presentation.DataSize);
		Assert.Null(presentation.ErrorCode);
		Assert.Equal("Read UpdateCommon.inf", presentation.LogMessage);
	}

	[Fact]
	public void Create_ReadErrorTakesPrecedenceAndOmitsSize()
	{
		UpdateDataLoadPresentation presentation = Create(
			new(true, [], 123, "invalid data", [], []));

		Assert.Equal(UpdateDataLoadPresentationOutcome.ReadFailed, presentation.Outcome);
		Assert.Null(presentation.DataSize);
		Assert.Equal(ErrorCode.UPDATE_READ_ERROR, presentation.ErrorCode);
		Assert.Equal("Read Error:invalid data", presentation.LogMessage);
	}

	[Fact]
	public void Create_MissingUpdateCommonReturnsUndefinedError()
	{
		UpdateDataLoadPresentation presentation = Create(
			new(false, [], null, null, [], []));

		Assert.Equal(
			UpdateDataLoadPresentationOutcome.UpdateCommonMissing,
			presentation.Outcome);
		Assert.Null(presentation.DataSize);
		Assert.Equal(ErrorCode.UPDATE_UNDEFINED_ERROR, presentation.ErrorCode);
		Assert.Equal("UpdateCommon missing", presentation.LogMessage);
	}

	private static UpdateDataLoadPresentation Create(UpdateDataLoadResult result) =>
		new UpdateDataLoadService().CreateLoadPresentation(
			result,
			"UpdateCommon.inf",
			"UpdateCommon missing");
}
