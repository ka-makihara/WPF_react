using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

		public MachineViewModel(ReactiveCollection<ModuleInfo> modules)
        {
            Modules = modules;

            foreach (var item in Modules)
            {
				item.IsSelected.Subscribe(x => SelectModules(item,x));
			}

			// 全選択用のチェックボックスの状態を管理するプロパティの初期化
            IsAllSelected = new ReactivePropertySlim<bool?>(true);
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
                    module.IsSelected.Value = isChecked.Value;
                }
            }
        }

		private void OnCheckBoxCommandExecuted(ModuleInfo module)
        {
            // チェックボックスの状態変更時の処理をここに記述
            Console.WriteLine($"Module {module.Name} is checked: {module.IsSelected.Value}");

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
