using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private ObservableCollection<MachineInfo> _machineInfos;
        public ObservableCollection<MachineInfo> MachineInfos
        {
            get => _machineInfos;
            set => SetProperty(ref _machineInfos, value);
        }
        public LcuViewModel( ObservableCollection<MachineInfo> machineInfos)
        {
            MachineInfos = machineInfos;

            // IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
            foreach (var item in MachineInfos)
            {
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(MachineInfo.IsSelected))
                    {
                        OnPropertyChanged(nameof(IsAllSelected));
                    }
                };
            }
        }
        public bool? IsAllSelected
        {
            get {
                var selected = MachineInfos.Select(item => item.IsSelected).Distinct().ToList();
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
                model.IsSelected = select;
            }
        }
    }
}
