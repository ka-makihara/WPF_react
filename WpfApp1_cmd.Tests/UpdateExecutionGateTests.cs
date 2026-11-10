using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public class UpdateExecutionGateTests
{
	[Fact]
	public async Task CreateUpdateOperation_StartsUpdateOnlyAfterSuccessfulBackup()
	{
		int updateStarts = 0;
		Func<Task<int>>? update = UpdateExecutionGate.CreateUpdateOperation(
			BackupOutcome.Succeeded,
			() =>
			{
				updateStarts++;
				return Task.FromResult(42);
			});

		Assert.Equal(0, updateStarts);
		Assert.NotNull(update);
		Assert.Equal(42, await update());
		Assert.Equal(1, updateStarts);
	}

	[Theory]
	[InlineData(BackupOutcome.Failed)]
	[InlineData(BackupOutcome.Cancelled)]
	public void CreateUpdateOperation_DoesNotCreateAnUpdateOperationAfterUnsuccessfulBackup(
		BackupOutcome backupOutcome)
	{
		int updateStarts = 0;

		Func<Task<int>>? update = UpdateExecutionGate.CreateUpdateOperation(
			backupOutcome,
			() =>
			{
				updateStarts++;
				return Task.FromResult(42);
			});

		Assert.Null(update);
		Assert.Equal(0, updateStarts);
	}
}
