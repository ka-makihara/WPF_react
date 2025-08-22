using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1_cmd.Models;

namespace WpfApp1_cmd.ViewModel
{
    class MachineViewModel : ViewModelBase
    {
		public string MachineName { get; set; } = "Machine";
        private ReactiveCollection<ModuleInfo> _modules;
        public ReactiveCollection<ModuleInfo> Modules
        {
            get => _modules;
            set
            {
                _modules = value;
                SetProperty(ref _modules, value);
            }
        }

		public ReactivePropertySlim<bool?> IsAllSelected { get; set; }
		public ReactiveCommand<ModuleInfo> CheckBoxCommand { get; }

		public void SelectModules(ModuleInfo module, bool? value)
		{
			if( module != null && value != null )
			{
				foreach (var unit in module.UnitVersions)
				{
					unit.IsSelected.Value = value;
				}
				OnPropertyChanged(nameof(IsAllSelected));
			}
		}

		/// <summary>
		/// モジュールに含まれるユニットの選択状態を取得して装置の選択状態を返す
		/// </summary>
		/// <param name="modules"></param>
		/// <returns></returns>
		private bool? GetModulesSelection(ReactiveCollection<ModuleInfo> modules)
		{
			int allTrue = 0;
			int allFalse = 0;
			int allNull = 0;

			if( MachineInfo != null && Config.Options.ContainsMachineType(MachineInfo.MachineType) == false )
			{
				//未対照の機種なので、選択状態は null とする
				return false;
			}

			foreach (var module in modules)
            {
				int tc = module.UnitVersions.Count(x => x.IsSelected.Value == true);
				int nc = module.UnitVersions.Count(x => x.IsSelected.Value == false);
				if (tc == module.UnitVersions.Count /*&& module.UnitVersions.Count != 0 */)
				{
					allTrue++;
				}
				else if (nc == module.UnitVersions.Count)
				{
					// ユニット情報が未取得の場合(UnitVersions.Count==0)は false とする
					allFalse++;
				}
				else
				{
					allNull++;
				}
            }
			if( allTrue == modules.Count )
			{
				return true;
			}
			else if( allFalse == modules.Count )
			{
				return false;
			}
			return null;
		}

		private MachineInfo _machineInfo;
		public MachineInfo MachineInfo
		{
			get => _machineInfo;
			set
			{
				_machineInfo = value;
				SetProperty(ref _machineInfo, value);
			}
		}

		public MachineViewModel(MachineInfo machine, ReactiveCollection<ModuleInfo> modules)
        {
            Modules = modules;
			MachineName = $"{machine.Name}:{modules.Count} modules";
			MachineInfo = machine;

			// 全選択用のチェックボックスの状態を管理するプロパティの初期化
			bool? sel = GetModulesSelection(modules);
            IsAllSelected = new ReactivePropertySlim<bool?>(sel);
            IsAllSelected.Subscribe(OnIsAllSelectedChanged);

			// チェックボックスの状態変更時の処理を登録
			CheckBoxCommand = new ReactiveCommand<ModuleInfo>();
			CheckBoxCommand.Subscribe(OnCheckBoxCommandExecuted);
		}

		private void OnIsAllSelectedChanged(bool? isChecked)
        {
			if( MachineInfo != null && Config.Options.ContainsMachineType(MachineInfo.MachineType) == false)
			{
				IsAllSelected.Value = false; // 未対照の機種なので、選択状態は false とする
				return; // 未対照の機種なので、選択状態は false とする
			}
			if (isChecked.HasValue)
            {
                foreach (var module in Modules)
                {
					module.IsSelected.Value = isChecked;
					SelectModules(module, isChecked);
				}
            }
        }

		/// <summary>
		/// データグリッドのチェックボックスをクリックした時のコマンド
		/// </summary>
		/// <param name="module"></param>
		private void OnCheckBoxCommandExecuted(ModuleInfo module)
        {
            // チェックボックスの状態変更時の処理をここに記述
            Debug.WriteLine($"Module {module.Name} is checked: {module.IsSelected.Value}");

			if( Config.Options.ContainsMachineType(MachineInfo.MachineType) == false )
			{
				//未対照の機種なので、選択状態はfalse とする
				module.IsSelected.Value = false;
				return;
			}
			// 全選択用のチェックボックスの状態を更新
			UpdateCheck();

			SelectModules(module, module.IsSelected.Value);
        }

		public void UpdateCheck()
		{
			// 全選択用のチェックボックスの状態を更新
            var selectedStates = Modules.Select(m => m.IsSelected.Value).Distinct().ToList();
            if (selectedStates.Count == 1)
            {
                IsAllSelected.Value = selectedStates.Single();
            }
            else
            {
                IsAllSelected.Value = null;
            }	
		}
    }
}
