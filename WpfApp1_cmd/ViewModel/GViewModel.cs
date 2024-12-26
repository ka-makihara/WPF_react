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
    internal class GViewModel : ViewModelBase
    {
        private ReactiveCollection<UnitVersion> _unitVersions;
        public ReactiveCollection<UnitVersion> UnitVersions
        {
            get => _unitVersions;
            set
            {
                _unitVersions = value;
                SetProperty(ref _unitVersions, value);
            } 
        }

        //public GViewModel(ObservableCollection<UnitVersion> versions)
        public GViewModel()
        {
            LoadUnitVersions();

            // IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
            /*
            foreach (var item in UnitVersions)
            {
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(UnitVersion.IsSelected))
                    {
                        OnPropertyChanged(nameof(IsAllSelected));
                    }
                };
            }
            */
        }

        private void LoadUnitVersions()
        {
            UnitVersions = new ReactiveCollection<UnitVersion>
            {
                new UnitVersion { Name = "Unit1", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { Name = "Unit2", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { Name = "Unit3", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { Name = "Unit4", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { Name = "Unit5", CurVersion = "1.0.0", NewVersion = "1.0.1" },
            };
        }
        public bool? IsAllSelected
        {
            get {
                var selected = UnitVersions.Select(item => item.IsSelected.Value).Distinct().ToList();
                return selected.Count == 1 ? selected.Single() : (bool?)null;
            }
            set {
                if (value.HasValue) {
                    SelectAll(value.Value, UnitVersions);
                    OnPropertyChanged();
                }
            }
        }
        private static void SelectAll(bool select, IEnumerable<UnitVersion> models)
        {
            foreach (var model in models) {
                model.IsSelected.Value = select;
            }
        }
    }
}
