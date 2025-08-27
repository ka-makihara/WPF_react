using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1_cmd.ViewModel;
using WpfApp1_cmd.Resources;

namespace WpfApp1_cmd.Models
{
    public class UnitVersion : CheckableItem
    {
        private string? _curVersion;
        private string? _newVersion;

		//public ReactivePropertySlim<bool?> IsSelected { get; set; } = new ReactivePropertySlim<bool?>(false);
        public ReactivePropertySlim<bool?> IsUpdated { get; set; } = new ReactivePropertySlim<bool?>(false);

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

		/// <summary>
		///  チェックボックスのツールチップ表示
		/// </summary>
		public string UpdateStatus
		{
			get {
				MachineInfo? mcInfo = Parent?.GetMachineInfo();
				if( mcInfo == null)
				{
					return Resource.Update_na;  //対象外
				}
				if( mcInfo.Status == ItemStatus.NOT_SUPPORTED || mcInfo.Status == ItemStatus.UNKNOWN)
				{
					return Resource.UnitsOnUnsupportedModels;// "未サポート機種上のユニット";
				}

				if ( Attribute == Define.NOT_UPDATE)
				{
					return Resource.Update_Prohibited;  //アップデート禁止
				}
				else if (CurVersion == NewVersion)
				{
					return Resource.Update_sameVersion;  //同一バージョン
				}
				else if( CurVersion == "N/A")
				{
					return Resource.Current_na;  //現在のバージョンが不明(装置に存在しない等)
				}
				else if( NewVersion == "N/A")
				{
					return Resource.Update_na;  //対象外
				}
				else
				{
					if( IsSelected.Value == true)
					{
						return Resource.Update_selected;  //選択中
					}
					else
					{
						//return Resource.Update_allowed;  //アップデート許可
						return Resource.Update_notSelected;  //未選択
					}
					//return Resource.Update_allowed;  //アップデート許可
				}
			}
		}

		public string? UnitGroup { get; set; } = "";

        public string Path { get; set; } = "";
        public string FuserPath { get; set; } = "";
        public int    Attribute { get; set; }
        public long   Size { get; set; }
        public ModuleInfo? Parent { get; set; }

		public void Update(bool? value)
		{
			//ModuleInfoの更新
			Parent?.UpdateSelf(value);
		}

		public int GetFileCount()
		{
			int cnt = 0;
			
			cnt += (Path != "") ? 1 : 0;
			cnt += (FuserPath != "") ? 1 : 0;

			return cnt;
		}

        public UnitVersion(bool sel)
        {
			if( Attribute == Define.NOT_UPDATE)
			{
				IsSelected.Value = false;
			}
			else
			{
				IsSelected.Value = sel;
			}
			IsSelected.Subscribe(x => Update(x));
		}

		/// <summary>
		///  ※グループチェックボックスのプロパティ IsChecked にバインドしているため必要だが
		///  　値はCoverter(GroupToIsCheckedConverter)で設定した値を利用しているため、ここでは何もしない(と思う)
		/// </summary>
		private bool? _isChecked;
		public bool? IsChecked
		{
			get => _isChecked;
			set
			{
				SetProperty(ref _isChecked, value);
			}
		}
	}

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

		public bool IsVisibled { get; set; } = true;
		public bool IsSelected { get; set; } = true;
	}

}
