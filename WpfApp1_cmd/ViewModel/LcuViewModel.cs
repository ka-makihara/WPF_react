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
        /*
        private ObservableCollection<LcuData> _lcuData;
        public ObservableCollection<LcuData> LcuData
        {
            get => _lcuData;
            set => SetProperty(ref _lcuData, value);
        }
        */
		public string MachineName { get; set; } = "Machine";
        private ReactiveCollection<MachineInfo> _machineInfos;
        public ReactiveCollection<MachineInfo> MachineInfos
        {
            get => _machineInfos;
            set => SetProperty(ref _machineInfos, value);
        }
        public LcuViewModel(string name, ReactiveCollection<MachineInfo> machineInfos)
        {
            MachineInfos = machineInfos;
			MachineName = $"{name}:{machineInfos.Count} Machines";

			// IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
			foreach (var item in MachineInfos)
            {
                item.IsSelected.Subscribe(_ =>
                {
                    OnPropertyChanged(nameof(IsAllSelected));
                });
            }
        }
        public bool? IsAllSelected
        {
            get {
                //var selected = MachineInfos.Select(item => item.IsSelected).Distinct().ToList();
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
        private static void SelectAll(bool select, IEnumerable<MachineInfo> models)
        {
            foreach (var model in models) {
                model.IsSelected.Value = select;
            }
        }
    }
}
