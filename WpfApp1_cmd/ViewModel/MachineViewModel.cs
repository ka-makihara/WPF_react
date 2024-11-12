using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.ViewModel
{
    class MachineViewModel : ViewModelBase
    {
        private ObservableCollection<MachineInfo> _machines;
        public ObservableCollection<MachineInfo> Machines
        {
            get => _machines;
            set
            {
                _machines = value;
                SetProperty(ref _machines, value);
            }
        }

        public MachineViewModel()
        {
            LoadMachineInfo();
            // IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
            foreach (var item in Machines)
            {
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(UnitVersion.IsSelected))
                    {
                        OnPropertyChanged(nameof(IsAllSelected));
                    }
                };
            }
        }

        private void LoadMachineInfo()
        {
            Machines = new ObservableCollection<MachineInfo>
            {
                new MachineInfo { IsSelected = true,  Name = "Machine1", ID = 1, Pos = 1, IPAddress="localhost"},
                new MachineInfo { IsSelected = false, Name = "Machine2", ID = 2, Pos = 2, IPAddress="localhost"},
                new MachineInfo { IsSelected = true,  Name = "Machine3", ID = 3, Pos = 3, IPAddress="localhost"},
                new MachineInfo { IsSelected = false, Name = "Machine4", ID = 4, Pos = 4, IPAddress="localhost"},
                new MachineInfo { IsSelected = true,  Name = "Machine5", ID = 5, Pos = 5, IPAddress="localhost"},
            };
        }
        public bool? IsAllSelected
        {
            get {
                var selected = Machines.Select(item => item.IsSelected).Distinct().ToList();
                return selected.Count == 1 ? selected.Single() : (bool?)null;
            }
            set {
                if (value.HasValue) {
                    SelectAll(value.Value, Machines);
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
