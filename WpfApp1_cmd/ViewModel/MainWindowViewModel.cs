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

namespace WpfApp1_cmd.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        [DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int getMcUser(StringBuilder s, Int32 len);
        [DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        static extern int getMcPass(StringBuilder s, Int32 len);

        public MetroWindow Metro { get; set; } = System.Windows.Application.Current.MainWindow as MetroWindow;

        public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();
        public CancellationToken Token => Cts.Token;

        public ReactiveCommand WindowLoadedCommand { get; } = new ReactiveCommand();
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

        /// <summary>
        /// 
        /// </summary>
        private async void LoadLineInfo_Start()
        {
            var controller = await Metro.ShowProgressAsync("please wait...", "message");
            
            for(var i = 0; i < 10; i++)
            {
                controller.SetProgress(1.0 / 10 * i);
                controller.SetMessage($"message {i}");
                await Task.Delay(1000);
            }
            
            //var r = DialogHost.Show(new WaitProgress("ユニット情報読み込み中"));    //時間がかかるので、クルクルを表示
            LoadLineInfo();
            //DialogHost.CloseDialogCommand.Execute(null, null);

            OnPropertyChanged(nameof(TreeViewItems));

            await controller.CloseAsync();
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
            WindowLoadedCommand.Subscribe(() => LoadLineInfo_Start());

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
                /*
                ModuleInfo item = SelectedItem as ModuleInfo;
                if (item != null)
                {
                    MachineInfo mc = item.Parent as MachineInfo;
                }
                */
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
        async void LcuNetworkChkCmd()
        {
            var result = await MsgBox.Show("Error","ErrorCode=E001","IP Address Error","サーバーに接続できませんでした",(int)(MsgDlgType.OK| MsgDlgType.ICON_ERROR));

            if( (string)result == "OK")
            {
                Debug.WriteLine("Click OK");
            }
            else
            {
                Debug.WriteLine("Click out");
            }
            /*
            await Metro.ShowMessageAsync("Error", "ErrorCode=E001", MessageDialogStyle.Affirmative, new MetroDialogSettings() { AffirmativeButtonText = "OK" });

            var controller = await Metro.ShowProgressAsync("please wait...", "message");
            for(var i = 0; i < 10; i++)
            {
                controller.SetProgress(1.0 / 10 * i);
                controller.SetMessage($"message {i}");
                await Task.Delay(1000);
            }
            await controller.CloseAsync();
            */
        }

        /// <summary>
        /// LCU のディスク容量を確認する
        /// </summary>
        public async void LcuDiskChkCmd()
        {
            LcuInfo lcu = SelectedItem as LcuInfo;
            if(lcu == null || lcu.LcuCtrl == null)
            {
                return;
            }
            CancelTokenSrc = new CancellationTokenSource();
            CancellationToken token = CancelTokenSrc.Token;

            AddLog($"{lcu.Name}::Check Disk Space");
            List<LcuDiskInfo>? info = await lcu.LcuCtrl.LCU_DiskInfo(token);
            if( info == null)
            {
                AddLog($"{lcu.Name}::Don't get disk space information.");
                CancelTokenSrc.Dispose();
                return;
            }
            foreach (var item in info)
            {
                AddLog($"Drive: {item.driveLetter}, Total: {item.total}, Use: {item.use}, Free: {item.free}");
            }
            CancelTokenSrc.Dispose();
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
            var r = await DialogHost.Show(new MyMessageBox());
            DialogHost.CloseDialogCommand.Execute(null, null);

            if(r == null || r.ToString() == "CANCEL")
            {
                return;
            }

            AddLog("StartTransfer");

            CanTransferStartFlag.Value = false;
            CanTransferStopFlag.Value = true;
            CanAppQuitFlag.Value = false;

            if (Options.HasSwitch("--backup") == true)
            {
                ret = await BackupUnitData();
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
/*
            CanTransferStartFlag.Value = true;
            CanTransferStopFlag.Value = false;
            CanAppQuitFlag.Value = true;
*/
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
        ///  選択状態にある装置のデータをバックアップする
        /// </summary>
        /// <returns></returns>
        public async Task<bool> BackupUnitData()
        {
            CancelTokenSrc = new CancellationTokenSource();
            CancellationToken token = CancelTokenSrc.Token;

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

                            bool ret = await DownloadModuleFiles(lcu, machine, module, Define.LOCAL_BACKUP_PATH,token);
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
                                var r = await DialogHost.Show(new MyMessageBox());
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
            List<string> folders = module.UnitVersions.Select(x => Path.GetDirectoryName(x.Path)).ToList().Distinct().ToList();

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
        public async void TreeViewSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
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
                    CanExecuteLcuCommand.Value = false; // LCU コマンドを実行不可にする
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
                                        IsTreeEnabled.Value = false;
                                        var r = DialogHost.Show(new WaitProgress("ユニット情報読み込み中"));    //時間がかかるので、クルクルを表示

                                        CancellationTokenSource tokenSource = new();

                                        var ret = await CreateVersionInfo(lcu, machine, module, tokenSource.Token);
                                        DialogHost.CloseDialogCommand.Execute(null, null);
                                        IsTreeEnabled.Value = true;

                                        tokenSource.Dispose();

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
                                    else
                                    {
                                        viewModeTable.Add($"ModuleView_{item.Name}", new ModuleViewModel(module.UnitVersions, Updates));
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
        public async Task<ReactiveCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module, CancellationToken token)
        //public async Task<ObservableCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
        {
            bool ret;

            if( lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
            {
                return null;
            }

            string tmpDir = Path.GetTempPath();

            string infoFile = Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
            string lcuRoot = $"LCU_{module.Pos}\\MCFiles\\";
            //string lcuPath = "/MCFiles/ "+ Define.MC_PERIPHERAL_PATH;

            // UpdateCommon.inf を取得するフォルダを作成
            ret = lcu.LcuCtrl.CreateFtpFolders(new List<string> { Path.GetDirectoryName(infoFile) }, lcuRoot);

            //装置から UpdateCommon.inf を取得してテンポラリに保存
            ret = await lcu.LcuCtrl.GetMachineFile(lcu.Name, machine.Name, module.Pos, infoFile,lcuRoot,tmpDir, token);

            if( ret == false)
            {
                AddLog($"{lcu.Name}:{machine.Name}:{module.Pos}={infoFile} Get error");
                return null;
            }
            IniFileParser parser = new(tmpDir + Define.UPDATE_INFO_FILE);

            //テンポラリに生成したファイルを削除
            System.IO.File.Delete(tmpDir + Define.UPDATE_INFO_FILE);

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
                    UnitVersion version = new()
                    {
                        Name = unit,
                        Attribute = parser.GetValue(unit, "Attribute"),
                        Path = parser.GetValue(unit, "Path"),
                        CurVersion = parser.GetValue(unit, "Version"),
                        NewVersion = newVer,
                        Parent = module
                    };
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
                        versions.Add(version);
                    }
                }
            }
            return versions;
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

        /// <summary>
        /// ライン情報を取得する
        /// </summary>
        private async void LoadLineInfo()
        {
            AddLog("LoadLineInfo");
            /*
            NeximDataControl.NeximDataControlApi nexim = new();

            List<object> lines = new List<object>();
            
            NeximDataControl.Common.NeximDataControlApiCode r = nexim.GetLines(null, ref lines);
            */

            TreeViewItems = new ReactiveCollection<LcuInfo>
            {
                new (){ Name = "localhost:9000", ItemType=MachineType.LCU},
                //new (){ Name = "ch-lcu33",       ItemType=MachineType.LCU},
            };
            TreeViewItems.ObserveAddChanged().Subscribe( x => Debug.WriteLine(x.Name));

            // LCU, LCU下のライン 情報を取得
            CancellationTokenSource tokenSource = new();
            foreach (var lcu in TreeViewItems)
            {
                bool ret = await UpdateLcuInfo(lcu, tokenSource.Token);
            }
            tokenSource.Dispose();
        }

        /// <summary>
        /// LCUの情報を更新する
        /// </summary>
        /// <param name="LcuInfo">LCU情報</param>
        public async Task<bool> UpdateLcuInfo(LcuInfo lcu, CancellationToken token) 
        {
            if( lcu.LcuCtrl == null)
            {
                lcu.LcuCtrl = new LcuCtrl(lcu.Name);
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

                            var ret = await CreateVersionInfo(lcu, machine, moduleItem, token);
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
