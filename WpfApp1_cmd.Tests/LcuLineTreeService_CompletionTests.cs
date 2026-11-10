using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuLineTreeService_CompletionTests
{
	[Fact]
	public void Create_UnselectsErroredLcuAndUsesErrorTooltip()
	{
		LcuLoadCompletionState state =
			new LcuLineTreeService().CreateCompletionState(
				hasError: true,
				selectionState: true,
				errorToolTip: "Connection failed",
				version: "2.3");

		Assert.False(state.IsSelected);
		Assert.Equal("Connection failed", state.ToolTip);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Create_PreservesSelectionAndVersionWhenSuccessful(bool? selectionState)
	{
		LcuLoadCompletionState state =
			new LcuLineTreeService().CreateCompletionState(
				hasError: false,
				selectionState,
				errorToolTip: "unused",
				version: "2.3");

		Assert.Equal(selectionState, state.IsSelected);
		Assert.Equal("2.3", state.ToolTip);
	}

	[Fact]
	public void Create_PreservesIndeterminateSelectionWhenSuccessful()
	{
		LcuLoadCompletionState state =
			new LcuLineTreeService().CreateCompletionState(
				hasError: false,
				selectionState: null,
				errorToolTip: null,
				version: "2.3");

		Assert.Null(state.IsSelected);
	}

	[Fact]
	public void Create_RequiresErrorTooltipForAnErroredLcu()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new LcuLineTreeService().CreateCompletionState(
				hasError: true,
				selectionState: true,
				errorToolTip: null,
				version: "2.3"));
	}
}
