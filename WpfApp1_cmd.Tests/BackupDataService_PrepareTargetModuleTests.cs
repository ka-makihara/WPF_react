using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class BackupDataService_PrepareTargetModuleTests
{
	[Fact]
	public async Task PrepareAsync_RestoresBackupSelectionModeWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		bool isBackupedData = true;
		bool versionCreationCalled = false;
		CancellationToken receivedToken = default;
		var service = new BackupDataService();

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.PrepareTargetModuleAsync(
				token =>
				{
					receivedToken = token;
					cancellation.Cancel();
					return Task.FromResult(true);
				},
				_ =>
				{
					versionCreationCalled = true;
					return Task.FromResult(true);
				},
				isAdministrator: false,
				setBackupSelectionMode: value => isBackupedData = value,
				cancellationToken: cancellation.Token));

		Assert.False(versionCreationCalled);
		Assert.True(isBackupedData);
		Assert.Equal(cancellation.Token, receivedToken);
	}

	[Fact]
	public async Task PrepareAsync_LoadsUpdateInfoBeforeCreatingVersions()
	{
		List<string> calls = [];
		bool isBackupedData = true;
		var service = new BackupDataService();

		BackupModulePreparationResult result = await service.PrepareTargetModuleAsync(
			_ =>
			{
				calls.Add("update");
				return Task.FromResult(true);
			},
			token =>
			{
				Assert.Equal(CancellationToken.None, token);
				calls.Add("version");
				return Task.FromResult(true);
			},
			isAdministrator: false,
			setBackupSelectionMode: value =>
			{
				calls.Add($"backup:{value}");
				isBackupedData = value;
			});

		Assert.Equal(["backup:False", "update", "version", "backup:True"], calls);
		Assert.Equal(BackupModulePreparationOutcome.Ready, result.Outcome);
		Assert.True(result.UpdateInfoLoaded);
		Assert.True(result.VersionInfoCreated);
		Assert.True(isBackupedData);
	}

	[Fact]
	public async Task PrepareAsync_StopsAfterUpdateInfoFailureForNonAdministrator()
	{
		bool versionCreationCalled = false;
		bool isBackupedData = true;
		var service = new BackupDataService();

		BackupModulePreparationResult result = await service.PrepareTargetModuleAsync(
			_ => Task.FromResult(false),
			_ =>
			{
				versionCreationCalled = true;
				return Task.FromResult(true);
			},
			isAdministrator: false,
			setBackupSelectionMode: value => isBackupedData = value);

		Assert.Equal(BackupModulePreparationOutcome.UpdateInfoUnavailable, result.Outcome);
		Assert.False(result.UpdateInfoLoaded);
		Assert.False(result.VersionInfoCreated);
		Assert.False(versionCreationCalled);
		Assert.True(isBackupedData);
	}

	[Fact]
	public async Task PrepareAsync_ContinuesAfterUpdateInfoFailureForAdministrator()
	{
		bool isBackupedData = true;
		var service = new BackupDataService();

		BackupModulePreparationResult result = await service.PrepareTargetModuleAsync(
			_ => Task.FromResult(false),
			_ => Task.FromResult(true),
			isAdministrator: true,
			setBackupSelectionMode: value => isBackupedData = value);

		Assert.Equal(BackupModulePreparationOutcome.Ready, result.Outcome);
		Assert.False(result.UpdateInfoLoaded);
		Assert.True(result.VersionInfoCreated);
		Assert.True(isBackupedData);
	}

	[Fact]
	public async Task PrepareAsync_ReportsVersionCreationFailure()
	{
		bool isBackupedData = true;
		var service = new BackupDataService();

		BackupModulePreparationResult result = await service.PrepareTargetModuleAsync(
			_ => Task.FromResult(true),
			_ => Task.FromResult(false),
			isAdministrator: false,
			setBackupSelectionMode: value => isBackupedData = value);

		Assert.Equal(BackupModulePreparationOutcome.VersionInfoUnavailable, result.Outcome);
		Assert.True(result.UpdateInfoLoaded);
		Assert.False(result.VersionInfoCreated);
		Assert.True(isBackupedData);
	}

	[Fact]
	public async Task PrepareAsync_PropagatesUpdateInfoExceptions()
	{
		var service = new BackupDataService();
		bool isBackupedData = true;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.PrepareTargetModuleAsync(
				_ => throw new InvalidOperationException("update failed"),
				_ => Task.FromResult(true),
				isAdministrator: false,
				setBackupSelectionMode: value => isBackupedData = value));
		Assert.True(isBackupedData);
	}
}
