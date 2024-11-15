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
using MaterialDesignThemes.Wpf;
using ControlzEx.Theming;

namespace WpfApp1_cmd.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase activeView = null;

        public ObservableCollection<LcuInfo> TreeViewItems { get; set; }

        private Dictionary<string, ViewModelBase> viewModeTable { get; }

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
                //{ "GView", new GViewModel() },
            };
            /*
                { "ModuleView",  new ModuleViewModel() },
                { "LcuView",     new LcuViewModel(TreeViewItems)  }
           */ 
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

            var ret = CreateVersionInfo(
                new LcuInfo { Name = "localhost:9000",LcuCtrl=new("localhost:9000") { } },
                new MachineInfo { Name = "localhost:9000" },
                new ModuleInfo { Name = "1-L", Module = new() { LogicalPos=1}}
            );
        }

        /// <summary>
        ///  ツリーの選択状態が変更されたときの処理
        /// </summary>
        /// <param name="e"></param>
        public async void TreeViewSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
        {
            CheckableItem? item = e.NewValue as CheckableItem;
            if( item == null)
            {
                return;
            }
            Debug.WriteLine($"TreeViewSelectedItemChanged={item.Name}:{item.ItemType}");

            switch (item.ItemType)
            {
                case MachineType.LCU:
                    if (viewModeTable.ContainsKey($"LcuViewi_{item.Name}") == false)
                    {
                        viewModeTable.Add($"LcuView_{item.Name}", new LcuViewModel(TreeViewItems.Where(x => x.Name == item.Name).First().Children));
                    }
                     ActiveView = viewModeTable[$"LcuView_{item.Name}"];
                    break;
                case MachineType.Machine:
                    if (viewModeTable.ContainsKey($"MachineView_{item.Name}") == false)
                    {
                        string lcuName = (item as MachineInfo).Parent.Name;
                        foreach (var lcu in TreeViewItems)
                        {
                            if (lcu.Children == null || lcu.Name != lcuName )
                            {
                                continue;
                            }
                            viewModeTable.Add($"MachineView_{item.Name}", new MachineViewModel(lcu.Children.Where(x => x.Name == item.Name).First().Children));
                            break;
                        }
                    }
                    ActiveView = viewModeTable[$"MachineView_{item.Name}"];
                    break;
                case MachineType.Module:
                    if (viewModeTable.ContainsKey($"ModuleView_{item.Name}") == false)
                    {
                        string lcuName = (item as ModuleInfo).Parent.Parent.Name;
                        foreach (var lcu in TreeViewItems)
                        {
                            if (lcu.Children == null || lcu.Name != lcuName)
                            {
                                continue;
                            }
                            foreach (var machine in lcu.Children)
                            {
                                if (machine.Children == null || machine.Name != (item as ModuleInfo).Parent.Name)
                                {
                                    continue;
                                }
                                foreach (var module in machine.Children)
                                {
                                    if (module.Name != item.Name)
                                    {
                                        continue;
                                    }
                                    if (module.UnitVersions == null)
                                    {
                                        //初めての場合はバージョン情報を取得する
                                        var ret = await CreateVersionInfo(lcu, machine, module);
                                        if (ret != null )
                                        {
                                            module.UnitVersions = ret;
                                            viewModeTable.Add($"ModuleView_{item.Name}", new ModuleViewModel(module.UnitVersions));
                                        }
                                    }
                                    ActiveView = viewModeTable[$"ModuleView_{item.Name}"];
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        ActiveView = viewModeTable[$"ModuleView_{item.Name}"];
                    }
                    break;
            }
        }

        /// <summary>
        /// LCUからユニットバージョン情報を取得する
        /// </summary>
        /// <param name="lcu"></param>
        /// <param name="machine"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public async Task<ObservableCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
        {
            if( lcu.LcuCtrl == null)
            {
                return null;
            }

            string tmpFile = Path.GetTempFileName();
            bool ret = await lcu.LcuCtrl.GetMachineFile(lcu.Name, machine.Name, module.Pos, "Peripheral/UpdateCommon.inf", tmpFile);

            if( ret == false)
            {
                return null;
            }
            IniFileParser parser = new(tmpFile);

            System.IO.File.Delete(tmpFile);

            IList<string> sec = parser.SectionCount();

            ObservableCollection<UnitVersion> versions = [];

            foreach (var unit in sec)
            {
                UnitVersion version = new()
                {
                    IsSelected = true,
                    Name = unit,
                    Attribute = parser.GetValue(unit, "Attribute"),
                    Path = parser.GetValue(unit, "Path"),
                    CurVersion = parser.GetValue(unit, "Version"),
                    NewVersion = parser.GetValue(unit, "Version") + "New" // 暫定
                };
                versions.Add(version);
            }
            return versions;
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
        
        private string _textValue = "Hello, World!";
        public string TextValue 
        {
            get => _textValue;
            set => SetProperty(ref _textValue,value);
        }

        public void ToggleTheme(bool isDark)
        {
            ToggleMetroTheme(isDark);
            ToggleMdTheme(isDark);
        }

        private void ToggleMdTheme(bool isDark)
        {
            var pallet = new PaletteHelper();
            var theme = pallet.GetTheme();
        }

        private void ToggleMetroTheme(bool isDark)
            => ThemeManager.Current.ChangeTheme(Application.Current, isDark ? "Dark.Blue" : "Light.Blue");

        /// <summary>
        /// ライン情報を取得する
        /// </summary>
        private async void LoadLineInfo()
        {
            TreeViewItems = new ObservableCollection<LcuInfo>
            {
                // Add Localhost[Debuge用 -> localhost:9000で仮想LCU(WebAPIサーバーを起動して確認する)]
                new (){ Name = "localhost:9000", IsSelected = true, ItemType=MachineType.LCU},
                new (){ Name = "ch-lcu33",       IsSelected = true, ItemType=MachineType.LCU},
            };
            // 起動時に情報取得する場合
            foreach (var lcu in TreeViewItems)
            {
                bool ret = await UpdateLcuInfo(lcu);
            }
        }

        /// <summary>
        /// LCUの情報を更新する
        /// </summary>
        /// <param name="LcuInfo">LCU情報</param>
        public async Task<bool> UpdateLcuInfo(LcuInfo lcu) 
        {
            if( lcu.LcuCtrl == null)
            {
                lcu.LcuCtrl = new LcuCtrl(lcu.Name);
            }

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

                // LCU バージョン取得
                str = await lcu.LcuCtrl.LCU_Command(LcuVersion.Command());
                IList<LcuVersion> versionInfo = LcuVersion.FromJson(str);
                lcu.Version = versionInfo.Where(x => x.itemName == "Fuji LCU Communication Server Service").First().itemVersion;
            }

            if( lcu.Children == null)
            {
                lcu.Children = [];
            }

            //装置情報が未取得の場合
            if ( lcu.Children.Count == 0)
            {
                // Machine 情報を登録
                XmlSerializer serializer = new(typeof(LineInfo));
                string response = await lcu.LcuCtrl.LCU_HttpGet("lines");

                LineInfo? lineInfo = (LineInfo)serializer.Deserialize( new StringReader(response));
                if(lineInfo == null)
                {
                    return false;
                }
                if( lineInfo.Line == null )
                {
                    return false;
                }
                if(lineInfo.Line.Machines == null)
                {
                    return false;
                }


                foreach (var mc in lineInfo.Line.Machines)
                {
                    MachineInfo machine = new()
                    {
                        Name = mc.MachineName,
                        ItemType = MachineType.Machine,
                        IsSelected = true,
                        Machine = mc,
                        Parent = lcu,
                        Bases = [],
                        Children = []
                    };
                    foreach (var base_ in mc.Bases)
                    {
                        BaseInfo baseInfo = new()
                        {
                            Name = "",
                            ItemType = MachineType.Base,
                            IsSelected = true,
                            Base = base_,
                            Parent = machine,
                            Children = []
                        };
                        machine.Bases.Add(baseInfo);

                        foreach (var module in base_.Modules)
                        {
                            ModuleInfo moduleItem = new()
                            {
                                Name = module.DispModule,
                                ItemType = MachineType.Module,
                                IsSelected = true,
                                Module = module,
                                Parent = machine,
                                IPAddress = base_.IpAddr,
                            };
                            baseInfo.Children.Add(moduleItem);
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
