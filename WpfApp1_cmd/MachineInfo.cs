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
    public class MachineInfo : ViewModelBase
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
