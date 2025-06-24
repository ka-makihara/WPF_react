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
using System.Windows.Media;
using System.Windows.Navigation;
using WpfApp1_cmd.Models;
using WpfApp1_cmd.Resources;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.ViewModel
{
	/*
	partial class UnitGroup  :ViewModelBase
	{
		private bool? _status;
		public bool? Status
		{
			get => _status;
			set
			{
				if( _status != value)
				{
					_status = value;
					OnPropertyChanged( nameof(Status) );
				}
			}
		}
		public List<UnitVersion>? Units { get; set; }

		public void UpdateStatus()
		{
			if( Units == null) { return; }
			Status = Utility.CheckState(Units);
		}
	}
	*/

	internal class ModuleViewModel : ViewModelBase
    {
        private ObservableCollection<UpdateInfo>? _updates = null;
		public ObservableCollection<UpdateInfo> Updates { get => _updates; set => _updates = value; }

		private void IsSelectedChk(UnitVersion item, bool? value)
        {
			if (ArgOptions.GetOptionBool("--ignore_version") == false ) 
			{
				// バージョンが違うものしかチェックが更新できないようにする
				var uv = Updates.FirstOrDefault(x => x.Name == item.Name);
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
			SetSelectedModule();
		}

        public ReactiveCollection<UnitVersion> UnitVersions { get; set; }

		public ReactivePropertySlim<bool> IsUnitSelectToggleEnabled { get; } = new ReactivePropertySlim<bool>(true);

		public ReactiveCommand AllSelectCommand { get; }

		public ReactiveCommand<UnitVersion> CheckBoxCommand { get; }            // 個々のアイテムのチェックボックスの変更イベント
		public ReactiveCommand<RoutedEventArgs> GroupCheckClick { get; }        // グループヘッダーのチェックボックスのクリックイベント
		public ReactiveCommand<CollectionViewGroup> GroupCheckCommand { get; }	// グループヘッダーのチェックボックス・コマンド	

		public string ModuleName { get; set; } = "Module";
		
		/// <summary>
		/// ユニット選択のトグルボタンの表示を切り替える
		///   ユーザーモードでは表示しない -> ユニット毎のチェックボックスを表示しない
		/// </summary>
		public static bool IsUnitSelectToggleVisible
		{
			get
			{
				string mode = ArgOptions.GetOption("--mode", "user");
				return mode == "administrator";
			}
		}

		/// <summary>
		/// 全選択コマンド実行時の処理
		/// </summary>
		private void OnAllSelectCommandExecuted()
		{
			var newValue = IsAllSelected;// == true;
			foreach (var unit in UnitVersions)
			{
				if (unit.Attribute == Define.NOT_UPDATE)
				{
					unit.IsSelected.Value = false;
				}
				else {
					if (newValue == null)
					{
						unit.IsSelected.Value = true;
					}
					else
					{
						unit.IsSelected.Value = newValue;
					}
				}
			}
		}

		public ICollectionView UnitVersionGroupView {get; set; }

		//private OrderedDictionary<string, UnitGroup> _unitGroup;

		/*
		private void InitGroupStatus()
		{
			_unitGroup = [];

			var groups = UnitVersionGroupView.Groups.Cast<CollectionViewGroup>().ToList();

			foreach(var group in groups)
			{
				var units = group.Items.Cast<UnitVersion>().ToList();
				UnitGroup ug = new()
				{
					Units = units,
					Status = Utility.CheckState(units)
				};
				_unitGroup.Add((string)group.Name, ug);
			}
		}
		*/
/*
		private double _scrollPosition = 0.0;
		public double ScrollPosition
		{
			get => _scrollPosition;
			set
			{
				_scrollPosition = value;
				OnPropertyChanged(nameof(ScrollPosition));
			}
		}
*/

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="name">モジュール名</param>
		/// <param name="unitVersions">バージョンリスト</param>
		/// <param name="updates">アップデートデータリスト</param>
		public ModuleViewModel(string name, ReactiveCollection<UnitVersion> unitVersions, ObservableCollection<UpdateInfo> updates)
        {
            UnitVersions = unitVersions;
            Updates = updates;
			//ModuleName = "Transfer Selection : " + name;    //ビューのタイトルに表示する文字列
			ModuleName = name;    //ビューのタイトルに表示する文字列
			SetSelectedModule();

			UnitVersionGroupView = CollectionViewSource.GetDefaultView(UnitVersions);
            UnitVersionGroupView.GroupDescriptions.Add(new PropertyGroupDescription("UnitGroup"));

			// グループヘッダーのチェックボックスの Click イベント
			//GroupCheckClick = new ReactiveCommand<RoutedEventArgs>();
			//GroupCheckClick.Subscribe(e => GroupCheckClickEvent(e));

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
				item.IsSelected.Subscribe( value =>
                {
					//Debug.WriteLine($"IsSelected Subscribe:{value}");
					if(item.Attribute == Define.NOT_UPDATE)
					{
						item.IsSelected.Value = false;
					}
					else
					{
						IsSelectedChk(item, value);
					}
					OnPropertyChanged();
				});
            }

			//ユニット選択のトグルボタン、チェックボックスの表示を切り替える(--mode=user/administrator, 未定義は administrator 以外とする)
			string mode = ArgOptions.GetOption("--mode", "user");

			IsUnitCheckBoxEnabled = (mode == "administrator");  // ユニット選択のトグルボタンの初期値(administrator のときのみ有効)
			IsChkVisibled = (mode == "administrator");          // ユニットチェックボックスの表示の初期値(administrator のときのみ表示)

			//InitGroupStatus();
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
			}
        }
        private void SelectAll(bool? select, IEnumerable<UnitVersion> units)
        {
			foreach (var unit in units)
			{
				if (unit.Attribute != Define.NOT_UPDATE) {
					unit.IsSelected.Value = select;
				}
            }
			UnitVersionGroupView.Refresh();
			//Debug.WriteLine("SelecaAll()");
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
		/*
		public bool? IsGroupChecked
		{
			get
			{
				//UnitVersionGroupView.Groups.Cast<CollectionViewGroup>().ToList().ForEach(group => Debug.WriteLine($"{group.Name}"));
				UnitGroup ug = _unitGroup.ElementAt(_groupIdx).Value;
				var data = _unitGroup.GetAt(_groupIdx);

				Debug.WriteLine($"Index[{_groupIdx}]:{data.Key}={ug.Status}");

				_groupIdx++;
				if( _groupIdx >= _unitGroup.Count)
				{
					_groupIdx = 0;
				}
				return ug.Status;
			}
			set {
				//set ではチェックボックスに何もしない(チェックボックスにアクセスする方法が分からない)
				// command(GroupCheckCmd) で処理する
			}
		}

		private void UpdateGroupStatus(UnitVersion unit)
		{
			UnitGroup? ug = _unitGroup.GetValueOrDefault(unit.UnitGroup);
			if( ug == null) { return;  }

			ug.UpdateStatus();
			OnPropertyChanged(nameof(IsGroupChecked));
		}
		*/

		/// <summary>
		/// 各アイテムのチェックボックスを操作した時のコマンド
		/// </summary>
		/// <param name="m"></param>
		public void UpdateGroupCheck(UnitVersion? m)
		{
			/*
			if (m != null)
			{
				Debug.WriteLine($"UpdateGroupCheck({m.Name})={m.IsSelected}");
				//UpdateGroupStatus(m);
			}
			else
			{
				Debug.WriteLine("UpdateGroupCheck(null)");
			}
			*/
            OnPropertyChanged(nameof(IsAllSelected));

			//Debug.WriteLine("Refresh");
			UnitVersionGroupView.Refresh();
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

			if (tc == 0 )
			{
				//全チェックされてないのなら、全チェックにする
				chk = true;
			}
			else
			{
				//全チェック、一部チェックされているのなら、全未チェックにする
				// ※チェックボックス値がnullの場合(一部チェック)にクリックするとチェックボックス値がfalseになる
				chk = false;
			}

			// グループ下のアイテムのチェックボックスを変更
			foreach (UnitVersion item in versions )
			{
				item.IsSelected.Value = chk;
			}
			OnPropertyChanged(nameof(IsAllSelected));
			//Debug.WriteLine("GroupCheckCmd");
		}

		/// <summary>
		/// グリッドビューのタイトルに表示する文字列
		/// </summary>
		private string _selectedModule;
		public string SelectedModule
		{
			get => _selectedModule;
			set => SetProperty(ref _selectedModule, value);
		}

		/// <summary>
		/// グリッドビューのタイトルに表示する文字列を設定する
		/// </summary>
		public void SetSelectedModule()
		{
			int sel = UnitVersions.Count(x => x.IsSelected.Value == true);
			SelectedModule = $"{Resource.TransferSelection}:{sel} {Resource.Units}";
		}
	}
}
