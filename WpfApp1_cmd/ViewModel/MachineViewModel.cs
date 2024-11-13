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
        private ObservableCollection<ModuleInfo> _modules;
        public ObservableCollection<ModuleInfo> Modules
        {
            get => _modules;
            set
            {
                _modules = value;
                SetProperty(ref _modules, value);
            }
        }

        public MachineViewModel()
        {
            LoadMachineInfo();
            // IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
            foreach (var item in Modules)
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
            Modules =
            [
                new () { IsSelected = true,  Name = "Machine1", ModuleId = 1, LogicalPos = 1, PhysicalPos=1},
                new () { IsSelected = false, Name = "Machine2", ModuleId = 2, LogicalPos = 2, PhysicalPos=2},
                new () { IsSelected = true,  Name = "Machine3", ModuleId = 3, LogicalPos = 3, PhysicalPos=3},
                new () { IsSelected = false, Name = "Machine4", ModuleId = 4, LogicalPos = 4, PhysicalPos=4},
                new () { IsSelected = true,  Name = "Machine5", ModuleId = 5, LogicalPos = 5, PhysicalPos=5},
            ];
        }
        public bool? IsAllSelected
        {
            get {
                var selected = Modules.Select(item => item.IsSelected).Distinct().ToList();
                return selected.Count == 1 ? selected.Single() : (bool?)null;
            }
            set {
                if (value.HasValue) {
                    SelectAll(value.Value, Modules);
                    OnPropertyChanged();
                }
            }
        }
        private static void SelectAll(bool select, IEnumerable<ModuleInfo> models)
        {
            foreach (var model in models) {
                model.IsSelected = select;
            }
        }
    }
}
