using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferProgressCalculatorTests
{
	private readonly TransferProgressCalculator _calculator = new();

	[Theory]
	[InlineData(0, 100, 0)]
	[InlineData(50, 100, 125)]
	[InlineData(100, 100, 250)]
	[InlineData(100, 0, 0)]
	public void CalculateUploadProgress_MapsBytesToFirstProgressStage(
		long transferred,
		long total,
		double expected)
	{
		Assert.Equal(expected, _calculator.CalculateUploadProgress(transferred, total));
	}

	[Theory]
	[InlineData(0, 10, 250)]
	[InlineData(5, 10, 625)]
	[InlineData(10, 10, 1000)]
	[InlineData(15, 10, 1000)]
	[InlineData(1, 0, 250)]
	public void CalculateModuleProgress_MapsCountsToSecondStageAndCapsAtMaximum(
		long transferred,
		long total,
		double expected)
	{
		Assert.Equal(expected, _calculator.CalculateModuleProgress(transferred, total));
	}
}
