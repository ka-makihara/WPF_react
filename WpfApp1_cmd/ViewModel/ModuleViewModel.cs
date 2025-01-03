﻿using Reactive.Bindings;
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
			if (Options.GetOptionBool("--diffOnly", false) == true)
			{
				// バージョンが違うものしかチェックが更新できないようにする
				try
				{
					string newVer = _updates.First(x => x.Name == item.Name).Version;
					if (newVer != item.CurVersion)
					{
						item.IsSelected.Value = value;
					}
					else
					{
						item.IsSelected.Value = false;
					}
				}
				catch (Exception e)
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
			ICollectionView cvs = CollectionViewSource.GetDefaultView(UnitVersions);

			// IsSelected プロパティが変更されたときに、IsAllSelected プロパティを更新する
			OnPropertyChanged(nameof(IsAllSelected));
			//OnPropertyChanged(nameof(IsGroupChecked));
        }

        public ReactiveCollection<UnitVersion> UnitVersions { get; set; }

		public ReactiveCommand<RoutedEventArgs> GroupCheckCommand { get; }
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

		public ModuleViewModel(ReactiveCollection<UnitVersion> unitVersions, ObservableCollection<UpdateInfo> updates)
        {
            UnitVersions = unitVersions;
            _updates = updates;

			ICollectionView cvs = CollectionViewSource.GetDefaultView(UnitVersions);
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("UnitGroup"));

			GroupCheckCommand = new ReactiveCommand<RoutedEventArgs>();
			GroupCheckCommand.Subscribe(e => GroupCheck(e));

			foreach (var item in UnitVersions)
            {
                // IsSelected プロパティが変更されたときに、IsSelectedChk メソッドを呼び出す
                item.IsSelected.Subscribe(x => IsSelectedChk(item,x));

                if (updates != null)
                {
                    try {
                        // 要素が見つからない場合は例外が発生する
                        string newVer = updates.First(x => x.Name == item.Name).Version;
                        if(newVer != item.CurVersion)
                        {
                            item.NewVersion = newVer;
							item.IsSelected.Value = (item.Attribute == Define.NOT_UPDATE) ? false : true ;
                        }
                        else
                        {
                            item.NewVersion = newVer;
							if (Options.GetOptionBool("--diffOnly", false) == true)	// --diffOnly オプションが設定されている場合はitemとして登録されていない??ので不要かも。
							{
								item.IsSelected.Value = false;
							}
							else
							{
								item.IsSelected.Value =  (item.Attribute == Define.NOT_UPDATE) ? false : true ;
							}
                        }
                    }
                    catch (Exception e)
                    {
                        item.NewVersion = "N/A";
                        item.IsSelected.Value = false;
                    }
                }
            }
		}

		/*
        public void UpdateVersions(ReactiveCollection<UpdateInfo> updates)
        {
            foreach (var item in UnitVersions)
            {
                try {
                    // 要素が見つからない場合は例外が発生する
                    string newVer = updates.First(x => x.Name == item.Name).Version;
                    if(newVer != item.CurVersion)
                    {
                        item.NewVersion = newVer;
                        item.IsSelected.Value = true;
                    }
                    else
                    {
                        item.NewVersion = newVer;
                        item.IsSelected.Value = false;
                    }
                }
                catch (Exception e)
                {
                    item.NewVersion = "N/A";
                    item.IsSelected.Value = false;
                }
            }
        }
*/
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
        private static void SelectAll(bool? select, IEnumerable<UnitVersion> models)
        {
            foreach (var model in models) {
                model.IsSelected.Value = select;
            }
        }

		public bool IsSelectedGroup
		{
			get;
			set;
		} = true;

		private bool _isChkVisibled = true;
		public bool IsChkVisibled
		{
			get => _isChkVisibled;
			set => SetProperty(ref _isChkVisibled, value);
		}// = true;

		/*
		private bool _isSelectedGroup;
		public bool IsSelectedGroup
		{
			get
			{
				return _isSelectedGroup;
			}
			set
			{
			}
		}
		*/
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
