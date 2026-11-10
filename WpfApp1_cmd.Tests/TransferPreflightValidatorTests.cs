using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferPreflightValidatorTests
{
	[Fact]
	public void Validate_RequiresASelectedTarget()
	{
		TransferSelectionSummary selection = new(0, 0, 0, 0, 0, []);

		TransferPreflightOutcome result =
			new TransferPreflightValidator().Validate(selection);

		Assert.Equal(TransferPreflightOutcome.NoTargetSelected, result);
	}

	[Fact]
	public void Validate_AllowsSelectedFilesWithoutRequiringBackupPath()
	{
		TransferSelectionSummary selection = new(1, 1, 1, 1, 1, ["update.bin"]);

		TransferPreflightOutcome result =
			new TransferPreflightValidator().Validate(selection);

		Assert.Equal(TransferPreflightOutcome.Ready, result);
	}

	[Fact]
	public void Validate_AllowsSelectedModuleWithoutFiles()
	{
		TransferSelectionSummary selection = new(0, 0, 1, 0, 0, []);

		TransferPreflightOutcome result =
			new TransferPreflightValidator().Validate(selection);

		Assert.Equal(TransferPreflightOutcome.Ready, result);
	}

	[Fact]
	public void Validate_DoesNotRequireBackupPathWhenNoFilesAreSelected()
	{
		TransferSelectionSummary selection = new(1, 1, 1, 0, 0, []);

		TransferPreflightOutcome result =
			new TransferPreflightValidator().Validate(selection);

		Assert.Equal(TransferPreflightOutcome.Ready, result);
	}
}
