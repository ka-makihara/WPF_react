namespace WpfApp1_cmd.Services;

public sealed record SelectedModulePreparationWork(
	string? LcuName,
	string? MachineName,
	string? ModuleName,
	bool HasUnitVersions,
	Func<CancellationToken, Task<bool>> LoadUpdateInfoAsync,
	Func<CancellationToken, Task<bool>> CreateVersionInfoAsync,
	Func<CancellationToken, Task<int>> CountTransferFilesAsync,
	Func<CancellationToken, Task<long>> CountSelectedUnitsAsync);

public enum SelectedModulePreparationOutcome
{
	Ready,
	UpdateInfoUnavailable,
	VersionInfoUnavailable
}

public sealed record SelectedModulePreparationResult(
	SelectedModulePreparationOutcome Outcome,
	long TransferFileCount,
	SelectedModulePreparationWork? FailedWork);

public sealed class SelectedModulePreparationService
{
	public async Task<SelectedModulePreparationResult> PrepareAsync(
		IReadOnlyList<SelectedModulePreparationWork> modules,
		bool isAdministrator,
		Action<SelectedModulePreparationWork, long>? onVersionInfoCreated = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(modules);

		long transferFileCount = 0;
		foreach (SelectedModulePreparationWork module in modules)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ArgumentNullException.ThrowIfNull(module.LoadUpdateInfoAsync);
			ArgumentNullException.ThrowIfNull(module.CreateVersionInfoAsync);
			ArgumentNullException.ThrowIfNull(module.CountTransferFilesAsync);
			ArgumentNullException.ThrowIfNull(module.CountSelectedUnitsAsync);

			if (!module.HasUnitVersions)
			{
				bool updateInfoLoaded = await module.LoadUpdateInfoAsync(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!updateInfoLoaded && !isAdministrator)
				{
					return new(
						SelectedModulePreparationOutcome.UpdateInfoUnavailable,
						transferFileCount,
						module);
				}

				bool versionInfoCreated = await module.CreateVersionInfoAsync(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!versionInfoCreated)
				{
					return new(
						SelectedModulePreparationOutcome.VersionInfoUnavailable,
						transferFileCount,
						module);
				}

				long selectedUnitCount = await module.CountSelectedUnitsAsync(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				onVersionInfoCreated?.Invoke(module, selectedUnitCount);
			}

			transferFileCount += await module.CountTransferFilesAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(
			SelectedModulePreparationOutcome.Ready,
			transferFileCount,
			null);
	}
}
