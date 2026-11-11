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
		private bool _isSupported = true;

		//public ReactivePropertySlim<bool?> IsSelected { get; set; } = new ReactivePropertySlim<bool?>(false);
        public ReactivePropertySlim<bool?> IsUpdated { get; set; } = new ReactivePropertySlim<bool?>(false);

        public string? CurVersion
        {
            get => _curVersion;
            set
            {
                if (SetProperty(ref _curVersion, value))
                {
                    OnPropertyChanged(nameof(UpdateStatus));
                }
            }
        }
        public string? NewVersion
        {
            get => _newVersion;
            set
            {
                if (SetProperty(ref _newVersion, value))
                {
                    OnPropertyChanged(nameof(UpdateStatus));
                }
            }
        }

		/// <summary>
		///  チェックボックスのツールチップ、状態列の表示
		/// </summary>
		public string UpdateStatus
		{
			get {
				if (!IsSupported)
				{
					return Resource.NotApplicable;
				}

				MachineInfo? mcInfo = Parent?.GetMachineInfo();
				if( mcInfo == null)
				{
					return Resource.Update_na;  //対象外
				}
				if( mcInfo.Status == ItemStatus.NOT_SUPPORTED || mcInfo.Status == ItemStatus.UNKNOWN)
				{
					return Resource.UnitsOnUnsupportedModels;// "未サポート機種上のユニット";
				}

				if ( IsUpdateProhibited)
				{
					return Resource.Update_Prohibited;  //アップデート禁止
				}
				else if (IsBlockedByMissingFile)
				{
					return Resource.Update_fileNotFound;  //アップデートファイルなし
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

		/// <summary>
		///  アップデートデータ側に更新ファイルが存在しない(一覧表示のみで選択不可)
		/// </summary>
		public bool IsUpdateFileMissing { get; set; }

		public bool IsUpdateProhibited => Attribute == Define.NOT_UPDATE;

		public bool IsSupported
		{
			get => _isSupported;
			set
			{
				if (SetProperty(ref _isSupported, value))
				{
					OnPropertyChanged(nameof(UpdateStatus));
					OnPropertyChanged(nameof(IsUpdatable));
					if (!value)
					{
						IsSelected.Value = false;
					}
				}
			}
		}

		/// <summary>
		///  更新ファイル無しのため選択不可
		/// </summary>
		public bool IsBlockedByMissingFile => IsUpdateFileMissing;

		public bool IsUpdatable => IsSupported && !IsUpdateProhibited && !IsBlockedByMissingFile;

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
			IsSelected.Subscribe(x =>
			{
				if ((!IsSupported || IsBlockedByMissingFile) && x != false)
				{
					IsSelected.Value = false;
					return;
				}
				Update(x);
				OnPropertyChanged(nameof(UpdateStatus));
			});
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

		public bool IsExistPath(string? path)
		{
			if( path == null )
			{
				return false;
			}
			if ( Path == path || FuserPath == path)
			{
				return true;
			}
			return false;
		}
	}

}
