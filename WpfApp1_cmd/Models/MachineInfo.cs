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

		private void CheckSelf()
		{
			int cntTrue = Children.Where(x => x.IsSelected.Value == true).ToList().Count();
			int cntNull = Children.Where(x => x.IsSelected.Value == null).ToList().Count();
			if (cntTrue == 0 && cntNull == 0)
			{
				IsSelected.Value = false;
			}
			else if ((cntTrue + cntNull) == Children.Count)
			{
				IsSelected.Value = true;
			}
			else
			{
				IsSelected.Value = null;
			}
		}

		public void UpdateParent(bool? value)
		{
			CheckSelf();

			//LcuInfo は最上位なので、親がいない
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
        }

        public LcuInfo(string name, int id=0)
        {
            _machines.ObserveAddChanged().Subscribe(x => AddMachine(x));
			//IsSelected.Subscribe(x => Update(x));

			LcuId = id;
			_lcuCtrl = new(name,id);
        }


        public string Version { get; set; } = "V1.00";
        private LcuCtrl _lcuCtrl;
        public LcuCtrl LcuCtrl { get => _lcuCtrl; set => _lcuCtrl = value; }
        public LineInfo LineInfo { get; set; }
        public long DiskSpace { get; set; }
        public bool IsUpdateOk { get; set; } = true;
		public int LcuId { get; set; }
	}

    public class MachineInfo : CheckableItem
    {
		private void CheckSelf()
		{
			int cntTrue = Children.Where(x => x.IsSelected.Value == true).ToList().Count();
			int cntNull = Children.Where(x => x.IsSelected.Value == null).ToList().Count();

			if (cntTrue == 0 && cntNull == 0)
            {
                IsSelected.Value = false;
            }
            else if ((cntTrue + cntNull) == Children.Count)
            {
                IsSelected.Value = true;
            }
            else
            {
                IsSelected.Value = null;
            }
		}

		public void UpdateParent(bool? value)
		{
			CheckSelf();	
			Parent?.UpdateParent(value);
		}
		public void UpdateChildren(bool? value)
		{
			CheckSelf();
			foreach (var child in Children)
			{
				child.UpdateChildren(value);
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
            //IsSelected.Subscribe(x => Update(x));
        }

        //public bool? IsParentSelected => Parent.IsSelected.Value;
    }

    public class BaseInfo : CheckableItem
    {
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
        }
    }

    public class ModuleInfo : CheckableItem
    {
		private void CheckSelf()
		{
			// ユニットバージョンの選択状態は true/false のみ
            int cnt = UnitVersions.Where(x => x.IsSelected.Value == true).ToList().Count();

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
		}

	    public void UpdateParent(bool? value)
        {
			CheckSelf();	
			Parent?.UpdateParent(value);
        }

		public void UpdateChildren(bool? value)
		{
			CheckSelf();

			foreach (var unit in UnitVersions)
			{
				unit.IsSelected.Value = value;
			}
		}

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
            UnitVersions.ObserveAddChanged().Subscribe(x => AddUnitVersion(x));
            //IsSelected.Subscribe(x => Update(x));
        }
    }
}
