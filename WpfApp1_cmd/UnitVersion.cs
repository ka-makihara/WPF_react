using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd
{
    public class UnitVersion : ViewModelBase
    {
        private bool? _isSelected;
        private string? _name;
        private string? _curVersion;
        private string? _newVersion;

        public bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value )
                {
                    if (_newVersion != null && _newVersion == "N/A")
                    {
                        _isSelected = false;
                        SetProperty(ref _isSelected, false);
                    }
                    else
                    {
                        _isSelected = value;
                        SetProperty(ref _isSelected, value);
                    }
                }
            }
        }
        public string? Name 
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string? CurVersion
        {
            get => _curVersion;
            set => SetProperty(ref _curVersion, value);
        }
        public string? NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
        }

        public string? Path { get; set; }
        public string? Attribute { get; set; }
        public ModuleInfo? Parent { get; set; }
    }

    public class UpdateInfo : ViewModelBase
    {
        private string? _name;
        public string? Name
        {
            get => _name;
            set  {_name = value; SetProperty(ref _name, value); }
        }
        public string? Attribute { get; set; }
        private string? _version;
        public string? Version
        {
            get => _version;
            set { _version = value; SetProperty(ref _version, value); }
        }
        public string? Path { get; set; }
    }
}
