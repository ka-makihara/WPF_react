using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class SelectedModulePreparationServiceTests
{
	[Fact]
	public async Task PrepareAsync_StopsBeforeProcessingNextModuleWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		bool secondModuleLoaded = false;
		SelectedModulePreparationWork[] modules =
		[
			CreateWork(
				"ModuleA",
				hasUnitVersions: true,
				countTransferFilesAsync: _ =>
				{
					cancellation.Cancel();
					return Task.FromResult(1);
				}),
			CreateWork(
				"ModuleB",
				hasUnitVersions: false,
				loadUpdateInfo: _ =>
				{
					secondModuleLoaded = true;
					return Task.FromResult(true);
				})
		];

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			new SelectedModulePreparationService().PrepareAsync(
				modules,
				isAdministrator: false,
				cancellationToken: cancellation.Token));

		Assert.False(secondModuleLoaded);
	}

	[Fact]
	public async Task PrepareAsync_LoadsThenCreatesVersionsThenCountsFiles()
	{
		List<string> calls = [];
		using CancellationTokenSource cancellation = new();
		CancellationToken receivedToken = default;
		SelectedModulePreparationWork[] modules =
		[
			CreateWork(
				"ModuleA",
				hasUnitVersions: false,
				loadUpdateInfo: token =>
				{
					receivedToken = token;
					calls.Add("load");
					return Task.FromResult(true);
				},
				createVersionInfoAsync: token =>
				{
					Assert.Equal(cancellation.Token, token);
					calls.Add("create");
					return Task.FromResult(true);
				},
				countTransferFilesAsync: token =>
				{
					Assert.Equal(cancellation.Token, token);
					calls.Add("count");
					return Task.FromResult(3);
				},
				countSelectedUnitsAsync: token =>
				{
					Assert.Equal(cancellation.Token, token);
					calls.Add("selected-count");
					return Task.FromResult(2L);
				})
		];
		List<long> selectedUnitCounts = [];

		SelectedModulePreparationResult result =
			await new SelectedModulePreparationService().PrepareAsync(
				modules,
				isAdministrator: false,
				(_, count) => selectedUnitCounts.Add(count),
				cancellation.Token);

		Assert.Equal(SelectedModulePreparationOutcome.Ready, result.Outcome);
		Assert.Equal(3, result.TransferFileCount);
		Assert.Null(result.FailedWork);
		Assert.Equal(["load", "create", "selected-count", "count"], calls);
		Assert.Equal([2L], selectedUnitCounts);
		Assert.Equal(cancellation.Token, receivedToken);
	}

	[Fact]
	public async Task PrepareAsync_StopsForUserWhenMachineInfoIsUnavailable()
	{
		bool created = false;
		SelectedModulePreparationWork[] modules =
		[
			CreateWork(
				"ModuleA",
				hasUnitVersions: false,
				loadUpdateInfo: _ => Task.FromResult(false),
				createVersionInfoAsync: _ =>
				{
					created = true;
					return Task.FromResult(true);
				})
		];

		SelectedModulePreparationResult result =
			await new SelectedModulePreparationService().PrepareAsync(
				modules,
				isAdministrator: false);

		Assert.Equal(SelectedModulePreparationOutcome.UpdateInfoUnavailable, result.Outcome);
		Assert.Same(modules[0], result.FailedWork);
		Assert.False(created);
	}

	[Fact]
	public async Task PrepareAsync_AdministratorContinuesAfterMachineInfoFailure()
	{
		SelectedModulePreparationWork[] modules =
		[
			CreateWork(
				"ModuleA",
				hasUnitVersions: false,
				loadUpdateInfo: _ => Task.FromResult(false),
				createVersionInfoAsync: _ => Task.FromResult(true),
				countTransferFilesAsync: _ => Task.FromResult(4))
		];

		SelectedModulePreparationResult result =
			await new SelectedModulePreparationService().PrepareAsync(
				modules,
				isAdministrator: true);

		Assert.Equal(SelectedModulePreparationOutcome.Ready, result.Outcome);
		Assert.Equal(4, result.TransferFileCount);
	}

	[Fact]
	public async Task PrepareAsync_DoesNotReloadExistingVersions()
	{
		bool loaded = false;
		bool created = false;
		SelectedModulePreparationWork[] modules =
		[
			CreateWork(
				"ModuleA",
				hasUnitVersions: true,
				loadUpdateInfo: _ =>
				{
					loaded = true;
					return Task.FromResult(false);
				},
				createVersionInfoAsync: _ =>
				{
					created = true;
					return Task.FromResult(false);
				},
				countTransferFilesAsync: _ => Task.FromResult(2))
		];

		SelectedModulePreparationResult result =
			await new SelectedModulePreparationService().PrepareAsync(
				modules,
				isAdministrator: false);

		Assert.Equal(SelectedModulePreparationOutcome.Ready, result.Outcome);
		Assert.Equal(2, result.TransferFileCount);
		Assert.False(loaded);
		Assert.False(created);
	}

	[Fact]
	public async Task PrepareAsync_StopsWhenVersionCreationFails()
	{
		SelectedModulePreparationWork[] modules =
		[
			CreateWork(
				"ModuleA",
				hasUnitVersions: false,
				createVersionInfoAsync: _ => Task.FromResult(false))
		];

		SelectedModulePreparationResult result =
			await new SelectedModulePreparationService().PrepareAsync(
				modules,
				isAdministrator: true);

		Assert.Equal(SelectedModulePreparationOutcome.VersionInfoUnavailable, result.Outcome);
		Assert.Same(modules[0], result.FailedWork);
	}

	[Fact]
	public async Task PrepareAsync_PropagatesCancellationFromSelectedUnitCount()
	{
		using CancellationTokenSource cancellation = new();
		bool transferCountCalled = false;
		SelectedModulePreparationWork[] modules =
		[
			CreateWork(
				"ModuleA",
				hasUnitVersions: false,
				countTransferFilesAsync: _ =>
				{
					transferCountCalled = true;
					return Task.FromResult(1);
				},
				countSelectedUnitsAsync: token =>
				{
					Assert.Equal(cancellation.Token, token);
					cancellation.Cancel();
					return Task.FromResult(1L);
				})
		];

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			new SelectedModulePreparationService().PrepareAsync(
				modules,
				isAdministrator: false,
				cancellationToken: cancellation.Token));

		Assert.False(transferCountCalled);
	}

	private static SelectedModulePreparationWork CreateWork(
		string moduleName,
		bool hasUnitVersions,
		Func<CancellationToken, Task<bool>>? loadUpdateInfo = null,
		Func<CancellationToken, Task<bool>>? createVersionInfoAsync = null,
		Func<CancellationToken, Task<int>>? countTransferFilesAsync = null,
		Func<CancellationToken, Task<long>>? countSelectedUnitsAsync = null) =>
		new(
			"LineA",
			"MachineA",
			moduleName,
			hasUnitVersions,
			loadUpdateInfo ?? (_ => Task.FromResult(true)),
			createVersionInfoAsync ?? (_ => Task.FromResult(true)),
			countTransferFilesAsync ?? (_ => Task.FromResult(0)),
			countSelectedUnitsAsync ?? (_ => Task.FromResult(2L)));
}
