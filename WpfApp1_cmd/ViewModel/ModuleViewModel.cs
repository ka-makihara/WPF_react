using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfApp1_cmd.Models;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.ViewModel
{
    internal class ModuleViewModel : ViewModelBase
    {
        private ObservableCollection<UpdateInfo>? _updates = null;
        private void IsSelectedChk(UnitVersion item, bool? value)
        {
			if (Options.GetOptionBool("--diffVerOnly") == true)
			{
				// バージョンが違うものしかチェックが更新できないようにする
				var uv = _updates.FirstOrDefault(x => x.Name == item.Name);
				if( uv != null)
				{
					string newVer = uv.Version;
					if (newVer != item.CurVersion)
					{
						if (item.Attribute == Define.NOT_UPDATE)
						{
							item.IsSelected.Value = false;
						}
						else
						{
							item.IsSelected.Value = value;
						}
					}
					else
					{
						item.IsSelected.Value = false;
					}
				}
				else
				{
					item.IsSelected.Value = false;
				}
			}
			else
			{
				if (item.Attribute == Define.NOT_UPDATE)
				{
					item.IsSelected.Value = false;
				}
				else
				{
					item.IsSelected.Value = value;
				}
			}

			//item.UpdateParent(value);
			// IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
			OnPropertyChanged(nameof(IsAllSelected));
			//OnPropertyChanged(nameof(IsGroupChecked));
        }

        public ReactiveCollection<UnitVersion> UnitVersions { get; set; }

		public ReactivePropertySlim<bool> IsUnitSelectToggleEnabled { get; } = new ReactivePropertySlim<bool>(true);

		public ReactiveCommand<RoutedEventArgs> GroupCheckCommand { get; }

		public ReactiveCommand<UnitVersion> CheckBoxCommand { get; }
		public ReactiveCommand AllSelectCommand { get; }

		public string ModuleName { get; set; } = "Module";
		/// <summary>
		/// グループヘッダーのチェックボックス変更時の処理(コマンド)
		/// </summary>
		/// <param name="e"></param>
		private void GroupCheck(RoutedEventArgs e)
		{
			if(e.Source is CheckBox checkBox)
			{
				if (checkBox.DataContext is CollectionViewGroup group)
				{
					//bool? isChecked = group.Items.OfType<UnitVersion>().All(x => x.IsSelected.Value == true);
					bool isChecked = checkBox.IsChecked.Value;

					foreach (UnitVersion item in group.Items.Cast<UnitVersion>())
					{
						Debug.WriteLine($"{item.Name}:{item.CurVersion}->{item.NewVersion}");
						item.IsSelected.Value = isChecked;
					}
				}
			}
		}
		public ReactiveCommand<CollectionViewGroup> GroupCheckCommand2 { get; }
		private void GroupCheck2(CollectionViewGroup cvs)
		{
			foreach (UnitVersion item in cvs.Items.Cast<UnitVersion>())
			{
				bool isChecked = IsGroupChecked;
				{
					item.IsSelected.Value = isChecked;
				}
			}
		}
		public static bool IsUnitSelectToggleVisible
		{
			get
			{
				string mode = Options.GetOption("--mode", "user");
				return mode == "administrator";
			}
		}

		private void UpdateGroupCheck(UnitVersion m)
		{
			OnPropertyChanged(nameof(IsGroupChecked));
		}

		private void OnAllSelectCommandExecuted()
		{
			var newValue = IsAllSelected.Value == true;
			foreach (var unit in UnitVersions)
			{
				unit.IsSelected.Value = newValue;
			}
		}

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="name">モジュール名</param>
		/// <param name="unitVersions">バージョンリスト</param>
		/// <param name="updates">アップデートデータリスト</param>
		public ModuleViewModel(string name, ReactiveCollection<UnitVersion> unitVersions, ObservableCollection<UpdateInfo> updates)
        {
            UnitVersions = unitVersions;
            _updates = updates;
			ModuleName = "Transfer Selection : " + name;    //ビューのタイトルに表示する文字列

			ICollectionView cvs = CollectionViewSource.GetDefaultView(UnitVersions);
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("UnitGroup"));

			GroupCheckCommand = new ReactiveCommand<RoutedEventArgs>();
			GroupCheckCommand.Subscribe(e => GroupCheck(e));

			GroupCheckCommand2 = new ReactiveCommand<CollectionViewGroup>();
			GroupCheckCommand2.Subscribe(e => GroupCheck2(e));

			CheckBoxCommand = new ReactiveCommand<UnitVersion>();
			//CheckBoxCommand.Subscribe(item => IsSelectedChk(item, item.IsSelected.Value));
			CheckBoxCommand.Subscribe(x => UpdateGroupCheck(x));

			AllSelectCommand = new ReactiveCommand();
			AllSelectCommand.Subscribe(OnAllSelectCommandExecuted);

			//有効/無効が切り替わったときに、チェックボックスの有効/無効を切り替える
			IsUnitSelectToggleEnabled.Subscribe(x => IsUnitCheckBoxEnabled = x);

			foreach (var item in UnitVersions)
            {
                if (updates != null)
                {
					var uv = updates.FirstOrDefault(x => x.Name == item.Name);
					if( uv != null)
					{
						item.NewVersion = uv.Version;
						if (item.Attribute == Define.NOT_UPDATE)
						{
							item.IsSelected.Value = false;
						}
					}
					else
					{
						item.NewVersion = "N/A";
						item.IsSelected.Value = false;
					}
				}
				// Model 側でも Subscribeしているが、
				// ここで Subscribe しても上書きされない(Subscribe が重複して登録されるっぽい=>チェックボックス操作で両方が呼ばれる)
				item.IsSelected.Subscribe(_ =>
                {
                    OnPropertyChanged(nameof(IsAllSelected));
					OnPropertyChanged(nameof(IsSelectedGroup));
				});
            }
			// ユニット選択のトグルボタンの初期値
			IsUnitSelectToggleEnabled.Value = true;

			//ユニット選択のトグルボタン、チェックボックスの表示を切り替える(--mode=user/administrator, 未定義は administrator 以外とする)
			string mode = Options.GetOption("--mode", "user");
			IsUnitCheckBoxEnabled = (mode == "administrator");
			IsChkVisibled = (mode == "administrator");
		}

		/// <summary>
		/// All チェックボックスの状態を取得または設定します。
		/// </summary>
		public bool? IsAllSelected
        {
            get {
                var selected = UnitVersions.Select(item => item.IsSelected.Value).Distinct().ToList();
                return selected.Count == 1 ? selected.Single() : (bool?)null;
            }
            set {
                SelectAll(value, UnitVersions);
				OnPropertyChanged();
			}
        }
        private void SelectAll(bool? select, IEnumerable<UnitVersion> units)
        {
			if( _isCheckBoxEnabled == false)
			{
				return;
			}
			foreach (var unit in units)
			{
				//if (unit.Attribute != Define.NOT_UPDATE) {
					unit.IsSelected.Value = select;
				//}
            }
        }

		/// <summary>
		///  グループヘッダーのチェックボックス表示で、初期値としてチェックを入れるかどうか
		/// </summary>
		private bool _isSelectedGroup = true;
		public bool IsSelectedGroup
		{
			get
			{
				return _isSelectedGroup;
			}
			set
			{
				Debug.WriteLine($"IsSelectedGroup");
				SetProperty(ref _isSelectedGroup, value);
			}
		}

		/// <summary>
		/// チェックボックスの表示を行うかどうか(未使用)
		/// </summary>
		private bool _isChkVisibled;
		public bool IsChkVisibled
		{
			get => _isChkVisibled;
			set => SetProperty(ref _isChkVisibled, value);
		}

		/// <summary>
		/// チェックボックスを有効にするかどうか
		/// </summary>
		private bool _isCheckBoxEnabled;
		public bool IsUnitCheckBoxEnabled
		{
			get => _isCheckBoxEnabled;
			set => SetProperty(ref _isCheckBoxEnabled, value);
		}

		public bool IsGroupChecked
		{
			get;
			set;
		} = true;
		/*
		public bool? IsGroupChecked
        {
            get
            {
                ICollectionView cvs = CollectionViewSource.GetDefaultView(UnitVersions);
                if (cvs.Groups == null) return null;

                foreach (CollectionViewGroup group in cvs.Groups)
                {
                    var selected = group.Items.Cast<UnitVersion>().Select(item => item.IsSelected.Value).Distinct().ToList();
                    if (selected.Count != 1) return null;
                }
                return true;
            }
			set { }
        }
		*/
		
	}
}
