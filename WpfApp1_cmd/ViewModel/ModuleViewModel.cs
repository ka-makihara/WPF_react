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
using System.Windows.Navigation;
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
        }

        public ReactiveCollection<UnitVersion> UnitVersions { get; set; }

		public ReactivePropertySlim<bool> IsUnitSelectToggleEnabled { get; } = new ReactivePropertySlim<bool>(true);

		public ReactiveCommand AllSelectCommand { get; }

		public ReactiveCommand<UnitVersion> CheckBoxCommand { get; }            // 個々のアイテムのチェックボックスの変更イベント
		public ReactiveCommand<RoutedEventArgs> GroupCheckClick { get; }        // グループヘッダーのチェックボックスのクリックイベント
		public ReactiveCommand<CollectionViewGroup> GroupCheckCommand { get; }	// グループヘッダーのチェックボックス・コマンド	

		public string ModuleName { get; set; } = "Module";
		/// <summary>
		/// グループヘッダーのチェックボックス変更時の処理(クリックイベント)
		///  コマンド実行後によびだされる
		/// </summary>
		/// <param name="e"></param>
		private void GroupCheckClickEvent(RoutedEventArgs e)
		{
			if(e.Source is CheckBox checkBox)
			{
				//bool? isChecked = group.Items.OfType<UnitVersion>().All(x => x.IsSelected.Value == true);
				bool? isChecked = checkBox.IsChecked;
				if (checkBox.DataContext is CollectionViewGroup group)
				{
					foreach (UnitVersion item in group.Items.Cast<UnitVersion>())
					{
						Debug.WriteLine($"{item.Name}:{item.CurVersion}->{item.NewVersion}");
						item.IsSelected.Value = isChecked;
					}
				}
			}
		}

		/// <summary>
		/// ユニット選択のトグルボタンの表示を切り替える
		///   ユーザーモードでは表示しない -> ユニット毎のチェックボックスを表示しない
		/// </summary>
		public static bool IsUnitSelectToggleVisible
		{
			get
			{
				string mode = Options.GetOption("--mode", "user");
				return mode == "administrator";
			}
		}

		/// <summary>
		/// 全選択コマンド実行時の処理
		/// </summary>
		private void OnAllSelectCommandExecuted()
		{
			var newValue = IsAllSelected.Value == true;
			foreach (var unit in UnitVersions)
			{
				unit.IsSelected.Value = newValue;
			}
		}

		public ICollectionView UnitVersionGroupView {get; set; }

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

			UnitVersionGroupView = CollectionViewSource.GetDefaultView(UnitVersions);
            UnitVersionGroupView.GroupDescriptions.Add(new PropertyGroupDescription("UnitGroup"));

			// グループヘッダーのチェックボックスの Click イベント
			GroupCheckClick = new ReactiveCommand<RoutedEventArgs>();
			GroupCheckClick.Subscribe(e => GroupCheckClickEvent(e));

			//グループヘッダーのチェックボックスの変更イベント
			GroupCheckCommand = new ReactiveCommand<CollectionViewGroup>();
			GroupCheckCommand.Subscribe(e => GroupCheckCmd(e));

			//個々のユニットのチェックボックスの変更イベント
			CheckBoxCommand = new ReactiveCommand<UnitVersion>();
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
					UpdateGroupCheck(null);
				});
            }

			//ユニット選択のトグルボタン、チェックボックスの表示を切り替える(--mode=user/administrator, 未定義は administrator 以外とする)
			string mode = Options.GetOption("--mode", "user");

			IsUnitCheckBoxEnabled = (mode == "administrator");  // ユニット選択のトグルボタンの初期値(administrator のときのみ有効)
			IsChkVisibled = (mode == "administrator");          // ユニットチェックボックスの表示の初期値(administrator のときのみ表示)
		}

		/// <summary>
		/// All チェックボックスの状態を取得または設定します。
		/// </summary>
		public bool? IsAllSelected
        {
            get {
                return Utility.CheckState(UnitVersions);
			}
            set {
                SelectAll(value, UnitVersions);
				//OnPropertyChanged();
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
			UpdateGroupCheck(null);
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

		// グループインデックス(IsGroupCheckedがグループヘッダーのチェックボックスの状態を取得するために使用)
		//   プロパティの更新で呼び出されるが、グループの数分だけ呼び出されるため、インデックスを保持しておく
		//   もっと良い方法がありそうな気はするが、ヘッダーに配置されたチェックボックスの状態を設定する方法が分からないので。
		private int _groupIdx = 0;
		public bool? IsGroupChecked
		{
			get
			{
				//UnitVersionGroupView.Groups.Cast<CollectionViewGroup>().ToList().ForEach(group => Debug.WriteLine($"{group.Name}"));
				if (UnitVersionGroupView.Groups.Count <= _groupIdx)
				{
					return null;
				}
				// グループ名を取得
				var groupName = UnitVersionGroupView.Groups.Cast<CollectionViewGroup>().ToList()[_groupIdx].Name;
				var group = UnitVersionGroupView.Groups.Cast<CollectionViewGroup>().ToList()[_groupIdx].Items;

				int trueCount = group.Cast<UnitVersion>().Count(x => x.IsSelected.Value == true);

				Debug.WriteLine($"IsGroupChecked:{groupName}");
				_groupIdx++;
				if (UnitVersionGroupView.Groups.Count <= _groupIdx)
				{
					_groupIdx = 0;
				}

				if (trueCount == 0)
				{
					return false;
				}
				else
				{
					return (trueCount == group.Cast<UnitVersion>().Count()) ? true : null;
				}
			}
			set {
				//set ではチェックボックスに何もしない(チェックボックスにアクセスする方法が分からない)
				// command(GroupCheckCmd) で処理する
				_groupIdx = 0;
			}
		}//=true;

		private void UpdateGroupCheck(UnitVersion? m)
		{
			_groupIdx = 0;
			OnPropertyChanged(nameof(IsGroupChecked));
			_groupIdx = 0;
            OnPropertyChanged(nameof(IsAllSelected));
			_groupIdx = 0;
		}

		/// <summary>
		/// グループヘッダーのチェックボックス変更時の処理(コマンド)
		/// </summary>
		/// <param name="cvs">グループ下のアイテム(UnitVersion)</param>
		private void GroupCheckCmd(CollectionViewGroup cvs)
		{
			bool chk;

			//グループ下のリストを取得
			var versions = cvs.Items.Cast<UnitVersion>().ToList();
			int tc = versions.Count(x => x.IsSelected.Value == true);

			if (tc == versions.Count)
			{
				//全チェックされているのなら、全未チェックにする
				chk = false;
			}
			else
			{
				//全未チェック、一部チェックされているのなら、全チェックにする
				chk = true;
			}

			// グループ下のアイテムのチェックボックスを変更
			foreach (UnitVersion item in versions )
			{
				item.IsSelected.Value = chk;
			}
			UpdateGroupCheck(null);
		}
	}
}
