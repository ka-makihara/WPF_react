namespace WpfApp1_cmd.Services;

public sealed record TransferModuleWork(
	string? Name,
	bool IsSelected,
	bool HasUnitVersions,
	Func<CancellationToken, Task<bool>> Transfer);

public sealed record TransferMachineWork(
	string? Name,
	bool HasChildren,
	bool IsSelected,
	IReadOnlyList<TransferModuleWork> Modules);

public sealed record TransferLcuWork(
	string? Name,
	bool? IsSelected,
	bool HasChildren,
	bool HasController,
	Func<CancellationToken, Task<bool>>? UploadFiles,
	IReadOnlyList<TransferMachineWork> Machines);

public enum TransferExecutionFailure
{
	None,
	LcuUpload,
	UpdateInfoLoad,
	ModuleTransfer,
	Cancelled
}

public sealed record TransferExecutionResult(
	bool IsSuccess,
	TransferExecutionFailure Failure);

public sealed class TransferExecutionService
{
	public async Task<TransferExecutionResult> ExecuteAsync(
		IReadOnlyList<TransferLcuWork> lcus,
		Func<CancellationToken, Task<long>> loadModulesUpdateInfo,
		Action<long> onModulesLoaded,
		Action<string?> onLcuSkipped,
		Action<string?, string?> onMachineSkipped,
		Action<string?, string?, string?> onModuleSkipped,
		Action<string?, string?, string?> onModuleStarted,
		Action<string?, string?, string?> onModuleEnded,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		ArgumentNullException.ThrowIfNull(loadModulesUpdateInfo);
		ArgumentNullException.ThrowIfNull(onModulesLoaded);
		ArgumentNullException.ThrowIfNull(onLcuSkipped);
		ArgumentNullException.ThrowIfNull(onMachineSkipped);
		ArgumentNullException.ThrowIfNull(onModuleSkipped);
		ArgumentNullException.ThrowIfNull(onModuleStarted);
		ArgumentNullException.ThrowIfNull(onModuleEnded);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (TransferLcuWork lcu in lcus)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (lcu.IsSelected == false || lcu.UploadFiles == null)
				{
					continue;
				}

				bool uploadSucceeded = await lcu.UploadFiles(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!uploadSucceeded)
				{
					return new TransferExecutionResult(false, TransferExecutionFailure.LcuUpload);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			long moduleCount = await loadModulesUpdateInfo(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (moduleCount == 0)
			{
				return new TransferExecutionResult(false, TransferExecutionFailure.UpdateInfoLoad);
			}
			onModulesLoaded(moduleCount);
			cancellationToken.ThrowIfCancellationRequested();

			foreach (TransferLcuWork lcu in lcus)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!lcu.HasChildren || !lcu.HasController || lcu.IsSelected == false)
				{
					onLcuSkipped(lcu.Name);
					cancellationToken.ThrowIfCancellationRequested();
					continue;
				}

				foreach (TransferMachineWork machine in lcu.Machines)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!machine.HasChildren || machine.Name == null || !machine.IsSelected)
					{
						onMachineSkipped(lcu.Name, machine.Name);
						cancellationToken.ThrowIfCancellationRequested();
						continue;
					}

					foreach (TransferModuleWork module in machine.Modules)
					{
						cancellationToken.ThrowIfCancellationRequested();
						if (!module.IsSelected || !module.HasUnitVersions)
						{
							onModuleSkipped(lcu.Name, machine.Name, module.Name);
							cancellationToken.ThrowIfCancellationRequested();
							continue;
						}

						onModuleStarted(lcu.Name, machine.Name, module.Name);
						cancellationToken.ThrowIfCancellationRequested();
						bool succeeded = await module.Transfer(cancellationToken);
						cancellationToken.ThrowIfCancellationRequested();
						onModuleEnded(lcu.Name, machine.Name, module.Name);
						cancellationToken.ThrowIfCancellationRequested();
						if (!succeeded)
						{
							return new TransferExecutionResult(false, TransferExecutionFailure.ModuleTransfer);
						}
					}
				}

			}

			cancellationToken.ThrowIfCancellationRequested();
			return new TransferExecutionResult(true, TransferExecutionFailure.None);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new TransferExecutionResult(false, TransferExecutionFailure.Cancelled);
		}
	}
}
