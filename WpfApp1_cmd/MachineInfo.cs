using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using WpfApp1_cmd.ViewModel;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd
{
    public class CheckableItem : ViewModelBase
    {
        private string? _name;
        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool? _isSelected;
        public virtual bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private MachineType? _itemType;
        public MachineType? ItemType { get => _itemType; set => SetProperty(ref _itemType, value); }
    }

    public class LcuInfo : CheckableItem
    {
        public override bool? IsSelected
        {
            get => base.IsSelected;
            set
            {
                if (base.IsSelected != value)
                {
                    base.IsSelected = value;
                    OnPropertyChanged(nameof(IsSelected));

                    if (Children != null )
                    {
                        foreach (var machine in Children)
                        {
                            machine.IsSelected = value;
                        }
                    }

                }
            }
        }
        public void Update(bool? value)
        {
            if (IsSelected != value)
            {
                if (Children != null)
                {
                    int cnt = Children.Where(x => x.IsSelected == true).ToList().Count();
                    if( cnt == 0)
                    {
                        base._isSelected = false;
                    }
                    else if( cnt != Children.Count())
                    {
                        base._isSelected = null;
                    }
                    else
                    {
                        base._isSelected = true;
                    }
                }
                else
                {
                    base._isSelected = value;
                }
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        private string? _ftpUser;
        public string? FtpUser { get => _ftpUser; set => SetProperty(ref _ftpUser, value); }

        private string? _ftpPassword;
        public string? FtpPassword { get => _ftpPassword; set => SetProperty(ref _ftpPassword, value); }

        private ObservableCollection<MachineInfo>? _machines;
        public ObservableCollection<MachineInfo>? Children { get => _machines; set => SetProperty(ref _machines, value); }

        public string Version { get; set; } = "V1.00";
        private LcuCtrl? _lcuCtrl;
        public LcuCtrl? LcuCtrl { get => _lcuCtrl; set =>  _lcuCtrl = value; }
    }

    public class MachineInfo : CheckableItem
    {
        public override bool? IsSelected
        {
            get => base.IsSelected;
            set
            {
                if(base.IsSelected != value)
                {
                    base.IsSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    if (Children != null && value != null)
                    {
                        foreach (var child in Children)
                        {
                            child.Update(value);
                        }
                    }
                    if(Parent != null)
                    {
                        Parent.Update(value);
                    }
                }
            }
        }
        public void Update(bool? value)
        {
            if (IsSelected != value)
            {
                if (Children != null)
                {
                    int cnt = Children.Where(x => x.IsSelected == true).ToList().Count();
                    if (cnt == 0) {
                        base._isSelected = false;
                    }
                    else if (cnt == Children.Count)
                    {
                        base._isSelected = true;
                    }
                    else
                    {
                        base._isSelected = null;
                    }
                }
                OnPropertyChanged(nameof(IsSelected));
                if( Parent != null)
                {
                    Parent.Update(value);
                }
            }
        }
        private Machine? _machine;
        public Machine? Machine { get => _machine; set => SetProperty(ref _machine, value); }

        public LcuInfo? Parent { get; set; }

        private ObservableCollection<BaseInfo>? _bases;
        public ObservableCollection<BaseInfo>?  Bases
        {
            get => _bases;
            set
            {
                _bases = value;
                SetProperty(ref _bases, value);
            }
        }
        private ObservableCollection<ModuleInfo>? _modules;
        public ObservableCollection<ModuleInfo>? Children
        {
            get
            {
                return _modules;
            }
            set
            {
                _modules = value;
            }
        }
        public string ModelName { get => Machine.ModelName; }
        public string MachineType { get => Machine.MachineType; }
    } 

    public class  BaseInfo : CheckableItem
    {
        public override bool? IsSelected
        {
            get => base.IsSelected;
            set
            {
                if (base.IsSelected != value)
                {
                    base.IsSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    if (Children != null)
                    {
                        foreach (var child in Children)
                        {
                            child.Update(value);
                        }
                    }
                    if(Parent != null)
                    {
                        Parent.Update( value);
                    }
                }
            }
        }
        public void Update(bool? value)
        {
            if (IsSelected != value)
            {
                if (Children != null)
                {
                    int cnt = Children.Where(x => x.IsSelected != true).ToList().Count();
                    if (cnt == 0) {
                        base._isSelected = false;
                    }
                    else if (cnt == Children.Count)
                    {
                        base._isSelected = false;
                    }
                    else
                    {
                        base._isSelected = null;
                    }
                }
                OnPropertyChanged(nameof(IsSelected));
                if( Parent != null)
                {
                    Parent.Update(value);
                }
            }
        }

        private Base? _base;
        public Base? Base { get => _base; set => SetProperty(ref _base, value); }

        private int _baseId;
        public int BaseId { get => _baseId; set => SetProperty(ref _baseId, value); }

        private int _baseType;
        public int BaseType { get => _baseType; set => SetProperty(ref _baseType, value); }

        private string? _ipAddress;
        public string? IPAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }

        private int _position;
        public int Position { get => _position; set => SetProperty(ref _position, value); }

        private string? _conveyor;
        public string? Conveyor { get => _conveyor; set => SetProperty(ref _conveyor, value); }

        public MachineInfo? Parent { get; set; }

        private ObservableCollection<ModuleInfo>? _modules;
        public ObservableCollection<ModuleInfo>? Children
        {
            get => _modules;
            set { _modules = value; SetProperty(ref _modules, value); }
        }
    }

    public class ModuleInfo : CheckableItem
    {
        public override bool? IsSelected
        {
            get => base.IsSelected;
            set
            {
                if (base.IsSelected != value)
                {
                    base.IsSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    if (Parent != null)
                    {
                        Parent.Update(value);
                    }
                }
            }
        }
        public void Update(bool? value)
        {
            if (IsSelected != value)
            {
                base._isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        private Module? _module;
        public Module? Module { get => _module; set => SetProperty(ref _module, value); }

        public MachineInfo? Parent { get; set; }

        // Treeで表示するためには Children が必要(表示させないためにプロパティを Units としている)
        private ObservableCollection<UnitInfo>? _units;
        public ObservableCollection<UnitInfo>? Units
        {
            get => _units;
            set { _units = value; SetProperty(ref _units, value); }
        }

        public int ID {get => Module.ModuleId; }
        public int Pos { get => Module.LogicalPos; }
        public string IPAddress { get; set; } = ""; // 本来Moduleにはない情報だが、アクセスの利便性のために追加
    }

    public class UnitInfo : CheckableItem
    {
        private string _version;
        public string Version
        {
            get => _version; set => SetProperty(ref _version, value);
        }
        private int _attribute;
        public int Attribute
        {
            get => _attribute; set => SetProperty(ref _attribute, value);
        }
        private string _path;
        public string Path
        {
            get => _path; set => SetProperty(ref _path, value);
        }
    }
}
