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
using WpfApp1_cmd.Windows;
using MaterialDesignThemes.Wpf;
using System.Runtime.InteropServices;

using WpfLcuCtrlLib;
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
using System.Text.Json;
using WpfApp1_cmd.Models;
using WpfApp1_cmd.Resources;
using FluentFTP;
using System.Net.Http;
using YamlDotNet.Serialization;
using System.Diagnostics.Eventing.Reader;

namespace WpfApp1_cmd.ViewModel
{
	public class MainWindowViewModel : ViewModelBase
	{
		[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
		static extern int getMcUser(StringBuilder s, Int32 len);
		[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
		static extern int getMcPass(StringBuilder s, Int32 len);
		[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
		static extern int getNeximDBName(StringBuilder name, Int32 len);
		[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
		static extern int getNeximDBUser(StringBuilder user, Int32 len);
		[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
		static extern int getNeximDBPass(StringBuilder pass, Int32 len);
		[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
		static extern int ExtractFolderFromImage(StringBuilder szDstRootFolder, byte[] pBin, UInt32 ImageSize, UIntPtr pstErr);
		[StructLayout(LayoutKind.Sequential)]
		private struct T_ERROR_INFO
		{
			public long errCode;
			public long subErrorCode;
		}

		/*
		// 暗号化ライブラリ(DLL)の関数 [ 現在は未使用 ]
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
		*/
		private readonly IDialogCoordinator _dialogCoordinator;

		public MetroWindow Metro { get; set; } = Application.Current.MainWindow as MetroWindow;

		public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();
		public CancellationToken Token => Cts.Token;

		public ReactiveCommand WindowLoadedCommand { get; } = new ReactiveCommand();
		public ReactiveCommand WindowClosingCommand { get; } = new ReactiveCommand();

		public ReactiveCommand TreeViewCommand { get; } = new ReactiveCommand();
		public ReactiveCommand TreeViewChkCommand { get; } = new ReactiveCommand();
/*
		public string IconKind
		{
			get
			{
				return Status switch
				{
					ItemStatus.NG => "AlertCircleOutline",
					ItemStatus.WARN => "AlertOutline",
					ItemStatus.UNKNOWN => "HelpCircleOutline",
					_ => "InformationOutline"
				};
			}
		}
		public bool IsIconVisible => Status != ItemStatus.OK;
*/
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
			if (LogWriter != null)
			{
				LogWriter.WriteLine(str);
			}
		}

		// ユニット連携情報
		//private Config _config;
		//public Config Config { get => _config; set => _config = value; }

		// ツリービューのアイテム
		public ReactiveCollection<LcuInfo> TreeViewItems { get; set; }

		// ツリービューの選択項目に対応するビューモデル
		private Dictionary<string, ViewModelBase> viewModeTable;
		public Dictionary<string, ViewModelBase> ViewModeTable { get => viewModeTable; set => viewModeTable = value; }

		// ツリーの選択項目
		public CheckableItem SelectedItem { get; set; }

		// ビューの切り替え
		private ViewModelBase activeView = null;
		public ViewModelBase ActiveView
		{
			get { return activeView; }
			set
			{
				if (activeView != value)
				{
					activeView = value;
					OnPropertyChanged(nameof(ActiveView));
				}
			}
		}
		private string DataFolder { get; set; } = "";   // UpdateCommon.inf のフォルダ
		public long UpdateDataSize { get; set; } = 0;   // UpdateCommon.inf のフォルダのサイズ

		public string DialogTitle { get; set; } = "Dialog Title";
		public string DialogText { get; set; } = "Dialog Text";

		//アップデート用のバージョンデータ
		private ReactiveCollection<UpdateInfo> _updateDataInfos;
		private ReactiveCollection<UpdateInfo>? UpdateDataInfos
		{
			get => _updateDataInfos;
			set
			{
				_updateDataInfos = value;
				OnPropertyChanged(nameof(UpdateDataInfos));
			}
		}
		private ReactiveCollection<UnitPath> _upDatePaths;
		public ReactiveCollection<UnitPath> UpdatePaths
		{
			get => _upDatePaths;
			set
			{
				_upDatePaths = value;
				OnPropertyChanged(nameof(UpdatePaths));
			}
		}

		public DelegateCommand<string> ScreenTransitionCommand { get; }

		public ReactiveCommand FileOpenCommand { get; } = new ReactiveCommand();
		public ReactiveCommand LcuNetworkChkCommand { get; } = new ReactiveCommand();
		public ReactiveCommand LcuDiskChkCommand { get; } = new ReactiveCommand();

		// 「Transfer」「Quit」のフラグ
		public ReactiveProperty<bool> CanTransferStartFlag { get; } = new ReactiveProperty<bool>(true);
		public ReactiveProperty<bool> CanAppQuitFlag { get; } = new ReactiveProperty<bool>(true);

		// 処理のキャンセルトークン
		//   変数に保持しておくことで、「Stop」ボタンでキャンセルできる->イベント処理内で、Cancel() しているので
		private CancellationTokenSource? _cancellationTokenSource;
		public CancellationTokenSource? CancelTokenSrc { get => _cancellationTokenSource; set => _cancellationTokenSource = value; }

		//Progressダイアログ(ライン情報読み込み時に使用
		private ProgressDialogController? _progress;
		public ProgressDialogController? Progress { get => _progress; set => _progress = value; }

		// 「メニュー」の LCU コマンドの実行可否
		public ReactiveProperty<bool> CanExecuteLcuCommand { get; } = new ReactiveProperty<bool>(false);

		// TreeView の選択項目が変更されたときのコマンド
		public ReactiveCommand<RoutedPropertyChangedEventArgs<object>> TreeViewSelectedItemChangedCommand { get; }

		public ReactiveCommand<MouseButtonEventArgs> TreeViewMouseLeftButtonDownCommand { get; }

		// 転送制御ボタン
		public ReactiveCommandSlim StartTransferCommand { get; } = new ReactiveCommandSlim();
		public ReactiveCommandSlim QuitApplicationCommand { get; } = new ReactiveCommandSlim();

		//Test Reult
		public ReactiveCommandSlim TestResultCommand { get; } = new ReactiveCommandSlim();

		public ReactiveCommandSlim HomeCommand { get; } = new ReactiveCommandSlim();

		// TreeViewを操作可能か
		public ReactivePropertySlim<bool> IsTreeEnabled { get; } = new ReactivePropertySlim<bool>(true);

		//メニューの有効無効
		public bool IsFileMenuEnabled { get; set; } = true;

		private bool _isLcuMenuEnabled;
		public bool IsLcuMenuEnabled
		{
			get => _isLcuMenuEnabled;
			set => SetProperty(ref _isLcuMenuEnabled, value);
		}

		public StreamWriter? LogWriter { get; set; } = null;

		public int TransferErrorCount { get; set; } = 0;    // PostMcFile でのエラー数
		public int TransferSuccessCount { get; set; } = 0;  // PostMcFile での成功数
		public long TransferCount { get; set; } = 0;          // 転送数(ユニット数)
		public long TransferedCount { get; set; } = 0;        // 転送済み数(ユニット数)
		private bool IsBackupedData { get; set; } = false;    // バックアップデータを読み込んだかどうか
		private long FtpTransferSize { get; set; } = 0;    // FTP 転送サイズ
		private long FtpTransferedSize { get; set; } = 0; // FTP 転送済みサイズ

		////////////////////////////////////////////////////////////////////////////////////////////////////////////

		/// <summary>
		/// エラー情報表示  
		/// </summary>
		public async void ShowErrorInfo(string title, string info,MsgDlgType type =(MsgDlgType.OK))
		{
			if (ErrorInfo.ErrCode != ErrorCode.OK)
			{
				//アップデートデータの読み込みでエラーが発生した場合
				await MsgBox.Show(title, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", info, (int)type, "MainWindow");
			}
		}

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
				//return "Administrator";
				return sb.ToString();
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
				return sb.ToString();
			}
			catch (Exception ex)
			{
				return "";
			}
		}

		/// <summary>
		/// NeximDB名を取得する(from DLL)
		/// </summary>
		/// <returns></returns>
		public static string GetNeximDBName()
		{
			if (ArgOptions.GetOption("--dbName", "") != "")
			{
				return ArgOptions.GetOption("--dbName", "");
			}
			else
			{
				try
				{
					StringBuilder sb = new StringBuilder(1024);
					Int32 len = getNeximDBName(sb, sb.Capacity);
					return sb.ToString();
				}
				catch (Exception ex)
				{
					return "XEPDB1";
				}
			}
		}

		/// <summary>
		/// NeximDBのホスト名を取得する。オプション指定がされていない場合は、localhost:1521 を返す
		/// </summary>
		/// <returns>DB Name:DB port</returns>
		public static string GetNeximDBHost()
		{
			if (ArgOptions.GetOption("--dbHost", "") != "")
			{
				return ArgOptions.GetOption("--dbHost", "");
			}
			else
			{
				//NeximPC 上で実行する場合は、localhost:1521 でOK
				return "localhost:1521";
			}
		}

		/// <summary>
		/// NeximDBのユーザー名を取得する(from DLL)
		/// </summary>
		/// <returns></returns>
		public static string GetNeximDBUser()
		{
			if (ArgOptions.GetOption("--dbUser", "") != "")
			{
				return ArgOptions.GetOption("--dbUser", "");
			}
			else
			{
				try
				{
					StringBuilder sb = new StringBuilder(1024);
					Int32 len = getNeximDBUser(sb, sb.Capacity);
					return sb.ToString();
				}
				catch (Exception ex)
				{
					return "";
				}
			}
		}

		/// <summary>
		/// NeximDBのパスワードを取得する(from DLL)
		/// </summary>
		/// <returns></returns>
		public static string GetNeximDBPass()
		{
			if (ArgOptions.GetOption("--dbPass", "") != "")
			{
				return ArgOptions.GetOption("--dbPass", "");
			}
			else
			{
				try
				{
					StringBuilder sb = new StringBuilder(1024);
					Int32 len = getNeximDBPass(sb, sb.Capacity);
					return sb.ToString();
				}
				catch (Exception ex)
				{
					return "";
				}
			}
		}

		/// <summary>
		/// アプリ起動時のログを出力する
		/// </summary>
		private void Startup_log()
		{
			string resultPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\UnitTransferResult";
			if (Directory.Exists(resultPath) == false)
			{
				DirectoryInfo info = Directory.CreateDirectory(resultPath);
			}
			DateTime dt = DateTime.Now;
			LogWriter = new StreamWriter($"{resultPath}\\Update_{dt.ToString("yyyyMMdd")}.txt");

			if (LogWriter != null)
			{
				LogWriter.WriteLine("Start Application");
			}
		}

		/// <summary>
		/// 起動時、Windowsのロード後に呼ばれる
		/// </summary>
		private async void WindowLoadedExec()
		{
			bool bRet;

			//ログファイル開始
			Startup_log();

			// Config.json の読み込み(ユニットのリンク情報、デフォルト)
			ReadConfig();

			//アップデートデータの読み込み
			LoadUpdateInfo();

			//ライン情報の読み込み
			if( ErrorInfo.ErrCode == ErrorCode.OK)
			{
				await LoadLineInfo();
			}

			//ビューの生成
			ViewModeTable = new Dictionary<string, ViewModelBase>
			{
				{ "AView", new AViewModel() },
				{ "BView", new BViewModel() },
				{ "CView", new CViewModel() },
				{ "UpdateVersionView", new UpdateVersionViewModel(UpdateDataInfos) }
			};
			ActiveView = viewModeTable["UpdateVersionView"];

			if( ErrorInfo.ErrCode != ErrorCode.OK)
			{
				//ビューを生成していないとダイアログが表示できないので、ここでエラーを表示する

				ShowErrorInfo(Resource.Error, "",(MsgDlgType.OK | MsgDlgType.ICON_ERROR));

				//エラーが発生している場合は、「転送」無効にする
				CanTransferStartFlag.Value = false;
			}
		}

		/// <summary>
		/// ライン情報の読み込み
		/// </summary>
		public bool _lineInfoLoaded = false;        // line 情報の読み込み完了フラグ
		private async Task<bool> LoadLineInfo()
		{
			CancelTokenSrc = new CancellationTokenSource();
			CancellationToken token = CancelTokenSrc.Token;

			//progress
			Progress = await Metro.ShowProgressAsync(Resource.ReadLineInformation, "");

			Progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・
			Progress.SetCancelable(true); // キャンセルボタンを表示する
			//Progress.SetProgressBarForegroundBrush(Brushes.Yellow);

			// この方法だとタスク(task)の二重起動となる
			//  Task.Run() で　async LoadLineInfo()(タスク)を起動するタスクとなるので
			//  task 自体は LoadLineInfo()タスクを(タスクとして)呼び出した事で「完了」となってしまう
			//Task task = Task.Run(() => {_ = LoadLineInfo(cts.Token);});

			// LoadLineInfo() は async なので、呼び出すとタスクとして実行される
			Task task = LoadLineInfo(token);

			for (var i = 0; i < 1000; i++)
			{
				if (task.IsCompleted)
				{
					if (_lineInfoLoaded == true)
					{
						// Task.Run()で起動していたため「終了」を検知できていなかったが
						// Task.Run()を使わない方法にしたので、この変数は不要だと思われる
						break;
					}
				}

				if (Progress.IsCanceled == true)
				{
					CancelTokenSrc?.Cancel();
					Debug.WriteLine("---------------------- Cancel");
					break;
				}
				//「待ち」を入れないと、ダイアログが更新されない
				await Task.Delay(100);
			}

			OnPropertyChanged(nameof(TreeViewItems));

			await Progress.CloseAsync();

			return true;
		}

		/// <summary>
		/// Peripheral.bin を展開する
		/// </summary>
		public int CallDecompExe(string path)
		{
			// mcAccount.dll の関数を呼び出して、Peripheral.bin を展開する
			var pst = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(T_ERROR_INFO)));
			var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
			byte[] data = new byte[fs.Length];
			int sz = fs.Read(data, 0, data.Length);
			fs.Dispose();
			string tmpDir = @"C:\DecompBin";// Path.GetTempPath();
			StringBuilder folderPath = new StringBuilder(1024);
			folderPath.Append(tmpDir);
			DataFolder = tmpDir;

			// メモリからファイルに展開する
			int ret = ExtractFolderFromImage(folderPath, data, (uint)sz, (UIntPtr)pst);

			Marshal.FreeHGlobal(pst);

			//現在の作業ディレクトリを変更する
			// (ExtractFolderFromImage では展開したフォルダに移動している。
			//   最終的に展開フォルダを削除するので、作業ディレクトリをそのままにしておくと「他プロセスが使用・・」とかで削除時に例外が発生する)
			Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

			return ret;
		}

		/// <summary>
		/// LCU ディスク空き容量確認コマンドの実行
		/// </summary>
		private async void LcuDiskChkCmdExec()
		{
			if (SelectedItem != null)
			{
				LcuInfo lcu = SelectedItem as LcuInfo;
				if (lcu != null)
				{
					long diskSpace = await LcuDiskChkCmd(lcu);
					long diskMB = diskSpace / 1024 / 1024;
					AddLog($"{lcu.Name}::DiskSpace={diskSpace}");
					var result = await MsgBox.Show(Resource.Confirm, $"{lcu.Name}", $"DiskSpace={diskMB} MB", "", (int)(MsgDlgType.OK | MsgDlgType.ICON_INFO), "MainWindow");
				}
			}
		}

		/// <summary>
		/// Peripheral.bin を展開して読み込む
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		private bool ReadPeripheralBin(string path)
		{
			if (Path.Exists(path + "\\Peripheral.bin") == true)
			{
				FileInfo fileInfo = new FileInfo(path + "\\Peripheral.bin");
				DriveInfo driveInfo = new DriveInfo("C:\\");

				UpdateDataSize = fileInfo.Length;

				//展開するデータがディスク容量を超えていないかチェック
				if (driveInfo.AvailableFreeSpace < fileInfo.Length)
				{
					ErrorInfo.ErrCode = ErrorCode.LCU_UPDATE_DISK_FULL;
					AddLog($"Not enough disk space:{driveInfo.AvailableFreeSpace} < UpdateData::{fileInfo.Length}");
					return false;
				}
				else
				{
					if( CallDecompExe(path + "\\Peripheral.bin") < 0)
					{
						AddLog($"DeCompress {path}\\Peripheral.bin Fail.");
						return false;
					}
					AddLog($"DeCompress {path}\\Peripheral.bin OK");
				}
				return true;
			}
			return false;
		}

		private static string GetSelectionString(bool? value)
		{
			return value switch
			{
				true => "True",
				false => "False",
				_ => "Null",
			};
		}

		/// <summary>
		///  アイテムの選択状態をデバッグ表示
		/// </summary>
		private void ShowItems()
		{
			foreach(var lcu in TreeViewItems)
			{
				Debug.WriteLine($"{lcu.Name}:{GetSelectionString(lcu.IsSelected.Value)}");
				foreach (var machine in lcu.Children)
				{
					Debug.WriteLine($"    {machine.Name}:{GetSelectionString(machine.IsSelected.Value)}");
					foreach (var module in machine.Children)
					{
						Debug.WriteLine($"       {module.Name}:{GetSelectionString(module.IsSelected.Value)}");
						foreach (var unit in module.UnitVersions)
						{
							Debug.WriteLine($"          {unit.Name}:{GetSelectionString(unit.IsSelected.Value)}");
						}
					}
				}
			}

		}

		/// <summary>
		///  ユニットの連携情報を読み込む(デフォルトはexeのリソースから)
		/// </summary>
		private static void ReadConfig()
		{
			Config.ReadConfig();

			// オプションでユニットリンクファイルが指定されている場合は、読み込んで更新する
			if (ArgOptions.GetOption("--config", "") != "")
			{
				//オプションで指定されたファイルを読み込む
				string path = ArgOptions.GetOption("--config", "");
				if (Path.Exists(path) == true)
				{
					string json = System.IO.File.ReadAllText(path);
					ConfigTemp? config = JsonSerializer.Deserialize<ConfigTemp>(json);

					if (config != null )
					{
						foreach (var unit in config.units)
						{
							var v = Config.units.FirstOrDefault(x => x.name == unit.name);

							if (v != null)
							{
								//存在する場合は、コンポーネントを更新
								v.components = unit.components;
							}
							else
							{
								//存在しない場合は、追加
								Config.units.Add(unit);
							}
						}
					}
				}
			}
		}

		/// <summary>
		///  アップデートデータ情報の読み込み
		/// </summary>
		/// <returns></returns>
		private bool LoadUpdateInfo()
		{
			string infoFile;

			//データフォルダ指定のオプション処理
			string dataFolder = ArgOptions.GetOption("--dataFolder", "");
			if (dataFolder != "")
			{
				//フォルダが指定されている場合は、そこをデータフォルダとする
				DataFolder = dataFolder;
			}
			else
			{
				//指定がない場合は、exeのディレクトリ下のDataをデータフォルダとする
				DataFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Data";
			}

			// Peripheral.bin を展開して読み込む(なければ false)
			if (ReadPeripheralBin(dataFolder) == true)
			{
				//Peripheral.bin が展開されたので、UpdateCommon.inf のパスを設定
				infoFile = (DataFolder + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE).Replace("/", "\\");
			}
			else
			{
				//Peripheral.bin ではない場合は、UpdateCommon.inf のパスを設定
				//infoFile = (DataFolder + "\\Fuji\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE).Replace("/", "\\");
				infoFile = (DataFolder + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE).Replace("/", "\\");
			}

			IsFileMenuEnabled = true;
			// UpdateCommon.inf が存在するか
			if (Path.Exists(infoFile) == true)
			{
				//UpdateCommon.inf を読み込む
				UpdateDataInfos = ReadUpdateCommon(infoFile);
				if (UpdateDataInfos == null)
				{
					AddLog($"Read Error:{infoFile}");
					ErrorInfo.ErrCode = ErrorCode.UPDATE_READ_ERROR;
				}
				else
				{
					AddLog($"Read {infoFile}");
					UpdatePaths = CreateUpdatePathList(infoFile);
				}
			}
			else
			{
				// UpdateCommon.inf が存在しない場合
				ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
				AddLog( $"{ErrorInfo.GetErrMessage()}" );
			}
			return (ErrorInfo.ErrCode == ErrorCode.OK);
		}

		/// <summary>
		/// コンストラクタ
		/// </summary>
		public MainWindowViewModel(IDialogCoordinator dialogCoordinator)
		{
			_dialogCoordinator = dialogCoordinator;

			//Windowのロード時(ライン情報のよみこみ)、	クローズ時(ファイルの後始末)のコマンド
			WindowLoadedCommand.Subscribe(() => WindowLoadedExec());
			WindowClosingCommand.Subscribe(() => ApplicationShutDown());

			// メニュー実行制御(Subscribe() するより先に設定する)
			LcuNetworkChkCommand = CanExecuteLcuCommand.ToReactiveCommand();
			LcuDiskChkCommand = CanExecuteLcuCommand.ToReactiveCommand();

			// メニューコマンドの設定 
			FileOpenCommand.Subscribe(() => FileOpenCmd());
			LcuNetworkChkCommand.Subscribe(() => { LcuNetworkChkCmd(); });
			LcuDiskChkCommand.Subscribe(() => { LcuDiskChkCmdExec(); });

			HomeCommand.Subscribe(() =>
			{
				ActiveView = viewModeTable["UpdateVersionView"];
				ShowItems();
			});

			/* フラグによるコマンドの実行可否(実装例)
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
			//フラグが一つでも true になったら CutCommand を実行不可とする
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

			TreeViewMouseLeftButtonDownCommand = new ReactiveCommand<MouseButtonEventArgs>();
			TreeViewMouseLeftButtonDownCommand.Subscribe(args => TreeViewMouseLeftButtonDown(args));

			// 転送制御ボタン
			StartTransferCommand = CanTransferStartFlag.ToReactiveCommandSlim();
			StartTransferCommand.Subscribe(() => StartTransfer());

			CanTransferStartFlag.Value = true;

			QuitApplicationCommand = CanAppQuitFlag.ToReactiveCommandSlim();
			QuitApplicationCommand.Subscribe(() => ApplicationShutDown());

			TestResultCommand.Subscribe(() => ShowTestResult());

			//TreeView 右クリックメニューのテスト
			TreeViewCommand.Subscribe((x) => { TreeViewMenu(x); });

			//TreeView のチェックボックスがクリックされた時に実行されるコマンドを登録
			TreeViewChkCommand.Subscribe((x) => { TreeViewChkCmd(x); });

			/* C++ DLL関数 呼び出しテスト
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

		/// <summary>
		/// NeximDB から LCU リストを取得する
		/// </summary>
		/// <returns></returns>
		private List<string> GetLcuListFromNexim()
		{
			List<string> lcuList = [];

			string connStr = $"User Id={GetNeximDBUser()};Password={GetNeximDBPass()};Data Source={GetNeximDBHost()}/{GetNeximDBName()}";
			//string connStr = "User Id=fujisuperuser;Password=em2g86fzjt945p73; Data Source=10.0.51.64:1521/neximdb";
			using (OracleConnection conn = new(connStr))
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
						int jobId = reader.GetInt32(0);
						int lineId = reader.GetInt32(1);
						string name = reader.GetString(2);
						int activeFlag = reader.GetInt32(6);
						int lcuId = reader.GetInt32(18);
						
						AddLog($"{name}::LineID={lineId} JobID={jobId} ActiveFlag={activeFlag} LcuID={lcuId}");

						OracleCommand cm = new($"select * from COMPUTER WHERE COMPUTERID={lcuId}", conn);
						OracleDataReader rd = cm.ExecuteReader();
						while (rd.Read())
						{
							int computerId = rd.GetInt32(0);
							string lcuName = rd.GetString(1);
							AddLog($"ComputerID={computerId} LcuName={lcuName}");
							lcuList.Add($"{name}={lcuName}={computerId}");
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
		///  TreeView で右クリックメニューでの実行
		/// </summary>
		/// <param name="x"></param>
		private void TreeViewMenu(object x)
		{
			CheckableItem item = x as CheckableItem;

			Debug.WriteLine($"TreeViewMenu:{item.Name}");

			List<string> lcuList = GetLcuListFromNexim();

		}

		/// <summary>
		/// ツリービューのチェックボックスがクリックされたときの処理
		/// </summary>
		/// <param name="x">TreeView Item</param>
		private void TreeViewChkCmd(object x)
		{
			CheckableItem? item = x as CheckableItem;

			if (item == null)
			{
				return;
			}
			bool? chk = item.IsSelected.Value;

			switch (item)
			{
				case LcuInfo lcu:
					UpdateChildren(lcu.Children, chk);
					break;
				case MachineInfo machine:
					//if( Config.Options.ContainsMachineType(machine.MachineType) == false )
					if( machine.Status == ItemStatus.NOT_SUPPORTED )
					{
						item.IsSelected.Value = false;
						break;
					}
					UpdateChildren(machine.Children, chk);
					if(ActiveView is MachineViewModel vm)
					{
						vm.IsAllSelected.Value = chk;
					}
					break;
				case ModuleInfo module:
					//if( Config.Options.ContainsMachineType(module.Parent.MachineType) == false )
					if( module.Status == ItemStatus.NOT_SUPPORTED )
					{
						item.IsSelected.Value = false;
						break;
					}
					UpdateChildren(module.UnitVersions, chk);
					if(ActiveView is ModuleViewModel mvm)
					{
						mvm.UpdateGroupCheck(null);
					}
					break;
			}
		}

		private void UpdateChildren<T>(IEnumerable<T> children, bool? chk) where T : CheckableItem
		{
			foreach (var child in children)
			{
				child.IsSelected.Value = chk;
				if (child is MachineInfo machine)
				{
					UpdateChildren(machine.Children, chk);
				}
				else if (child is ModuleInfo module)
				{
					UpdateChildren(module.UnitVersions, chk);
				}
			}
		}

		/// <summary>
		/// ツリービューの選択項目を全てクリアする
		/// </summary>
		private void ClearSelectedItems()
		{
			foreach (var lcu in TreeViewItems)
			{
				ClearSelection(lcu);
			}
		}

		private void ClearSelection(CheckableItem item)
		{
			item.IsSelected.Value = false;

			switch (item)
			{
				case LcuInfo lcu:
				foreach (var machine in lcu.Children)
				{
					ClearSelection(machine);
				}
				break;
			case MachineInfo machine:
				foreach (var module in machine.Children)
				{
					ClearSelection(module);
				}
				break;
			case ModuleInfo module:
				foreach (var unit in module.UnitVersions)
				{
					ClearSelection(unit);
				}
				break;
			}
		}

		/// <summary>
		/// バックアップデータの情報をチェックする
		/// </summary>
		/// <param name="info">UpdateCommon.inf の一行目(バックアップデータで追記されている)</param>
		/// <returns>-1=該当ライン無し、-2=該当装置無し、-3=該当モジュール無し</returns>
		private int CheckBackupInfo(string lcuName, string machineName, string moduleName)
		{
			//var (line, machine, module, date) = info.Split('=')[1].Split(';') switch { var a => (a[0], a[1], a[2], a[3]) };
			/*
			if( strings[0].Split('=')[1].Split(';') is [var line, var machine, var module, var date] )
			{
				Debug.WriteLine($"{line}::{machine}::{module}::{date}");
			}
			*/

			LcuInfo? lcu = TreeViewItems.FirstOrDefault(x => x.Name == lcuName);
			if (lcu == null)
			{
				return -1;  // 該当ライン無し
			}
			MachineInfo? mc = lcu.Children.FirstOrDefault(x => x.Name == machineName);
			if(mc == null)
			{
				return -2; // 該当装置無し
			}

			ModuleInfo? md = mc.Children.FirstOrDefault(x => x.Name == moduleName);
			if (md == null)
			{
				return -3;  // 該当モジュール無し
			}

			return 0;
		}

		/// <summary>
		/// ビューをクリアする
		/// </summary>
		/// <param name="viewName"></param>
		private void ClearView(string viewName)
		{
			foreach (var key in viewModeTable.Keys)
			{
				if (key.Contains(viewName))
				{
					viewModeTable.Remove(key);
				}
			}
		}

		/// <summary>
		///  ユニットアップデートデータのフォルダを選択して読み込む
		/// </summary>
		public async void FileOpenCmd()
		{
			string? backupPath = ArgOptions.GetOption("--backup");
			bool IsAdmin = ArgOptions.GetOption("--mode", "user") == "administrator";

			backupPath ??= @"C:\";

			using (var cofd = new CommonOpenFileDialog()
			{
				Title = Resource.PleaseSelectFolder,// "フォルダを選択してください"
				InitialDirectory = backupPath,
				//フォルダ選択モード
				IsFolderPicker = true,
			})
			{
				string infoFilePath = "";
				if (cofd.ShowDialog() == CommonFileDialogResult.Ok)
				{
					infoFilePath = cofd.FileName;
					AddLog($"FileOpenCommand={infoFilePath}");

					// 指定されたフォルダに UpdateCommon.inf が存在するかチェック
					if (Path.Exists(infoFilePath + "\\" + Define.UPDATE_INFO_FILE) == false)
					{
						// 指定されたフォルダの Fuji/System3/Program/Peripheral に UpdateCommon.inf が存在するかチェック
						if (Path.Exists(infoFilePath + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE) == true)
						{
							//UpdateCommon.inf のパスを設定
							infoFilePath = (infoFilePath + "\\" + Define.MC_PERIPHERAL_PATH).Replace("/", "\\");
						}
						else
						{
							//UpdateCommon.inf が存在しない
							AddLog($"[ERROR] Path={infoFilePath} Undefined UpdateCommon.inf");
							ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
							var result = await MsgBox.Show(Resource.Error, $"ErrorCode=0x {ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", "UpdateCommon.inf not exist.", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
							return;
						}
					}

					// バックアップデータかどうかをチェック(バックアップデータは一行目に情報が追加されている)
					IsBackupedData = false;
					string[] strings = System.IO.File.ReadAllLines(infoFilePath + "\\" + Define.UPDATE_INFO_FILE);
					var (line, machine, module, date) = strings[0].Split('=')[1].Split(';') switch { var a => (a[0], a[1], a[2], a[3]) };

					if (strings[0].Contains("UpdateInfo=") == true)
					{
						// バックアップデータの場合
						if( CheckBackupInfo(line, machine, module) < 0)
						{
							//バックアップデータの情報が不正
							//現在のNeximDBのライン情報と一致しない(ライン、装置、モジュールが見つからない)
							ErrorInfo.ErrCode = ErrorCode.BACKUP_DATA_MISSMATCH;
							await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}",$"{line}-{machine}-{module}", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
							return;
						}
						var ret = await MsgBox.Show(Resource.Info, $"{line}={machine}={module}", "Backup Data", "Load Backup Data ?", (int)(MsgDlgType.OK_CANCEL | MsgDlgType.ICON_INFO), "MainWindow");
						if ((string)ret == "CANCEL")
						{
							return;
						}
						IsBackupedData = true;
					}
					AddLog($"Load BackupData={infoFilePath}");

					UpdateDataInfos = ReadUpdateCommon(infoFilePath + "\\" + Define.UPDATE_INFO_FILE);

					//アップデートデータのサイズを取得
					DirectoryInfo di = new(infoFilePath);
					long dataSize = Utility.GetDirectorySize(di);

					//バージョン情報ビューの更新
					viewModeTable["UpdateVersionView"] = new UpdateVersionViewModel(UpdateDataInfos);

					//アップデートデータが変更されたので、 ツリーアイテムの選択をクリアする
					ClearSelectedItems();

					//モジュールビュー,ビューデータを削除
					ClearView("ModuleView");
					TreeViewItems
						.SelectMany(lcu => lcu.Children)
						.SelectMany(machine => machine.Children)
						.ToList()
						.ForEach(module => module.UnitVersions.Clear());

					CancellationTokenSource cts = new();

					if (IsBackupedData == true)
					{
						// モジュールビューを再生成する

						//一致するモジュールを検索して、バージョン情報を取得する
						var item = TreeViewItems
									.Where(lcu => lcu.Name == line)
									.SelectMany(lcu => lcu.Children)
									.Where(machineItem => machineItem.Name == machine)
									.SelectMany(machineItem => machineItem.Children)
									.FirstOrDefault(moduleItem => moduleItem.Name == module);

						if (item != null)
						{
							//一致するモジュールがある場合
							LcuInfo? lcuInfo = TreeViewItems.FirstOrDefault(x => x.Name == line);
							MachineInfo? machineInfo = lcuInfo?.Children.FirstOrDefault(x => x.Name == machine);
							ModuleInfo? moduleInfo = machineInfo?.Children.FirstOrDefault(x => x.Name == module);

							// モジュールからバージョン情報を取得して、モジュールビューを生成する
							IsBackupedData = false; // 一時的に false にしておく -> CheckBox のチェック状態をOnにするため
							moduleInfo.IsSelected.Value = true;

							//装置からアップデート情報を取得する
							bool ret = await GetModuleUpdateInfo(lcuInfo, machineInfo, moduleInfo, cts.Token);
							if( ret == false && IsAdmin == false )
							{
								//アップデート情報の取得に失敗
								AddLog($"GetModuleUpdateInfo Fail:{lcuInfo.Name}-{machineInfo.Name}-{moduleInfo.Name}");
								ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
								await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", $"{line}:{machine}:{module}", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
								return;
							}

							// バージョン情報を取得して、モジュールビューを生成する
							var retVersion = CreateVersionInfo(lcuInfo, machineInfo, moduleInfo, cts.Token);
							if (retVersion == true)
							{
								//moduleInfo.UnitVersions = retVersion;

								string viewName = item.GetViewName();
								viewModeTable.Add(viewName, new ModuleViewModel((ModuleInfo)item, UpdateDataInfos));

								moduleInfo.IsSelected.Value = Utility.CheckState<UnitVersion>(moduleInfo.UnitVersions);

								//該当のツリービューを展開する
								lcuInfo.IsExpanded.Value = true;
								machineInfo.IsExpanded.Value = true;
								moduleInfo.IsExpanded.Value = true;
							}
							IsBackupedData = true; // 以降はバックアップデータを読み込んだ事にする -> 他モジュールのチェックボックスをflase状態にするため
						}
						else
						{
							//ロードしたバックアップデータのライン、装置、モジュールが見つからない
							AddLog($"Not Found Module:{line}-{machine}-{module}");
							ErrorInfo.ErrCode = ErrorCode.BACKUP_DATA_MISSMATCH;
							await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", $"{line}:{machine}:{module}", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
						}
					}
					else
					{
						//バックアップデータでない場合(インストールディス等)
						//  ツリーのアイテムをクリックした時にビューを生成するので、ここでは何もしない
					}
					cts.Dispose();

					SelectedItem = null;
					ActiveView = viewModeTable["UpdateVersionView"];
				}
			}
		}

		/// <summary>
		///  LCU の接続状態を確認する
		/// </summary>
		async void LcuNetworkChkCmd()
		{
			CheckableItem item = SelectedItem;
			
			if( item is LcuInfo lcu)
			{
				bool ping = await CheckComputer(lcu.LcuCtrl.Name.Split(':')[0], 3);
				if (ping == false)
				{
					ErrorInfo.ErrCode = ErrorCode.LCU_CONNECT_ERROR;
					var result = await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage() }", "", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");

					if ((string)result == "OK")
					{
						Debug.WriteLine("Click OK");
					}
					else
					{
						Debug.WriteLine("Click out");
					}
				}
				else
				{
					string str = await lcu.LcuCtrl.LCU_Command(LcuVersion.Command());
					IList<LcuVersion>? versionInfo = LcuVersion.FromJson(str);
					if (versionInfo == null)
					{
						return;
					}
					string? ver = versionInfo.First(x => x.itemName == "Fuji LCU Communication Server Service").itemVersion;

					await MsgBox.Show(Resource.Info, $"{lcu.LcuCtrl.Name}", "Access OK", $"Version:{ver}", (int)(MsgDlgType.OK), "MainWindow");
				}
			}
		}

		/// <summary>
		/// LCU のディスク容量を確認する
		/// </summary>
		public async Task<long> LcuDiskChkCmd(LcuInfo lcu)
		{
			CancellationTokenSource cts = new CancellationTokenSource();
			//CancelTokenSrc = new CancellationTokenSource();
			CancellationToken token = cts.Token;

			AddLog($"{lcu.Name}:Check Disk Space");
			List<LcuDiskInfo>? info = await lcu.LcuCtrl.LCU_DiskInfo(token);
			if (info == null)
			{
				ErrorInfo.ErrCode = ErrorCode.LCU_DISK_INFO_ERROR;
				AddLog($"{lcu.Name}::Don't get disk space information.");
				//CancelTokenSrc.Dispose();
				cts.Dispose();
				return 0;
			}
			//CancelTokenSrc.Dispose();
			cts.Dispose();

			long diskSpace = long.Parse(info.Find(x => x.driveLetter == "D").free);

			return diskSpace;
		}

		/// <summary>
		/// アップデートパスのリストを作成する
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public ReactiveCollection<UnitPath> CreateUpdatePathList(string path)
		{
			ReactiveCollection<UnitPath> unitPaths = [];

			IniFileParser parser = new(path);

			//UpdateCommon.inf 内の Path=, FuserPath= のリストを取得
			List<(string name, string path, string fuserPath)> updateList = parser.GetPathList();

			foreach (var unit in updateList)
			{
				if (unit.path != "")
				{
					string fullPath = DataFolder + unit.path;
					if (Path.Exists(fullPath) == true)
					{
						if( Directory.Exists(fullPath) == true)
						{
							//Path がフォルダの場合は無視
							continue;
						}

						// ユニット・グループを取得
						UnitLink? unitLink = Config.units.Where(x => x.components.Where(y => y == unit.name).ToList().Count != 0).FirstOrDefault();

						if ( unitLink != null)
						{
							//var unitPath = unitPaths.Where(x => x.GroupName == unitLink.name).FirstOrDefault();
							var unitPath = unitPaths.Where(x => unitLink.ContainsName(x.GroupName)).FirstOrDefault();
							if (unitPath == null)
							{
								//unitPath = new UnitPath { GroupName = unitLink.name, units = [] };
								unitPath = new UnitPath { GroupName = unitLink.GetName(), units = [] };
								unitPaths.Add(unitPath);
							}

							if (unitLink.mode == "File")
							{
								unitPath.AddUnitFile("",unit);
							}
							else if( unitLink.mode == "Folder")
							{
								//フォルダ内のファイルを取得
								string[] fi = Directory.GetFiles((DataFolder + Path.GetDirectoryName(unit.path)), "*", SearchOption.AllDirectories);
								foreach (var f in fi)
								{
									string up = f.Split(DataFolder)[1].Replace('\\', '/');

									List<string> sections = parser.GetSectionByValue("Path", up);
									if( sections.Count != 0)
									{
										sections.ForEach(section => 
										{
											unitPath.AddUnitFile("", (section, up, ""));
										});
										//unitPath.AddUnitFile("",(section, up, ""));
									}
									else
									{
										unitPath.AddUnitFile("", (unitLink.GetName(),up,"") );
									}
								}
							}
						}
					}
				}
			}
			return unitPaths;
		}

		/// <summary>
		///  UpdateCommon.inf を読み込み、UpdateInfo のリストを返す
		/// </summary>
		/// <param name="path">UpdateCommon.inf ファイルのパス</param>
		/// <returns></returns>
		private ReactiveCollection<UpdateInfo> ReadUpdateCommon(string path)
		{
			ReactiveCollection<UpdateInfo> updates = [];

			try
			{
				IniFileParser parser = new(path);

				IList<string> sec = parser.SectionCount();
				foreach (var unit in sec)
				{
					string tmpPath = DataFolder + parser.GetValue(unit, "Path");

					if ( Directory.Exists(tmpPath))
					{
						//Path がフォルダの場合(存在しない場合はfalse)
						continue;
					}
					else if ( System.IO.File.Exists(tmpPath) )
					{
						UpdateInfo update = new()
						{
							Name = unit,
							Attribute = parser.GetValue(unit, "Attribute"),
							Path = parser.GetValue(unit, "Path"),
							Version = parser.GetValue(unit, "Version"),
							FuserPath = parser.GetValue(unit, "FuserPath"),
							IsVisibled = true,//チェックボックスの表示/非表示
											  //リンク情報を取得
							UnitGroup = Config.units.Where(x => x.components.Find(y => y == unit) != null).FirstOrDefault()?.GetName()
						};
						updates.Add(update);
					}
				}
				UpdateDataSize = GetPeripheralSize(DataFolder);
				return updates;
			}
			catch (Exception ex)
			{
				AddLog($"Read Error:{ex.Message}");
				return updates;
			}
		}

		/// <summary>
		/// ping によるネットワーク接続確認
		/// </summary>
		/// <param name="name">コンピューター名</param>
		/// <param name="count">繰り返し回数</param>
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
			catch (PingException ex)
			{
				Debug.WriteLine(ex.Message);
				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Exception:{ex.Message}");
				throw;
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

		/// <summary>
		/// アップデートデータのサイズを取得する
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static long GetPeripheralSize(string path)
		{
			long size = 0;
			DirectoryInfo di = new(path);

			FileInfo[] files = di.GetFiles("Peripheral.bin");
			if (files.Length != 0)
			{
				//アーカイブファイルのサイズを取得
				size = files[0].Length;
			}
			else
			{
				size = GetDirectorySize(di);
			}
			return size;
		}

		private TransferResultWindow? resultWindow;

		/// <summary>
		/// 転送結果ウインドウを表示する
		/// </summary>
		/// <param name="resultData"></param>
		private void LaunchResultWindow(string resultData)
		{
			if (resultWindow is null)
			{
				resultWindow = new TransferResultWindow(resultData);
				resultWindow.Closed += (o, args) => resultWindow = null;
			}
			this.resultWindow.Launch();
		}

		/// <summary>
		/// 転送開始(StartTransferCommand から呼び出される)
		/// </summary>
		private bool IsTransfering = false;
		private async void StartTransfer()
		{
			long lines = 0, machines = 0, modules = 0, units = 0, files = 0;

			List<string> paths = GetTransferFiles(ref lines, ref machines, ref modules, ref units, ref files);
			TransferCount = paths.Count;
			TransferedCount = 0;
			TransferErrorCount = 0;
			TransferSuccessCount = 0;

			foreach (var path in paths)
			{
				Debug.WriteLine($"Transfer Path={path}");
			}

			if (lines == 0 && machines == 0 && modules == 0)
			{
				var result = await MsgBox.Show(Resource.Confirm, Resource.NoTargetMachine, Resource.TargetSelect, "", (int)(MsgDlgType.OK | MsgDlgType.ICON_CAUTION), "MainWindow");
				return;
			}

			DialogTitle = Resource.Confirm;// "確認";
			DialogText = $"{modules} Module\n{Resource.QuestionTransferStart}";

			var r = await DialogHost.Show(new MyMessageBox(), "MainWindow");
			DialogHost.CloseDialogCommand.Execute(null, null);

			if (r == null || r.ToString() == "CANCEL")
			{
				return;
			}

			bool ret = true;
			ErrorInfo.ErrCode = ErrorCode.OK;   //エラーコードをクリア

			CancelTokenSrc = new CancellationTokenSource();
			CancellationToken token = CancelTokenSrc.Token;

			//転送操作のボタンの有効/無効を設定
			CanTransferStartFlag.Value = true;
			CanAppQuitFlag.Value = false;

			string? backupPath = ArgOptions.GetOption("--backup");

			// バックアップを実行
			if (backupPath != null)
			{
				Progress = await Metro.ShowProgressAsync(Resource.BackupUnitSoft, "");
				Progress.SetCancelable(true); // キャンセルボタンを表示する
				Progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・

				IsTransfering = true;
				//Task backUpTask = Task.Run(() => { Task<bool> task1 = BackupUnitData(backupPath, token); });
				Task backUpTask = BackupUnitData(backupPath, token);
				bool isBackupCancel = false;
				while (true)
				{
					if( backUpTask.IsCompleted == true || IsTransfering == false )
					{
						//転送が終了した
						break;
					}
					if (Progress.IsCanceled == true)
					{
						CanTransferStartFlag.Value = true;  // 転送を中断(このフラグで、転送中止を待っているスレッドがあるので、このフラグを設定してから中断処理を行う)

						//プログレスの「Cancel」ボタンが押されたので、プログレスは一旦クローズする
						await Progress.CloseAsync();

						//「Cancel」をキャンセルするかどうかの確認(ダイアログ -> このダイアログを表示するために一旦プログレスダイアログをクローズする必要がある)
						isBackupCancel = await StopTransferExecute();
						if (isBackupCancel == true)
						{
							// バックアップ中止
							break;
						}
						// 再開(「Cancel」がキャンセルされたので、プログレスを再表示する)
						Progress = await Metro.ShowProgressAsync(Resource.BackupUnitSoft, "");
						Progress.SetCancelable(true); // キャンセルボタンを表示する
						Progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・

						CanTransferStartFlag.Value = true;
					}
					await Task.Delay(100);  //Delay することで、プログレスウインドウの表示が更新される
				}
				if (isBackupCancel == false)
				{
					await Progress.CloseAsync();
					if( ErrorInfo.ErrCode != ErrorCode.OK)
					{
						//エラーが発生した
						var result = await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", "", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
						return;
					}
				}
				else
				{
					//バックアップが中止されたが、転送を続行するか確認
					var bRet = await MsgBox.Show(Resource.Confirm, $"{Resource.BackupCanceled}", $"{Resource.ContinueTransfer}", "", (int)(MsgDlgType.OK_CANCEL | MsgDlgType.ICON_INFO), "MainWindow");
					if ((string)bRet == "CANCEL")
					{
						CanTransferStartFlag.Value = true;
						CanAppQuitFlag.Value = true;
						return;
					}
				}
			}

			//　転送処理を実行
			if (ret == true)
			{
				ErrorInfo.ErrCode = ErrorCode.OK;   //エラーコードをクリア

				Progress = await Metro.ShowProgressAsync(Resource.UnitSoftTransfer, "");
				Progress.SetCancelable(true); // キャンセルボタンを表示する
				Progress.SetProgress(0);
				Progress.Minimum = 0;
				Progress.Maximum = 1000;

				await Task.Delay(500);

				//別スレッドで転送処理を実行
				IsTransfering = true;
				Task task = TransferExecute(token);

				bool isCancel = false;
				while (true)
				{
					if (task.IsCompleted || IsTransfering == false)
					{
						//転送が終了した
						break;
					}
					if (Progress.IsCanceled == true)
					{
						CanTransferStartFlag.Value = true;	// 転送を中断
						
						await Progress.CloseAsync();    // 進捗ダイアログを閉じる

						isCancel = await StopTransferExecute();
						if (isCancel == true)
						{
							AddLog("[Transfer] Cancel");
							break;
						}
						// 再開
						Progress = await Metro.ShowProgressAsync(Resource.UnitSoftTransfer, "");
						Progress.SetCancelable(true); // キャンセルボタンを表示する
						Progress.Minimum = 0;
						Progress.Maximum = 1000;

						//プログレスを再表示(再生成?)した後で、転送を再開する
						//　　(転送中にこのフラグを待っているスレッドがあるので、プログレスを生成(?)後にフラグを設定する事)
						CanTransferStartFlag.Value = true;
						AddLog("[Transfer] Restart");
					}
					await Task.Delay(100);  //Delay することで、プログレスウインドウの表示が更新される
				}
				if (isCancel == false)
				{
					await Progress.CloseAsync();
					if( ErrorInfo.ErrCode != ErrorCode.OK )
					{
						//エラーが発生した
						var result = await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", "", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
					}
				}
			}

			CanTransferStartFlag.Value = true;
			CanAppQuitFlag.Value = true;

			//転送結果ウインドウを表示する
			var showResult = await MsgBox.Show(Resource.Info,
									$"{Resource.Success}={TransferSuccessCount}",
									$"{Resource.Fail}={TransferErrorCount}",
									Resource.TransferInformation,
									(int)(MsgDlgType.YES_NO | MsgDlgType.ICON_INFO), "MainWindow");
			if ((string)showResult == "YES")
			{
				LaunchResultWindow(LogData);
			}
		}

		/// <summary>
		/// 選択されたライン下の装置、モジュール、ユニットのUpdateCommon.inf を読み込み、アップデートファイルのリストを作成する
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		private async Task<long> LoadModulesUpdateCommon(CancellationToken token)
		{
			long count = 0;
			bool IsAdmin = ArgOptions.GetOption("--mode", "user") == "administrator";

			var modules = TreeViewItems
				.Where(lcu => lcu.Children != null && lcu.LcuCtrl != null && lcu.IsSelected.Value != false)
				.SelectMany(lcu => lcu.Children
				.Where(machine => machine.Children != null && machine.Name != null && machine.IsSelected.Value != false)
				.SelectMany(machine => machine.Children
				.Where(module => module.IsSelected.Value != false && module.UnitVersions != null)
				.Select(module => new { lcu, machine, module })
				)
				).ToList();

			foreach (var item in modules)
			{
				var lcu = item.lcu;
				var machine = item.machine;
				var module = item.module;

				if (module.UnitVersions.Count == 0)
				{
					bool ret = await GetModuleUpdateInfo(lcu, machine, module, token);
					if( ret == false && IsAdmin == false)
					{
						//アップデート情報の取得に失敗
						AddLog($"GetModuleUpdateInfo Fail:{lcu.Name}-{machine.Name}-{module.Name}");
						ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
						return 0;
					}
					//var version = CreateVersionInfo(lcu, machine, module, token);
					var version = CreateVersionInfo(lcu, machine, module, token);
					if (version == false)
					{
						ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
						return 0;
					}
					//module.UnitVersions = version;
					long nn = module.UnitVersions.Count(x => x.IsSelected.Value == true);
					Debug.WriteLine($"[LoadModulesUpdateCommon] {lcu.Name};{machine.Name};{module.Name}={nn}");
				}
				List<UnitPath> unitFiles = CreateUpdateFileList(module.UnitVersions);
				count += unitFiles.Select(x => x.FileCount()).Sum();
			}
			return count;
		}
		/// <summary>
		/// データ転送
		/// </summary>
		/// <returns></returns>
		public async Task<bool> TransferExecute(CancellationToken token)
		{
			// 「選択」されている LCU のリストを取得
			var lcuList = TreeViewItems.Where(x => x.IsSelected.Value != false).ToList();

			//FTP転送のサイズを取得
			// UpdateData の全データ× LCU の数
			//   (選択されているもの以外のデータも転送する。-> ユニット毎に選択/非選択を考慮すると手間なので)
			//      A というユニットでは未選択でも Bでは選択されている場合がある、など)
			//    ※実際のFTPサイズは　UpDateCommon.inf ファイルを除いたサイズとなるが、総サイズに対して微量なので、無視する
			FtpTransferSize = GetDirectorySize(new DirectoryInfo(DataFolder + "\\Fuji")) * lcuList.Count;
			FtpTransferedSize = 0;

			foreach (var x in lcuList)
			{
				//対象のLCUに対して転送を実行
				bool ret = await UploadUnitFilesToLcu(x, token);
				if( ret == false)
				{
					//LCUにファイルを転送する際にエラーが発生した
					IsTransfering = false;
					return false;
				}
			}
			// 選択されたユニットのUpdateCommon.inf を読み込む
			//   (Treeで選択された場合はその時に読み込んでいるが、そうでない場合はここで纏めて読み込む)
			long bLoad = await LoadModulesUpdateCommon(token);
			if( bLoad == 0)
			{
				//UpdateCommon.inf の読み込みに失敗した
				IsTransfering = false;
				return false;
			}

			TransferCount = bLoad;
			TransferedCount = 0;

			//LCUから装置に転送
			foreach (var lcu in TreeViewItems)
			{
				if (lcu.Children == null || lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
				{
					AddLog($"[Transfer] {lcu.Name}=Skip");
					continue;
				}
				foreach (var machine in lcu.Children)
				{
					if (machine.Children == null || machine.Name == null || machine.IsSelected.Value == false)
					{
						AddLog($"[Transfer] {lcu.Name};{machine.Name}=Skip");
						continue;
					}
					foreach (var module in machine.Children)
					{
						if (module.IsSelected.Value == false || module.UnitVersions == null)
						{
							AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=Skip");
							continue;
						}

						// UpdateCommon.inf の Path のフォルダにファイルを転送する
						//   ※ UpdateCommon.inf に記載されているデータは存在するものとする
						//       指定されたフォルダにあるUpdateCommon.inf を読み込んでいるので
						//
						AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=Start");

						bool ret = await UploadModuleFiles(lcu, machine, module, token);

						AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=End");

						// LCU 上に作成、転送したファイルを削除する
						//lcu.LcuCtrl.ClearFtpFolders(Define.LCU_ROOT_PATH);

						if (ret == false)
						{
							IsTransfering = false;
							return false;
						}
					}
				}
			}
			IsTransfering = false;
			return true;
		}

		/// <summary>
		/// 転送済みのファイルをチェックする
		/// </summary>
		/// <param name="files">転送ファイル一覧(転送先から再取得)</param>
		/// <param name="versions">アップデートデータ</param>
		private void CheckTransferedFiles(List<string> files, ReactiveCollection<UnitVersion> versions)
		{
			//葉の尻は
			foreach(var ver in versions)
			{
				if( ver.IsSelected.Value == false)
				{
					//選択されていないバージョンはスキップ
					continue;
				}
				if (ver.Name != null && ver.Name != "")
				{
					//転送済みのファイル名をチェック
					if (files.Contains(ver.Path) == false)
					{
						//転送済みのファイルが存在しない
						Debug.WriteLine($"[INSPECTION] {ver.Name}={ver.Path} is not found in LCU.");
					}
					else
					{
						//転送済みのファイルが存在する
						Debug.WriteLine($"[INSPECTION] {ver.Name}={ver.Path} is found in LCU.");
					}
					if (ver.FuserPath != null && ver.FuserPath != "")
					{
						//転送済みのFuserPathをチェック
						if (files.Contains(ver.FuserPath) == false)
						{
							//転送済みのFuserPathが存在しない
							Debug.WriteLine($"[INSPECTION] {ver.FuserPath} is not found in LCU.");
						}
						else
						{
							//転送済みのFuserPathが存在する
							Debug.WriteLine($"[INSPECTION] {ver.FuserPath} is found in LCU.");
						}
					}
				}
			}
			//UpdateCommon.inf のチェック
		}

		/// <summary>
		/// ファイルの差分を比較する
		/// </summary>
		/// <param name="file1"></param>
		/// <param name="file2"></param>
		/// <returns>比較不可=-1, 差分無し=0. 差分あり=差分バイト位置(+1)</returns>
		public static int DiffFile(string file1, string file2)
		{
			if (System.IO.File.Exists(file1) == false || System.IO.File.Exists(file2) == false)
			{
				return -1; // ファイルが存在しない場合は比較できない
			}
			byte[] file1Bytes = System.IO.File.ReadAllBytes(file1);
			byte[] file2Bytes = System.IO.File.ReadAllBytes(file2);
			if (file1Bytes.Length != file2Bytes.Length)
			{
				return -1; // サイズが異なる場合は異なる
			}
			for (int i = 0; i < file1Bytes.Length; i++)
			{
				if (file1Bytes[i] != file2Bytes[i])
				{
					return i+1; // バイト単位で異なる場合は異なる
				}
			}
			return 0; // 全てのバイトが同じ場合は同じ
		}

		/// <summary>
		///  転送中止
		/// </summary>
		private async Task<bool> StopTransferExecute()
		{
			AddLog("[Transfer] Command=Stop");

			DialogTitle = Resource.Confirm;// "確認";
			DialogText = Resource.QuitTransferMsg;// "転送を中止しますか？";

			var r = await DialogHost.Show(new MyMessageBox(), "MainWindow");
			DialogHost.CloseDialogCommand.Execute(null, null);

			if (r == null || r.ToString() == "CANCEL")
			{
				return false;
			}

			CancelTokenSrc?.Cancel();
			return true;
		}

		/// <summary>
		///  選択状態にある装置のデータをバックアップする
		/// </summary>
		/// <param name="path">バックアップ先フォルダ</param>
		/// <param name="token">キャンセルトークン</param>
		/// <returns>bool</returns>
		public async Task<bool> BackupUnitData(string path, CancellationToken token)
		{
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
					if (machine.Children == null || machine.Name == null || machine.IsSelected.Value == false)
					{
						continue;
					}
					foreach (var module in machine.Children)
					{
						if (module.IsSelected.Value == false )
						{
							continue;
						}
						AddLog($"[BACKUP] {lcu.Name};{machine.Name};{module.Name}=Backup Start");

						//バックアップ先フォルダ名
						string bkupPath = path + $"\\{hd}{lcu.Name.Split(":")[0]}_{machine.Name.Split(":")[0]}_{module.Name}";

						if (Directory.Exists(bkupPath) == false)
						{
							Directory.CreateDirectory(bkupPath);
						}

						bool ret = await DownloadModuleFiles(lcu, machine, module, bkupPath, token);
						if( ret == false )
						{
							IsTransfering = false;
							return false;
						}
						AddLog($"[BACKUP] {lcu.Name};{machine.Name};{module.Name}=Backup End");

						//転送が完了したので、転送中止ボタンが押されていないか確認
						bool bt = await WaitTransferState(token);
						if (bt == false)
						{
							AddLog($"[Backup] {lcu.Name};{machine.Name};{module.Name}=Transfer Stop");
							IsTransfering = false;
							return false;
						}
					}
				}
			}
			IsTransfering = false;
			return true;
		}

		/// <summary>
		/// 転送ユニット数をカウントする(進捗表示用)
		/// </summary>
		public int GetTransferUnitCount()
		{
			int transferCount = 0;

			//対象のユニット数をカウント
			//　　(ユニット以外は3ステートチェックボックスなので
			//　　以下が全選択されていない場合はIsSelected.Value が null になる)
			TreeViewItems.Where(x => x.IsSelected.Value != false).ToList()
							.ForEach(x => x.Children.Where(x => x.IsSelected.Value != false).ToList()
								.ForEach(x => x.Children.Where(x => x.IsSelected.Value != false).ToList()
									.ForEach(x => transferCount += x.UnitVersions.Count(y => y.IsSelected.Value == true))));

			return transferCount;
		}

		/// <summary>
		/// path 下にあるファイル数を取得する
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		private static int GetDirectoryFiles(string path)
		{
			int files = 0;

			string pathWin = path.Replace("/", "\\");

			if (System.IO.File.Exists(pathWin) == true || System.IO.Directory.Exists(pathWin) == true )
			{
				FileAttributes attr = System.IO.File.GetAttributes(pathWin);

				// ディレクトリかどうか判定する(※ディレクトリの場合、FileAttributes.Directory | FileAttributes.Archive になる)
				if ( (System.IO.File.GetAttributes(pathWin) & FileAttributes.Directory) == FileAttributes.Directory)
				{
					string[] fi = Directory.GetFiles(pathWin, "*", SearchOption.AllDirectories);
					files += fi.Length;
					Debug.WriteLine($"File:{pathWin}={fi.Length}");
				}
				else
				{
					string dir = Path.GetDirectoryName(pathWin) ?? "";
					string[] fi = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
					files += fi.Length;
					Debug.WriteLine($"File:{pathWin}={fi.Length}");
				}
			}
			return files;
		}

		/// <summary>
		/// 転送するユニットデータファイルの数を取得する
		/// </summary>
		/// <returns>転送するファイル数</returns>
		//public (long, long, long, long, long) GetTransferFiles(ref long lines, ref long machines, ref long modules, ref long units, ref long files)
		public List<string> GetTransferFiles(ref long lines, ref long machines, ref long modules, ref long units, ref long files)
		{
			lines = 0;
			machines = 0;
			modules = 0;
			units = 0;
			files = 0;

			List<string> paths = [];

			//対象のユニット数をカウント
			foreach (var item in TreeViewItems)
			{
				if (item.IsSelected.Value == false)
				{
					continue;
				}
				lines++;
				foreach (var machine in item.Children)
				{
					if (machine.IsSelected.Value == false)
					{
						continue;
					}
					machines++;
					foreach (var module in machine.Children)
					{
						if (module.IsSelected.Value == false)
						{
							continue;
						}
						modules++;
						foreach (var unit in module.UnitVersions)
						{
							if (unit.IsSelected.Value == true)
							{
								UnitLink? un = Config.units.Where(x => x.components.Find(y => y == unit.Name) != null).FirstOrDefault();

								if (un != null && un.mode == "Folder")
								{
									string? path = Path.GetDirectoryName( (DataFolder + unit.Path).Replace("/", "\\") );
									DirectoryInfo di = new(path ?? ".\\");
									long sz = GetDirectorySize(di);
									string[] fi = Directory.GetFiles(di.FullName, "*", SearchOption.AllDirectories);
									
									foreach(var f in fi)
									{
										if (f != null && f != "")
										{
											if (paths.Contains(f) == false) {
												files++;
												paths.Add(f);
											}
										}
									}
								}
								else
								{
									paths.Add(DataFolder + unit.Path.Replace("/","\\"));

									files++;
									if (unit.FuserPath != null && unit.FuserPath != "")
									{
										files++;
										paths.Add(DataFolder + unit.FuserPath.Replace("/","\\"));
									}
								}
								units++;
							}
						}
					}
				}
			}
			// paths の中に重複がある場合は削除する
			paths = [.. paths.Distinct()];

			return paths;
			//return (lines, machines, modules, units, files);
		}

		/// <summary>
		/// LCUに対してユニットデータを転送する
		/// </summary>
		/// <param name="lcu">LCU情報</param>
		/// <param name="token">キャンセルトークン</param>
		/// <returns></returns>
		public async Task<bool> UploadUnitFilesToLcu(LcuInfo lcu, CancellationToken token)
		{
			bool ret;

			string lcuRoot = $"/MCFiles";

			// folder に含まれるファイルをリスト化
			List<string> folders = CreateUpdateFolderList();

			//対象ファイルをすべて LCU にアップロード
			//対象のみとすると、ユニット毎に要/不要を計算する手間があるので、アップデートデータは全てアップロードする
			AddLog($"[Transfer] {lcu.Name}=UploadFiles Start");

			// デバッグ環境でない場合
			ret = await UploadFilesWithFolder(lcu, folders, lcuRoot, DataFolder,token);

			//デバッグ環境の場合、FTPが一つだけなので、LCU_n のフォルダで対応する
			//ret = await UploadFilesWithFolder(lcu, folders, $"{App.GetLcuRootPath(lcu.LcuId)}" + lcuRoot, DataFolder, token);
			if (ret == false)
			{
				AddLog($"[Transfer] {lcu.Name}=UploadFilesWithFolder Error");
				ErrorInfo.ErrCode = ErrorCode.LCU_UPLOAD_ERROR;
				await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", $"{lcu.Name}", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
				return false;
			}
			AddLog($"[Transfer] {lcu.Name}=UploadFiles End");

			return ret;
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
			string retMsg;

			if (UpdateDataInfos == null)
			{
				return false;
			}

			Progress?.SetMessage($"[Backup] {lcu.Name};{machine.Name};{module.Name}");

			// moduleからUpdateCommon.inf を取り出す
			ret = await GetModuleUpdateInfo(lcu, machine, module, token);
			if(ret == false)
			{
				AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=GetModuleUpdateInfo Error");
				ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
				return false;
			}

			//装置から取得したUpdateCommon.inf のパスのみを取り出してリスト化(重複を削除)
			//    ※パスはファイル名を含むので、ファイル名を削除してフォルダのみを取り出す
			//      WebAPIに装置からフォルダを含む一覧を取得するコマンドがないため、UpdateCommon.inf のパスを利用する
			List<string> folders = module.UpdateFiles(Config.Options).Select(x => x).ToList().Distinct().ToList();

			//UpdateCommon.inf のパスを追加(UpdateCommon.inf は必ず存在する)
			folders.Add( $"/{Define.MC_PERIPHERAL_PATH}{Define.UPDATE_INFO_FILE}");

			//UpDateData 下の全ファイルを対象
			string[] allFiles = Directory.GetFiles(DataFolder + "\\Fuji", "*", SearchOption.AllDirectories);

			var folderList = allFiles.Select(x => Path.GetDirectoryName(x)?.Split(DataFolder)[1].Replace("\\", "/") ?? "")
				.Distinct()
				.Where(x => x != null && x != "")
				.ToList();
			//バックアップ用のフォルダを作成
			foreach (var folder in folderList)
			{
				Directory.CreateDirectory(backupPath + folder);
			}

			//retMsg = await lcu.LcuCtrl.LCU_Command(SetLcu.Command(lcu.LcuId));

			//LCU上にフォルダを作成する(装置からファイルを取得するフォルダ,装置と同じフォルダ構造をLCUのFTP下に作る)
			//string lcuRoot = $"LCU_{lcu.LcuId}/MCFiles";
			string lcuRoot = "/MCFiles";
			ret = lcu.LcuCtrl.CreateFtpFolders(folderList, lcuRoot);
			if (ret == false)
			{
				AddLog($"[DownLoad] {lcu.Name}=CreateFtpFolders Error");
				ErrorInfo.ErrCode = ErrorCode.LCU_CREATE_FOLDER_ERROR;
				return ret;
			}

			var fileList = allFiles.Select(x => x.Split(DataFolder)[1].Replace("\\", "/") ?? "")
				.Distinct()
				.Where(x => x != null && x != "")
				.ToList();

			var mc = fileList.Select(x => x).ToList();
			var lc = fileList.Select(x => $"{lcuRoot}{x}").ToList();

			bool bRet = await lcu.LcuCtrl.GetMachineFiles(machine.Name, module.Pos, mc, lc, backupPath+"\\",token);
			if(bRet == false)
			{
				AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=GetMachineFiles Error");
				ErrorInfo.ErrCode = ErrorCode.LCU_DOWNLOAD_ERROR;
				return false;
			}

			//UpdateCommon.inf の先頭にバックアップ情報を追記する
			DateTime dt = DateTime.Now;
			string infoFile = backupPath + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
			string[] data = System.IO.File.ReadAllLines(infoFile, Encoding.GetEncoding(Define.TXT_ENCODING));
			string[] newData = new string[data.Length + 1];
			newData[0] = $"#UpdateInfo={lcu.Name};{machine.Name};{module.Name};{dt.ToString("yyyy/MM/dd HH:mm:ss")}";
			Array.Copy(data, 0, newData, 1, data.Length);
			StringsToFile(newData, infoFile);

			return true;

#if false
			try
			{
				AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=GetMcFileList");
				List<string> mcFiles = [];
				List<string> lcuFiles = [];

				string mcUser = GetMcUser();
				string mcPass = GetMcPass();
				foreach (var folder in folders)
				{
					AddLog($"[DownLoad] {folder}");
					Progress?.SetMessage($"[Backup] {folder}");

					string cmdRet = await lcu.LcuCtrl.LCU_Command(GetMcFileList.Command(machine.Name, module.Pos, mcUser, mcPass, folder), token);
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
									mcFiles.Add(file.name);
									lcuFiles.Add("/MCFiles" + file.name);

									/*
									if (Path.GetExtension(file.name) == ".inf" && Path.GetFileName(file.name) != "UpdateCommon.inf")
									{
										string lcuFile = $"/MCFiles/{Path.GetFileName(file.name)}";
										string tmpDir = Path.GetTempPath();
										bool rr = await lcu.LcuCtrl.GetMachineFile(machine.Name, module.Pos, file.name, lcuFile, tmpDir);

										System.IO.File.Delete(tmpDir + Path.GetFileName(file.name));
									}
									*/
								}
							}
						}
					}
				}

				Progress?.SetMessage($"[Backup] {lcu.Name};{machine.Name};Unit DownLoad OK");

				//装置からLCUにファイルを取得する
				retMsg = await lcu.LcuCtrl.LCU_Command(GetMcFiles.Command(machine.Name, module.Pos, mcUser, mcPass, mcFiles, lcuFiles), token);

				if (retMsg == "" || retMsg == "Internal Server Error")
				{
					AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=WebApi(GetMCFile) Error");
					ErrorInfo.ErrCode = ErrorCode.LCU_WEBAPI_ERROR;
					return false;
				}
				// LCU からFTPでファイルを取得 
				bool bRet = await lcu.LcuCtrl.DownloadFiles(lcuRoot, backupPath, mcFiles, token);

				//LCU 上に作成したファイルを削除
				ret = lcu.LcuCtrl.ClearFtpFolders(lcuRoot);

				if (bRet == false)
				{
					AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=DownloadFiles Error");
					ErrorInfo.ErrCode = ErrorCode.LCU_DOWNLOAD_ERROR;
					return false;
				}
				Progress?.SetMessage($"[Backup] {lcu.Name};{machine.Name};LCU DownLoad OK");

				//UpdateCommon.inf の先頭にバックアップ情報を追記する
				DateTime dt = DateTime.Now;
				string infoFile = backupPath + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
				string[] data = System.IO.File.ReadAllLines(infoFile, Encoding.GetEncoding(Define.TXT_ENCODING));
				string[] newData = new string[data.Length + 1];
				newData[0] = $"#UpdateInfo={lcu.Name};{machine.Name};{module.Name};{dt.ToString("yyyy/MM/dd HH:mm:ss")}";
				Array.Copy(data, 0, newData, 1, data.Length);
				StringsToFile(newData, infoFile);
			}
			catch (Exception e)
			{
				AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=DownloadModuleFiles Error");
				ErrorInfo.ErrCode = ErrorCode.LCU_DOWNLOAD_ERROR;
				throw;
			}
			return ret;
#endif
		}

		/// <summary>
		/// プログレスダイアログ表示中のキャンセルの確認
		/// </summary>
		private async Task<bool> WaitTransferState(CancellationToken? token)
		{
			if (CanTransferStartFlag.Value == false)
			{
				Debug.WriteLine("WaitTransferState");
				while (true)
				{
					if (CanTransferStartFlag.Value == true)
					{
						Debug.WriteLine("WaitTransferState=End");
						break;
					}
					if (token != null)
					{
						if (token.Value.IsCancellationRequested == true)
						{
							return false;
						}
					}
					//await Task.Delay(500);
					//Debug.WriteLine(".");
				}
			}
			return true;
		}

		/// <summary>
		/// アップデート対象のフォルダを取得する
		/// </summary>
		/// <returns></returns>
		private List<string> CreateUpdateFolderList()
		{
			List<string> folders = [];

			if (UpdateDataInfos == null)
			{
				//例外除け
				return folders;
			}

			foreach (var item in UpdateDataInfos)
			{
				string localPath = DataFolder + item.Path;
				if (Path.Exists(localPath) == false)
				{
					//存在しなければ次へ
					continue;
				}
				if ( (System.IO.File.GetAttributes(localPath) & FileAttributes.Directory) == FileAttributes.Directory )
				{
					//folders.Add((item.Name, localPath));
					folders.Add(localPath);
				}
				else
				{
					string? path = Path.GetDirectoryName(localPath);
					if (path != null)
					{
						//folders.Add((item.Name, path));
						folders.Add(path);
					}
				}
			}
			//重複を削除
			folders = folders.Distinct().ToList();

			return folders;
		}

		/// <summary>
		/// ファイルが存在するかどうかを確認する
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		private static bool IsFileExists(string path)
		{
			if (System.IO.File.Exists(path) == false || ((System.IO.File.GetAttributes(path) & FileAttributes.Directory)) == FileAttributes.Directory)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// アップデート対象のファイルを取得する
		/// </summary>
		/// <returns></returns>
		private List<UnitPath> CreateUpdateFileList(ReactiveCollection<UnitVersion> unitVersions)
		{
			List<UnitPath> unitFiles = [];

			if (UpdateDataInfos == null)
			{
				//例外除け
				return unitFiles;
			}

			foreach(var item in unitVersions)
			{
				var group = Config.units.Where(x => x.components.Contains(item.Name)).FirstOrDefault();
				if(group == null)
				{
					continue;
				}
				var un = unitFiles.Where(x => group.ContainsName(x.GroupName)).FirstOrDefault();

				if (group.mode == "File")
				{
					if (item.IsSelected.Value == false)
					{
						continue;
					}
					if( un == null)
					{
						if( item.FuserPath != null && item.FuserPath != "")
						{
							unitFiles.Add( new UnitPath { GroupName = group.GetName(), units = [(item.Name, DataFolder + item.Path, DataFolder + item.FuserPath)] });
						}
						else
						{
							unitFiles.Add( new UnitPath { GroupName = group.GetName(), units = [(item.Name, DataFolder + item.Path, "")] });
						}
					}
					else
					{
						un.AddUnitFile(DataFolder, (item.Name, item.Path, item.FuserPath));
					}
				}
				else
				{
					var path = UpdatePaths.Where(x => group.ContainsName(x.GroupName)).FirstOrDefault();
					if( path.units == null || path.units.Count == 0)
					{
						//アップデート対象のファイルが存在しない
						continue;
					}

					foreach (var unit in path.units)
					{
						var im = unitVersions.Where(x => x.Path == unit.path).FirstOrDefault();
						if (im != null)
						{
							//対象ファイルがアップデート情報に存在する
							if (im.IsSelected.Value == false)
							{
								//アップデート対象として選択されていない
								continue;
							}
							if( un == null)
							{
								if(unit.upath != null && unit.upath != "")
								{
									un = new UnitPath { GroupName = group.GetName(), units = [(unit.name, DataFolder + unit.path, DataFolder + unit.upath)] };
								}
								else
								{
									un = new UnitPath { GroupName = group.GetName(), units = [(unit.name, DataFolder + unit.path, "")] };
								}
								unitFiles.Add(un);
							}
							else
							{
								un.AddUnitFile(DataFolder,(unit.name, unit.path, ""));
							}
						}
						else
						{
							//対象ファイルがアップデート情報に存在しない

							//含まれるグループの中で選択されているものをカウント
							var aa = unitVersions.Where(x => x.UnitGroup == path.GroupName).ToList().Count(y => y.IsSelected.Value == true);
							if( aa != 0 )
							{
								// 一つでも選択されているので、(UpdateCommon.inf に含まれていないものは)転送対象とする
								if( un == null)
								{
									if (unit.upath != null && unit.upath != "")
									{
										un = new UnitPath { GroupName = group.GetName(), units = [(unit.name, DataFolder + unit.path, DataFolder + unit.upath)] };
									}
									else
									{
										un = new UnitPath { GroupName = group.GetName(), units = [(unit.name, DataFolder + unit.path, "")] };
									}
									unitFiles.Add(un);
								}
								else
								{
									un.AddUnitFile(DataFolder,(unit.name, unit.path, ""));
								}
							}
						}
					}
				}
			}
			return unitFiles;
		}

		/// <summary>
		///  転送済みファイルと転送元ファイルの差分をチェックする
		/// </summary>
		/// <param name="lcu">LCU情報</param>
		/// <param name="machine">装置情報</param>
		/// <param name="module">モジュール情報</param>
		/// <param name="fileGroup">ファイルグループ名</param>
		/// <param name="mc">送信したファイルパス</param>
		/// <returns>true=差分無し</returns>
		public bool CheckTransferedFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module, UnitPath fileGroup, List<string> mc)
		{
			bool ret = true;

			foreach(var file in mc)
			{
				string file1 = (DataFolder + file).Replace("/","\\");
				string file2 = (Path.GetTempPath() + "MCFiles" + file).Replace("/","\\");

				string filePeripheral = file.Split("Peripheral")[1];
				var unl = fileGroup.units
							.Select(u => new
							{
								Name = u.name,
								Path = !string.IsNullOrEmpty(u.path) ? u.path.Split("Peripheral")[1] : null,
								uPath = !string.IsNullOrEmpty(u.upath) ? u.upath.Split("Peripheral")[1] : null
							}).ToList();
				string? un = unl .FirstOrDefault(x => (x.Path != null && filePeripheral.Contains(x.Path)) || (x.uPath != null && filePeripheral.Contains(x.uPath))) ?.Name ?? "";

				if (DiffFile(file1, file2) != 0)
				{
					//転送したファイルが正しくない
					AddLog($"[INSPECTION] {lcu.Name};{machine.Name};{module.Name};{un}=NG:DiffFile Inspection Error({file})");
					ret = false;
				}
				else
				{
					AddLog($"[INSPECTION] {lcu.Name};{machine.Name};{module.Name};{un};{file}=OK:Inspection Success");
				}
			}
			return ret;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="updateCommonFile">UpdateCommon.inf</param>
		/// <param name="unitVersions"></param>
		/// <exception cref="NotImplementedException"></exception>
		private void CheckUpdateCommon(string updateCommonFile, ReactiveCollection<UnitVersion> unitVersions)
		{
			// UpdateCommon.inf の内容を確認する
			if (IsFileExists(updateCommonFile) == false)
			{
				AddLog($"[ERROR] UpdateCommon.inf is not exists: {updateCommonFile}");
				return;
			}
			var update = GetSectionVersionList(updateCommonFile);
			foreach (var (section, version) in update)
			{
				//セクション名とバージョンを取得
				Debug.WriteLine($"Section={section}, Version={version}");
				var unit = unitVersions.FirstOrDefault(x => x.Name == section);
				if (unit != null)
				{
					if (unit.CurVersion != version)
					{
						AddLog($"[ERROR] {section} version mismatch: {unit.CurVersion} != {version}");
					}
					else
					{
						AddLog($"[OK] {section} version match: {unit.CurVersion} == {version}");
					}
				}
				else
				{
					AddLog($"[ERROR] {section} is not found in UnitVersions");
				}
			}
		}
		/// <summary>
		/// フォルダを含むファイルをアップロードする
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="folders"></param>
		/// <param name="lcuRoot"></param>
		/// <param name="srcRoot"></param>
		/// <returns></returns>
		public async Task<bool> UploadFilesWithFolder(LcuInfo lcu, List<string> folders, string lcuRoot, string srcRoot, CancellationToken token)
		{
			bool ret = true;
			string user = lcu.FtpUser;
			string password = lcu.FtpPassword;

			var ftpClient = new AsyncFtpClient(lcu.LcuCtrl.Name.Split(":")[0], user, password);

			await ftpClient.Connect(token);
			foreach (string folder in folders)
			{
				// 元フォルダからLCUフォルダへ変換する
				string lcuPath = lcuRoot + folder[srcRoot.Length..];
				try
				{
					// folder 下の全ファイルを lcuPath フォルダへアップロードする(フォルダがない場合は作成してくれる)
					int ftpFail = 0;
					do {
						ftpFail = 0;
						List<FtpResult> ftpRet = await ftpClient.UploadDirectory(folder, lcuPath,
																					FtpFolderSyncMode.Update,
																					FtpRemoteExists.Overwrite,
																					FtpVerify.OnlyVerify,
																					null,
																					null,
																					token);

						foreach (var rr in ftpRet)
						{
							Debug.WriteLine($"[FTP Upload] {rr.LocalPath} => {rr.RemotePath} : {rr.IsSuccess}");
							if( rr.IsSuccess == false)
							{
								//失敗した場合
								if (rr.IsSkipped == false)
								{
									Debug.WriteLine($"[FTP Upload] Retry={ftpFail}");
									ftpFail++;
									break;
								}
							}
						}
						// 失敗がなければループを抜ける
					} while ( ftpFail != 0);

					long bytes = GetDirectorySize(new DirectoryInfo(folder));
					Debug.WriteLine($"{folder}={bytes} bytes");

					FtpTransferedSize += bytes;

					Progress?.SetMessage($"Transfering {lcu.Name}:{Path.GetFileName(folder)}");

					// 進捗%の計算
					//  PC->LCU で 25% とする(LCU->MC で 75%)　※プログレスバーのレンジを0-1000としているため 25% は 250
					long rate = (long)(((double)FtpTransferedSize / (double)FtpTransferSize) * 250.0);
					Debug.WriteLine($"TransferedFtpSize={FtpTransferedSize}, FtpDataSize={FtpTransferSize}, rate={rate}");

					Progress?.SetProgress( ((double)FtpTransferedSize / (double)FtpTransferSize) * 250.0);
					await Task.Delay(100, CancellationToken.None);  // プログレスバーの更新のためのDelay

					Debug.WriteLine($"[FTP Upload] {folder} => {lcu.Name}/{lcuPath}");

					bool bt = await WaitTransferState(token);
					if (bt == false)
					{
						AddLog($"[Transfer] {lcu.Name}=Stop");
						ret = false;
						break;
					}
				}
				catch (Exception e)
				{
					ret = false;
					AddLog($"[Transfer] {lcu.Name}=UploadFilesWithFolder Error({e.Message})");
					break;
				}
			}
			await ftpClient.Disconnect(token);
			return ret;
		}


		public async Task<bool> PostMcFilesCommand(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string group, List<string> mc, List<string> lc, CancellationToken token)
		{
			bool ret = true;
			int errIdx = 0;
			string retMsg;
			string mcUser = GetMcUser();
			string mcPass = GetMcPass();

			if (mcUser == "" || mcPass == "")
			{
				// モジュールにアクセスするためのFTP情報が取得できない(mcAccount.Dll が無い、など)
				ErrorInfo.ErrCode = ErrorCode.FTP_ACCOUNT_ERROR;
				AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}={ErrorInfo.GetErrMessage()}");
				return false;
			}

			retMsg = await lcu.LcuCtrl.LCU_Command(PostMcFile.Command(machine.Name, module.Pos, mcUser, mcPass, mc, lc), token);

			if (retMsg == "" || retMsg == "Internal Server Error")
			{
				ErrorInfo.ErrCode = ErrorCode.LCU_CONNECT_ERROR;
				AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}=WebAPI(PostMcFile) Error");
				return false;
			}
			// エラーメッセージがある場合はログに出力(デバッグ用、FTPでファイル転送に失敗した場合など)
			PostMcFile? retData = JsonSerializer.Deserialize<PostMcFile>(retMsg);
			if( retData != null && retData.HasError(ref errIdx))
			{
				TransferErrorCount++;
				if( retData.ftp.data == null || retData.ftp.data.Count == 0)
				{
					// エラーが発生しているが、エラー情報が取得できない
					AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}=WebAPI(PostMcFile) Error:No Error Data");
					ErrorInfo.ErrCode = ErrorCode.FTP_TRANSFER_ERROR;
					return false;
				}
				var ec = retData.ftp.data[errIdx].errorCode;
				ErrorInfo.ErrCode = ErrorCode.FTP_TRANSFER_ERROR;

				AddLog($"[ERROR] {retData.ftp.data[errIdx].errorMessage} errCode={ec}");
				AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{group}=NG:{retData.ftp.data[errIdx].errorMessage}");
				return false;
			}
			return ret;
		}

		public void UpdateProgress(long transfered, long total, long cnt)
		{
			if (Progress != null)
			{
				// 進捗%の計算
				double r = 250.0 + ((double)transfered / (double)total) * 750.0;
				Debug.WriteLine($"Progress={r} Transfered={TransferedCount} cnt={cnt}");
				if( r > 1000.0)
				{
					//最大値を超えてセットすると例外が発生するので
					r = 1000.0;
				}
				Progress?.SetProgress(r);
			}
		}

		/// <summary>
		/// LCUからモジュールにユニットソフトを転送する(LCUにデータは転送ずみであること)
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="machine"></param>
		/// <param name="module"></param>
		/// <param name="token"></param>
		/// <returns></returns>
		public async Task<bool> UploadModuleFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module, CancellationToken token)
		{
			bool ret = true;
			string lcuRoot = $"/MCFiles";
			bool IsAdmin = ArgOptions.GetOption("--mode", "user") == "administrator";

			if (module.UnitVersions == null)
			{
				return false;
			}

			/*
			if (module.UnitVersions.Count == 0)
			{
				// アップデートバージョンが未生成の場合は UpdateCommon.inf を取得する
				ret = await GetModuleUpdateInfo(lcu, machine, module, token);
				if (ret == false)
				{
					AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}=GetModuleUpdateInfo Error");
					ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
					if (IsAdmin == false)
					{
						return false;
					}
				}
				var version = CreateVersionInfo(lcu, machine, module, token);
				if (version == null) {
					ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
					return false;
				}
				module.UnitVersions = version;

				// バージョン情報からファイル数を取得
				int files = version.Select(x => x.GetFileCount()).Sum();
				TransferCount += files;
			}
			*/

			string[] lines = module.UpdateStrings;

			// UpdateCommon.inf の Path のフォルダにファイルを転送する
			//   ※ UpdateCommon.inf に記載されているデータは存在するものとする
			//
			//バージョン情報からパスのみを取り出してリスト化
			List<string> mcFiles = module.UnitVersions.Select(x => x.Path).ToList();

			//(転送データの)バージョン情報からパスのみを取り出してリスト化
			List<UnitPath> unitFiles = CreateUpdateFileList(module.UnitVersions);

			//LCUに転送したファイルを装置に送信する
			string mcUser = GetMcUser();
			string mcPass = GetMcPass();

			if( mcUser == "" || mcPass == "" )
			{
				// モジュールにアクセスするためのFTP情報が取得できない(mcAccount.Dll が無い、など)
				ErrorInfo.ErrCode = ErrorCode.FTP_ACCOUNT_ERROR;
				AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}={ErrorInfo.GetErrMessage()}");
				return false;
			}

			foreach(var fileGroup in unitFiles)
			{
				// //Fuji 以降を取り出してリスト化(装置に送るファイル名)
				//  path, fuserPath を取り出してリスト化
				List<string> mc = [.. fileGroup.units
					.SelectMany(u => 
						new[] { u.path[DataFolder.Length..] }
						.Concat(
							!string.IsNullOrEmpty(u.upath) ? [u.upath[DataFolder.Length..]] : Array.Empty<string>()
						)
					)];

				// LCU にアップロードされたファイル名をリスト化
				List<string> lc = [.. mc.Select(x => $"{lcuRoot}" + x)];

				//転送が完了したので、転送中止ボタンが押されていないか確認(フォルダ単位)
				bool bt = await WaitTransferState(token);
				if (bt == false)
				{
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{fileGroup.GroupName}=NG:Transfer Stop");
					ret = false;
					break;
				}

				Progress?.SetMessage($"Transfering {fileGroup.GroupName}");

				if (mc.Count > 10)
				{
					var chunkedMc = mc.Chunk(10).Select(x => x.ToList()).ToList();
					var chunkedLc = lc.Chunk(10).Select(x => x.ToList()).ToList();
					foreach (var (mcChunk, lcChunk) in chunkedMc.Zip(chunkedLc))
					{
						// LCUにアップロードされたファイルを装置に送信する
						ret = await PostMcFilesCommand(lcu, machine, module, fileGroup.GroupName, mcChunk, lcChunk, token);
						if (ret == false)
						{
							TransferErrorCount++;
							break;
						}
						TransferSuccessCount += mcChunk.Count;
						TransferedCount += mcChunk.Count;
						UpdateProgress(TransferedCount, TransferCount, mcChunk.Count);
					}
				}
				else
				{
					ret = await PostMcFilesCommand(lcu, machine, module, fileGroup.GroupName, mc, lc, token);
					TransferSuccessCount += mc.Count;
					TransferedCount += mc.Count;

					UpdateProgress(TransferedCount, TransferCount, mc.Count);
				}

				//転送が完了したのでUpdateを「済」にする
				foreach(var (name, path, upath) in fileGroup.units)
				{
					//選択されているユニットのバージョン情報を取得
					UnitVersion? unitVersion = module.UnitVersions.FirstOrDefault(x => x.Name == name);
					if (unitVersion != null && path != null)
					{
						unitVersion.IsUpdated.Value = true;

						string tmp = path.Split(DataFolder)[1];
						if (tmp == unitVersion.Path || tmp == unitVersion.FuserPath)
						{
							//UpdateCommon.inf の Path, FuserPath に記載されているものを対象とする
							//対象となるユニットのバージョン情報(string)を更新
							ModifyUpdateInfo(lines, unitVersion.Name, unitVersion.NewVersion);

							AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{unitVersion.Name}=OK:{unitVersion.CurVersion}->{unitVersion.NewVersion}");
							AddLog($"[File] {name} Path={path} FuserPath={upath}");
						}
					}
					else
					{
						//AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{name}=OK:no version");
					}
				}
				//(検査)転送したファイルを取得し直す(送信したファイルが正しいか確認するため)
				if( ArgOptions.GetOptionBool("--inspection",Config.Options.GetDefaultOption("inspection",false)) == true)
				{
					//転送したファイルを取得し直す
					bool bRet = await lcu.LcuCtrl.GetMachineFiles(machine.Name, module.Pos, mc, lc, Path.GetTempPath()+"MCFiles\\",token);
					if (bRet == false)
					{
						AddLog($"[INSPECTION] {lcu.Name};{machine.Name};{module.Name};{fileGroup.GroupName}=NG:GetMcFile Inspection Error");
					}
					else
					{
						CheckTransferedFiles(lcu, machine, module, fileGroup, mc);
					}
				}
			}

			if (TransferSuccessCount != 0)
			{
				// 転送を完了したものがある場合、UpdateCommon.inf を更新する

				// UpdateCommon.inf の最後尾に更新日時を追加(デバッグの確認用、本番では不要)
				if( ArgOptions.GetOptionBool("--inspection",Config.Options.GetDefaultOption("inspection",false)) == true)
				{
					//UpdateCommon.inf の最後尾に更新日時を追加
					DateTime dt = DateTime.Now;
					Array.Resize(ref lines, lines.Length + 1);
					lines[^1] = $";UpdateCommon.inf({dt:yyyy/MM/dd HH:mm:ss})";
				}

				string tmp = Path.GetTempPath();

				//string[] をファイルに書き込む(UpdateCommon.infの生成)
				StringsToFile(lines, tmp + Define.UPDATE_INFO_FILE);

				// ファイル(UpdateCommon.inf)を モジュールに転送
				ret = await PutMcPeripheralFile(lcu, machine, module, Define.UPDATE_INFO_FILE, tmp);

				if( ArgOptions.GetOptionBool("--inspection", Config.Options.GetDefaultOption("inspection", false)) == true)
				{
					//転送したファイルを取得し直す
					List<string> mc = ["/" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE];
					List<string> lc = [Define.UPDATE_INFO_FILE];
					bool bRet = await lcu.LcuCtrl.GetMachineFiles(machine.Name, module.Pos, mc, lc, Path.GetTempPath()+"MCFiles\\",token);
					if (bRet == false)
					{
						AddLog($"[INSPECTION] {lcu.Name};{machine.Name};{module.Name}=NG:Inspection Error(UpdateCommon.inf)");
					}

					// UpdateCommon.inf の内容を確認する
					CheckUpdateCommon(tmp + "MCFiles\\" +  Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE, module.UnitVersions);
				}
			}

			//for Debug
			//Directory.Delete(tmpDir, recursive: true);
			if( ArgOptions.GetOptionBool("--inspection",Config.Options.GetDefaultOption("inspection",false)) == true)
			{
				string getPath = Path.GetTempPath() + "MCFiles\\";
				List<string> files = Directory.GetFiles(getPath, "*", SearchOption.AllDirectories).ToList();
				List<string> paths = files.Select(x => x.Split("MCFiles")[1].Replace("\\", "/")).ToList();

				CheckTransferedFiles(paths, module.UnitVersions);

			}

			return ret;
		}

		/// <summary>
		/// モジュールからファイルを取得する
		/// </summary>
		/// <param name="lcu">LCU</param>
		/// <param name="machine">装置</param>
		/// <param name="module">モジュール</param>
		/// <param name="peripheralFile">取得するファイル名</param>
		/// <param name="targetPath">取得したファイルを格納するローカルパス</param>
		/// <returns></returns>
		private static async Task<bool> GetMcPeripheralFile(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string peripheralFile, string targetPath, CancellationToken token)
		{
			string mcFile = Define.MC_PERIPHERAL_PATH + peripheralFile;
			//string lcuFile = $"/MCFiles/" + peripheralFile;
			string lcuFile = $"/MCFiles/" + mcFile;

			string retMsg = await lcu.LcuCtrl.LCU_Command(SetLcu.Command(lcu.LcuId));

			string temp = targetPath + Define.MC_PERIPHERAL_PATH;
			Directory.CreateDirectory(targetPath + Define.MC_PERIPHERAL_PATH);

			//装置からファイルを取得
			return await lcu.LcuCtrl.GetMachineFile(machine.Name, module.Pos, mcFile, lcuFile, targetPath,token);
		}

		/// <summary>
		/// モジュールにファイルを転送する
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="machine"></param>
		/// <param name="module"></param>
		/// <param name="peripheralFile"></param>
		/// <param name="localPath"></param>
		/// <returns></returns>
		private static async Task<bool> PutMcPeripheralFile(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string peripheralFile, string localPath)
		{
			string mcFile = Define.MC_PERIPHERAL_PATH + peripheralFile;
			string lcuFile = $"/MCFiles/" + peripheralFile;
			//装置へファイルを転送
			return await lcu.LcuCtrl.PutMachineFile(machine.Name, module.Pos, mcFile, lcuFile, localPath);
		}

		/// <summary>
		///  UpdateCommon.inf をユニット単位で更新する
		/// </summary>
		/// <param name="lines"></param>
		/// <param name="Name"></param>
		/// <param name="version"></param>
		private static void ModifyUpdateInfo(string[] lines, string Name, string version)
		{
			int section = 0;
			foreach (ref string line in lines.AsSpan())
			{
				if (line.Equals($"[{Name}]"))
				{
					section = 1;
				}
				if (section == 1 && line.StartsWith("Version="))
				{
					line = "Version=" + version;
					break;
				}
			}
		}

		/// <summary>
		///  string[] からファイルを作成する
		/// </summary>
		/// <param name="lines"></param>
		/// <param name="path"></param>
		private static void StringsToFile(string[] lines, string path)
		{
			// string[] をファイルに書き込む append = false で上書き
			StreamWriter outFile = new(path, false, Encoding.GetEncoding(Define.TXT_ENCODING));
			foreach (var line in lines)
			{
				outFile.WriteLine(line);
			}
			outFile.Close();
		}

		/// <summary>
		/// ツリーのマウス左ボタンダウン処理(Expand/Collapseでも呼ばれる)
		/// 　※SelectedItemChanged より先に呼び出される。
		/// </summary>
		/// <param name="e"></param>
		public async void TreeViewMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			string viewName = "";

			if (e.OriginalSource is FrameworkElement element && element.DataContext is CheckableItem item)
			{
				Debug.WriteLine($"MouseLeftButtonDown={item.Name}:{item.ItemType}");
				viewName = item.GetViewName();

				//現在のビューのキーを取得
				string key = viewModeTable.FirstOrDefault(x => x.Value == ActiveView).Key;

				SelectedItem = item;

				if (key != viewName)
				{
					//表示しているビューが選択された項目と異なっている場合は、ビューを切り替える
					if (viewModeTable.ContainsKey(viewName) == true)
					{
						// ビューが生成済みである場合は、生成済みのビューを表示する
						ActiveView = viewModeTable[viewName];
						switch( item.ItemType)
						{
							case MachineType.LCU:
								IsLcuMenuEnabled = true;
								break;
							case MachineType.Machine:
								IsLcuMenuEnabled = false;
								if(ActiveView is MachineViewModel machineView)
								{
									machineView.UpdateCheck();
								}
								break;
							case MachineType.Module:
								IsLcuMenuEnabled = false;
								break;
						}
						if(IsLcuMenuEnabled == true)
						{
							CanExecuteLcuCommand.Value = true;
						}
					}
					else
					{
						// 一度もツリーを操作していない場合は、選択されたアイテムを選択状態にする
						switch (item.ItemType)
						{
							case MachineType.LCU:
								if( item is LcuInfo lcuInfo)
								{
									if( lcuInfo.ErrCode != ErrorCode.OK)
									{
										await MsgBox.Show(Resource.Error, $"{lcuInfo.Name}",
														  $"{ErrorInfo.GetErrMessage(lcuInfo.ErrCode)}",
														  "", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
									}
								}
								var machines = TreeViewItems.Where(x => x.Name == item.Name).First().Children;
								viewModeTable.Add(viewName, new LcuViewModel((LcuInfo)item, machines));
								IsLcuMenuEnabled = true;
								CanExecuteLcuCommand.Value = true;
								break;
							case MachineType.Machine:
								var modules = TreeViewItems.SelectMany(lcu => lcu.Children).Where(x => x.Name == item.Name).First().Children;

								viewModeTable.Add(viewName, new MachineViewModel( (MachineInfo)item, modules));
								IsLcuMenuEnabled = false;
								break;
							case MachineType.Module:
								IsLcuMenuEnabled = false;
								ModuleInfo moduleInfo = (ModuleInfo)item;
								MachineInfo machineInfo = moduleInfo.Parent;
								LcuInfo lcu = machineInfo.Parent;

								//時間がかかるので、プログレスダイアログを表示
								//var dlg = DialogHost.Show(new WaitProgress(Resource.ReadingUnitInfo),"DataGridView");
								var dlg = DialogHost.Show(new WaitProgress(Resource.ReadingUnitInfo),"MainWindow");
								await Task.Delay(500); // ダイアログ表示のための遅延(タイマーが無いとダイアログが表示されるより前に CloseDialogCommnad が実行される場合あり)

								var token = new CancellationToken();
								bool ret = await GetModuleUpdateInfo(lcu, machineInfo, moduleInfo, token);

								DialogHost.CloseDialogCommand.Execute(null, null);

								if( ret == false )
								{
									if( ArgOptions.GetOption("--mode","user") == "user")
									{
										//アップデート情報の取得に失敗した場合は、アップデート情報を取得できない旨をログに出力
										AddLog($"[ERROR] {lcu.Name}:{machineInfo.Name}:{moduleInfo.Name}={ErrorInfo.GetErrMessage()}");

										//アップデート情報の取得に失敗した場合は、アップデート情報を取得できない旨を表示
										await MsgBox.Show(Resource.Error, $"{lcu.Name}:{machineInfo.Name}:{moduleInfo.Name}",
														  Resource.Error_UPDATE_UNDEFINED_ERROR,
														  "", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
										return;
									}
								}
								//var version = CreateVersionInfo(lcu, machineInfo, moduleInfo, token);
								bool bRet = CreateVersionInfo(lcu, machineInfo, moduleInfo, token);
								if (bRet == true)
								{
									moduleInfo.ToolTipText = $"{moduleInfo.UnitVersions.Count} units";
								}

								viewModeTable.Add(viewName, new ModuleViewModel((ModuleInfo)item, UpdateDataInfos));
								moduleInfo.IsSelected.Value = Utility.CheckState<UnitVersion>(moduleInfo.UnitVersions);
								break;
						}
						ActiveView = viewModeTable[viewName];
					}
				}

				// ユニットがない場合はメッセージを表示
				if ( ActiveView is ModuleViewModel moduleView)
				{
					if (moduleView.UnitVersions.Count == 0)
					{
						ModuleInfo moduleInfo = (ModuleInfo)item;
						await MsgBox.Show(Resource.Info, $"{moduleInfo.Name}",
										  Resource.NoUnitToUpdate,
										  "", (int)(MsgDlgType.OK | MsgDlgType.ICON_INFO), "MainWindow");
					}
				}
			}
		}

		/// <summary>
		///  ツリーの選択状態が変更されたときの処理
		/// </summary>
		/// <param name="e"></param>
		public async void TreeViewSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
		{
			// TreeViewMouseLeftButtonDown() の処理が先に行われるので、
			// ここで何かする必要はない、かも。

			/*
			 *	SelectedItemChanged() は選択状態が「変化」した場合しか呼び出されないので
			 *  プログラムでビューを切り替えた場合(ActiveViewを変更)に
			 *  ツリーの選択をプログラムから変更できない(IsSelected は readOnly)のため
			 *  ツリーの選択状態と表示しているビューの不一致が発生する。
			 *
			 *  よって、LeftButtonDown でクリックしたビューに切り替える事にした
			 */
			Debug.WriteLine("TreeViewSelectedItemChanged");
		}

		/// <summary>
		/// バージョン情報を作成する
		/// </summary>
		/// <param name="versions">バージョン情報リスト</param>
		/// <param name="unit">ユニット名</param>
		/// <param name="newVer">新バージョン</param>
		/// <param name="module">モジュール情報</param>
		/// <returns></returns>
		private void CreateUnitVersionList(ReactiveCollection<UnitVersion> versions, string unit, string newVer, ModuleInfo module)
		{
			if ( module.UpdateInfo == null)
			{
				return;
			}

			long fileSz = -1;
			IniFileParser parser = module.UpdateInfo;
			string path = parser.GetValue(unit, "Path");
			string binPath = DataFolder + path;

			if (System.IO.File.Exists(binPath) == true)
			{
				if (System.IO.File.GetAttributes(binPath) == FileAttributes.Directory)
				{
					return;
				}
				else
				{
					FileInfo fi = new(binPath);
					fileSz = fi.Length;
				}
			}

			bool sel = (module.IsSelected.Value != false);
			if (int.Parse(parser.GetValue(unit, "Attribute")) == Define.NOT_UPDATE || newVer == "N/A" || IsBackupedData == true )
			{
				//Attribute が 2 の場合はアップデート禁止
				//ユニットが対象外
				//バックアップデータの場合
				sel = false;
			}

			string group = string.Empty;
			Dictionary<string, object> gp = Config.units.FirstOrDefault(x => x.components.Contains(unit))?.name;
			if (gp != null)
			{
				group = gp.TryGetValue(module.Parent.MachineType, out object? obj) ? obj.ToString() : gp["*"].ToString();
			}

			string p1 = parser.GetValue(unit, "Path");
			string p2 = parser.GetValue(unit, "FuserPath");

			if ( System.IO.File.Exists(DataFolder + p1) == false )
			{
				Debug.WriteLine($"[UnitVersion] {module.Name}:{unit}={p1} Path Not Found");
				//Path が存在しない場合(転送できない)は、アップデート対象外とする
				return;
			}
			if( p2 != "")
			{
				if ( System.IO.File.Exists(DataFolder + p2) == false )
				{
					// FuserPath はダミーとなっている場合があるので(SE_FeederTypeT)、ファイルが存在しない場合は空文字にする
					Debug.WriteLine($"[UnitVersion] {module.Name}:{unit}={p2} FuserPath Not Found");
					parser.SetValue(unit, "FuserPath", string.Empty);
				}
			}
			string curVer = module.UpdateInfo.GetValue(unit, "Version");
			if( module.UpdateInfoLoaded == UnitInfoLoadStatus.LoadedBase )
			{
				// 装置からUpdateCommon.inf の取得に失敗した場合は、現在のバージョンは "Undefined" とする
				//   ※ただし --mode=administrator の場合のみ
				curVer = "Undefined";
			}
			else
			{
				if( curVer == "" )
				{
					// 装置のUpdateCommon.inf に unit が存在しない場合は、現在のバージョンは "N/A" とする
					//  ※　--ignore_unit オプションが指定されている場合は、UpdateCommon.inf に記載されていないユニットも対象
					sel = false; // 選択状態
					curVer = "N/A";
				}
			}

			UnitVersion version = new(sel)
			{
				Name = unit,
				Attribute = int.TryParse(parser.GetValue(unit, "Attribute"), out int attr) ? attr : 0,
				Path = path ?? string.Empty,
				FuserPath = parser.GetValue(unit, "FuserPath") ?? string.Empty,
				CurVersion = curVer,
				NewVersion = newVer ?? "N/A",
				Parent = module,
				Size = fileSz >= 0 ? fileSz : 0,
				UnitGroup = group
			};

			versions.Add(version);
			Debug.WriteLine($"[UnitVersion] {module.Name}:{unit}={version.CurVersion}=>{version.NewVersion}");
		}

		/// <summary>
		/// LCUからモジュールのアップデート情報(UpdateCommon.inf)を取得する
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="machine"></param>
		/// <param name="module"></param>
		/// <returns></returns>
		public static async Task<bool> GetModuleUpdateInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module, CancellationToken token)
		{
			bool bRet;

			if (module.UpdateInfo == null || module.UpdateInfoLoaded == UnitInfoLoadStatus.NotLoaded)
			{
				// (装置から)UpdateCommon.inf ファイルを取得
				string tmpDir = Path.GetTempPath() + "MCFiles\\";
				bRet = await GetMcPeripheralFile(lcu, machine, module, Define.UPDATE_INFO_FILE, tmpDir, token);
				if( bRet == false)
				{
					ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
					module.UpdateInfoLoaded = UnitInfoLoadStatus.LoadError; // UpdateCommon.inf の取得に失敗
					return false;
				}

				// UpdateCommon.inf を読み込んで iniFile, string[] にセット
				string inf_file = tmpDir + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
				module.SetUpdateInfo(inf_file);

				//テンポラリに生成したファイルを削除
				foreach (var dir in Directory.GetDirectories(tmpDir))
				{
					Directory.Delete(dir, recursive: true);
				}
				module.UpdateInfoLoaded = UnitInfoLoadStatus.LoadedMachine; // UpdateCommon.inf の取得に成功
			}
			return true;
		}

		/// <summary>
		/// LCUからユニットバージョン情報を取得する
		/// </summary>
		/// <param name="lcu">LcuInfo</param>
		/// <param name="machine">MachineInfo</param>
		/// <param name="module">ModuleInfo</param>
		/// <param name="progCtrl">ProgressDialogController</param>
		/// <param name="token">CancellationToken</param>
		/// <returns></returns>
		public bool CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module, CancellationToken token)
		{
			if (lcu.LcuCtrl == null || UpdateDataInfos == null)
			{
				//return null;
				return false;
			}
			//ReactiveCollection<UnitVersion> versions = [];

			// ユニット一覧作成時のオプション
			//   --ignore_version が指定されている場合は、バージョンの違いを無視する
			bool ignoreVersion = ArgOptions.GetOptionBool("--ignore_version", Config.Options.GetDefaultOption("ignore_version",false));

			bool IsAdmin = ArgOptions.GetOption("--mode", "user") == "administrator";

			if( module.UpdateInfo == null)
			{
				if(IsAdmin == false)
				{
					// ユーザーモードで UpdateCommon.inf が取得できない場合は、アップデートを中止する
					AddLog($"[ERROR] {lcu.LcuCtrl.Name};{machine.Name};{module.Name}={Define.UPDATE_INFO_FILE} Get error");
					return false;
				}
				// Administrator モードで UpdateCommon.inf が取得できていない場合はアップデートデータと同じとする
				module.SetUpdateInfo(DataFolder + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE);

				module.UpdateInfoLoaded = UnitInfoLoadStatus.LoadedBase; // UpdateCommon.inf の取得に失敗したが、強制的にアップデートを行う場合
				ignoreVersion = true; // バージョンの違いを無視する(アップデートデータと同じなので)
			}

			IniFileParser parser = module.UpdateInfo;

			List<string> mcUnits = parser.SectionCount();  //[Unit]セクションのリストを取得

			foreach (var updateInfo in UpdateDataInfos)
			{
				//アップデート情報のユニット名を取得
				string unit = updateInfo.Name;
				if (string.IsNullOrEmpty(unit))
				{
					continue; //ユニット名が空の場合はスキップ
				}
				//装置内に該当するユニットのインデックスを取得(ない場合は -1)
				if( mcUnits.IndexOf(unit) != -1 )
				{
					if (ignoreVersion == false && parser.GetValue(unit, "Version") == updateInfo.Version)
					{
						//同じバージョンは対象外とするオプション指定の場合
						AddLog($"[UnitVersion] {lcu.LcuCtrl.Name};{machine.Name};{module.Name}:{unit}=Skip(same version)");
						continue;
					}
					CreateUnitVersionList(module.UnitVersions, unit, updateInfo.Version, module);
				}
				else
				{
					//装置内に該当するユニットがない場合は、アップデート対象外とする
					//  ※装置内にLCU経由でフォルダを生成できないため、Peripheral フォルダ下に未対応ユニットのフォルダを作れない
				}
			}

			if ( IsAdmin == false )
			{
				// ユニットグループ内のチェックを統一する
				//   ユーザーモードではユニット単位の選択ができないので、ユニットグループ単位で選択状態を統一する
				//      (false がある場合は全て false にする)

				// 一旦グループ単位でまとめる
				OrderedDictionary<string, UnitGroup> od = [];
				foreach (var item in module.UnitVersions)
				{
					if (od.ContainsKey(item.UnitGroup) == false)
					{
						od.Add(item.UnitGroup, new UnitGroup(item));
					}
					else
					{
						od[item.UnitGroup].Units.Add(item);
					}
				}
				// グループ内の選択状態を統一する
				foreach (var item in od)
				{
					item.Value.UnificationUnitState();
				}
				// チェック状態を更新したのでグループ分けの情報は不要
				od.Clear();
			}

			//return versions;
			return true;
		}

		private string _textValue = "Hello, World!";
		public string TextValue
		{
			get => _textValue;
			set => SetProperty(ref _textValue, value);
		}

		/// <summary>
		/// ライン情報を取得する
		/// </summary>
		private async Task<bool> LoadLineInfo(CancellationToken token)
		{
			AddLog("LoadLineInfo Start");

			// NeximDB より LCU のリストを取得(list=> Line名=LCU名)
			List<string> lcuList = GetLcuListFromNexim();

			TreeViewItems = [];

			foreach (var lcu in lcuList)
			{
				string lineName = lcu.Split('=')[0];
				string lcuName = lcu.Split('=')[1];
				string id = lcu.Split('=')[2]; // computerId
				TreeViewItems.Add(new LcuInfo(lcuName, Int32.Parse(id)) { Name = lineName, ItemType = MachineType.LCU });
			}

			// LCU, LCU下のライン 情報を取得
			foreach (var lcu in TreeViewItems)
			{
				bool bRet = true;
				try
				{
					Progress?.SetMessage($"reading {lcu.LcuCtrl.Name}");
					bRet = await UpdateLcuInfo(lcu, token);
				}
				catch (HttpRequestException e)
				{
					AddLog($"[ERROR] LoadLineInfo={e.Message}");
					Progress?.SetMessage($"{lcu.LcuCtrl.Name}={e.Message}");
					lcu.ErrCode = ErrorCode.LCU_LINEINFO_ERROR;
					lcu.Status = ItemStatus.NG;
					lcu.IconToolTipText = Resource.ConnectionError;
				}
				catch (Exception e)
				{
					AddLog("[ERROR] LoadLineInfo=Canceled");
					Progress?.SetMessage($"{lcu.LcuCtrl.Name}={e.Message}");
					lcu.IsSelected.Value = false;
					lcu.Status = ItemStatus.NG;
					lcu.IconToolTipText = Resource.ConnectionError;
					return false;
				}

				Debug.WriteLine($"[LineInfo] {lcu.Name}={bRet}");
				//await Task.Delay(3000,token);

				//LCU下の装置、モジュール、ユニット数を取得
				int machineCount = lcu.Children.Count(x => x.IsSelected.Value != false);
				int moduleCount = lcu.Children.SelectMany(x => x.Children).Count(x => x.IsSelected.Value != false);
				int unitCount = lcu.Children.SelectMany(x => x.Children).SelectMany(x => x.UnitVersions).Count(x => x.IsSelected.Value != false);
				AddLog($"[LineInfo] {lcu.Name};Machine={machineCount};Module={moduleCount};Unit={unitCount}");

				foreach (var machine in lcu.Children)
				{
					if (machine.IsSelected.Value == false)
					{
						continue;
					}
					int mduleCount = machine.Children.Count(x => x.IsSelected.Value != false);
					foreach (var module in machine.Children)
					{
						if (module.IsSelected.Value == false)
						{
							continue;
						}
						int unit = module.UnitVersions.Count(x => x.IsSelected.Value == true);
					}
				}
				if (lcu.ErrCode != ErrorCode.OK)
				{
					//LCUの情報にエラーがある場合は、選択状態を解除
					lcu.IsSelected.Value = false;
					lcu.ToolTipText = ErrorInfo.GetErrMessage(lcu.ErrCode);
				}
				else
				{
					lcu.IsSelected.Value = Utility.CheckState<MachineInfo>(lcu.Children);
					lcu.ToolTipText = $"{lcu.Version}";
				}
			}

			// LCU下に装置が無い LCU(Line)を削除
			/*
			List<LcuInfo> tmpList = TreeViewItems.Where(x => x.Children.Count == 0).ToList();
			foreach (var item in tmpList)
			{
				TreeViewItems.Remove(item);
			}
			*/

			AddLog("LoadLineInfo End");
			_lineInfoLoaded = true;

			return true;
		}

		/// <summary>
		/// LCUの情報を取得する
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public async Task<bool> GetLineInfoFromLcu(LcuInfo lcu, CancellationToken? token)
		{
			// lines コマンドで LCU下の装置情報を取得
			XmlSerializer serializer = new(typeof(LineInfo));
			string response = await lcu.LcuCtrl.LCU_HttpGet("lines");

			if (response.Contains("errorCode"))
			{
				AddLog($"[ERROR] {lcu.Name};get lines error");
				AddLog($"[LineInfo] {lcu.Name};get linesInfomation error");
				ErrorInfo.ErrCode = ErrorCode.LCU_LINEINFO_ERROR;
				await MsgBox.Show(Resource.Error, $"{lcu.Name}",$"{Resource.GetLineInfoError}","", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
				return false;
			}

			LineInfo? lineInfo = (LineInfo)serializer.Deserialize(new StringReader(response));
			if (lineInfo == null || lineInfo.Line == null || lineInfo.Line.Machines == null )
			{
				ErrorInfo.ErrCode = ErrorCode.LCU_LINEINFO_ERROR;
				await MsgBox.Show(Resource.Error, $"{lcu.Name}",$"{Resource.GetLineInfoError}","", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
				return false;
			}

			//ライン情報を保持
			lcu.LineInfo = lineInfo;

			//ライン下の装置、モジュールをTreeViewに追加
			foreach (var mc in lineInfo.Line.Machines)
			{
				if( false == ArgOptions.GetOptionBool("--ignore_machineType",Config.Options.GetDefaultOption("ignore_machineType",false)) )
				{
					//マシンタイプ指定が有効の場合
					if ( Config.Options.machineType.Contains(mc.MachineType) == false )
					{
						//登録されているマシンタイプ以外は対象外
						AddLog($"[LineInfo] {lcu.Name};{mc.MachineName}=Skip:{mc.MachineType}");
						continue;
					}
				}
				MachineInfo machine = new()
				{
					Name = mc.MachineName,
					ItemType = MachineType.Machine,
					Machine = mc,
					Parent = lcu,
					Status = Config.Options.machineType.Contains(mc.MachineType) ? ItemStatus.OK : ItemStatus.NOT_SUPPORTED,
					ToolTipText = mc.MachineType
				};
				if (machine.Status == ItemStatus.NOT_SUPPORTED)
				{
					machine.IconToolTipText = Resource.NotApplicable;// "Not Supported";
				}

				Progress?.SetMessage($"Machine={mc.MachineName}");
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
					if (Config.Options.machineType.Contains(mc.MachineType) == false)
					{
						//未サポート機種の場合は、選択状態を解除
						machine.IsSelected.Value = false;
					}
					else
					{
						machine.IsSelected.Value = lcu.IsSelected.Value;
					}

					foreach (var module in base_.Modules)
					{
						ModuleInfo moduleItem = new()
						{
							Name = module.DispModule,
							ItemType = MachineType.Module,
							Module = module,
							Parent = machine,
							IPAddress = base_.IpAddr,
							//ToolTipText = "ユニット情報未取得"
							ToolTipText = Resource.UnitInfoNotAcquired,
							Status = machine.Status
						};

						Progress?.SetMessage($"Module={module.DispModule}");
						baseInfo.Children.Add(moduleItem);
						machine.Children.Add(moduleItem);

						if (Config.Options.machineType.Contains(mc.MachineType) == false)
						{
							//未サポート機種の場合は、選択状態を解除
							moduleItem.IsSelected.Value = false;
							moduleItem.IconToolTipText = Resource.NotApplicable;
						}
						else {
							//上位の選択を反映させる
							moduleItem.IsSelected.Value = lcu.IsSelected.Value;
						}
					}
				}
				lcu.Children.Add(machine);
			}
			return true;
		}

		/// <summary>
		/// LCUの情報を更新する
		/// </summary>
		/// <param name="LcuInfo">LCU情報</param>
		public async Task<bool> UpdateLcuInfo(LcuInfo lcu, CancellationToken token)
		{
			bool bRet = true;
			bool ping = await CheckComputer(lcu.LcuCtrl.Name.Split(':')[0], 3);

			if (ping == false)
			{
				AddLog($"[ERROR] {lcu.Name}=Access Fail");
				AddLog($"[LineInfo]:{lcu.Name}=Access Fail");
				Progress?.SetMessage($"LCU:{lcu.LcuCtrl.Name}=Access Fail");
				lcu.ErrCode = ErrorCode.LCU_CONNECT_ERROR;
				lcu.Status = ItemStatus.NG;
				lcu.IconToolTipText = Resource.ConnectionError;
				return false;
			}

			if (lcu.LcuCtrl.FtpUser == null)
			{		
				// FTPアカウント情報を取得
				var str = await lcu.LcuCtrl.LCU_Command(FtpData.Command(), token);
				FtpData? data = FtpData.FromJson(str);
				if (data == null)
				{
					Progress?.SetMessage($"LCU:{lcu.LcuCtrl.Name}=FTP information Fail");
					lcu.ErrCode = ErrorCode.FTP_SERVER_ERROR;
					return false;
				}
				if (data.username == null || data.password == null)
				{
					Progress?.SetMessage($"LCU:{lcu.LcuCtrl.Name}=FTP user, password Fail");
					lcu.ErrCode = ErrorCode.FTP_ACCOUNT_ERROR;
					return false;
				}
				// password を復号化
				string password = FtpData.GetPasswd(data.username, data.password);

				if (data.username.Contains('\\'))
				{
					// ユーザ名にドメイン名が含まれている場合は、ドメイン名を除去する))
					data.username = data.username.Split('\\')[1];
				}

				// FTPアカウント情報を設定
				lcu.FtpUser = data.username;
				lcu.FtpPassword = password;
				//lcu.FtpUser = "ka.makihara";
				//lcu.FtpPassword = "wildgeese@2702";

				lcu.LcuCtrl.FtpUser = lcu.FtpUser;
				lcu.LcuCtrl.FtpPassword = lcu.FtpPassword;

				// LCU バージョン取得
				str = await lcu.LcuCtrl.LCU_Command(LcuVersion.Command(), token);
				IList<LcuVersion>? versionInfo = LcuVersion.FromJson(str);
				if (versionInfo == null)
				{
					AddLog($"[ERROR] {lcu.Name}=LcuVersion Get Error");
					AddLog($"[LineInfo] {lcu.Name};LcuVersion Get Error");
					Progress?.SetMessage($"LCU:{lcu.LcuCtrl.Name}=LcuVersion Get Error");
					lcu.ErrCode = ErrorCode.LCU_VERSION_ERROR;
					return false;
				}
				lcu.Version = versionInfo.First(x => x.itemName == "Fuji LCU Communication Server Service").itemVersion;

				Progress?.SetMessage($"LCU:{lcu.LcuCtrl.Name}=Version({lcu.Version})");

				//ディスク情報
				lcu.DiskSpace = await LcuDiskChkCmd(lcu);

				if (lcu.DiskSpace < UpdateDataSize)
				{
					// アップデートデータを転送するだけのディスク容量がない
					lcu.ErrCode = ErrorCode.UPDATE_DISKSPACE_ERROR;
					lcu.IsUpdateOk = false;

					AddLog($"[ERROR] {lcu.Name}=DiskSpace Error({lcu.DiskSpace}<{UpdateDataSize})");
					AddLog($"[LineInfo] {lcu.Name};DiskSpace Error({lcu.DiskSpace}<{UpdateDataSize})");

					Progress?.SetMessage($"LCU:{lcu.LcuCtrl.Name}=DiskSpace Error({lcu.DiskSpace}<{UpdateDataSize})");
					lcu.IconToolTipText = Resource.DiskSpaceError;
				}
			}

			//装置情報が未取得の場合
			if (lcu.Children.Count == 0)
			{
				await GetLineInfoFromLcu(lcu, token);
			}
			return bRet;
		}

		/// <summary>
		///  アプリ終了
		/// </summary>
		private async void ApplicationShutDown()
		{
			CanAppQuitFlag.Value = false;           //ボタンを無効にする(Command で実行されているので、ダイアログ表示中にボタンを押せないようにする)
			CanTransferStartFlag.Value = false;

			DialogTitle = Resource.Confirm;// "確認";
			DialogText = Resource.QuitAppMsg;// "アプリケーションを終了しますか？";

			var r = await DialogHost.Show(new MyMessageBox(), "MainWindow");
			DialogHost.CloseDialogCommand.Execute(null, null);

			if (r == null || r.ToString() == "CANCEL")
			{
				CanAppQuitFlag.Value = true;
				CanTransferStartFlag.Value = true;
				return;
			}
			if (LogWriter != null)
			{
				LogWriter.Close();
			}

			//Peripheral.bin の展開フォルダを削除
			if (Directory.Exists("C:\\DecompBin") == true)
			{
				Directory.Delete("C:\\DeCompBin", true);
			}

			Application.Current.Shutdown();
		}

		//for Test
		private async void ShowTestResult()
		{
			//ファイルを読み込む
			DateTime dt = DateTime.Now;
			//string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
			//	+ "UnitTransferResult\\Update_" + $"{dt.ToString("yyyyMMdd")}.txt";
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
				+ "\\UnitTransferResult\\Update.txt";

			string resultData = System.IO.File.ReadAllText(desktopPath, Encoding.UTF8);


			if (resultWindow != null && resultWindow.IsVisible)
			{
				resultWindow.Close();
				resultWindow = null;
			}
			// 転送結果ウィンドウを表示する

			if (resultWindow is null)
			{
				resultWindow = new TransferResultWindow(resultData);
				resultWindow.Closed += (o, args) => resultWindow = null;
			}
			this.resultWindow.Launch();
		}

		/// <summary>
		/// iniファイルからセクション名とVersionのリストを取得
		/// </summary>
		/// <param name="iniFilePath">iniファイルのパス</param>
		/// <returns>List<(string section, string version)></returns>
		public List<(string Section, string Version)> GetSectionVersionList(string iniFilePath)
		{
			var result = new List<(string Section, string Version)>();
			try
			{
				var parser = new IniFileParser(iniFilePath);
				var sections = parser.SectionCount();
				foreach (var section in sections)
				{
					var version = parser.GetValue(section, "Version");
					result.Add((section, version));
				}
			}
			catch (Exception ex)
			{
				AddLog($"iniファイル読み込みエラー: {ex.Message}");
			}
			return result;
		}

		/// <summary>
		/// 転送進捗表示ダイアログを表示する
		/// </summary>
		/// <returns></returns>
		/*
		public async Task<CustomDialog> ShowCustomDialog()
		{
			var customDialog = new CustomDialog { Title = "Unit Soft Transfer" };
			var dataContext = new TransferProgressViewModel(InstanceData =>
			{
				// キャンセルボタンが押されたときの処理
				this._dialogCoordinator.HideMetroDialogAsync(this, customDialog);
				InstanceData.IsCanceled = true;
			});
			customDialog.Content = new TransferProgressDialog { DataContext = dataContext };

			await this._dialogCoordinator.ShowMetroDialogAsync(this, customDialog);

			return customDialog; 
		}
		public async Task<CustomDialog> ShowLineInfoResultDialog()
		{
			var customDialog = new CustomDialog { Title = "Line Info Result" };
			var dataContext = new LineInfoResultDialogViewModel(InstanceData =>
			{
				// キャンセルボタンが押されたときの処理
				this._dialogCoordinator.HideMetroDialogAsync(this, customDialog);
			});
			customDialog.Content = new LineInfoResultDialog { DataContext = dataContext };
			await this._dialogCoordinator.ShowMetroDialogAsync(this, customDialog);
			return customDialog;
		}
		*/
	}
}
