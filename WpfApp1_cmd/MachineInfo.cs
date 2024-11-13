using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WpfApp1_cmd.ViewModel;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd
{
    /*
    public class InfoBase : ViewModelBase
    {
        private bool? _isSelected;
        public bool? IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string? _name;
        public string? Name { get => _name; set => SetProperty(ref _name, value); }
    }
    */

    public class CheckableItem : ViewModelBase
    {
        private string? _name;
        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private bool? _isChecked;
        public  virtual bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if( _isChecked != value )
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        } 
    }

    public class MachineInfo : ViewModelBase
    {
        private bool? _isSelected;
        public bool? IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string? _name;
        public string? Name { get => _name; set => SetProperty(ref _name, value); }

        private string? _boardFlow;
        public string? BoardFlow { get => _boardFlow; set => SetProperty(ref _boardFlow, value); }

        private string? _machineType;
        public string? MachineType { get => _machineType; set => SetProperty(ref _machineType, value); }

        private int _maxLane;
        public int MaxLane { get => _maxLane; set => SetProperty(ref _maxLane, value); }

        private string? _modelName;
        public string? ModelName { get => _modelName; set => SetProperty(ref _modelName, value); }

        private ObservableCollection<BaseInfo>? _bases;
        public ObservableCollection<BaseInfo>? Bases { get => _bases; set { _bases = value; SetProperty(ref _bases, value); } }
    } 

    public class  BaseInfo : ViewModelBase
    {
        private bool? _isSelected;
        public bool? IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string? _name;
        public string? Name { get => _name; set => SetProperty(ref _name, value); }

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

        private ObservableCollection<ModuleInfo>? _modules;
        public ObservableCollection<ModuleInfo>? Modules
        {
            get => _modules;
            set { _modules = value; SetProperty(ref _modules, value); }
        }
    }

    public class ModuleInfo : ViewModelBase
    {
        private bool? _isSelected;
        public bool? IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private string? _name;
        public string? Name { get => _name; set => SetProperty(ref _name, value); }

        private int _moduleId;
        public int ModuleId { get => _moduleId; set => SetProperty(ref _moduleId, value); }

        private int _moduleType;
        public int ModuleType { get => _moduleType; set => SetProperty(ref _moduleType, value); }

        private int _logicalPos;
        public int LogicalPos { get => _logicalPos; set => SetProperty(ref _logicalPos, value); }

        private int _physicalPos;
        public int PhysicalPos { get => _physicalPos; set => SetProperty(ref _physicalPos, value); }
    }

/*
    public class ModuleInfo : ViewModelBase
    {
        private bool? _isSelected;
        private string? _name;
        private string? _ipAddress;
        private int _id;
        private int _pos;

        public bool? IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        public string? Name 
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? IPAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int ID 
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        public int Pos
        {
            get => _pos;
            set => SetProperty(ref _pos, value);
        }
    }
*/
    public class TreeItem : ViewModelBase
    {
        private string? _name;
        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        private MachineType? _itemType;
        public MachineType? ItemType
        {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }

        private bool? _isChecked;
        public  virtual bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if( _isChecked != value )
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        } 
    }

    public class TreeItemModule : TreeItem
    {
        public Base? Base { get; set; }
        public Module? Module { get; set; }
        public TreeItemMachine? Parent { get; set; }
        public ObservableCollection<UpdateInfo>? UnitUpdateInfo { get; set; }
        public override bool? IsChecked
        {
            get => base.IsChecked;
            set
            {
                if (base.IsChecked != value)
                {
                    base.IsChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    if (Parent != null)
                    {
                        Parent.Update(this,value);
                    }
                }
            }
        }
        public void Update(TreeItem item, bool? value)
        {
            if (IsChecked != value)
            {
                base.IsChecked = value;
            }
        }
        public TreeItemLcu? GetLcu()
        {
            if(Parent != null)
            {
                return Parent.GetLcu();
            }
            return null;
        }
        public TreeItemMachine? GetMachine()
        {
            if (Parent != null)
            {
                return Parent;
            }
            return null;
        }
    }

    public class TreeItemMachine : TreeItem
    {
        public Machine? Machine { get; set; }
        public TreeItemLcu? Parent { get; set; }
        public ObservableCollection<TreeItemModule>? Children { get; set; }
        public void Update(TreeItem item, bool? value)
        {
            if (IsChecked != value)
            {
                base.IsChecked = value;
            }
        }
        public override bool? IsChecked
        {
            get => base.IsChecked;
            set
            {
                if (base.IsChecked != value)
                {
                    base.IsChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    if (Children != null)
                    {
                        foreach (var child in Children)
                        {
                            child.Update(this,value);
                        }
                    }
                    if (Parent != null)
                    {
                        Parent.Update(this,value);
                    }
                }
            }
        }
        public TreeItemLcu? GetLcu()
        {
            if (Parent != null)
            {
                return Parent;
            }
            return null;
        }
    }

    public class TreeItemLcu(string name) : TreeItem//ViewModelBase
    {
        private LcuCtrl _lcuCtrl = new LcuCtrl(name);
        public LcuCtrl LcuCtrl
        {
            get => _lcuCtrl;
            set => SetProperty(ref _lcuCtrl, value);
        }
        private ObservableCollection<TreeItemMachine> _children = [];
        public ObservableCollection<TreeItemMachine>? Children
        {
            get => _children;
            set => _children = value;
        }

        private string? _ftpUser;
        public string? FtpUser
        {
            get => _ftpUser;
            set => _ftpUser = value;
        }

        private string? _ftpPassword;
        public string? FtpPassword
        {
            get => _ftpPassword;
            set => _ftpPassword = value;
        }

        public void Update(TreeItem item, bool? value)
        {
            if (IsChecked != value)
            {
                base.IsChecked = value;
            }
        }
        public override bool? IsChecked
        {
            get => base.IsChecked;
            set
            {
                if (base.IsChecked != value)
                {
                    base.IsChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    if (Children != null)
                    {
                        foreach (var machine in Children)
                        {
                            machine.Update(this, value);
                        }
                    }
                }
            }
        }
    }
}
