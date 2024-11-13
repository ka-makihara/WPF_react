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
        private ObservableCollection<LcuData> _lcuData;
        public ObservableCollection<LcuData> LcuData
        {
            get => _lcuData;
            set => SetProperty(ref _lcuData, value);
        }
        public LcuViewModel()
        {
            LcuData =
            [
                new (){ Name = "LCU1", Version = "1.0.0" },
                new (){ Name = "LCU2", Version = "1.0.0" },
                new (){ Name = "LCU3", Version = "1.0.0" },
            ];
            // IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
            foreach (var item in LcuData)
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
        public bool? IsAllSelected
        {
            get {
                var selected = LcuData.Select(item => item.IsSelected).Distinct().ToList();
                return selected.Count == 1 ? selected.Single() : (bool?)null;
            }
            set {
                if (value.HasValue) {
                    SelectAll(value.Value, LcuData);
                    OnPropertyChanged();
                }
            }
        }
        private static void SelectAll(bool select, IEnumerable<LcuData> models)
        {
            foreach (var model in models) {
                model.IsSelected = select;
            }
        }
    }

    public class LcuData : ViewModelBase
    {
        private bool _isSelected;
        private string _name = "";
        private string _version = "";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }
    }
}
