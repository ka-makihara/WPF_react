using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TreeSelectionPolicyTests
{
	[Theory]
	[InlineData(ItemStatus.OK, true, true)]
	[InlineData(ItemStatus.OK, false, false)]
	[InlineData(ItemStatus.OK, null, null)]
	public void Evaluate_RejectsUnsupportedSelectionAndPropagatesSupportedSelection(
		ItemStatus status,
		bool? requested,
		bool? expected)
	{
		TreeSelectionDecision result = new TreeSelectionPolicy().Evaluate(status, requested);

		Assert.Equal(expected, result.Selection);
		Assert.True(result.PropagateToChildren);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	[InlineData(null)]
	public void Evaluate_AlwaysStopsPropagationForUnsupportedItems(bool? requested)
	{
		TreeSelectionDecision result =
			new TreeSelectionPolicy().Evaluate(ItemStatus.NOT_SUPPORTED, requested);

		Assert.False(result.Selection);
		Assert.False(result.PropagateToChildren);
	}
}
