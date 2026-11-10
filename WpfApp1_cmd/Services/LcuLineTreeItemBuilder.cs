using System.Net; // �� ���̍s��ǉ�

using WpfApp1_cmd.Models;
using WpfApp1_cmd.Resources;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

// =============================================================================
// LcuLineTreeItemBuilder
// -----------------------------------------------------------------------------
// 役割:
//   LcuLineTreeService の計画を WPF の装置・ベース・モジュールへ反映する。
// 処理の流れ:
//   1. バックグラウンドでライン情報の索引とツリー計画を作成する。
//   2. 呼出し元のコンテキストで項目・親子関係・選択状態・表示状態を準備する。
//   3. 全項目の準備後に LCU へ追加する。
// 備考:
//   - キャンセル時は途中まで準備した装置を LCU へ追加しない。
//   - 同一ベースの 2 番目以降のモジュールの IPv4 は、インデックスごとに 5 加算する。
// =============================================================================

/// <summary>画面に依存しないツリー計画から、表示用の LCU 配下項目を構築する。</summary>
public sealed class LcuLineTreeItemBuilder(LcuLineTreeService treeService)
{
	private sealed record BaseSource(
		WpfLcuCtrlLib.Base Data,
		Dictionary<(int ModuleId, int LogicalPosition), Module> Modules);

	private sealed record TreeSnapshot(
		LcuLineTreePlan Plan,
		Dictionary<(string? Name, string? MachineType), Machine> MachinesByKey,
		Dictionary<
			(string? Name, string? MachineType),
			Dictionary<(int BaseId, int Position), BaseSource>> BasesByMachine);

	/// <summary>項目を準備し、全件の構築成功後に LCU の子項目へ反映する。</summary>
	public async Task<LcuLineTreePlan> PopulateAsync(
		LcuInfo lcu,
		LineInfo lineInfo,
		IReadOnlyCollection<string?> supportedMachineTypes,
		bool ignoreMachineType,
		Action<LcuLineTreeProgress>? onProgress = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcu);
		ArgumentNullException.ThrowIfNull(lineInfo);
		ArgumentNullException.ThrowIfNull(supportedMachineTypes);

		cancellationToken.ThrowIfCancellationRequested();
		bool? lcuIsSelected = lcu.IsSelected.Value == true;
		TreeSnapshot snapshot = await Task.Run(
			() => CreateSnapshot(
				lineInfo,
				supportedMachineTypes,
				ignoreMachineType,
				lcuIsSelected,
				cancellationToken),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		Dictionary<(string? Name, string? MachineType), Machine> machinesByKey =
			snapshot.MachinesByKey;
		Dictionary<
			(string? Name, string? MachineType),
			Dictionary<(int BaseId, int Position), BaseSource>> basesByMachine =
			snapshot.BasesByMachine;
		LcuLineTreePlan treePlan = snapshot.Plan;

		List<MachineInfo> machineItems = [];
		foreach (LcuMachineTreePlan machinePlan in treePlan.Machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			(string? Name, string? MachineType) machineKey =
				(machinePlan.Name, machinePlan.MachineType);
			if (!machinesByKey.TryGetValue(machineKey, out Machine? machineData))
			{
				throw new InvalidOperationException("Sequence contains no matching element.");
			}

			Dictionary<(int BaseId, int Position), BaseSource> basesByKey =
				basesByMachine[machineKey];
			MachineInfo machine = new()
			{
				Name = machinePlan.Name,
				ItemType = MachineType.Machine,
				Machine = machineData,
				Parent = lcu,
				Status = machinePlan.IsSupported ? ItemStatus.OK : ItemStatus.NOT_SUPPORTED,
				ToolTipText = machinePlan.MachineType
			};
			if (!machinePlan.IsSupported)
			{
				machine.IconToolTipText = Resource.NotApplicable;
			}

			onProgress?.Invoke(new(
				LcuLineTreeProgressStage.Machine,
				machinePlan.Name));
			cancellationToken.ThrowIfCancellationRequested();
			foreach (LcuBaseTreePlan basePlan in machinePlan.Bases)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!basesByKey.TryGetValue(
					(basePlan.BaseId, basePlan.Position),
					out BaseSource? baseDataEntry))
				{
					throw new InvalidOperationException("Sequence contains no matching element.");
				}

				WpfLcuCtrlLib.Base baseData = baseDataEntry.Data;
				BaseInfo baseInfo = new()
				{
					Name = "",
					ItemType = MachineType.Base,
					Base = baseData,
					Parent = machine
				};
				machine.Bases.Add(baseInfo);
				machine.IsSelected.Value = machinePlan.IsSelected;

				int moduleIndex = 0;
				foreach (LcuModuleTreePlan modulePlan in basePlan.Modules)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!baseDataEntry.Modules.TryGetValue(
						(modulePlan.ModuleId, modulePlan.LogicalPosition),
						out Module? moduleData))
					{
						throw new InvalidOperationException("Sequence contains no matching element.");
					}

					ModuleInfo moduleItem = new()
					{
						Name = modulePlan.DisplayName,
						ItemType = MachineType.Module,
						Module = moduleData,
						Parent = machine,
						IPAddress = ResolveModuleIpAddress(modulePlan.IpAddress, moduleIndex),
						ToolTipText = Resource.UnitInfoNotAcquired,
						Status = machine.Status
					};
					moduleIndex++;

					onProgress?.Invoke(new(
						LcuLineTreeProgressStage.Module,
						modulePlan.DisplayName));
					cancellationToken.ThrowIfCancellationRequested();
					baseInfo.Children.Add(moduleItem);
					machine.Children.Add(moduleItem);
					moduleItem.IsSelected.Value = modulePlan.IsSelected;
					if (!machinePlan.IsSupported)
					{
						moduleItem.IconToolTipText = Resource.NotApplicable;
					}
				}
			}
			machineItems.Add(machine);
		}

		cancellationToken.ThrowIfCancellationRequested();
		foreach (MachineInfo machine in machineItems)
		{
			lcu.Children.Add(machine);
		}

		return treePlan;
	}

	private TreeSnapshot CreateSnapshot(
		LineInfo lineInfo,
		IReadOnlyCollection<string?> supportedMachineTypes,
		bool ignoreMachineType,
		bool? lcuIsSelected,
		CancellationToken cancellationToken)
	{
		List<Machine> machines = lineInfo.Line!.Machines!;
		List<LcuMachineDescriptor> machineDescriptors = [];
		Dictionary<(string? Name, string? MachineType), Machine> machinesByKey = [];
		Dictionary<
			(string? Name, string? MachineType),
			Dictionary<(int BaseId, int Position), BaseSource>> basesByMachine = [];

		foreach (Machine machine in machines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			(string? Name, string? MachineType) machineKey =
				(machine.MachineName, machine.MachineType);
			bool indexMachine = machinesByKey.TryAdd(machineKey, machine);
			Dictionary<(int BaseId, int Position), BaseSource>? basesByKey = null;
			if (indexMachine)
			{
				basesByKey = [];
				basesByMachine.Add(machineKey, basesByKey);
			}

			List<LcuBaseDescriptor> baseDescriptors = [];
			foreach (WpfLcuCtrlLib.Base baseInfo in machine.Bases!)
			{
				cancellationToken.ThrowIfCancellationRequested();
				List<LcuModuleDescriptor> moduleDescriptors = [];
				bool indexBase = indexMachine
					&& basesByKey!.TryAdd(
						(baseInfo.BaseId, baseInfo.Position),
						new BaseSource(baseInfo, []));
				Dictionary<(int ModuleId, int LogicalPosition), Module>? modulesByKey =
					indexBase
						? basesByKey![(baseInfo.BaseId, baseInfo.Position)].Modules
						: null;
				foreach (Module module in baseInfo.Modules!)
				{
					cancellationToken.ThrowIfCancellationRequested();
					modulesByKey?.TryAdd((module.ModuleId, module.LogicalPos), module);
					moduleDescriptors.Add(new LcuModuleDescriptor(
						module.ModuleId,
						module.LogicalPos,
						module.DispModule));
				}

				baseDescriptors.Add(new LcuBaseDescriptor(
					baseInfo.BaseId,
					baseInfo.BaseType,
					baseInfo.IpAddr,
					baseInfo.Position,
					baseInfo.FSSisConveyor,
					moduleDescriptors));
			}

			machineDescriptors.Add(new LcuMachineDescriptor(
				machine.MachineName,
				machine.MachineType,
				machine.ModelName,
				baseDescriptors));
		}

		LcuLineTreePlan plan = treeService.CreatePlan(
			machineDescriptors,
			supportedMachineTypes,
			ignoreMachineType,
			lcuIsSelected,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return new TreeSnapshot(plan, machinesByKey, basesByMachine);
	}

	private static string ResolveModuleIpAddress(string? ipAddress, int moduleIndex)
	{
		if (string.IsNullOrWhiteSpace(ipAddress))
		{
			return "";
		}

		// 1�ڂ�Module�͂��̂܂ܕԂ�
		if (moduleIndex <= 0)
		{
			return ipAddress;
		}

		// IPv4�ȊO��p�[�X�s�͂��̂܂ܕԂ�
		if (!IPAddress.TryParse(ipAddress, out IPAddress? parsed))
		{
			return ipAddress;
		}

		byte[] bytes = parsed.GetAddressBytes();
		if (bytes.Length != 4)
		{
			return ipAddress;
		}

		// 2�ڈȍ~�� +5 �Â��Z�i�J��オ��Ή��j
		int carry = 5 * moduleIndex;
		for (int index = bytes.Length - 1; index >= 0 && carry > 0; index--)
		{
			int sum = bytes[index] + carry;
			bytes[index] = (byte)(sum & 0xFF);
			carry = sum >> 8;
		}

		return new IPAddress(bytes).ToString();
	}
}
