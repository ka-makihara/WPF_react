using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.ViewModel
{
    internal class ModuleViewModel : ViewModelBase
    {
        /*
        private ObservableCollection<UnitVersion> _unitVersions;
        public ObservableCollection<UnitVersion> UnitVersions
        {
            get => _unitVersions;
            set
            {
                _unitVersions = value;
                SetProperty(ref _unitVersions, value);
            } 
        }
        */

        public ObservableCollection<UnitVersion> UnitVersions { get; set; }
        public ModuleViewModel(ObservableCollection<UnitVersion> unitVersions, ObservableCollection<UpdateInfo> updates)
        {
            //LoadUnitVersions();
            UnitVersions = unitVersions;

            // IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
            foreach (var item in UnitVersions)
            {
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(UnitVersion.IsSelected))
                    {
                        OnPropertyChanged(nameof(IsAllSelected));
                    }
                };
                if (updates != null)
                {
                    try {
                        // 要素が見つからない場合は例外が発生する
                        string newVer = updates.First(x => x.Name == item.Name).Version;
                        if(newVer != item.CurVersion)
                        {
                            item.NewVersion = newVer;
                            item.IsSelected = true;
                        }
                        else
                        {
                            item.NewVersion = newVer;
                            item.IsSelected = false;
                        }
                    }
                    catch (Exception e)
                    {
                        item.NewVersion = "N/A";
                        item.IsSelected = false;
                    }
                }
            }
        }

        private void LoadUnitVersions()
        {
            UnitVersions = new ObservableCollection<UnitVersion>
            {
                new UnitVersion { IsSelected = true,  Name = "Unit1", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = false, Name = "Unit2", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = true,  Name = "Unit3", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = false, Name = "Unit4", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = true,  Name = "Unit5", CurVersion = "1.0.0", NewVersion = "1.0.1" },
            };
        }
        public bool? IsAllSelected
        {
            get {
                var selected = UnitVersions.Select(item => item.IsSelected).Distinct().ToList();
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
                model.IsSelected = select;
            }
        }
    }
}
