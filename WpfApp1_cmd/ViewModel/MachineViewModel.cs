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

		private bool? GetModulesSelection(ReactiveCollection<ModuleInfo> modules)
		{
			int allTrue = 0;
			int allFalse = 0;
			int allNull = 0;

			foreach (var module in modules)
            {
				int tc = module.UnitVersions.Count(x => x.IsSelected.Value == true);
				int nc = module.UnitVersions.Count(x => x.IsSelected.Value == false);
				if (tc == module.UnitVersions.Count)
				{
					allTrue++;
				}
				else if (nc == module.UnitVersions.Count)
				{
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

		public MachineViewModel(ReactiveCollection<ModuleInfo> modules)
        {
            Modules = modules;

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
            if (isChecked.HasValue)
            {
                foreach (var module in Modules)
                {
					module.IsSelected.Value = isChecked;
					SelectModules(module, isChecked);
				}
            }
        }

		private void OnCheckBoxCommandExecuted(ModuleInfo module)
        {
            // チェックボックスの状態変更時の処理をここに記述
            Debug.WriteLine($"Module {module.Name} is checked: {module.IsSelected.Value}");

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
			SelectModules(module, module.IsSelected.Value);
        }
    }
}
