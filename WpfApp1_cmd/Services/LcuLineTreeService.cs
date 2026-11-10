namespace WpfApp1_cmd.Services;

// =============================================================================
// LcuLineTreeService
// -----------------------------------------------------------------------------
// 役割:
//   ライン情報から画面に依存しないツリー計画を作り、選択集計と読込完了状態を決める。
// 処理の流れ:
//   1. CreatePlan            : 対応機種の判定と装置・ベース・モジュールの選択状態を決める。
//   2. CreateSelectionSummary: 選択数を階層ごとに集計し、LCU の三状態選択を求める。
//   3. CreateCompletionState : エラーの有無から選択状態とツールチップを決める。
// 備考:
//   - WPF オブジェクトへの反映は LcuLineTreeItemBuilder が担当する。
//   - 機種名の大文字小文字の扱いと、未確定選択を含めた件数の数え方は変更しない。
// =============================================================================

#region 型定義

public sealed record LcuModuleDescriptor(
	int ModuleId,
	int LogicalPosition,
	string? DisplayName);

public sealed record LcuBaseDescriptor(
	int BaseId,
	int BaseType,
	string? IpAddress,
	int Position,
	string? Conveyor,
	IReadOnlyList<LcuModuleDescriptor> Modules);

public sealed record LcuMachineDescriptor(
	string? Name,
	string? MachineType,
	string? ModelName,
	IReadOnlyList<LcuBaseDescriptor> Bases);

public sealed record LcuModuleTreePlan(
	int ModuleId,
	int LogicalPosition,
	string? DisplayName,
	string? IpAddress,
	bool? IsSelected);

public sealed record LcuBaseTreePlan(
	int BaseId,
	int BaseType,
	string? IpAddress,
	int Position,
	string? Conveyor,
	IReadOnlyList<LcuModuleTreePlan> Modules);

public sealed record LcuMachineTreePlan(
	string? Name,
	string? MachineType,
	string? ModelName,
	bool IsSupported,
	bool? IsSelected,
	IReadOnlyList<LcuBaseTreePlan> Bases);

public sealed record LcuLineTreePlan(
	IReadOnlyList<(string? Name, string? MachineType)> SkippedMachines,
	IReadOnlyList<LcuMachineTreePlan> Machines);

public enum LcuLineTreeProgressStage
{
	Machine,
	Module
}

public sealed record LcuLineTreeProgress(
	LcuLineTreeProgressStage Stage,
	string? Name);

public sealed record LcuModuleSelection(bool? IsSelected, IReadOnlyList<bool?> UnitSelections);

public sealed record LcuMachineSelection(
	bool? IsSelected,
	IReadOnlyList<LcuModuleSelection> Modules);

public sealed record LcuSelectionSummary(
	int MachineCount,
	int ModuleCount,
	int UnitCount,
	bool? SelectionState);

public sealed record LcuLoadCompletionState(bool? IsSelected, string? ToolTip);

#endregion

/// <summary>LCU ツリーの構築計画・選択集計・読込完了状態を扱う。</summary>
public sealed class LcuLineTreeService
{
	#region 1. ツリー計画

	/// <summary>対応機種に応じて表示対象と選択状態を決め、階層構造の計画を作る。</summary>
	public LcuLineTreePlan CreatePlan(
		IReadOnlyList<LcuMachineDescriptor> machines,
		IReadOnlyCollection<string?> supportedMachineTypes,
		bool ignoreMachineType,
		bool? lcuIsSelected,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(supportedMachineTypes);
		cancellationToken.ThrowIfCancellationRequested();

		HashSet<string?> supportedTypes = new();
		foreach (string? supportedType in supportedMachineTypes)
		{
			cancellationToken.ThrowIfCancellationRequested();
			supportedTypes.Add(supportedType);
		}

		List<(string? Name, string? MachineType)> skippedMachines = [];
		List<LcuMachineTreePlan> machinePlans = [];
		foreach (LcuMachineDescriptor machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			bool isSupported = supportedTypes.Contains(machine.MachineType);
			if (!ignoreMachineType && !isSupported)
			{
				skippedMachines.Add((machine.Name, machine.MachineType));
				continue;
			}

			List<LcuBaseTreePlan> basePlans = [];
			bool? isSelected = true;
			foreach (LcuBaseDescriptor baseInfo in machine.Bases)
			{
				cancellationToken.ThrowIfCancellationRequested();
				isSelected = isSupported ? lcuIsSelected : false;
				List<LcuModuleTreePlan> modulePlans = [];
				foreach (LcuModuleDescriptor module in baseInfo.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					modulePlans.Add(new LcuModuleTreePlan(
						module.ModuleId,
						module.LogicalPosition,
						module.DisplayName,
						baseInfo.IpAddress,
						isSupported ? lcuIsSelected : false));
				}
				basePlans.Add(new LcuBaseTreePlan(
					baseInfo.BaseId,
					baseInfo.BaseType,
					baseInfo.IpAddress,
					baseInfo.Position,
					baseInfo.Conveyor,
					modulePlans.ToArray()));
			}

			machinePlans.Add(new LcuMachineTreePlan(
				machine.Name,
				machine.MachineType,
				machine.ModelName,
				isSupported,
				isSelected,
				basePlans));
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new LcuLineTreePlan(skippedMachines, machinePlans);
	}

	#endregion

	#region 2. 選択集計

	/// <summary>false 以外の項目を選択件数に含め、装置の選択状態から LCU の三状態選択を求める。</summary>
	public LcuSelectionSummary CreateSelectionSummary(
		IReadOnlyList<LcuMachineSelection> machines,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(machines);
		cancellationToken.ThrowIfCancellationRequested();

		int machineCount = 0;
		int moduleCount = 0;
		int unitCount = 0;
		bool allMachinesSelected = machines.Count > 0;
		bool allMachinesUnselected = machines.Count > 0;
		foreach (LcuMachineSelection machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (machine.IsSelected != false)
			{
				machineCount++;
			}
			allMachinesSelected &= machine.IsSelected == true;
			allMachinesUnselected &= machine.IsSelected == false;

			foreach (LcuModuleSelection module in machine.Modules)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (module.IsSelected != false)
				{
					moduleCount++;
				}

				foreach (bool? unitSelection in module.UnitSelections)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (unitSelection != false)
					{
						unitCount++;
					}
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		bool? selectionState = machines.Count == 0
			? false
			: allMachinesSelected
			? true
			: allMachinesUnselected
				? false
				: null;

		return new(machineCount, moduleCount, unitCount, selectionState);
	}

	#endregion

	#region 3. 読込完了状態

	/// <summary>エラー時は LCU を未選択にし、正常時は集計した選択状態とバージョンを使う。</summary>
	public LcuLoadCompletionState CreateCompletionState(
		bool hasError,
		bool? selectionState,
		string? errorToolTip,
		string? version)
	{
		if (hasError)
		{
			ArgumentNullException.ThrowIfNull(errorToolTip);
			return new(false, errorToolTip);
		}

		return new(selectionState, version);
	}

	#endregion
}
