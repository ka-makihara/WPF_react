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
    internal class LcuViewModel : ViewModelBase
    {
		public string MachineName { get; set; } = "Machine";
		private LcuInfo? _lcuInfo;
		public LcuInfo? LcuInfo
		{ 
			get => _lcuInfo;
			set => SetProperty(ref _lcuInfo, value);
		}

		// MachineInfos プロパティは、ReactiveCollection<MachineInfo> 型で定義
		private ReactiveCollection<MachineInfo>? _machineInfos;
        public ReactiveCollection<MachineInfo>? MachineInfos
        {
            get => _machineInfos;
            set => SetProperty(ref _machineInfos, value);
        }
        public LcuViewModel(LcuInfo lcuInfo, ReactiveCollection<MachineInfo> machineInfos)
        {
			LcuInfo = lcuInfo;
            MachineInfos = machineInfos;
			MachineName = $"{lcuInfo.Name}:{machineInfos.Count} Machines";

			// IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
			foreach (var item in MachineInfos)
            {
                item.IsSelected.Subscribe(_ =>
                {
                    OnPropertyChanged(nameof(IsAllSelected));
                });
            }
			// チェックボックスの状態変更時の処理を登録
			CheckBoxCommand = new ReactiveCommand<MachineInfo>();
			CheckBoxCommand.Subscribe(OnCheckBoxCommandExecuted);
        }
        public bool? IsAllSelected
        {
            get {
				if (MachineInfos == null || MachineInfos.Count == 0)
				{
					return false;
				}
				var selected = MachineInfos.Select(item => item.IsSelected.Value == true).Distinct().ToList();
                return selected.Count == 1 ? selected.Single() : (bool?)null;
            }
            set {
                if (value.HasValue) {
                    SelectAll(value.Value, MachineInfos);
                    OnPropertyChanged();
                }
            }
        }
		public ReactiveCommand<MachineInfo> CheckBoxCommand { get; }
        private static void SelectAll(bool select, IEnumerable<MachineInfo>? models)
        {
			if(models == null) return;

			foreach (var model in models)
			{
				if (Config.Options.ContainsMachineType(model.MachineType) == false)
				{
					model.IsExpanded.Value = false;
				}
				else {
					model.IsSelected.Value = select;
				}
            }
        }
		private void OnCheckBoxCommandExecuted(MachineInfo machine)
		{

		}
    }
}
