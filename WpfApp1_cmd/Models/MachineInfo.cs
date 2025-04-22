using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
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
        public ReactivePropertySlim<bool?> IsExpanded { get; set; } = new ReactivePropertySlim<bool?>(false);

		private string? _toolTipText;
		public string? ToolTipText
		{
			get => _toolTipText;
			set => SetProperty(ref _toolTipText, value);
		}

		private ErrorCode _errCode = ErrorCode.OK;
		public ErrorCode ErrCode
		{
			get => _errCode;
			set
			{
				SetProperty(ref _errCode, value);
				if( _errCode != ErrorCode.OK)
				{
					IsSelected.Value = false;
				}
			}
		}
		public ItemStatus Status { get; set; } = ItemStatus.OK;

		public BitmapImage ImagePath { get; set; }

		public CheckableItem()
        {
			ToolTipText = "No information.";
			//ImagePath = new BitmapImage(new Uri("pack://application:,,,/Resources/warning.png"));
		}

		public string GetViewName()
		{
			string viewName = "";

			if (this is LcuInfo lcuInfo)
			{
				viewName = $"LcuView_{Name}";
			}
			else if (this is MachineInfo machineInfo)
			{
				string lcuName = machineInfo.Parent.Name;
				viewName = $"MachineView_{lcuName}_{Name}";
			}
			else if (this is ModuleInfo moduleInfo)
			{
				string machineName = moduleInfo.Parent.Name;
				viewName = $"ModuleView_{machineName}_{Name}";
			}
			return viewName;
		}
	}

    public class LcuInfo : CheckableItem
    {
        //LcuInfo の Name は ライン名
        // LCUの名前は、LcuCtrl.Name で取得する
        //    LcuCtrl.Name は  LCUのPC名(==IPAddress)であり、アクセスに使用

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

		/// <summary>
		/// 子の選択状態を更新(全て選択/全て選択解除, viewModelで使用)
		/// </summary>
		/// <param name="value"></param>
		public void UpdateChildren(bool? value)
		{
			foreach( var machine in Children)
			{
				machine.IsSelected.Value = value;
			}
		}

		public void UpdateSelf(bool? value)
		{
			if( IsSelected.Value == value)
			{
				return;
			}
			IsSelected.Value = Utility.CheckState(Children);
/*
			int trueCount = Children.Count(x => x.IsSelected.Value == true);
			int nullCount = Children.Count(x => x.IsSelected.Value == null);

			if (value == true)
			{
				//全ての子が選択されている==true, 一つでも選択されていない==null
				IsSelected.Value = (trueCount == Children.Count) ? true : null ;
			}
			else
			{
				//子が一つでも選択されている==null, 全て選択されていない==false
				IsSelected.Value = (trueCount == 0 && nullCount == 0) ? false : null ;
			}
*/
		}
		public void Update(bool? value)
		{
			//親がいないので何もしない
		}

		public LcuInfo(string name, int id=0)
        {
			IsSelected.Value = true;
			LcuId = id;
			_lcuCtrl = new(name,id);

			_machines.ObserveAddChanged().Subscribe(x => AddMachine(x));
			IsSelected.Subscribe(x => Update(x));
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
        }
        private void AddModule(ModuleInfo module)
        {
            module.Parent = this;
        }

		public void UpdateSelf(bool? value)
		{
			int trueCount = Children.Count(x => x.IsSelected.Value == true);
			int nullCount = Children.Count(x => x.IsSelected.Value == null);

			if (value == true)
			{
				//全ての子が選択されている==true, 一つでも選択されていない==null
				IsSelected.Value = (trueCount == Children.Count) ? true : null ;
			}
			else
			{
				//子が一つでも選択されている==null, 全て選択されていない==false
				IsSelected.Value = (nullCount == 0 && trueCount == 0) ? false : null ;
			}
			Parent?.UpdateSelf(value);
		}

		/// <summary>
		/// 子の選択状態を更新(全て選択/全て選択解除, viewModelで使用)
		/// </summary>
		/// <param name="value"></param>
		public void UpdateChildren(bool? value)
		{
			foreach (var module in Children)
			{
				module.IsSelected.Value = value;
			}
		}

		public void Update(bool? value)
		{
			Parent?.UpdateSelf(value);
		}

        public MachineInfo()
        {
            _bases.ObserveAddChanged().Subscribe(x => AddBase(x));
            _modules.ObserveAddChanged().Subscribe(x => AddModule(x));
            IsSelected.Subscribe(x => Update(x));
        }
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
		public string[] UpdateStrings;

		public void SetUpdateInfo(string path)
		{
			UpdateInfo = new IniFileParser(path);

			//UpdateCommon.inf　を読み込んでおく
			UpdateStrings = System.IO.File.ReadAllLines(path, Encoding.GetEncoding(Define.TXT_ENCODING));	
		}

		public List<string> UpdateFiles(Options opt)
        {
            List<string> paths = [];

            if (UpdateInfo != null)
            {
                foreach (var sec in UpdateInfo.SectionCount())
                {
					string p = UpdateInfo.GetValue(sec, "Path");

					if (opt.ContainsExt(Path.GetExtension(p)) == false)
					{
						continue;	
					}

					paths.Add(UpdateInfo.GetValue(sec, "Path"));
					var d = UpdateInfo.GetValue(sec, "FuserPath");
					if( d != "" )
					{
						paths.Add(d);
					}
				}
            }
            return paths;
        }
        private void AddUnitVersion(UnitVersion unitVersion)
        {
            unitVersion.Parent = this;
            Debug.WriteLine($"AddUnitVersion: {unitVersion.Name}");
        }

		public void UpdateSelf(bool? value)
		{
			int trueCount = UnitVersions.Count(x => x.IsSelected.Value == true);
			int nullCount = UnitVersions.Count(x => x.IsSelected.Value == null);

			if (value == true)
			{
				//全ての子が選択されている==true, 一つでも選択されていない==null
				IsSelected.Value = (trueCount == UnitVersions.Count) ? true : null ;
			}
			else
			{
				//子が一つでも選択されている==null, 全て選択されていない==false
				IsSelected.Value = (trueCount == 0 && nullCount == 0) ? false : null ;
			}
		}

		/// <summary>
		/// 子の選択状態を更新(全て選択/全て選択解除, viewModelで使用)
		/// </summary>
		/// <param name="value"></param>
		public void UpdateChildren(bool? value)
		{
			foreach (var unit in UnitVersions)
			{
				unit.IsSelected.Value = value;
			}
		}

		public void Update(bool? value)
		{
			Parent?.UpdateSelf(value);
		}

		public ModuleInfo()
        {
            UnitVersions.ObserveAddChanged().Subscribe(x => AddUnitVersion(x));
            IsSelected.Subscribe(x => Update(x));
        }
    }
}
