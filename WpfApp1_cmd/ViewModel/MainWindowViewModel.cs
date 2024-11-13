using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;
using WpfApp1_cmd.Command;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase activeView = null;

        private ObservableCollection<UnitVersion> _versions;
        public ObservableCollection<UnitVersion> Versions
        {
            get => _versions;
            set => SetProperty(ref _versions, value);
        }
        public ObservableCollection<TreeItemLcu> TreeViewItems { get; set; }

        public ViewModelBase ActiveView
        {
            get { return activeView; }
            set
            {
                if (activeView != value) {
                    activeView = value;
                    OnPropertyChanged(nameof(ActiveView));
                }
            }
        }

        private Dictionary<string, ViewModelBase> viewModeTable { get; }


        private bool flag = true;
        public bool Flag
        {
            get { return flag; }
            set { flag = value; ButtonCommand.DelegateCanExecute(); }
        }

        public DelegateCommand ButtonCommand { get; }
        public DelegateCommand<string> ScreenTransitionCommand { get; }
        public ReactiveCommand ButtonCommand2 { get; } = new ReactiveCommand();

        public ReactiveCommand CutCommand { get; private set; } = new ReactiveCommand();
        public ReactiveCommand CopyCommand { get; } = new ReactiveCommand();
        public ReactiveCommand PasteCommand { get; } = new ReactiveCommand();

        public ReactiveProperty<bool> FlagProperty1 { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> FlagProperty2 { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> FlagProperty3 { get; } = new ReactiveProperty<bool>(false);

        public ReactiveCommand TreeViewSelectedItemChangedCommand { get; }

        public MainWindowViewModel()
        {
            LoadUnitVersions();

            LoadLineInfo();

            ButtonCommand = new DelegateCommand(async () =>
            {
                Flag = false;
                await Task.Delay(5000);
                Flag = true;
            }, canExecuteCommand);

            ScreenTransitionCommand = new DelegateCommand<string>(screenTransitionExecute);

            viewModeTable = new Dictionary<string, ViewModelBase>
            {
                { "AView", new AViewModel() },
                { "BView", new BViewModel() },
                { "CView", new CViewModel() },
                { "GView", new GViewModel(Versions) },
                { "HView", new GViewModel(Versions) },
                { "MView", new MachineViewModel() },
                { "LView", new LcuViewModel()  }
            };
            ActiveView = viewModeTable["AView"];

            ButtonCommand2.Subscribe(async () =>
            {
                Flag = false;
                await Task.Delay(1000);
                Flag = true;

                //FlagProperty1.Value = true;
                FlagProperty2.Value = true;
                //FlagProperty3.Value = true;
            });

            /*
            //FlagProperty1 が true になったら CutCommand を実行可とする
            CutCommand = FlagProperty1.ToReactiveCommand();

            //フラグが二つとも true になったら CutCommand を実行可とする
            CutCommand = FlagProperty1.CombineLatest(FlagProperty2, (x, y) => x && y).ToReactiveCommand();

            //フラグが三つとも true になったら CutCommand を実行可とする
            CutCommand = new[] { FlagProperty1, FlagProperty2, FlagProperty3 }
                .CombineLatest(x => x.All(y => y)).ToReactiveCommand();

            CutCommand = new[] { FlagProperty1, FlagProperty2, FlagProperty3 }
                .CombineLatestValuesAreAllTrue().ToReactiveCommand();
                //.CombineLatestValuesAreAllFalse().ToReactiveCommand();
            */
            //フラグが一つでも true になったら CutCommand を実行不可とする
            CutCommand = new[] { FlagProperty1, FlagProperty2, FlagProperty3 }
                .CombineLatest(x => x.Any(y => y)).ToReactiveCommand();

            CutCommand.Subscribe(() => CutCmdExecute());

            CopyCommand.Subscribe(() => { Debug.WriteLine("Copy"); });
            PasteCommand.Subscribe(() => { Debug.WriteLine("Paste"); });

            TreeViewSelectedItemChangedCommand = new ReactiveCommand();
            TreeViewSelectedItemChangedCommand.Subscribe(args => TreeViewSelectedItemChanged(args as RoutedPropertyChangedEventArgs<object>));
        }

        /// <summary>
        ///  ツリーの選択状態が変更されたときの処理
        /// </summary>
        /// <param name="e"></param>
        public void TreeViewSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
        {
            TreeItem? item = e.NewValue as TreeItem;
            if( item == null)
            {
                return;
            }
            Debug.WriteLine($"TreeViewSelectedItemChanged={item.Name}:{item.ItemType}");

            switch (item.ItemType)
            {
                case MachineType.LCU:
                    ActiveView = viewModeTable["AView"];    //
                    break;
                case MachineType.Machine:
                    ActiveView = viewModeTable["MView"];
                    break;
                case MachineType.Module:
                    ActiveView = viewModeTable["GView"];
                    break;
            }
        }

        private void CutCmdExecute()
        {
            Debug.WriteLine("Cut");
        }
        private bool canExecuteCommand()
        {
            return Flag;
        }

        private void screenTransitionExecute(string screenName)
        {
            ActiveView = viewModeTable[screenName];
        }
        private void LoadUnitVersions()
        {
            Versions = new ObservableCollection<UnitVersion>
            {
                new UnitVersion { IsSelected = true,  Name = "Unit1", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = false, Name = "Unit2", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = true,  Name = "Unit3", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = false, Name = "Unit4", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = true,  Name = "Unit5", CurVersion = "1.0.0", NewVersion = "1.0.1" },
            };
        }
        private async void LoadLineInfo()
        {
            TreeViewItems = new ObservableCollection<TreeItemLcu>
            {
                // Add Localhost[Debuge用 -> localhost:9000で仮想LCU(WebAPIサーバーを起動して確認する)]
                new ("localhost:9000"){ Name = "localhost:9000", IsChecked = true, ItemType=MachineType.LCU},
                new ("ch-lcu33"){ Name = "ch-lcu33", IsChecked = true, ItemType=MachineType.LCU},
            };
            // 起動時に情報取得する場合
            foreach (var lcu in TreeViewItems)
            {
                bool ret = await UpdateLcuInfo(lcu);
            }
        }

        private string _textValue = "Hello, World!";
        public string TextValue 
        {
            get => _textValue;
            set => SetProperty(ref _textValue,value);
        }

        /// <summary>
        /// LCUの情報を更新する
        /// </summary>
        /// <param name="lcuName"></param>
        public async Task<bool> UpdateLcuInfo(TreeItemLcu lcu) 
        {
            if (lcu.LcuCtrl.FtpUser == null)
            {
                // FTPアカウント情報を取得
                var str = await lcu.LcuCtrl.LCU_Command(FtpData.Command());
                FtpData? data = FtpData.FromJson(str);
                if(data == null)
                {
                    return false;
                }
                if(data.username == null || data.password == null)
                {
                    return false;
                } 
                string password = FtpData.GetPasswd(data.username, data.password);

                lcu.FtpUser = data.username;
                lcu.FtpPassword = password;
            }

            //装置情報が未取得の場合
            if (lcu.Children != null && lcu.Children.Count == 0)
            {
                // Machine 情報を登録
                XmlSerializer serializer = new(typeof(LineInfo));
                string response = await lcu.LcuCtrl.LCU_HttpGet("lines");

                LineInfo lineInfo = (LineInfo)serializer.Deserialize( new StringReader(response));

                foreach(var mc in lineInfo.Line.Machines)
                {
                    TreeItemMachine machine = new()
                    {
                        Name = mc.MachineName,
                        ItemType = MachineType.Machine,
                        IsChecked = true,
                        Machine = mc,
                        Parent = lcu,
                        Children = new ObservableCollection<TreeItemModule>(),
                    };
                    foreach (var base_ in mc.Bases)
                    {
                        foreach (var module in base_.Modules)
                        {
                            TreeItemModule moduleItem = new()
                            {
                                Name = module.DispModule,
                                ItemType = MachineType.Module,
                                IsChecked = true,
                                Base = base_,
                                Module = module,
                                Parent = machine,
                            };
                            machine.Children.Add(moduleItem);
                        }
                    }
                    lcu.Children.Add(machine);
                }
            }
            return true;
        }
    }
}
