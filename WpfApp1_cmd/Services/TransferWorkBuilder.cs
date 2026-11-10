namespace WpfApp1_cmd.Services;

public sealed record TransferModuleNode<TModule>(
	string? Name,
	bool? IsSelected,
	bool HasUnitVersions,
	TModule Context);

public sealed record TransferMachineNode<TMachine, TModule>(
	string? Name,
	bool HasChildren,
	bool? IsSelected,
	TMachine Context,
	IReadOnlyList<TransferModuleNode<TModule>> Modules);

public sealed record TransferLcuNode<TLcu, TMachine, TModule>(
	string? Name,
	bool? IsSelected,
	bool HasChildren,
	bool HasController,
	TLcu Context,
	IReadOnlyList<TransferMachineNode<TMachine, TModule>> Machines);

public sealed class TransferWorkBuilder
{
	public IReadOnlyList<TransferLcuWork> Build<TLcu, TMachine, TModule>(
		IReadOnlyList<TransferLcuNode<TLcu, TMachine, TModule>> lcus,
		Func<TLcu, CancellationToken, Task<bool>>? uploadFiles,
		Func<TLcu, TMachine, TModule, CancellationToken, Task<bool>> transferModule,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		ArgumentNullException.ThrowIfNull(transferModule);
		cancellationToken.ThrowIfCancellationRequested();

		TransferLcuWork[] work = new TransferLcuWork[lcus.Count];
		for (int lcuIndex = 0; lcuIndex < lcus.Count; lcuIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TransferLcuNode<TLcu, TMachine, TModule> lcu = lcus[lcuIndex];
			TransferMachineWork[] machines = new TransferMachineWork[lcu.Machines.Count];
			for (int machineIndex = 0; machineIndex < lcu.Machines.Count; machineIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				TransferMachineNode<TMachine, TModule> machine = lcu.Machines[machineIndex];
				TransferModuleWork[] modules = new TransferModuleWork[machine.Modules.Count];
				for (int moduleIndex = 0; moduleIndex < machine.Modules.Count; moduleIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					TransferModuleNode<TModule> module = machine.Modules[moduleIndex];
					modules[moduleIndex] = new TransferModuleWork(
						module.Name,
						module.IsSelected != false,
						module.HasUnitVersions,
						token => transferModule(
							lcu.Context,
							machine.Context,
							module.Context,
							token));
				}

				machines[machineIndex] = new TransferMachineWork(
					machine.Name,
					machine.HasChildren,
					machine.IsSelected != false,
					modules);
			}

			work[lcuIndex] = new TransferLcuWork(
				lcu.Name,
				lcu.IsSelected,
				lcu.HasChildren,
				lcu.HasController,
				uploadFiles == null ? null : token => uploadFiles(lcu.Context, token),
				machines);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return work;
	}
}
