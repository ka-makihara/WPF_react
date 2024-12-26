using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd.Models
{
    public class UnitVersion : ViewModelBase
    {
        private string? _name;
        private string? _curVersion;
        private string? _newVersion;

        public ReactivePropertySlim<bool?> IsSelected { get; set; } = new ReactivePropertySlim<bool?>(true);
        public ReactivePropertySlim<bool?> IsUpdated { get; set; } = new ReactivePropertySlim<bool?>(false);

        public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string? CurVersion
        {
            get => _curVersion;
            set => SetProperty(ref _curVersion,value);
        }
        public string? NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
        }

        public string Path { get; set; } = "";
        public string FuserPath { get; set; } = "";
        public int    Attribute { get; set; }
        public long   Size { get; set; }
        public ModuleInfo? Parent { get; set; }

        private void Update(bool? value)
        {
            if (Parent != null)
            {
                Parent.SetCheck(value);
            }
        }
        public UnitVersion()
        {
            IsSelected.Subscribe(x => Update(x));
        }
    }

	/*
	public class UnitInfo : ViewModelBase
    {
        private string? _name;
        private string? _curVersion;
        private string? _newVersion;

        public ReactivePropertySlim<bool?> IsSelected { get; set; } = new ReactivePropertySlim<bool?>(true);
        //public ReactivePropertySlim<bool?> IsUpdated { get; set; } = new ReactivePropertySlim<bool?>(false);

		public ReactivePropertySlim<bool?> IsEnabled { get; set; } = new ReactivePropertySlim<bool?>(true);
		public ReactivePropertySlim<bool?> IsVisibled { get; set; } = new ReactivePropertySlim<bool?>(true);

		public string? Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string? CurVersion
        {
            get => _curVersion;
            set => SetProperty(ref _curVersion,value);
        }
        public string? NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
        }

        public string Path { get; set; } = "";
        public string FuserPath { get; set; } = "";
        public int    Attribute { get; set; }
        public long   Size { get; set; }
        public ModuleInfo? Parent { get; set; }

        private void Update(bool? value)
        {
            if (Parent != null)
            {
                Parent.SetCheck(value);
            }
        }
        public UnitInfo()
        {
            IsSelected.Subscribe(x => Update(x));
        }
    }
	*/

	/// <summary>
	///  UpdateCommon.inf データ
	/// </summary>
    public class UpdateInfo// : ViewModelBase
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set => _name = value;
        }
        public string? Attribute { get; set; }
        private string _version = "N/A";
        public string Version
        {
            get => _version;
            set => _version = value;
        }
        public string? Path { get; set; }
		public string? FuserPath { get; set; }

		public string? UnitGroup { get; set; } = "";
	}

}
