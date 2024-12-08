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
using System.Windows.Media;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Input;
using System.IO.Packaging;
using System.Net.NetworkInformation;

namespace WpfApp1_cmd.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        [DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int getMcUser(StringBuilder s, Int32 len);
        [DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int getMcPass(StringBuilder s, Int32 len);

        [DllImport("JigFormat.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int GetDataCount(StringBuilder s, UInt32 len);
        [DllImport("JigFormat.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int ConvertJigData(int cnt, byte[] pData, UInt32 size, UIntPtr pInfo);
        [DllImport("JigFormat.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int ConvertJigData2(int cnt, byte[] pData, UInt32 size, UIntPtr pInfo);
        [StructLayout(LayoutKind.Sequential)]
        private struct JIGDATA_INFO
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] //固定長文字列配列
            public string Filename;
            public int dataSize;
            public long dataAddr;
        }

        public MetroWindow Metro { get; set; } = System.Windows.Application.Current.MainWindow as MetroWindow;

        public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();
        public CancellationToken Token => Cts.Token;

        public ReactiveCommand WindowLoadedCommand { get; } = new ReactiveCommand();
        public ReactiveCommand WindowClosingCommand { get; } = new ReactiveCommand();

        public ReactiveCommand TreeViewCommand { get; } = new ReactiveCommand();

        /*
        private ObservableCollection<RichTextItem> _logMessage = [];// new RichTextItem() { Text = "original", Color = Colors.Wheat };
        public ObservableCollection<RichTextItem> LogMessage
        {
            get => _logMessage;
            set
            {
                _logMessage = value;
                OnPropertyChanged(nameof(LogMessage));
            }
        }
        */

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
        public void AddLog(string str)
        {
            LogData += "\n";
            LogData += str;

            /*
            RichTextItem item = new RichTextItem()
            {
                Text = log,
                Color = Colors.White
            };
            LogMessage.Add(item);
            */
            //OnPropertyChanged(nameof(LogMessage));
            Debug.WriteLine(str);
            if(LogWriter != null)
            {
                LogWriter.WriteLine(str);
            }
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
        private string DataFolder { get; set; } = "";   // UpdateCommon.inf のフォルダ
        public long DataSize { get; set; } = 0;         // UpdateCommon.inf のフォルダのサイズ

        private ReactiveCollection<UpdateInfo> _upDates;
        private ReactiveCollection<UpdateInfo>? Updates
        {
            get => _upDates;
            set {
                _upDates = value;
                OnPropertyChanged(nameof(Updates));
            }
        }
/*
        private bool flag = true;
        public bool Flag
        {
            get { return flag; }
            set { flag = value; ButtonCommand.DelegateCanExecute(); }
        }
*/
        public DelegateCommand<string> ScreenTransitionCommand { get; }

        public ReactiveCommand FileOpenCommand { get; } = new ReactiveCommand();
        public ReactiveCommand LcuNetworkChkCommand { get; } = new ReactiveCommand();
        public ReactiveCommand LcuDiskChkCommand { get; } = new ReactiveCommand();
/*
        public DelegateCommand ButtonCommand { get; }
        public ReactiveCommand ButtonCommand2 { get; } = new ReactiveCommand();

        public ReactiveCommand CutCommand { get; private set; } = new ReactiveCommand();
        public ReactiveCommand CopyCommand { get; } = new ReactiveCommand();
        public ReactiveCommand PasteCommand { get; } = new ReactiveCommand();
*/

        // 「Transfer」「Stop」「App Quit」のフラグ
        public ReactiveProperty<bool> CanTransferStartFlag { get; } = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> CanTransferStopFlag { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> CanAppQuitFlag { get; } = new ReactiveProperty<bool>(true);

        private CancellationTokenSource? _cancellationTokenSource;
        public CancellationTokenSource? CancelTokenSrc { get => _cancellationTokenSource; set => _cancellationTokenSource = value; }

        // 「メニュー」の LCU コマンドの実行可否
        public ReactiveProperty<bool> CanExecuteLcuCommand { get; } = new ReactiveProperty<bool>(false);

        // TreeView の選択項目が変更されたときのコマンド
        public ReactiveCommand<RoutedPropertyChangedEventArgs<object>> TreeViewSelectedItemChangedCommand { get; }
//        public ReactiveCommand<RoutedPropertyChangedEventArgs<object>> LogMessageChangedCommand { get; }

        public string DialogTitle { get; set; } = "Dialog Title";
        public string DialogText { get; set; } = "Dialog Text";

        // 転送制御ボタン
        public ReactiveCommandSlim StartTransferCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim StopTransferCommand { get; } = new ReactiveCommandSlim();
        public ReactiveCommandSlim QuitApplicationCommand { get; } = new ReactiveCommandSlim();

        public ReactiveCommandSlim HomeCommand { get; } = new ReactiveCommandSlim();

        // TreeViewを操作可能か
        public ReactiveProperty<bool> IsTreeEnabled { get; } = new ReactiveProperty<bool>(true);

        //メニューの有効無効
        public bool IsFileMenuEnabled { get; set; } = true;
        public bool IsLcuMenuEnabled { get; set; } = true;

        public StreamWriter? LogWriter { get; set; } = null;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        ///  装置のFTPユーザー名を取得する(from DLL)
        /// </summary>
        /// <returns></returns>
        public static string GetMcUser()
        {
            try
            {
                // DLL内の関数呼び出し。 DLLが無い場合は例外が発生する
                StringBuilder sb = new StringBuilder(1024);
                Int32 len = getMcUser(sb, sb.Capacity);
                return "Administrator";
                //return sb.ToString();
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        /// <summary>
        /// 装置のFTPパスワードを取得する(from DLL)
        /// </summary>
        /// <returns></returns>
        public static string GetMcPass()
        {
            try
            {
                // DLL内の関数呼び出し。 DLLが無い場合は例外が発生する
                StringBuilder sb = new StringBuilder(1024);
                Int32 len = getMcPass(sb, sb.Capacity);
                return "password";
                //return sb.ToString();
            }
            catch(Exception ex)
            {
                return "";
            }
        }

        private void Startup_log()
        {
            string resultPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\UnitTransferResult";
            if( Directory.Exists(resultPath) == false)
            {
                DirectoryInfo info = Directory.CreateDirectory(resultPath);
            }
            DateTime dt = DateTime.Now;
            LogWriter = new StreamWriter($"{resultPath}\\Update_{ dt.ToString("yyyyMMdd")}.txt");

            if (LogWriter != null)
            {
                LogWriter.WriteLine("Start Application");
            }
        }
        /// <summary>
        /// 起動時、Windowsのロード後に呼ばれる
        /// </summary>
        public bool _lineInfoLoaded = false;        // line 情報の読み込み完了フラグ
        private async void LoadLineInfo_Start()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            //ログファイル
            Startup_log();

            //progress
            ProgressDialogController controller = await Metro.ShowProgressAsync("Read Machine information ...", "");

            controller.SetIndeterminate();// 進捗(?)をそれらしく流す・・・
            controller.SetCancelable(true); // キャンセルボタンを表示する

            Task task = Task.Run(() => { LoadLineInfo(controller, cts.Token); });

            Debug.WriteLine($"{nameof(task.IsCompleted)} ; {task.IsCompleted}");
            for (var i = 0; i < 1000; i++)
            {
                Debug.WriteLine($"{nameof(task.IsCompleted)} ; {task.IsCompleted}");
                if (task.IsCompleted)
                {
                    // タスクの完了を待つ(※なぜか正しく取得できないので、変数を使用する)
                    if (_lineInfoLoaded == true)
                    {
                        break;
                    }
                }
                if( controller.IsCanceled == true)
                {
                    cts.Cancel();
                    break;
                }

                await Task.Delay(100);
            }

            OnPropertyChanged(nameof(TreeViewItems));

            await controller.CloseAsync();
        }

        /// <summary>
        /// DecompBin.exe を呼び出す
        /// </summary>
        public void CallDecompExe()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = $"{ Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}\\DecompBin.exe";
            psi.Arguments =$"{DataFolder}\\Peripheral.bin";
            Process? p = Process.Start(psi);

            if(p != null)
            {
                p.WaitForExit();
            }   
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindowViewModel()
        {
            //オプション処理
            string dataFolder = Options.GetOption("--dataFolder", "");
            if(dataFolder != "")
            {
                DataFolder = dataFolder;

                if( Path.Exists(dataFolder + "\\Peripheral.bin") == true)
                {
                    //指定フォルダにPeripheral.bin がある場合は、DecompBin.exe を呼び出す
                    // Decompbin.exe は、Peripheral.bin を展開して、C:\\DeCompBin フォルダに展開する
                    CallDecompExe();
                    DataFolder = @"C:\DeCompBin\Fuji\System3\Program\Peripheral";
                }
                Updates = ReadUpdateCommon(dataFolder + "\\UpdateCommon.inf");
                AddLog($"Read {dataFolder}/UpdateCommon.inf");
                IsFileMenuEnabled = true;
            }
            else
            {
                DataFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                Updates = [];
                IsFileMenuEnabled = true;
                IsLcuMenuEnabled = true;
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
            WindowLoadedCommand.Subscribe(() => LoadLineInfo_Start() );
            WindowClosingCommand.Subscribe(() => ApplicationShutDown() );

            // メニュー実行制御(Subscribe() するより先に設定する)
            LcuNetworkChkCommand = CanExecuteLcuCommand.ToReactiveCommand();
            LcuDiskChkCommand = CanExecuteLcuCommand.ToReactiveCommand();

            // メニューコマンドの設定 
            FileOpenCommand.Subscribe(() => FileOpenCmd());
            LcuNetworkChkCommand.Subscribe(() => { LcuNetworkChkCmd(); });
            LcuDiskChkCommand.Subscribe(() => { LcuDiskChkCmd(); });

            HomeCommand.Subscribe(() =>
            {
                ActiveView = viewModeTable["UpdateVersionView"];
            });
            /*
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
            */
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
            /*
                        CutCommand = new[] { FlagProperty1, FlagProperty2, FlagProperty3 }
                            .CombineLatest(x => x.Any(y => y)).ToReactiveCommand();

                        CutCommand.Subscribe(() => CutCmdExecute());

                        CopyCommand.Subscribe(() => { Debug.WriteLine("Copy"); });
                        PasteCommand.Subscribe(() => { Debug.WriteLine("Paste"); });


                        LogMessageChangedCommand = new ReactiveCommand<RoutedPropertyChangedEventArgs<object>>();
                        //LogMessageChangedCommand.Subscribe(args => LogMessageChanged(args));
            */

            // ツリービューの選択項目が変更されたときのコマンド
            TreeViewSelectedItemChangedCommand = new ReactiveCommand<RoutedPropertyChangedEventArgs<object>>();
            TreeViewSelectedItemChangedCommand.Subscribe(args => TreeViewSelectedItemChanged(args));

            // 転送制御ボタン
            StartTransferCommand = CanTransferStartFlag.ToReactiveCommandSlim();
            StartTransferCommand.Subscribe(() => StartTransfer() );

            StopTransferCommand = CanTransferStopFlag.ToReactiveCommandSlim();
            StopTransferCommand.Subscribe(() => StopTransferExecute() );

            QuitApplicationCommand = CanAppQuitFlag.ToReactiveCommandSlim();
            QuitApplicationCommand.Subscribe(() => ApplicationShutDown() );

            //TreeView 右クリックメニューのテスト
            TreeViewCommand.Subscribe((x) => { TreeViewMenu(x); });

/*
 C++ DLL関数 呼び出しテスト
            var pst = Marshal.AllocHGlobal( Marshal.SizeOf(typeof(JIGDATA_INFO)) * 4 );
            var fs = new FileStream("C:\\Users\\ka.makihara\\aa.bin", FileMode.Open, FileAccess.Read);

            byte[] data = new byte[fs.Length];
            int sz = fs.Read(data, 0, data.Length);
            fs.Dispose();

            ConvertJigData2(4,data,(uint)sz, (UIntPtr)pst);

            JIGDATA_INFO[] st_rtn = new JIGDATA_INFO[4];
            for(int i = 0; i < 4; i++)
            {
                st_rtn[i] = (JIGDATA_INFO)Marshal.PtrToStructure(pst + i * Marshal.SizeOf(typeof(JIGDATA_INFO)), typeof(JIGDATA_INFO));
                AddLog($"{st_rtn[i].Filename}::{st_rtn[i].dataSize}::{st_rtn[i].dataAddr}");
            }
            Marshal.FreeHGlobal(pst);
*/
        }

        private List<string> GetLcuListFromNexim()
        {
            List<string> lcuList = [];

            string connStr = "User Id=makihara;Password=wildgeese;Data Source=localhost:1521/XEPDB1";
            //string connStr = "User Id=fujisuperuser;Password=em2g86fzjt945p73; Data Source=10.0.51.64:1521/neximdb";
            using(OracleConnection conn = new(connStr))
            {
                try
                {
                    conn.Open();
                    OracleCommand cmd = new("select * from LINE WHERE JOBID=0 AND ACTIVEFLG=1", conn);

                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;

                    OracleDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        int lineId = reader.GetInt32(0);
                        int jobId = reader.GetInt32(1);
                        string name = reader.GetString(2);
                        int lcuId = reader.GetInt32(3);
                        int activeFlag = reader.GetInt32(4);

                        /*
                        NeximPC DBの場合
                        int jobId = reader.GetInt32(0);
                        int lineId = reader.GetInt32(1);
                        string name = reader.GetString(2);
                        int activeFlag = reader.GetInt32(6);
                        int lcuId = reader.GetInt32(18);
                        */
                        AddLog($"{name}::LineID={lineId} JobID={jobId} ActiveFlag={activeFlag} LcuID={lcuId}");

                        OracleCommand cm = new($"select * from COMPUTER WHERE COMPUTERID={lcuId}", conn);
                        OracleDataReader rd = cm.ExecuteReader();
                        while (rd.Read())
                        {
                            int computerId = rd.GetInt32(0);
                            string lcuName = rd.GetString(1);
                            AddLog($"ComputerID={computerId} LcuName={lcuName}");
                            lcuList.Add($"{name}={lcuName}");
                        }

                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            return lcuList;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        private void TreeViewMenu(object x)
        {
            CheckableItem item = x as CheckableItem;

            Debug.WriteLine($"TreeViewMenu:{item.Name}");

            List<string> lcuList = GetLcuListFromNexim();

        }

        /// <summary>
        ///  ユニットアップデートデータのフォルダを選択して読み込む
        /// </summary>
        public async void FileOpenCmd()
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
                        var result = await MsgBox.Show("Error","ErrorCode=E004","Can not Found UpdateInfo","UpdateCommon.inf not exist.",(int)(MsgDlgType.OK| MsgDlgType.ICON_ERROR),"DataGridView");
                        return;
                    }
                    Updates = ReadUpdateCommon(cofd.FileName + "\\UpdateCommon.inf");

                    DirectoryInfo di = new DirectoryInfo(cofd.FileName);

                    long dataSize = Utility.GetDirectorySize(di);

                    //バージョン情報ビューの更新
                    viewModeTable["UpdateVersionView"] = new UpdateVersionViewModel(Updates);

                    //アップデートデータが変更されたので、 モジュールのバージョン情報をクリアする
                    foreach (LcuInfo lcu in TreeViewItems)
                    {
                        foreach(MachineInfo machine in lcu.Children)
                        {
                            foreach (ModuleInfo module in machine.Children)
                            {
                                module.UnitVersions.Clear();
                            }
                        }
                    }

                    // モジュールビューを再生成する
                    CancellationTokenSource cts = new CancellationTokenSource();
                    foreach (LcuInfo lcu in TreeViewItems)
                    {
                        foreach(MachineInfo machine in lcu.Children)
                        {
                            foreach (ModuleInfo module in machine.Children)
                            {
                                var ret = await CreateVersionInfo(lcu, machine, module, null, cts.Token);
                                if(ret != null)
                                {
                                    module.UnitVersions = ret;
                                    if( viewModeTable.ContainsKey($"ModuleView_{module.Name}") == true)
                                    {
                                        viewModeTable[$"ModuleView_{module.Name}"] = new ModuleViewModel( module.UnitVersions,Updates);
                                    }
                                    else
                                    {
                                        viewModeTable.Add($"ModuleView_{module.Name}", new ModuleViewModel(module.UnitVersions, Updates));
                                    }
                                }   
                            }
                        }   
                    }
                    cts.Dispose();

                    ModuleInfo? item = SelectedItem as ModuleInfo;
                    if(item != null)
                    {
                        ActiveView = viewModeTable[$"ModuleView_{item.Name}"];
                    }
                }
            }
        }

        /// <summary>
        ///  LCU の接続状態を確認する
        /// </summary>
        async void LcuNetworkChkCmd()
        {
            var result = await MsgBox.Show("Error","ErrorCode=E001","IP Address Error","サーバーに接続できませんでした",(int)(MsgDlgType.OK| MsgDlgType.ICON_ERROR),"DataGridView");

            if( (string)result == "OK")
            {
                Debug.WriteLine("Click OK");
            }
            else
            {
                Debug.WriteLine("Click out");
            }
        }

        /// <summary>
        /// LCU のディスク容量を確認する
        /// </summary>
        public async Task<long> LcuDiskChkCmd()
        {
            LcuInfo lcu = SelectedItem as LcuInfo;
            if(lcu == null || lcu.LcuCtrl == null)
            {
                return 0;
            }
            CancelTokenSrc = new CancellationTokenSource();
            CancellationToken token = CancelTokenSrc.Token;

            AddLog($"{lcu.Name}::Check Disk Space");
            List<LcuDiskInfo>? info = await lcu.LcuCtrl.LCU_DiskInfo(token);
            if( info == null)
            {
                AddLog($"{lcu.Name}::Don't get disk space information.");
                CancelTokenSrc.Dispose();
                return 0;
            }
            foreach (var item in info)
            {
                AddLog($"Drive: {item.driveLetter}, Total: {item.total}, Use: {item.use}, Free: {item.free}");
            }
            CancelTokenSrc.Dispose();

            long diskSpace = long.Parse(info.Find(x => x.driveLetter == "D").free);

            return diskSpace;
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
        /// ping によるネットワーク接続確認
        /// </summary>
        /// <param name="name"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private static async Task<bool> CheckComputer(string name, int count)
        {
            var ping = new System.Net.NetworkInformation.Ping();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    var reply = ping.Send(name);
                    if (reply.Status == IPStatus.Success)
                    {
                        return true;
                    }
                    await Task.Delay(100);
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
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
        public static long GetPeripheradSize(string path)
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
        /// 転送開始(StartTransferCommand から呼び出される)
        /// </summary>
        private async void StartTransfer()
        {
            bool ret = true;

            /*
            var result = await MsgBox.Show("確認","Start Transfer","","",(int)(MsgDlgType.OK_CANCEL| MsgDlgType.ICON_INFO));
            if( result != null && (string)result == "CANCEL")
            {
                return;
            }
            */
            
            DialogTitle = "確認";
            DialogText = "転送を開始しますか？";
            var r = await DialogHost.Show(new MyMessageBox(),"DataGridView");
            DialogHost.CloseDialogCommand.Execute(null, null);

            if(r == null || r.ToString() == "CANCEL")
            {
                return;
            }

            AddLog("StartTransfer");

            CanTransferStartFlag.Value = false;
            CanTransferStopFlag.Value = true;
            CanAppQuitFlag.Value = false;

            string? backupPath = Options.GetOption("--backup");

            if( backupPath != null)
            {
                ret = await BackupUnitData(backupPath);
            }

            if (ret == true)
            {
                //ret = await TransferExecute();
            }

            CanTransferStartFlag.Value = true;
            CanTransferStopFlag.Value = false;
            CanAppQuitFlag.Value = true;
        }

        /// <summary>
        ///  転送中止
        /// </summary>
        private void StopTransferExecute()
        {
            AddLog("StopTransfer Command");

            //CancellationToken.ThrowIfCancellationRequested();

            if(CancelTokenSrc != null)
            {
                CancelTokenSrc.Cancel();
            }
        }

        /// <summary>
        ///  アプリ終了
        /// </summary>
        private async void ApplicationShutDown()
        {
            DialogTitle = "確認";
            DialogText = "アプリケーションを終了しますか？";

            var r = await DialogHost.Show(new MyMessageBox(), "DataGridView");
            DialogHost.CloseDialogCommand.Execute(null, null);

            if (r == null || r.ToString() == "CANCEL")
            {
                return;
            }
            if(LogWriter != null)
            {
                LogWriter.Close();
            }

            //Peripheral.bin の展開フォルダを削除
            Directory.Delete("C:\\DeCompBin", true);

            Application.Current.Shutdown();
        }
        /// <summary>
        ///  選択状態にある装置のデータをバックアップする
        /// </summary>
        /// <returns></returns>
        public async Task<bool> BackupUnitData(string path)
        {
            CancelTokenSrc = new CancellationTokenSource();
            CancellationToken token = CancelTokenSrc.Token;
            DateTime dt = DateTime.Now;

            string hd = dt.ToString("yyyyMMdd_HHmmss_");

            foreach (var lcu in TreeViewItems)
            {
                if (lcu.Children == null || lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
                {
                    continue;
                }
                foreach (var machine in lcu.Children)
                {
                    if( machine.Children == null || machine.Name == null || machine.IsSelected.Value == false)
                    {
                        continue;
                    }
                    foreach (var module in machine.Children)
                    {
                        if (module.IsSelected.Value == false || module.UnitVersions == null)
                        {
                            continue;
                        }

                        // UpdateCommon.inf の Path のフォルダにファイルを転送する
                        //   ※ UpdateCommon.inf に記載されているデータは存在するものとする
                        //       指定されたフォルダにあるUpdateCommon.inf を読み込んでいるので
                        //
                        try
                        {
                            AddLog($"{lcu.Name}::{machine.Name}::{module.Name}::Backup Start");
                            string bkupPath = path + $"\\{hd}{lcu.Name.Split(":")[0]}_{machine.Name.Split(":")[0]}_{module.Name}";

                            if (Directory.Exists(bkupPath) == false)
                            {
                                Directory.CreateDirectory(bkupPath);
                            }

                            bool ret = await DownloadModuleFiles(lcu, machine, module, bkupPath, token);
                            //await Task.Delay(3000,token);

                            AddLog($"{lcu.Name}::{machine.Name}::{module.Name}::Backup End");

                            // LCU 上に作成、転送したファイルを削除する
                            //lcu.LcuCtrl.ClearFtpFolders(Define.LCU_ROOT_PATH);
                        }
                        catch (Exception ex)
                        {
                            //if (CanTransferStopFlag.Value == false)
                            //{
                                //「Stop」ボタンが押された
                                DialogTitle = "確認";
                                DialogText = "転送を中止しますか？";
                                var r = await DialogHost.Show(new MyMessageBox(),"DataGridView");
                                DialogHost.CloseDialogCommand.Execute(null, null);

                                CancelTokenSrc.Dispose();
                                if (r != null && r.ToString() == "OK")
                                {
                                    return false;
                                }
                                CancelTokenSrc = new CancellationTokenSource();
                                token = CancelTokenSrc.Token;
                            //}
                        }
                    }
                }
            }
            return true;
        }
        /// <summary>
        /// データ転送
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TransferExecute()
        {
            CancelTokenSrc = new CancellationTokenSource();
            CancellationToken token = CancelTokenSrc.Token;

            foreach(var lcu in TreeViewItems)
            {
                if (lcu.Children == null || lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
                {
                    continue;
                }
                foreach (var machine in lcu.Children)
                {
                    if( machine.Children == null || machine.Name == null || machine.IsSelected.Value == false)
                    {
                        continue;
                    }
                    foreach(var module in machine.Children)
                    {
                        if( module.IsSelected.Value == false || module.UnitVersions == null )
                        {
                            continue;
                        }

                        // UpdateCommon.inf の Path のフォルダにファイルを転送する
                        //   ※ UpdateCommon.inf に記載されているデータは存在するものとする
                        //       指定されたフォルダにあるUpdateCommon.inf を読み込んでいるので
                        //
                        bool ret = await UploadModuleFiles(lcu, machine, module, token);

                        // LCU 上に作成、転送したファイルを削除する
                        lcu.LcuCtrl.ClearFtpFolders(Define.LCU_ROOT_PATH);
                    }
                }
            }
            CancelTokenSrc.Dispose();
            return true;
        }

        /// <summary>
        ///  バージョン情報にあるデータを装置から取得する
        /// </summary>
        /// <param name="lcu"></param>
        /// <param name="machine"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public async Task<bool> DownloadModuleFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string backupPath, CancellationToken token)
        {
            bool ret;

            if( Updates == null)
            {
                return false;
            }

            //装置から取得したUpdateCommon.inf のパスのみを取り出してリスト化(重複を削除)
            //    ※パスはファイル名を含むので、ファイル名を削除してフォルダのみを取り出す
            //List<string> folders = module.UnitVersions.Select(x => Path.GetDirectoryName(x.Path)).ToList().Distinct().ToList();
            List<string> folders = module.UpdateFiles().Select(x => Path.GetDirectoryName(x)).ToList().Distinct().ToList();

            //LCU上にフォルダを作成する(装置からファイルを取得するフォルダ)
            string lcuRoot = $"LCU_{module.Pos}\\MCFiles\\";
            //string lcuRoot = "/MCFiles/";
            ret = lcu.LcuCtrl.CreateFtpFolders(folders, lcuRoot);
            if( ret == false)
            {
                AddLog($"{lcu.Name}::CreateFtpFolders Error");
                return ret;
            }

            try
            {
                AddLog($"{lcu.Name}::{machine.Name}::{module.Name} GetMcFileList");
                List<string> fileList = [];

                string mcUser = GetMcUser();
                string mcPass = GetMcPass();
                foreach (var folder in folders)
                {
                    AddLog($"GetMCFileList::{folder}");
                    string cmdRet = await lcu.LcuCtrl.LCU_Command(GetMcFileList.Command(machine.Name, module.Pos, mcUser, mcPass, folder),token);
                    GetMcFileList? list = GetMcFileList.FromJson(cmdRet);

                    // 取得結果から、フォルダとファイルのリストを作成
                    if (list != null)
                    {
                        if (list.ftp != null && list.ftp.data != null)
                        {
                            foreach (var item in list.ftp.data)
                            {
                                foreach (var file in item.list)
                                {
                                    fileList.Add(item.mcPath + "/" + file.name);
                                }
                            }
                        }
                    }
                }

                //装置からLCUにファイルを取得する
                string retMsg = await lcu.LcuCtrl.LCU_Command(GetMcFile.Command(machine.Name, module.Pos, mcUser, mcPass, fileList, lcuRoot),token);

                if (retMsg == "" || retMsg == "Internal Server Error")
                {
                    AddLog($"{lcu.Name}::{machine.Name}::{module.Name}::GetMCFile Error");
                    return false;
                }
                // LCU からFTPでファイルを取得 
                ret = await lcu.LcuCtrl.DownloadFiles(lcuRoot, backupPath, fileList, token);

                //LCU 上に作成したファイルを削除
                ret = lcu.LcuCtrl.ClearFtpFolders(lcuRoot);
            }
            catch (Exception e)
            {
                AddLog("DownloadModuleFiles Error");
                throw;
            }
            return ret;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lcu"></param>
        /// <param name="machine"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        public async Task<bool> UploadModuleFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module, CancellationToken token)
        {
            bool ret;

            // UpdateCommon.inf の Path のフォルダにファイルを転送する
            //   ※ UpdateCommon.inf に記載されているデータは存在するものとする
            //       指定されたフォルダにあるUpdateCommon.inf を読み込んでいるので
            //
            //バージョン情報からパスのみを取り出してリスト化
            List<string> folders = module.UnitVersions.Select(x => Path.GetDirectoryName(x.Path)).ToList();

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
            string mcUser = GetMcUser();
            string mcPass = GetMcPass();
            string retMsg = await lcu.LcuCtrl.LCU_Command(PostMcFile.Command(machine.Name, module.Pos, mcUser, mcPass,folders, lcuRoot),token);

            if (retMsg == "" || retMsg == "Internal Server Error")
            {
                Debug.WriteLine("GetMCFile Error");
                return false;
            }
            return true;
        }

        /// <summary>
        ///  ツリーの選択状態が変更されたときの処理
        /// </summary>
        /// <param name="e"></param>
        public void TreeViewSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
        {
            CheckableItem? item = e.NewValue as CheckableItem;

            if ( item == null)
            {
                return;
            }
            SelectedItem = item;
            AddLog($"TreeViewSelectedItemChanged={item.Name}:{item.ItemType}");

            switch (item.ItemType)
            {
                case MachineType.LCU:
                    CanExecuteLcuCommand.Value = true;
                    if (viewModeTable.ContainsKey($"LcuView_{item.Name}") == false)
                    {
                        viewModeTable.Add($"LcuView_{item.Name}", new LcuViewModel(TreeViewItems.Where(x => x.Name == item.Name).First().Children));
                    }
                     ActiveView = viewModeTable[$"LcuView_{item.Name}"];
                    break;
                case MachineType.Machine:
                    CanExecuteLcuCommand.Value = false; // LCU コマンドを実行不可にする
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
                    CanExecuteLcuCommand.Value = false; //LCU コマンドを実行不可にする
                    if (viewModeTable.ContainsKey($"ModuleView_{item.Name}") == false)
                    {
                        ModuleInfo module = item as ModuleInfo;
                        if (module != null)
                        {
                            viewModeTable.Add($"ModuleView_{item.Name}", new ModuleViewModel(module.UnitVersions, Updates));
                        }
                    }
                    ActiveView = viewModeTable[$"ModuleView_{item.Name}"];
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
        public async Task<ReactiveCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module, ProgressDialogController? progCtrl, CancellationToken token)
        //public async Task<ObservableCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
        {
            bool ret;

            if ( lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
            {
                return null;
            }

            progCtrl?.SetMessage($"{lcu.LcuCtrl.Name}::{machine.Name}::{module.Name} Get Update Info");

            if (module.UpdateInfo == null)
            {
                string tmpDir = Path.GetTempPath();

                string infoFile = Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
                string lcuRoot = $"LCU_{module.Pos}\\MCFiles\\";
                //string lcuPath = "/MCFiles/ "+ Define.MC_PERIPHERAL_PATH;

                // UpdateCommon.inf を取得するフォルダを作成
                ret = lcu.LcuCtrl.CreateFtpFolders(new List<string> { Path.GetDirectoryName(infoFile) }, lcuRoot);

                //装置から UpdateCommon.inf を取得してテンポラリに保存
                ret = await lcu.LcuCtrl.GetMachineFile(machine.Name, module.Pos, infoFile, lcuRoot, tmpDir, token);

                if (ret == false)
                {
                    AddLog($"{lcu.LcuCtrl.Name}:{machine.Name}:{module.Pos}={infoFile} Get error");
                    return null;
                }
                module.UpdateInfo = new(tmpDir + Define.UPDATE_INFO_FILE);

                //テンポラリに生成したファイルを削除
                System.IO.File.Delete(tmpDir + Define.UPDATE_INFO_FILE);
            }
            IniFileParser parser = module.UpdateInfo;

            IList<string> sec = parser.SectionCount();

            //バージョン情報を生成
            ReactiveCollection<UnitVersion> versions = [];

            // ユニット一覧作成時のオプション
            //   --matchUnit が指定されている場合は、UpdateCommon.inf に記載されているユニットのみを対象とする
            bool opt  = Options.GetOptionBool("--matchUnit", false);
            bool only = Options.GetOptionBool("--diffOnly", false);

            foreach (var unit in sec)
            {
                //バージョンアップの中に該当するユニットのインデックスを取得(ない場合は -1)
                int idx = -1;
                string newVer = "N/A";

                if (Updates != null )
                {
                    idx = Updates.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(x => x.Value.Name == unit)?.Index ?? -1;
                }

                if (idx != -1)
                {
                    newVer = Updates[idx].Version;
                    if( only == true && parser.GetValue(unit, "Version") == newVer)
                    {
                        //同じバージョンは対象外とするオプション指定の場合
                        continue;
                    }
                    //データサイズを取得
                    string binPath = DataFolder + parser.GetValue(unit, "Path").Split("Peripheral")[1];
                    FileInfo fi = new FileInfo(binPath);

                    UnitVersion version = new()
                    {
                        Name = unit,
                        Attribute = parser.GetValue(unit, "Attribute"),
                        Path = parser.GetValue(unit, "Path"),
                        CurVersion = parser.GetValue(unit, "Version"),
                        NewVersion = newVer,
                        Parent = module,
                        Size = fi.Length
                    };
                    progCtrl?.SetMessage($"{lcu.LcuCtrl.Name}::{machine.Name}::{module.Name}::{unit}={version.CurVersion}");
                    versions.Add(version);
                }
                else
                {
                    //アップデートデータ内に対象ユニットが無い場合
                    if( opt == false)
                    {
                        //存在するユニット以外も対しようとする場合(--matchUnit==false)
                        UnitVersion version = new()
                        {
                            Name = unit,
                            Attribute = parser.GetValue(unit, "Attribute"),
                            Path = parser.GetValue(unit, "Path"),
                            CurVersion = parser.GetValue(unit, "Version"),
                            NewVersion = newVer,
                            Parent = module
                        };
                        progCtrl?.SetMessage($"{lcu.LcuCtrl.Name}::{machine.Name}::{module.Name}::{unit}={version.CurVersion}");
                        versions.Add(version);
                    }
                }
            }
            return versions;
        }
        
        private string _textValue = "Hello, World!";
        public string TextValue 
        {
            get => _textValue;
            set => SetProperty(ref _textValue,value);
        }

        /// <summary>
        /// ライン情報を取得する
        /// </summary>
        private async Task<bool> LoadLineInfo(ProgressDialogController progCtrl, CancellationToken token)
        {
            AddLog("LoadLineInfo Start");
            /*
            NeximDataControl.NeximDataControlApi nexim = new();

            List<object> lines = new List<object>();
            
            NeximDataControl.Common.NeximDataControlApiCode r = nexim.GetLines(null, ref lines);
            */

            // NeximDB より LCU のリストを取得(list=> Line名=LCU名)
            List<string> lcuList = GetLcuListFromNexim();

            TreeViewItems = [];
            /*
            TreeViewItems = new ReactiveCollection<LcuInfo>
            {
                new (){ Name = "localhost:9000", ItemType=MachineType.LCU},
                //new (){ Name = "ch-lcu33",       ItemType=MachineType.LCU},
            };
            */

            TreeViewItems.ObserveAddChanged().Subscribe( x => Debug.WriteLine(x.Name));
            foreach (var lcu in lcuList)
            {
                string lineName = lcu.Split('=')[0];
                string lcuName = lcu.Split('=')[1];
                TreeViewItems.Add(new LcuInfo(lcuName) { Name = lineName, ItemType = MachineType.LCU });
            }

            // LCU, LCU下のライン 情報を取得
            foreach (var lcu in TreeViewItems)
            {
                progCtrl.SetMessage($"reading {lcu.LcuCtrl.Name}");
                bool ret = await UpdateLcuInfo(lcu, progCtrl, token);
            }

            AddLog("LoadLineInfo End");
            _lineInfoLoaded = true;

            return true;
        }

        /// <summary>
        /// LCUの情報を更新する
        /// </summary>
        /// <param name="LcuInfo">LCU情報</param>
        public async Task<bool> UpdateLcuInfo(LcuInfo lcu,ProgressDialogController? progCtrl, CancellationToken token) 
        {
            /*
            if( lcu.LcuCtrl == null)
            {
                lcu.LcuCtrl = new LcuCtrl(lcu.Name);
            }
            */
            bool ping = await CheckComputer(lcu.LcuCtrl.Name.Split(':')[0], 3);
            if(ping == false)
            {
                return false;
            }

            if (lcu.LcuCtrl.FtpUser == null)
            {
                // FTPアカウント情報を取得
                var str = await lcu.LcuCtrl.LCU_Command(FtpData.Command(),token);
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
                str = await lcu.LcuCtrl.LCU_Command(LcuVersion.Command(),token);
                IList<LcuVersion> versionInfo = LcuVersion.FromJson(str);
                lcu.Version = versionInfo.Where(x => x.itemName == "Fuji LCU Communication Server Service").First().itemVersion;

                progCtrl?.SetMessage($"LCU:{lcu.LcuCtrl.Name} Version={lcu.Version}");

                //ディスク情報
                lcu.DiskSpace = await LcuDiskChkCmd();
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

                //ライン情報を保持
                lcu.LineInfo = lineInfo;

                foreach (var mc in lineInfo.Line.Machines)
                {
                    MachineInfo machine = new()
                    {
                        Name = mc.MachineName,
                        ItemType = MachineType.Machine,
                        Machine = mc,
                        Parent = lcu,
                    };
                    progCtrl?.SetMessage($"Machine={mc.MachineName}");
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
                            progCtrl?.SetMessage($"Module={module.DispModule}");
                            baseInfo.Children.Add(moduleItem);
                            machine.Children.Add(moduleItem);

                            var ret = await CreateVersionInfo(lcu, machine, moduleItem,progCtrl, token);
                            if(ret != null)
                            {
                                moduleItem.UnitVersions = ret;
                            }   
                        }
                    }
                    lcu.Children.Add(machine);
                }
            }
            return true;
        }

    }
}
