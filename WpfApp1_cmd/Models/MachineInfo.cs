using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WpfApp1_cmd.ViewModel;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Models
{
    public class CheckableItem : ViewModelBase
    {
        private string? _name;
        public string? Name
        {
            get => _name;
            set => _name = value;
        }
        private MachineType? _itemType;
        public MachineType? ItemType { get => _itemType; set => _itemType = value; }

        public ReactivePropertySlim<bool?> IsSelected { get; set; } = new ReactivePropertySlim<bool?>(true);
        public ReactivePropertySlim<bool?> IsExpanded { get; set; } = new ReactivePropertySlim<bool?>(true);

        public CheckableItem()
        {
        }
    }

    public class LcuInfo : CheckableItem
    {
        //LcuInfo の Name は ライン名
        // LCUの名前は、LcuCtrl.Name で取得する
        //    LcuCtrl.Name は  LCUのPC名(==IPAddress)であり、アクセスに使用

        public void Update(bool? value)
        {
            if (IsSelected.Value != value)
            {
                if (Children != null)
                {
                    int cnt1 = Children.Where(x => x.IsSelected.Value == true).ToList().Count();
                    int cnt2 = Children.Where(x => x.IsSelected.Value == null).ToList().Count();
                    if (cnt1 == 0 && cnt2 == 0)
                    {
                        IsSelected.Value = false;
                    }
                    else
                    {
                        if (cnt1 == Children.Count())
                        {
                            IsSelected.Value = true;
                        }
                        else
                        {
                            IsSelected.Value = null;
                        }
                    }
                }
                else
                {
                    //IsSelected.Value = value;
                    IsSelected.Value = false;
                }
            }
            else
            {
                if (Children != null)
                {
                    if (value == null) { return; }
                    if (Children.Count() == 0)
                    {
                        IsSelected.Value = false;
                    }
                    else
                    {
                        foreach (var child in Children)
                        {
                            child.IsSelected.Value = value;
                        }
                    }
                }
            }
        }

        private string? _ftpUser;
        public string? FtpUser { get => _ftpUser; set => _ftpUser = value; }

        private string? _ftpPassword;
        public string? FtpPassword { get => _ftpPassword; set => _ftpPassword = value; }

        private ReactiveCollection<MachineInfo> _machines = [];
        public ReactiveCollection<MachineInfo> Children
        {
            get => _machines;
            set => _machines = value;
        }
        private void AddMachine(MachineInfo machine)
        {
            machine.Parent = this;
            //Debug.WriteLine($"AddMachine: {machine.Name}");
        }

        public LcuInfo(string name)
        {
            _machines.ObserveAddChanged().Subscribe(x => AddMachine(x));
            IsSelected.Subscribe(x => Update(x));

            _lcuCtrl = new(name);
        }


        public string Version { get; set; } = "V1.00";
        private LcuCtrl _lcuCtrl;
        public LcuCtrl LcuCtrl { get => _lcuCtrl; set => _lcuCtrl = value; }
        public LineInfo LineInfo { get; set; }
        public long DiskSpace { get; set; }
        public bool IsUpdateOk { get; set; } = true;
    }

    public class MachineInfo : CheckableItem
    {
        public void Update(bool? value)
        {
            if (IsSelected.Value != value)
            {
                if (Children != null)
                {
                    int cnt = Children.Where(x => x.IsSelected.Value == true).ToList().Count();
                    if (cnt == 0)
                    {
                        IsSelected.Value = false;
                    }
                    else if (cnt == Children.Count)
                    {
                        IsSelected.Value = true;
                    }
                    else
                    {
                        IsSelected.Value = null;
                    }
                }
                if (Parent != null)
                {
                    Parent.Update(value);
                }
            }
            else
            {
                if (Children != null)
                {
                    if (value == null) { return; }
                    foreach (var child in Children)
                    {
                        child.IsSelected.Value = value;
                    }
                }
                if (Parent != null)
                {
                    Parent.Update(value);
                }
            }
        }
        private Machine? _machine;
        public Machine? Machine { get => _machine; set => _machine = value; }

        public LcuInfo? Parent { get; set; }

        private ReactiveCollection<BaseInfo> _bases = [];
        public ReactiveCollection<BaseInfo> Bases
        {
            get => _bases;
            set => _bases = value;
        }
        private ReactiveCollection<ModuleInfo> _modules = [];
        public ReactiveCollection<ModuleInfo> Children
        {
            get => _modules;
            set => _modules = value;
        }

        public string ModelName { get => Machine.ModelName; }
        public string MachineType { get => Machine.MachineType; }

        private void AddBase(BaseInfo baseInfo)
        {
            baseInfo.Parent = this;
            //Debug.WriteLine($"AddBase: {baseInfo.Name}");
        }
        private void AddModule(ModuleInfo module)
        {
            module.Parent = this;
            //Debug.WriteLine($"AddModule: {module.Name}");
        }
        public MachineInfo()
        {
            _bases.ObserveAddChanged().Subscribe(x => AddBase(x));
            _modules.ObserveAddChanged().Subscribe(x => AddModule(x));
            IsSelected.Subscribe(x => Update(x));
        }

        public bool? IsParentSelected => Parent.IsSelected.Value;
    }

    public class BaseInfo : CheckableItem
    {
        public void Update(bool? value)
        {
            if (IsSelected.Value != value)
            {
                if (Children != null)
                {
                    int cnt = Children.Where(x => x.IsSelected.Value != true).ToList().Count();
                    if (cnt == 0)
                    {
                        IsSelected.Value = false;
                    }
                    else if (cnt == Children.Count)
                    {
                        IsSelected.Value = false;
                    }
                    else
                    {
                        IsSelected.Value = null;
                    }
                }
                if (Parent != null)
                {
                    Parent.Update(value);
                }
            }
        }

        private Base? _base;
        public Base? Base { get => _base; set => _base = value; }

        private int _baseId;
        public int BaseId { get => _baseId; set => _baseId = value; }

        private int _baseType;
        public int BaseType { get => _baseType; set => _baseType = value; }

        private string? _ipAddress;
        public string? IPAddress { get => _ipAddress; set => _ipAddress = value; }

        private int _position;
        public int Position { get => _position; set => _position = value; }

        private string? _conveyor;
        public string? Conveyor { get => _conveyor; set => _conveyor = value; }

        public MachineInfo? Parent { get; set; }

        private ReactiveCollection<ModuleInfo> _modules = [];
        public ReactiveCollection<ModuleInfo> Children
        {
            get => _modules;
            set => _modules = value;
        }
        private void AddModule(ModuleInfo module)
        {
            module.Parent = Parent;
            //Debug.WriteLine($"AddModule: {module.Name}");
        }
        public BaseInfo()
        {
            Children.ObserveAddChanged().Subscribe(x => AddModule(x));
            IsSelected.Subscribe(x => Update(x));
        }
    }

    public class ModuleInfo : CheckableItem
    {
        public void Update(bool? value)
        {
            if (Parent != null)
            {
                Parent.Update(value);
            }

            // 装置の選択で装置下の全モジュールの選択を変更する
            if (Options.GetOptionBool("--module_selection_unit", false) == true)
            {
                if (UpdateVersionInfo)
                {
                    foreach (var unit in UnitVersions)
                    {
                        unit.IsSelected.Value = value;
                    }
                }
            }
        }

        private bool UpdateVersionInfo { get; set; } = true;
        private Module? _module;
        public Module? Module { get => _module; set => _module = value; }

        public MachineInfo? Parent { get; set; }

        // Treeで表示するためには Children が必要(表示させないためにプロパティを UnitVersions としている)
        private ReactiveCollection<UnitVersion> _unitVersions = [];
        public ReactiveCollection<UnitVersion> UnitVersions
        {
            get => _unitVersions;
            set
            {
                _unitVersions = value;
                SetProperty(ref _unitVersions, value);
            }
        }
        /*
		private ObservableCollection<UnitVersion> _unitVersions = [];
		public ObservableCollection<UnitVersion> UnitVersions
		{
			get => _unitVersions;
			set => _unitVersions = value;
		}
		*/
        public void SetCheck(bool? value)
        {
            int cnt = UnitVersions.Where(x => x.IsSelected.Value == true).ToList().Count();

            //モジュールの選択から、呼ばれるので、UpdateVersionInfoをfalseにしておく
            //   false にして、IsSelect の Subscribe のUpdate で UnitVersion の更新が行われないようにする
            //   これをしないと、UnitVersion の更新が行われて、また、この関数がよばれて、無限ループとなる
            UpdateVersionInfo = false;

            if (cnt == 0)
            {
                IsSelected.Value = false;
            }
            else if (cnt == UnitVersions.Count)
            {
                IsSelected.Value = true;
            }
            else
            {
                IsSelected.Value = null;
            }
            UpdateVersionInfo = true;
        }

        public int ID { get => Module.ModuleId; }
        public int Pos { get => Module.LogicalPos; }
        public string IPAddress { get; set; } = ""; // 本来Moduleにはない情報だが、アクセスの利便性のために追加
        public IniFileParser? UpdateInfo { get; set; } = null;

        public List<string> UpdateFiles()
        {
            List<string> paths = [];

            if (UpdateInfo != null)
            {
                foreach (var sec in UpdateInfo.SectionCount())
                {
                    paths.Add(UpdateInfo.GetValue(sec, "Path"));
                }
            }
            return paths;
        }
        private void AddUnitVersion(UnitVersion unitVersion)
        {
            unitVersion.Parent = this;
            Debug.WriteLine($"AddUnitVersion: {unitVersion.Name}");
        }

        public ModuleInfo()
        {
            IsSelected.Subscribe(x => Update(x));
            UnitVersions.ObserveAddChanged().Subscribe(x => AddUnitVersion(x));
        }
    }

	/*
    public class UnitInfo : CheckableItem
    {
        private string _version;
        public string Version
        {
            get => _version; set => _version = value;
        }
        private int _attribute;
        public int Attribute
        {
            get => _attribute; set => _attribute = value;
        }
        private string _path;
        public string Path
        {
            get => _path; set => _path = value;
        }
    }
	*/
}
