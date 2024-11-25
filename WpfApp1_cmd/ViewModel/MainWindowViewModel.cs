using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControlzEx.Theming;
using System.Windows.Navigation;
using System.Reactive.Linq;
using System.Windows;
using System.Xml.Serialization;
using WpfApp1_cmd.Command;
using WpfApp1_cmd.View;
using MaterialDesignThemes.Wpf;
using System.Runtime.InteropServices;

using WpfLcuCtrlLib;
//using NeximDataControl;
using ControlzEx.Standard;
using Microsoft.WindowsAPICodePack.Dialogs;
using Oracle.ManagedDataAccess.Client;
using System.Windows.Controls;

namespace WpfApp1_cmd.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        [DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int getMcUser(StringBuilder s, Int32 len);

        private string _logData = "";
        public string LogData
        {
            get => _logData;
            set
            {
                _logData = value;
                OnPropertyChanged(nameof(LogData));
            }
        }
        public void AddLog(string log)
        {
            LogData += "\n";
            LogData += log;
        }

        // ツリービューのアイテム
        //public ObservableCollection<LcuInfo> TreeViewItems { get; set; }
        public ReactiveCollection<LcuInfo>  TreeViewItems { get; set; }

        // ツリービューの選択項目に対応するビューモデル
        private Dictionary<string, ViewModelBase> viewModeTable { get; }

        // ツリーの選択項目
        public CheckableItem SelectedItem { get; set; }

        // ビューの切り替え
        private ViewModelBase activeView = null;
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
        private string DataFolder { get; set; } = "";
        public long DataSize { get; set; } = 0;
        private ReactiveCollection<UpdateInfo> _upDates;
        private ReactiveCollection<UpdateInfo>? Updates
        {
            get => _upDates;
            set {
                _upDates = value;
                OnPropertyChanged(nameof(Updates));
            }
        }

        private bool flag = true;
        public bool Flag
        {
            get { return flag; }
            set { flag = value; ButtonCommand.DelegateCanExecute(); }
        }

        public DelegateCommand<string> ScreenTransitionCommand { get; }
        public ReactiveCommand FileOpenCommand { get; } = new ReactiveCommand();
        public ReactiveCommand LcuNetworkChkCommand { get; } = new ReactiveCommand();
        public ReactiveCommand LcuDiskChkCommand { get; } = new ReactiveCommand();

        public DelegateCommand ButtonCommand { get; }
        public ReactiveCommand ButtonCommand2 { get; } = new ReactiveCommand();

        public ReactiveCommand CutCommand { get; private set; } = new ReactiveCommand();
        public ReactiveCommand CopyCommand { get; } = new ReactiveCommand();
        public ReactiveCommand PasteCommand { get; } = new ReactiveCommand();

        public ReactiveProperty<bool> FlagProperty1 { get; } = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> FlagProperty2 { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> FlagProperty3 { get; } = new ReactiveProperty<bool>(true);

        public ReactiveProperty<bool> CanExecuteLcuCommand { get; } = new ReactiveProperty<bool>(false);

        public ReactiveCommand<RoutedPropertyChangedEventArgs<object>> TreeViewSelectedItemChangedCommand { get; }

        public string DialogTitle { get; set; } = "Dialog Title";
        public string DialogText { get; set; } = "Dialog Text";

        // 転送制御ボタン
        public ReactiveCommand StartTransferCommand { get; } = new ReactiveCommand();
        public ReactiveCommand StopTransferCommand { get; } = new ReactiveCommand();
        public ReactiveCommand QuitApplicationCommand { get; } = new ReactiveCommand();

        // TreeView を Enable/Disable しようとしたがうまく動作しない
        //  MainWindow から設定するとOK
        private bool _isTreeEnabled = true;
        public bool IsTreeEnabled
        {
            get => _isTreeEnabled;
            set
            {
                _isTreeEnabled = value;
                SetProperty(ref _isTreeEnabled, value);
            }
        }

        public MainWindowViewModel()
        {
            //オプション処理
            string dataFolder = Options.GetOption("--dataFolder", "");
            if(dataFolder != "")
            {
                DataFolder = dataFolder;
                Updates = ReadUpdateCommon(dataFolder + "\\UpdateCommon.inf");
                AddLog($"Read {dataFolder}/UpdateCommon.inf");
            }
            try
            {
                // DLL内の関数呼び出し。 DLLが無い場合は例外が発生する
                StringBuilder sb = new StringBuilder(1024);
                Int32 len = getMcUser(sb, sb.Capacity);
                Debug.WriteLine($"user={sb.ToString()} len={len}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            //ビューの生成
            viewModeTable = new Dictionary<string, ViewModelBase>
            {
                { "AView", new AViewModel() },
                { "BView", new BViewModel() },
                { "CView", new CViewModel() },
                { "UpdateVersionView", new UpdateVersionViewModel(Updates) }
            };
            ActiveView = viewModeTable["UpdateVersionView"];

            //ライン情報読み込み
            LoadLineInfo();

            // メニューコマンドの設定 
            FileOpenCommand.Subscribe(() => FileOpenCmd());
            LcuNetworkChkCommand.Subscribe(() => { LcuNetworkChkCmd(); });
            LcuDiskChkCommand.Subscribe(() => { LcuDiskChkCmd(); });
            // メニュー実行制御
            LcuNetworkChkCommand = CanExecuteLcuCommand.ToReactiveCommand();
            LcuDiskChkCommand = CanExecuteLcuCommand.ToReactiveCommand();

            ButtonCommand = new DelegateCommand(async () =>
            {
                Flag = false;
                var r = DialogHost.Show(new MyMessageBox());
                await Task.Delay(2000);
                Flag = true;
                //DialogHost.CloseDialogCommand.Execute(null, null);
            }, canExecuteCommand);

            ScreenTransitionCommand = new DelegateCommand<string>(screenTransitionExecute);

            ButtonCommand2.Subscribe(async () =>
            {
                Flag = false;
                await Task.Delay(1000);
                Flag = true;

                FlagProperty2.Value = true;
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

            TreeViewSelectedItemChangedCommand = new ReactiveCommand<RoutedPropertyChangedEventArgs<object>>();
            TreeViewSelectedItemChangedCommand.Subscribe(args => TreeViewSelectedItemChanged(args));

            // 転送制御ボタン
            StartTransferCommand = FlagProperty1.ToReactiveCommand();
            StartTransferCommand.Subscribe(() => StartTransferExecute() );

            StopTransferCommand = FlagProperty2.ToReactiveCommand();
            StopTransferCommand.Subscribe(() => StopTransferExecute() );

            QuitApplicationCommand = FlagProperty3.ToReactiveCommand();
            QuitApplicationCommand.Subscribe(() => ApplicationShutDown() );

        }

        /// <summary>
        ///  ユニットアップデートデータのフォルダを選択して読み込む
        /// </summary>
        public void FileOpenCmd()
        {
            using (var cofd = new CommonOpenFileDialog()
            {
                Title= "フォルダを選択してください",
                InitialDirectory = @"C:\Users\ka.makihara\develop",
                //フォルダ選択モード
                IsFolderPicker = true,
            })
            {
                if (cofd.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    AddLog($"FileOpenCommand={cofd.FileName}");
                    if( Path.Exists(cofd.FileName + "\\UpdateCommon.inf") == false)
                    {
                        AddLog("UpdateCommon.inf が見つかりません");
                        return;
                    }
                    Updates = ReadUpdateCommon(cofd.FileName + "\\UpdateCommon.inf");

                    viewModeTable["UpdateVersionView"] = new UpdateVersionViewModel(Updates);
                    ActiveView = viewModeTable["UpdateVersionView"];
                }
            }

            /*
            string connStr = "User Id=fujisuperuser;Password=em2g86fzjt945p73;Data Source=10.0.51.64:1521/neximdb";
            using(OracleConnection conn = new(connStr))
            {
                try
                {
                    conn.Open();
                    OracleCommand cmd = new("select * from LINE", conn);
                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        AddLog(reader.GetString(0));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            */
            /*
             * SELECT * FROM LINE WHERE JOBID=0 AND ACTIVEFLAG=1 AND LCUID!=0
SELECT * FROM COMPUTER WHERE COMPUTERID=44*/

        }

        /// <summary>
        ///  LCU の接続状態を確認する
        /// </summary>
        void LcuNetworkChkCmd()
        {

        }

        /// <summary>
        /// LCU のディスク容量を確認する
        /// </summary>
        void LcuDiskChkCmd()
        {

        }

        /// <summary>
        ///  UpdateCommon.inf を読み込み、UpdateInfo のリストを返す
        /// </summary>
        /// <param name="path">UpdateCommon.inf ファイルのパス</param>
        /// <returns></returns>
        private ReactiveCollection<UpdateInfo> ReadUpdateCommon(string path)
        {
            ReactiveCollection<UpdateInfo> updates = [];
            IniFileParser parser = new(path);
            IList<string> sec = parser.SectionCount();

            foreach (var unit in sec)
            {
                UpdateInfo update = new()
                {
                    Name = unit,
                    Attribute = parser.GetValue(unit, "Attribute"),
                    Path = parser.GetValue(unit, "Path"),
                    Version = parser.GetValue(unit, "Version"),
                };
                updates.Add(update);
            }

            DataSize = GetPeripheradSize(DataFolder);

            return updates;
        }

        /// <summary>
        /// フォルダのサイズを取得する
        /// </summary>
        /// <param name="dirInfo">サイズを取得するフォルダ</param>
        /// <returns>フォルダのサイズ（バイト）</returns>
        public static long GetDirectorySize(DirectoryInfo dirInfo)
        {
            long size = 0;

            //フォルダ内の全ファイルの合計サイズを計算する
            foreach (FileInfo fi in dirInfo.GetFiles())
                size += fi.Length;

            //サブフォルダのサイズを合計していく
            foreach (DirectoryInfo di in dirInfo.GetDirectories())
                size += GetDirectorySize(di);

            //結果を返す
            return size;
        }
        private long GetPeripheradSize(string path)
        {
            long size = 0;
            DirectoryInfo di = new(path);

           FileInfo[] files = di.GetFiles("Peripheral.bin");
            if (files.Length != 0)
            {
                //圧縮ファイルのサイズを取得
                size = files[0].Length;
            }
            else
            {
                size = GetDirectorySize(di);
            }
            return size;
        }

        /// <summary>
        /// 転送開始コマンド
        /// </summary>
        private async void StartTransferExecute()
        {
            Debug.WriteLine("StartTransfer");
            FlagProperty1.Value = false;
            FlagProperty2.Value = true;
            FlagProperty3.Value = false;

            /*
            switch(SelectedItem.ItemType)
            {
                case MachineType.LCU:
                    LcuInfo lcu = SelectedItem as LcuInfo;
                    List<string> folders = [
                        "Fuji/System3/Program/Peripheral/UpdateCommon.inf",
                        ]; 
                    lcu.LcuCtrl.CreateFtpFolders(folders,"MCFiles\\");
                    lcu.LcuCtrl.ClearFtpFolders("MCFiles\\");
                    break;
                case MachineType.Machine:
                    break;
                case MachineType.Module:
                    break;
            }
            */
            /*
            ModuleInfo module = SelectedItem as ModuleInfo;
            MachineInfo machine = module.Parent;
            LcuInfo lcu = machine.Parent;

            await DownloadModuleFiles(lcu, machine, module);
            */
            //await TransferExecute();

            FlagProperty1.Value = true;
            FlagProperty3.Value = true;
            FlagProperty2.Value = false;
        }

        /// <summary>
        /// データ転送
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TransferExecute()
        {
            foreach(var lcu in TreeViewItems)
            {
                //if (lcu.Children == null || lcu.LcuCtrl == null || lcu.IsSelected == false)
                if (lcu.Children == null || lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
                {
                    continue;
                }
                foreach (var machine in lcu.Children)
                {
                    //if( machine.Children == null || machine.Name == null || machine.IsSelected == false)
                    if( machine.Children == null || machine.Name == null || machine.IsSelected.Value == false)
                    {
                        continue;
                    }
                    foreach(var module in machine.Children)
                    {
                        //if( module.IsSelected == false || module.UnitVersions == null )
                        if( module.IsSelected.Value == false || module.UnitVersions == null )
                        {
                            continue;
                        }

                        // UpdateCommon.inf の Path のフォルダにファイルを転送する
                        //   ※ UpdateCommon.inf に記載されているデータは存在するものとする
                        //       指定されたフォルダにあるUpdateCommon.inf を読み込んでいるので
                        //
                        bool ret = await UploadModuleFiles(lcu, machine, module);

                        // LCU 上に作成、転送したファイルを削除する
                        lcu.LcuCtrl.ClearFtpFolders(Define.LCU_ROOT_PATH);
                    }
                }
            }
            return true;
        }

        /// <summary>
        ///  バージョン情報にあるデータを装置から取得する
        /// </summary>
        /// <param name="lcu"></param>
        /// <param name="machine"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public async Task<bool> DownloadModuleFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
        {
            bool ret;

            //バージョン情報からパスのみを取り出してリスト化
            List<string> folders = module.UnitVersions.Select(x => x.Path).ToList();

            //LCU上にフォルダを作成する(装置からファイルを取得するフォルダ)
            string lcuRoot = $"LCU_{module.Pos}\\MCFiles\\";
            //string lcuRoot = "/MCFiles/";
            ret = lcu.LcuCtrl.CreateFtpFolders(folders, lcuRoot);
            if( ret == false)
            {
                Debug.WriteLine("CreateFtpFolders Error");
                return ret;
            }

            //装置からLCUにファイルを取得する
            string mcUser = "Administrator";
            string mcPass = "password";
            string retMsg = await lcu.LcuCtrl.LCU_Command(GetMcFile.Command(machine.Name, module.Pos, mcUser, mcPass,folders, lcuRoot));

            if (retMsg == "" || retMsg == "Internal Server Error")
            {
                Debug.WriteLine("GetMCFile Error");
                return false;
            }
            // LCU からFTPでファイルを取得
            ret = lcu.LcuCtrl.DownloadFiles(lcuRoot, Define.LOCAL_BACKUP_PATH, folders);

            //LCU 上に作成したファイルを削除
            ret = lcu.LcuCtrl.ClearFtpFolders(lcuRoot);

            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lcu"></param>
        /// <param name="machine"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public async Task<bool> UploadModuleFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
        {
            bool ret;

            // UpdateCommon.inf の Path のフォルダにファイルを転送する
            //   ※ UpdateCommon.inf に記載されているデータは存在するものとする
            //       指定されたフォルダにあるUpdateCommon.inf を読み込んでいるので
            //
            //バージョン情報からパスのみを取り出してリスト化
            List<string> folders = module.UnitVersions.Select(x => x.Path).ToList();

            //LCU上にフォルダを作成する(ファイルを送るフォルダ)
            string lcuRoot = $"LCU_{module.Pos}\\MCFiles\\";
            //string lcuRoot = "/MCFiles/";
            ret = lcu.LcuCtrl.CreateFtpFolders(folders, lcuRoot);
            if( ret == false)
            {
                Debug.WriteLine("CreateFtpFolders Error");
                return ret;
            }
             // LCUに FTPでファイルを送信
            ret = lcu.LcuCtrl.UploadFiles(lcuRoot, Define.LOCAL_BACKUP_PATH, folders);

            //LCUから装置にファイルを送信するコマンド
            string mcUser = "Administrator";
            string mcPass = "password";
            string retMsg = await lcu.LcuCtrl.LCU_Command(PostMcFile.Command(machine.Name, module.Pos, mcUser, mcPass,folders, lcuRoot));

            if (retMsg == "" || retMsg == "Internal Server Error")
            {
                Debug.WriteLine("GetMCFile Error");
                return false;
            }
            return true;
        }
        /// <summary>
        ///  転送中止
        /// </summary>
        private void StopTransferExecute()
        {
            Debug.WriteLine("StopTransfer");
            FlagProperty1.Value = true;
            FlagProperty2.Value = false;
            FlagProperty3.Value = true;
        }

        /// <summary>
        ///  アプリ終了
        /// </summary>
        private async void ApplicationShutDown()
        {
            DialogTitle = "確認";
            DialogText = "アプリケーションを終了しますか？";
            var r = await DialogHost.Show(new MyMessageBox());
            DialogHost.CloseDialogCommand.Execute(null, null);

            if(r == null || r.ToString() == "CANCEL")
            {
                return;
            }

            Application.Current.Shutdown();
        }

        /// <summary>
        ///  ツリーの選択状態が変更されたときの処理
        /// </summary>
        /// <param name="e"></param>
        public async void TreeViewSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
        {
            CheckableItem? item = e.NewValue as CheckableItem;

            if ( item == null)
            {
                return;
            }
            SelectedItem = item;
            Debug.WriteLine($"TreeViewSelectedItemChanged={item.Name}:{item.ItemType}");

            switch (item.ItemType)
            {
                case MachineType.LCU:
                    if (viewModeTable.ContainsKey($"LcuView_{item.Name}") == false)
                    {
                        viewModeTable.Add($"LcuView_{item.Name}", new LcuViewModel(TreeViewItems.Where(x => x.Name == item.Name).First().Children));
                    }
                     ActiveView = viewModeTable[$"LcuView_{item.Name}"];
                    CanExecuteLcuCommand.Value = true;
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
                    CanExecuteLcuCommand.Value = false; // LCU コマンドを実行可にする
                    ActiveView = viewModeTable[$"MachineView_{item.Name}"];
                    break;
                case MachineType.Module:
                    CanExecuteLcuCommand.Value = false; //LCU コマンドを実行不可にする
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
                                    if (module.UnitVersions.Count == 0)
                                    {
                                        //初めての場合はバージョン情報を取得する
                                        IsTreeEnabled = false;
                                        var r = DialogHost.Show(new WaitProgress("ユニット情報読み込み中"));    //時間がかかるので、クルクルを表示
                                        var ret = await CreateVersionInfo(lcu, machine, module);
                                        DialogHost.CloseDialogCommand.Execute(null, null);

                                        if (ret != null )
                                        {
                                            module.UnitVersions = ret;
                                            viewModeTable.Add($"ModuleView_{item.Name}", new ModuleViewModel(module.UnitVersions, Updates));
                                        }
                                        else
                                        {
                                            // LCU からの情報取得に失敗した場合
                                            return;
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
                        ModuleViewModel vm = viewModeTable[$"ModuleView_{item.Name}"] as ModuleViewModel;

                        vm.UpdateVersions(Updates);
                        //viewModeTable[$"ModuleView_{item.Name}"] = new ModuleViewModel(vm.UnitVersions, Updates);

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
        public async Task<ReactiveCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
        //public async Task<ObservableCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
        {
            bool ret;

            //if( lcu.LcuCtrl == null || lcu.IsSelected == false)
            if( lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
            {
                return null;
            }

            string tmpDir = Path.GetTempPath();

            string infoFile = Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
            string lcuRoot = $"LCU_{module.Pos}\\MCFiles\\";
            //string lcuPath = "/MCFiles/ "+ Define.MC_PERIPHERAL_PATH;

            // UpdateCommon.inf を取得するフォルダを作成
            ret = lcu.LcuCtrl.CreateFtpFolders(new List<string> { infoFile }, lcuRoot);

            //装置から UpdateCommon.inf を取得してテンポラリに保存
            ret = await lcu.LcuCtrl.GetMachineFile(lcu.Name, machine.Name, module.Pos, infoFile,lcuRoot,tmpDir);

            if( ret == false)
            {
                Debug.WriteLine("GetMachineFile Error");
                return null;
            }
            IniFileParser parser = new(tmpDir + Define.UPDATE_INFO_FILE);

            //テンポラリに生成したファイルを削除
            System.IO.File.Delete(tmpDir + Define.UPDATE_INFO_FILE);

            IList<string> sec = parser.SectionCount();

            //バージョン情報を生成
            ReactiveCollection<UnitVersion> versions = [];
            //ObservableCollection<UnitVersion> versions = [];

            foreach (var unit in sec)
            {
                UnitVersion version = new()
                {
                    Name = unit,
                    Attribute = parser.GetValue(unit, "Attribute"),
                    Path = parser.GetValue(unit, "Path"),
                    CurVersion = parser.GetValue(unit, "Version"),
                    NewVersion = "N/A",
                    Parent = module
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

 /*
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
*/
        /// <summary>
        /// ライン情報を取得する
        /// </summary>
        private async void LoadLineInfo()
        {
            /*
            NeximDataControl.NeximDataControlApi nexim = new();

            List<object> lines = new List<object>();
            
            NeximDataControl.Common.NeximDataControlApiCode r = nexim.GetLines(null, ref lines);
            */

            TreeViewItems = new ReactiveCollection<LcuInfo>
            {
                new (){ Name = "localhost:9000", ItemType=MachineType.LCU},
                new (){ Name = "ch-lcu33",       ItemType=MachineType.LCU},
            };
            TreeViewItems.ObserveAddChanged().Subscribe( x => Debug.WriteLine(x.Name));

            /*
            TreeViewItems = new ObservableCollection<LcuInfo>
            {
                // Add Localhost[Debuge用 -> localhost:9000で仮想LCU(WebAPIサーバーを起動して確認する)]
                new (){ Name = "localhost:9000",  ItemType=MachineType.LCU},
                //new (){ Name = "localhost:9000", IsSelected = true,  ItemType=MachineType.LCU},
                //new (){ Name = "DESKTOP-P98TLDK",IsSelected = true,  ItemType=MachineType.LCU},
                //new (){ Name = "ch-lcu33",       IsSelected = false, ItemType=MachineType.LCU},
            };
        */
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

                // デバッグ用のFTPアカウント情報を設定
                lcu.FtpUser = data.username;
                lcu.FtpPassword = password;
                lcu.LcuCtrl.FtpUser = lcu.FtpUser;
                lcu.LcuCtrl.FtpPassword = lcu.FtpPassword;

                // LCU バージョン取得
                str = await lcu.LcuCtrl.LCU_Command(LcuVersion.Command());
                IList<LcuVersion> versionInfo = LcuVersion.FromJson(str);
                lcu.Version = versionInfo.Where(x => x.itemName == "Fuji LCU Communication Server Service").First().itemVersion;
            }

            //装置情報が未取得の場合
            if ( lcu.Children.Count == 0)
            {
                // Machine 情報を登録
                XmlSerializer serializer = new(typeof(LineInfo));
                string response = await lcu.LcuCtrl.LCU_HttpGet("lines");

                if( response.Contains("errorCode") )
                {
                    return false;
                }

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
                        Machine = mc,
                        Parent = lcu,
                    };
                    foreach (var base_ in mc.Bases)
                    {
                        BaseInfo baseInfo = new()
                        {
                            Name = "",
                            ItemType = MachineType.Base,
                            Base = base_,
                            Parent = machine,
                        };
                        machine.Bases.Add(baseInfo);

                        foreach (var module in base_.Modules)
                        {
                            ModuleInfo moduleItem = new()
                            {
                                Name = module.DispModule,
                                ItemType = MachineType.Module,
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
