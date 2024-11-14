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
        /*
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
        */
        public ObservableCollection<ModuleInfo> Modules { get; set; }

        public MachineViewModel(ObservableCollection<ModuleInfo> modules)
        {
            //LoadMachineInfo();
            Modules = modules;

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
                new (){Name="Module1", IsSelected=true}
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
