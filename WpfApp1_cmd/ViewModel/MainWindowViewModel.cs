﻿using Reactive.Bindings;
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
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using WpfApp1_cmd.Models;
using FluentFTP;
using Renci.SshNet;
using System.Windows.Threading;

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
			//Debug.WriteLine(str);
			if (LogWriter != null)
			{
				LogWriter.WriteLine(str);
			}
		}

		// ユニット連携情報
		private UnitLink _unitLink;
		public UnitLink UnitLink { get => _unitLink; set => _unitLink = value; }

		// ツリービューのアイテム
		public ReactiveCollection<LcuInfo> TreeViewItems { get; set; }

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
				if (activeView != value)
				{
					activeView = value;
					OnPropertyChanged(nameof(ActiveView));
				}
			}
		}
		private string DataFolder { get; set; } = "";   // UpdateCommon.inf のフォルダ
		public long UpdateDataSize { get; set; } = 0;         // UpdateCommon.inf のフォルダのサイズ

		public string DialogTitle { get; set; } = "Dialog Title";
		public string DialogText { get; set; } = "Dialog Text";

		//アップデート用のバージョンデータ
		private ReactiveCollection<UpdateInfo> _upDateInfos;
		private ReactiveCollection<UpdateInfo>? UpdateInfos
		{
			get => _upDateInfos;
			set
			{
				_upDateInfos = value;
				OnPropertyChanged(nameof(UpdateInfos));
			}
		}

		public DelegateCommand<string> ScreenTransitionCommand { get; }

		public ReactiveCommand FileOpenCommand { get; } = new ReactiveCommand();
		public ReactiveCommand LcuNetworkChkCommand { get; } = new ReactiveCommand();
		public ReactiveCommand LcuDiskChkCommand { get; } = new ReactiveCommand();

		// 「Transfer」「Stop」「App Quit」のフラグ
		public ReactiveProperty<bool> CanTransferStartFlag { get; } = new ReactiveProperty<bool>(true);
		public ReactiveProperty<bool> CanTransferStopFlag { get; } = new ReactiveProperty<bool>(false);
		public ReactiveProperty<bool> CanAppQuitFlag { get; } = new ReactiveProperty<bool>(true);
		public ReactivePropertySlim<bool> CanTransferFlag { get; } = new ReactivePropertySlim<bool>(true);

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

		// 転送制御ボタン
		public ReactiveCommandSlim StartTransferCommand { get; } = new ReactiveCommandSlim();
		//public ReactiveCommandSlim StopTransferCommand { get; } = new ReactiveCommandSlim();
		public ReactiveCommandSlim QuitApplicationCommand { get; } = new ReactiveCommandSlim();

		public ReactiveCommandSlim HomeCommand { get; } = new ReactiveCommandSlim();

		// TreeViewを操作可能か
		public ReactiveProperty<bool> IsTreeEnabled { get; } = new ReactiveProperty<bool>(true);

		//メニューの有効無効
		public bool IsFileMenuEnabled { get; set; } = true;
		public bool IsLcuMenuEnabled { get; set; } = true;

		public StreamWriter? LogWriter { get; set; } = null;

		public int TransferErrorCount { get; set; } = 0;    // PostMcFile でのエラー数
		public int TransferSuccessCount { get; set; } = 0;  // PostMcFile での成功数
		public long TransferCount { get; set; } = 0;          // 転送数(ユニット数)
		public long TransferedCount { get; set; } = 0;        // 転送済み数(ユニット数)

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
			if (Options.GetOption("--dbName", "") != "")
			{
				return Options.GetOption("--dbName", "");
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
			if (Options.GetOption("--dbHost", "") != "")
			{
				return Options.GetOption("--dbHost", "");
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
			if (Options.GetOption("--dbUser", "") != "")
			{
				return Options.GetOption("--dbUser", "");
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
			if (Options.GetOption("--dbPass", "") != "")
			{
				return Options.GetOption("--dbPass", "");
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
		public bool _lineInfoLoaded = false;        // line 情報の読み込み完了フラグ
		private async void LoadLineInfo_Start()
		{
			CancellationTokenSource cts = new CancellationTokenSource();

			//ログファイル
			Startup_log();

			//progress
			Progress = await Metro.ShowProgressAsync("Read Machine information ...", "");

			Progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・
			Progress.SetCancelable(true); // キャンセルボタンを表示する
			//Progress.Maximum = 1000;
			//Progress.Minimum = 0;

			Task task = Task.Run(() => { Task<bool> task1 = LoadLineInfo(cts.Token); });

			//Debug.WriteLine($"{nameof(task.IsCompleted)} ; {task.IsCompleted}");
			for (var i = 0; i < 1000; i++)
			{
				//Debug.WriteLine($"{nameof(task.IsCompleted)} ; {task.IsCompleted}");
				if (task.IsCompleted)
				{
					// タスクの完了を待つ(※なぜか正しく取得できないので、変数を使用する)
					if (_lineInfoLoaded == true)
					{
						break;
					}
				}
				if (Progress.IsCanceled == true)
				{
					cts.Cancel();
					break;
				}
				//Progress.SetProgress((double)i / 1000);

				//「待ち」を入れないと、ダイアログが更新されない
				await Task.Delay(100);
			}

			OnPropertyChanged(nameof(TreeViewItems));

			await Progress.CloseAsync();
		}

		/// <summary>
		/// Peripheral.bin を展開する
		/// </summary>
		public void CallDecompExe(string path)
		{
			/*
			ProcessStartInfo psi = new ProcessStartInfo();
			psi.FileName = $"{ Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}\\DecompBin.exe";
			psi.Arguments =$"{DataFolder}\\Peripheral.bin";
			Process? p = Process.Start(psi);

			if(p != null)
			{
				p.WaitForExit();
			}   
			*/
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
					var result = await MsgBox.Show("Info", $"{lcu.Name}", $"DiskSpace={diskMB} MB", "", (int)(MsgDlgType.OK | MsgDlgType.ICON_INFO), "DataGridView");
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
					AddLog($"Not enough disk space:{driveInfo.AvailableFreeSpace} < UpdateData::{fileInfo.Length}");
					MsgBox.Show("Error", "ErrorCode=E003", "Not enough disk space", "Not enough disk space for UpdateData", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "DataGridView");
					return false;
				}
				else
				{
					AddLog($"DeCompress {path}\\Peripheral.bin");
					CallDecompExe(path + "\\Peripheral.bin");
				}
				return true;
			}
			return false;
		}

		/// <summary>
		///  ユニットの連携情報を読み込む(デフォルトはexeのリソースから)
		/// </summary>
		private void ReadUnitLink()
		{
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			var srcName = $"{assembly.GetName().Name}.Resources.unitLink.json";
			string[] resources = assembly.GetManifestResourceNames();
			string str = "";

			using (var stream = assembly.GetManifestResourceStream(srcName))
			{
				using (var reader = new StreamReader(stream))
				{
					str = reader.ReadToEnd();
				}
			}
			UnitLink = JsonSerializer.Deserialize<UnitLink>(str);

			// オプションでユニットリンクファイルが指定されている場合は、読み込んで更新する
			if (Options.GetOption("--unitLink", "") != "")
			{
				//オプションで指定されたファイルを読み込む
				string path = Options.GetOption("--unitLink", "");
				if (Path.Exists(path) == true)
				{
					string json = System.IO.File.ReadAllText(path);
					UnitLink? list = JsonSerializer.Deserialize<UnitLink>(json);

					if (list != null && UnitLink != null)
					{
						foreach (var unit in list.units)
						{
							var v = UnitLink.units.FirstOrDefault(x => x.name == unit.name);

							if (v != null)
							{
								//存在する場合は、コンポーネントを更新
								v.components = unit.components;
							}
							else
							{
								//存在しない場合は、追加
								UnitLink.units.Add(unit);
							}
						}
					}

				}
			}
		}
		/// <summary>
		/// コンストラクタ
		/// </summary>
		public MainWindowViewModel(IDialogCoordinator dialogCoordinator)
		{
			string infoFile = "";

			_dialogCoordinator = dialogCoordinator;

			// unitLink.json の読み込み(ユニットのリンク情報、デフォルト)
			ReadUnitLink();

			//オプション処理
			string dataFolder = Options.GetOption("--dataFolder", "");
			if (dataFolder != "")
			{
				DataFolder = dataFolder;

				//データフォルダが指定されている場合は、展開する
				if (ReadPeripheralBin(dataFolder) == true)
				{
					//Peripheral.bin が展開されたので、UpdateCommon.inf のパスを設定
					infoFile = (DataFolder + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE).Replace("/", "\\");
				}
				else
				{
					infoFile = (DataFolder + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE).Replace("/", "\\");
				}
				if (Path.Exists(infoFile) == true)
				{
					//UpdateCommon.inf を読み込む
					UpdateInfos = ReadUpdateCommon(infoFile);
					if (UpdateInfos == null)
					{
						AddLog($"Read Error:{infoFile}");
					}
					else
					{
						AddLog($"Read {infoFile}");
						IsFileMenuEnabled = true;
					}
				}
				else
				{
					// UpdateCommon.inf が存在しない場合
					AddLog($"Can not found Update Infoemation");
					MsgBox.Show("Error", "ErrorCode=E001", "Not Found Update Data", "UpdateData", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "DataGridView");
				}
			}
			else
			{
				DataFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
				UpdateInfos = [];
				IsFileMenuEnabled = true;
				IsLcuMenuEnabled = true;
			}

			//ビューの生成
			viewModeTable = new Dictionary<string, ViewModelBase>
			{
				{ "AView", new AViewModel() },
				{ "BView", new BViewModel() },
				{ "CView", new CViewModel() },
				{ "UpdateVersionView", new UpdateVersionViewModel(UpdateInfos) },
				{ "UnitVersionView", new UnitVersionViewModel(UpdateInfos) },
				{ "TransferResultView", new TransferResultViewModel() }
			};
			//ActiveView = viewModeTable["UpdateVersionView"];
			//ActiveView = viewModeTable["UnitVersionView"];
			ActiveView = viewModeTable["TransferResultView"];

			//ライン情報読み込み
			WindowLoadedCommand.Subscribe(() => LoadLineInfo_Start());
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
			StartTransferCommand.Subscribe(() => StartTransfer());

			//StopTransferCommand = CanTransferStopFlag.ToReactiveCommandSlim();
			//StopTransferCommand.Subscribe(() => StopTransferExecute() );

			QuitApplicationCommand = CanAppQuitFlag.ToReactiveCommandSlim();
			QuitApplicationCommand.Subscribe(() => ApplicationShutDown());

			//TreeView 右クリックメニューのテスト
			TreeViewCommand.Subscribe((x) => { TreeViewMenu(x); });
			TreeViewChkCommand.Subscribe((x) => { TreeViewChkCmd(x); });

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
		/// 
		/// </summary>
		/// <param name="x"></param>
		private void TreeViewMenu(object x)
		{
			CheckableItem item = x as CheckableItem;

			Debug.WriteLine($"TreeViewMenu:{item.Name}");

			List<string> lcuList = GetLcuListFromNexim();

		}

		private void TreeViewChkCmd(object x)
		{
			CheckableItem item = x as CheckableItem;

			if (item == null)
			{
				return;
			}

			if (item is LcuInfo)
			{
				LcuInfo lcu = item as LcuInfo;
				bool? chk = item.IsSelected.Value;  // 値を取得しておく、(UpdateChildren()で子が更新されると IsSelected.Value 値が更新されてしまうので)
				foreach (MachineInfo machine in lcu.Children)
				{
					machine.UpdateChildren(chk);
				}
			}
			else if (item is MachineInfo)
			{
				MachineInfo machine = item as MachineInfo;
				bool? chk = item.IsSelected.Value;
				foreach (ModuleInfo module in machine.Children)
				{
					module.UpdateChildren(chk);
				}
			}
			else if (item is ModuleInfo)
			{
				ModuleInfo module = item as ModuleInfo;
				bool? chk = item.IsSelected.Value;
				foreach (UnitVersion unit in module.UnitVersions)
				{
					unit.IsSelected.Value = (chk == true);
				}
			}
		}

		/// <summary>
		///  ユニットアップデートデータのフォルダを選択して読み込む
		/// </summary>
		public async void FileOpenCmd()
		{
			using (var cofd = new CommonOpenFileDialog()
			{
				Title = "フォルダを選択してください",
				InitialDirectory = @"C:\Users\ka.makihara\develop",
				//フォルダ選択モード
				IsFolderPicker = true,
			})
			{
				if (cofd.ShowDialog() == CommonFileDialogResult.Ok)
				{
					AddLog($"FileOpenCommand={cofd.FileName}");
					if (Path.Exists(cofd.FileName + "\\UpdateCommon.inf") == false)
					{
						AddLog("UpdateCommon.inf が見つかりません");
						var result = await MsgBox.Show("Error", "ErrorCode=E004", "Can not Found UpdateInfo", "UpdateCommon.inf not exist.", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "DataGridView");
						return;
					}
					UpdateInfos = ReadUpdateCommon(cofd.FileName + "\\UpdateCommon.inf");

					DirectoryInfo di = new DirectoryInfo(cofd.FileName);

					long dataSize = Utility.GetDirectorySize(di);

					//バージョン情報ビューの更新
					viewModeTable["UpdateVersionView"] = new UpdateVersionViewModel(UpdateInfos);

					//アップデートデータが変更されたので、 モジュールのバージョン情報をクリアする
					foreach (LcuInfo lcu in TreeViewItems)
					{
						foreach (MachineInfo machine in lcu.Children)
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
						foreach (MachineInfo machine in lcu.Children)
						{
							foreach (ModuleInfo module in machine.Children)
							{
								var ret = await CreateVersionInfo(lcu, machine, module, cts.Token);
								if (ret != null)
								{
									module.UnitVersions = ret;
									if (viewModeTable.ContainsKey($"ModuleView_{module.Name}") == true)
									{
										viewModeTable[$"ModuleView_{module.Name}"] = new ModuleViewModel(module.UnitVersions, UpdateInfos);
									}
									else
									{
										viewModeTable.Add($"ModuleView_{module.Name}", new ModuleViewModel(module.UnitVersions, UpdateInfos));
									}
								}
							}
						}
					}
					cts.Dispose();

					ModuleInfo? item = SelectedItem as ModuleInfo;
					if (item != null)
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
			var result = await MsgBox.Show("Error", "ErrorCode=E001", "IP Address Error", "サーバーに接続できませんでした", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "DataGridView");

			if ((string)result == "OK")
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
		public async Task<long> LcuDiskChkCmd(LcuInfo lcu)
		{
			CancelTokenSrc = new CancellationTokenSource();
			CancellationToken token = CancelTokenSrc.Token;

			AddLog($"{lcu.Name}:Check Disk Space");
			List<LcuDiskInfo>? info = await lcu.LcuCtrl.LCU_DiskInfo(token);
			if (info == null)
			{
				AddLog($"{lcu.Name}::Don't get disk space information.");
				CancelTokenSrc.Dispose();
				return 0;
			}
			/*
			foreach (var item in info)
			{
				AddLog($"Drive: {item.driveLetter}, Total: {item.total}, Use: {item.use}, Free: {item.free}");
			}
			*/
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

			try
			{
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
						FuserPath = parser.GetValue(unit, "FuserPath"),
						IsVisibled = true,//チェックボックスの表示/非表示
										  //リンク情報を取得
						UnitGroup = UnitLink.units.Where(x => x.components.Find(y => y == unit) != null).FirstOrDefault()?.name
					};
					updates.Add(update);
				}
				UpdateDataSize = GetPeripheradSize(DataFolder);
				return updates;
			}
			catch (Exception ex)
			{
				return updates;
			}
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
			TransferCount = GetTransferFiles();
			TransferedCount = 0;
			TransferErrorCount = 0;
			TransferSuccessCount = 0;

			//long files = GetTransferFiles();

			if (TransferCount == 0)
			{
				var result = await MsgBox.Show("Info", "No target machine selected",
													  "Please select Update machine.",
													  "",
													  (int)(MsgDlgType.OK | MsgDlgType.ICON_INFO), "DataGridView");
				return;
			}

			bool ret = true;
			CancelTokenSrc = new CancellationTokenSource();
			CancellationToken token = CancelTokenSrc.Token;

			DialogTitle = "確認";
			DialogText = $"{TransferCount}個のソフトを転送します。\n転送を開始しますか？";
			var r = await DialogHost.Show(new MyMessageBox(), "DataGridView");
			DialogHost.CloseDialogCommand.Execute(null, null);

			if (r == null || r.ToString() == "CANCEL")
			{
				return;
			}

			//転送操作のボタンの有効/無効を設定
			CanTransferStartFlag.Value = false;
			//CanTransferStopFlag.Value = true;
			CanAppQuitFlag.Value = false;

			string? backupPath = Options.GetOption("--backup");

			if (backupPath != null)
			{
				//ret = await BackupUnitData(backupPath);
			}

			if (ret == true)
			{
				Progress = await Metro.ShowProgressAsync("Unit Soft Transfer...", "");
				//Progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・
				Progress.SetCancelable(true); // キャンセルボタンを表示する
				Progress.SetProgress(0);
				Progress.Minimum = 0;
				Progress.Maximum = 1000;

				//別スレッドで転送処理を実行
				IsTransfering = true;
				Task task = Task.Run(() => { Task<bool> task1 = TransferExecute(token); });

				bool isCancel = false;
				while (true)
				{
					if (IsTransfering == false)
					{
						//転送が終了した
						break;
					}
					if (Progress.IsCanceled == true)
					{
						await Progress.CloseAsync();

						CanTransferFlag.Value = false;
						isCancel = await StopTransferExecute();
						if (isCancel == true)
						{
							break;
						}
						// 再開
						Progress = await Metro.ShowProgressAsync("Unit Soft Transfer...", "");
						//Progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・
						Progress.SetCancelable(true); // キャンセルボタンを表示する

						CanTransferFlag.Value = true;
					}
					await Task.Delay(100);  //Delay することで、プログレスウインドウの表示が更新される
				}
				if (isCancel == false)
				{
					await Progress.CloseAsync();
				}
			}

			CanTransferStartFlag.Value = true;
			//CanTransferStopFlag.Value = false;
			CanAppQuitFlag.Value = true;

			//転送結果ウインドウを表示する
			//ShowTransferResult();
		}

		/// <summary>
		/// 転送結果ウインドウを表示する
		/// </summary>
		/// <returns></returns>
		public bool ShowTransferResult()
		{
			string[] wordList = LogData.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

			return true;
		}

		/// <summary>
		///  転送中止
		/// </summary>
		private async Task<bool> StopTransferExecute()
		{
			AddLog("[Transfer] Command=Stop");

			DialogTitle = "確認";
			DialogText = "転送を中止しますか？";

			var r = await DialogHost.Show(new MyMessageBox(), "DataGridView");
			DialogHost.CloseDialogCommand.Execute(null, null);

			if (r == null || r.ToString() == "CANCEL")
			{
				return false;
			}
			//CancellationToken.ThrowIfCancellationRequested();

			if (CancelTokenSrc != null)
			{
				CancelTokenSrc.Cancel();
			}
			return true;
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
					if (machine.Children == null || machine.Name == null || machine.IsSelected.Value == false)
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
							var r = await DialogHost.Show(new MyMessageBox(), "DataGridView");
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

				// ディレクトリかどうか判定する(※ディレクトリの場合、FileAttributes.Directorr | FileAttributes.Archive になる)
				if ( (System.IO.File.GetAttributes(pathWin) & FileAttributes.Directory) == FileAttributes.Directory)
				{
					string[] fi = Directory.GetFiles(pathWin, "*", SearchOption.AllDirectories);
					files += fi.Length;
					Debug.WriteLine($"File:{pathWin}={fi.Length}");
				}
				/*
				else if (Path.GetExtension(pathWin) == ".inf")
				{
					// .inf の場合はディレクトリとして扱う(ただし、ディレクトリ名は .inf ファイルを除いたもの)
					string dir = Path.GetDirectoryName(pathWin) ?? "";
					string[] fi = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
					files += fi.Length;
					Debug.WriteLine($"File:{pathWin}={fi.Length}");
				}
				*/
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
		public long GetTransferFiles()
		{
			long files = 0;

			//対象のユニット数をカウント
			foreach (var item in TreeViewItems)
			{
				if (item.IsSelected.Value == false)
				{
					continue;
				}
				foreach (var machine in item.Children)
				{
					if (machine.IsSelected.Value == false)
					{
						continue;
					}
					foreach (var module in machine.Children)
					{
						if (module.IsSelected.Value == false)
						{
							continue;
						}
						foreach (var unit in module.UnitVersions)
						{
							if (unit.IsSelected.Value == true)
							{
								files += GetDirectoryFiles( DataFolder + unit.Path );
							}
						}
					}
				}
			}
			return files;
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
			List<(string unit, string path)> unitFolders = CreateUpdateFolderList();
			List<string> folders = unitFolders.Select(x => x.path).ToList();

			//対象ファイルをすべて LCU にアップロード
			AddLog($"[Transfer] {lcu.Name}=UploadFiles Start");

			// デバッグ環境でない場合
			//ret = await UploadFilesWithFolder(lcu, folders, lcuRoot, DataFolder,token);

			//デバッグ環境の場合、FTPが一つだけなので、LCU_n のフォルダで対応する
			ret = await UploadFilesWithFolder(lcu, folders, $"LCU_{lcu.LcuId}" + lcuRoot, DataFolder, token);
			if (ret == false)
			{
				AddLog($"[Transfer] {lcu.Name}=UploadFilesWithFolder Error");
				return false;
			}
			AddLog($"[Transfer] {lcu.Name}=UploadFiles End");

			return ret;
		}

		/// <summary>
		/// データ転送
		/// </summary>
		/// <returns></returns>
		public async Task<bool> TransferExecute(CancellationToken token)
		{
			//対象のLCUに対して転送を実行
			//   「選択」されている LCU のリストを取得
			var lcuList = TreeViewItems.Where(x => x.IsSelected.Value != false).ToList();

			foreach (var x in lcuList)
			{
				bool ret = await UploadUnitFilesToLcu(x, token);
			}

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
		///  バージョン情報にあるデータを装置から取得する
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="machine"></param>
		/// <param name="module"></param>
		/// <returns></returns>
		public async Task<bool> DownloadModuleFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string backupPath, CancellationToken token)
		{
			bool ret;

			if (UpdateInfos == null)
			{
				return false;
			}

			//装置から取得したUpdateCommon.inf のパスのみを取り出してリスト化(重複を削除)
			//    ※パスはファイル名を含むので、ファイル名を削除してフォルダのみを取り出す
			//      WebAPIに装置からフォルダを含む一覧を取得するコマンドがないため、UpdateCommon.inf のパスを利用する
			//List<string> folders = module.UnitVersions.Select(x => Path.GetDirectoryName(x.Path)).ToList().Distinct().ToList();
			List<string> folders = module.UpdateFiles().Select(x => x).ToList().Distinct().ToList();

			//LCU上にフォルダを作成する(装置からファイルを取得するフォルダ,装置と同じフォルダ構造をLCUのFTP下に作る)
			string lcuRoot = $"LCU_{module.Pos}/MCFiles";
			//string lcuRoot = "/MCFiles";
			ret = lcu.LcuCtrl.CreateFtpFolders(folders, lcuRoot);
			if (ret == false)
			{
				AddLog($"[DownLoad] {lcu.Name}=CreateFtpFolders Error");
				return ret;
			}

			try
			{
				AddLog($"[DownLoad] {lcu.Name}:{machine.Name}:{module.Name}=GetMcFileList");
				List<string> mcFiles = [];
				List<string> lcuFiles = [];

				string mcUser = GetMcUser();
				string mcPass = GetMcPass();
				foreach (var folder in folders)
				{
					AddLog($"[DownLoad] {lcu.Name}:{folder}=WebApi(GetMCFileList)");
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
									lcuFiles.Add(lcuRoot + file.name);
								}
							}
						}
					}
				}

				//装置からLCUにファイルを取得する
				string retMsg = await lcu.LcuCtrl.LCU_Command(GetMcFiles.Command(machine.Name, module.Pos, mcUser, mcPass, mcFiles, lcuFiles), token);

				if (retMsg == "" || retMsg == "Internal Server Error")
				{
					AddLog($"[DownLoad] {lcu.Name}:{machine.Name}:{module.Name}=WebApi(GetMCFile) Error");
					return false;
				}
				// LCU からFTPでファイルを取得 
				ret = await lcu.LcuCtrl.DownloadFiles(lcuRoot, backupPath, mcFiles, token);

				//LCU 上に作成したファイルを削除
				ret = lcu.LcuCtrl.ClearFtpFolders(lcuRoot);
			}
			catch (Exception e)
			{
				AddLog($"[DownLoad] {lcu.Name}:{machine.Name}:{module.Name}=DownloadModuleFiles Error");
				throw;
			}
			return ret;
		}

		/// <summary>
		/// 
		/// </summary>
		private async Task<bool> WaitTransferState(CancellationToken? token)
		{
			if (CanTransferFlag.Value == false)
			{
				while (true)
				{
					if (CanTransferFlag.Value == true)
					{
						break;
					}
					if (token != null)
					{
						if (token.Value.IsCancellationRequested == true)
						{
							return false;
						}
					}
					await Task.Delay(500);
				}
			}
			return true;
		}

		private List<string> CreateUpdateFileList(List<string> folders)
		{
			List<string> files = [];

			foreach (var folder in folders)
			{
				string[] fs = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
				files.AddRange(fs);
			}

			return files;
		}

		/// <summary>
		/// アップデート対象のフォルダを取得する
		/// </summary>
		/// <returns></returns>
		private List<(string, string)> CreateUpdateFolderList()
		{
			List<(string unit, string path)> folders = [];

			if (UpdateInfos == null)
			{
				//例外除け
				return folders;
			}

			foreach (var item in UpdateInfos)
			{
				string localPath = DataFolder + item.Path;
				if (Path.Exists(localPath) == false)
				{
					//存在しなければ次へ
					continue;
				}
				if ( (System.IO.File.GetAttributes(localPath) & FileAttributes.Directory) == FileAttributes.Directory )
				{
					folders.Add((item.Name, localPath));
				}
				else
				{
					string? path = Path.GetDirectoryName(localPath);
					if (path != null)
					{
						folders.Add((item.Name, path));
					}
				}
			}
			//重複を削除
			folders = folders.Distinct().ToList();

			return folders;
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
			long transferedSize = 0;

			// FTP 総バイト数を計算
			//   送信するアップデートデータ(フォルダ)のサイズ * 選択されているLCUの数
			long ftpSize = folders.Select(x => GetDirectorySize(new DirectoryInfo(x))).Sum() * TreeViewItems.Count(x => x.IsSelected.Value != false);

			var ftpClient = new FtpClient(lcu.LcuCtrl.Name.Split(":")[0], user, password);

			ftpClient.AutoConnect();
			foreach (string folder in folders)
			{
				// 元フォルダからLCUフォルダへ変換する
				string lcuPath = lcuRoot + folder[srcRoot.Length..];
				try
				{
					// folder 下の全ファイルを lcuPath フォルダへアップロードする(フォルダがない場合は作成してくれる)
					ftpClient.UploadDirectory(folder, lcuPath, FtpFolderSyncMode.Update, FtpRemoteExists.Overwrite);

					long bytes = GetDirectorySize(new DirectoryInfo(folder));

					transferedSize += bytes;

					Progress?.SetMessage($"Transfering {lcu.Name}:{Path.GetFileName(folder)}");

					// 進捗%の計算
					//  PC->LCU で 50% とする(LCU->MC で 50%)　※プログレスバーのレンジを0-1000としているため 50% は 500
					long rate = (long)(((double)transferedSize / (double)ftpSize) * 1000.0 / 2.0);
					Debug.WriteLine($"TransferedFtpSize={transferedSize}, FtpDataSize={ftpSize}, rate={rate}");

					Progress?.SetProgress( ((double)transferedSize / (double)ftpSize) * 1000.0 / 2.0);

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
			ftpClient.Disconnect();
			return ret;
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

			if (module.UnitVersions == null)
			{
				return false;
			}
			// (装置から)UpdateCommon.inf ファイルを取得
			string tmp = Path.GetTempPath();
			ret = await GetMcPeripheralFile(lcu, machine, module, Define.UPDATE_INFO_FILE, tmp);

			// UpdateCommon.inf ファイルを読み込んで string[] としておく(各行がstring)
			string[] lines = System.IO.File.ReadAllLines(tmp + Define.UPDATE_INFO_FILE, Encoding.GetEncoding(Define.TXT_ENCODING));

			// UpdateCommon.inf の Path のフォルダにファイルを転送する
			//   ※ UpdateCommon.inf に記載されているデータは存在するものとする
			//       指定されたフォルダにあるUpdateCommon.inf を読み込んでいるので
			//
			//バージョン情報からパスのみを取り出してリスト化
			List<string> mcFiles = module.UnitVersions.Select(x => x.Path).ToList();

			//(転送データの)バージョン情報からパスのみを取り出してリスト化
			List<(string unit, string path)> unitFolders = CreateUpdateFolderList();

			//LCUに転送したファイルを装置に送信する
			string mcUser = GetMcUser();
			string mcPass = GetMcPass();
			foreach (var (unit, folder) in unitFolders)
			{
				if (module.UnitVersions.First(x => x.Name == unit).IsSelected.Value == false)
				{
					//選択されていないユニットは転送しない
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}:{unit}=Skip");
					continue;
				}

				string[] fs = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

				// //Fuji 以降を取り出してリスト化(装置に送るファイル名)
				List<string> mc = fs.Select(x => x[DataFolder.Length..]).ToList();

				// LCU にアップロードされたファイル名をリスト化
				List<string> lc = mc.Select(x => $"{lcuRoot}" + x).ToList();

				//転送が完了したので、転送中止ボタンが押されていないか確認(フォルダ単位)
				bool bt = await WaitTransferState(token);
				if (bt == false)
				{
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=Transfer Stop");
					ret = false;
					break;
				}

				Progress?.SetMessage($"Transfering {unit}");

				string retMsg = await lcu.LcuCtrl.LCU_Command(PostMcFile.Command(machine.Name, module.Pos, mcUser, mcPass, mc, lc), token);

				//FTPでアクセスできない場合などのエラー
				if (retMsg == "" || retMsg == "Internal Server Error")
				{
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=WebAPI(PostMcFile) Error");
					AddLog($"[Transfer] {module.Name};{unit}=NG");
					TransferErrorCount++;
					ret = false;
					break;
				}
				// エラーメッセージがある場合はログに出力(デバッグ用、FTPでファイル転送に失敗した場合など)
				var data = JsonSerializer.Deserialize<LcuErrMsg>(retMsg);
				if (data != null && data.errorMsg != "")
				{
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=WebAPI(PostMcFile) Error");
					AddLog($"[ERROR] Status:{data.errorMsg} Msg:{data.errorStatus}");
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{unit}=NG");
					ret = false;
					TransferErrorCount++;
					break;
				}

				AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{unit}=OK");
				TransferSuccessCount++;
				TransferedCount += mc.Count;

				Debug.WriteLine($"Transfered={TransferedCount} cnt={mc.Count}");
				double r = 500.0 + ((double)TransferedCount / (double)TransferCount) * 1000.0 / 2.0;
				Debug.WriteLine($"Progress={r}");
				if( r > 1000.0)
				{
					//最大値を超えてセットすると例外が発生するので
					r = 1000.0;
				}
				Progress?.SetProgress(r);

				//転送が完了したのでUpdateを「済」にする
				UnitVersion unitVersion = module.UnitVersions.First(x => x.Name == unit);
				unitVersion.IsUpdated.Value = true;

				//対象となるユニットのバージョン情報(string)を更新
				ModifyUpdateInfo(lines, unitVersion.Name, unitVersion.NewVersion);
			}

			if (TransferSuccessCount != 0)
			{
				DateTime dt = DateTime.Now;
				Array.Resize(ref lines, lines.Length + 1);
				lines[^1] = $";UpdateCommon.inf({dt:yyyy/MM/dd HH:mm:ss})";
				StringsToFile(lines, tmp + Define.UPDATE_INFO_FILE);
				ret = await PutMcPeripheralFile(lcu, machine, module, Define.UPDATE_INFO_FILE, tmp);
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
		private static async Task<bool> GetMcPeripheralFile(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string peripheralFile, string targetPath)
		{
			string mcFile = Define.MC_PERIPHERAL_PATH + peripheralFile;
			string lcuFile = $"/MCFiles/" + peripheralFile;

			//装置からファイルを取得
			return await lcu.LcuCtrl.GetMachineFile(machine.Name, module.Pos, mcFile, lcuFile, targetPath);
		}

		/// <summary>
		/// モジュールにファイルを転送する
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="machine"></param>
		/// <param name="module"></param>
		/// <param name="peripheralFile"></param>
		/// <param name="targetPath"></param>
		/// <returns></returns>
		private static async Task<bool> PutMcPeripheralFile(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string peripheralFile, string targetPath)
		{
			string mcFile = Define.MC_PERIPHERAL_PATH + peripheralFile;
			string lcuFile = $"/MCFiles/" + peripheralFile;
			//装置へファイルを転送
			return await lcu.LcuCtrl.PutMachineFile(machine.Name, module.Pos, mcFile, lcuFile, targetPath);
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
			StreamWriter outFile = new(path, false, Encoding.GetEncoding(Define.TXT_ENCODING));
			foreach (var line in lines)
			{
				outFile.WriteLine(line);
			}
			outFile.Close();
		}

		/// <summary>
		/// UpdateCommon.inf をマージして更新する
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="machine"></param>
		/// <param name="module"></param>
		/// <returns></returns>
		private async Task<bool> MergeUpdateCommon(LcuInfo lcu, MachineInfo machine, ModuleInfo module)
		{
			bool ret;
			//string tmpDir = Path.GetTempPath();
			string tmpDir = @"C:\Users\ka.makihara\temp\";

			//装置・モジュールから UpdateCommon.inf を取得する
			string mcFile = Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
			string lcuFile = $"LCU_{module.Pos}/MCFiles" + Define.UPDATE_INFO_FILE;
			ret = await lcu.LcuCtrl.GetMachineFile(machine.Name, module.Pos, mcFile, lcuFile, tmpDir);

			//ret = await GetMcPeripheralFile(lcu, machine, module, Define.UPDATE_INFO_FILE, tmpDir);

			if (ret == false)
			{
				AddLog($"{lcu.Name}::{machine.Name}::{module.Name}={mcFile} Get error");
				return ret;
			}

			// UpdateCommon.inf を読み込む
			string[] lines;
			string path = tmpDir + Define.UPDATE_INFO_FILE;
			try
			{
				lines = System.IO.File.ReadAllLines(path, Encoding.GetEncoding(Define.TXT_ENCODING));
			}
			catch (Exception e)
			{
				AddLog($"{lcu.Name};{machine.Name};{module.Name}={path} Read error");
				return false;
			}
			StreamWriter outFile = new StreamWriter(path + ".new", false, Encoding.GetEncoding(Define.TXT_ENCODING));

			string sectionName = "";
			string regxSection = @"^\[[0-9a-zA-Z|_]+\]$";
			string regxSectionName = @"[0-9a-zA-Z|_]+";
			foreach (var line in lines)
			{
				if (line.StartsWith("Version=") == true)
				{
					//バージョン行
					string ver = line.Split('=')[1];
					try
					{
						var item = module.UnitVersions.Where(x => x.Name == sectionName).First();
						if (item != null)
						{
							//バージョンアップ対象のユニット(オプションによっては、同バージョンでも含まれる)
							if (item.IsSelected.Value == true)
							{
								//バージョンアップが選択されたユニット
								var up = UpdateInfos.Where(x => x.Name == sectionName).First();
								if (up != null && up.Version != null)
								{
									ver = up.Version;
								}
							}
						}
						outFile.WriteLine($"Version={ver}");
					}
					catch (InvalidOperationException ex)
					{
						// UnitVersions に該当するユニットがない(バージョンが同一で除外(オプション))
						outFile.WriteLine(line);
					}
				}
				else if (Regex.IsMatch(line, regxSection) == true)
				{
					//セクション行はそのまま出力
					outFile.WriteLine(line);
					sectionName = Regex.Match(line, regxSectionName).Value;  //[]の中身を取り出す
					continue;
				}
				else
				{
					//その他の行はそのまま出力
					outFile.WriteLine(line);
					continue;
				}
			}
			outFile.Close();

			// UpdateCommon.inf を削除
			System.IO.File.Delete(path);

			return ret;
		}

		/// <summary>
		///  ツリーの選択状態が変更されたときの処理
		/// </summary>
		/// <param name="e"></param>
		public void TreeViewSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
		{
			CheckableItem? item = e.NewValue as CheckableItem;

			if (item == null)
			{
				return;
			}
			SelectedItem = item;
			//AddLog($"TreeViewSelectedItemChanged={item.Name}:{item.ItemType}");

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
							if (lcu.Children == null || lcu.Name != lcuName)
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
							viewModeTable.Add($"ModuleView_{item.Name}", new ModuleViewModel(module.UnitVersions, UpdateInfos));
						}
					}
					ActiveView = viewModeTable[$"ModuleView_{item.Name}"];
					break;
			}
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
			IniFileParser? parser = module.UpdateInfo;  //装置から取得した UpdateCommon.inf
			long fileSz = -1;

			if (parser == null)
			{
				return;// null;
			}

			string path = parser.GetValue(unit, "Path");
			string binPath = DataFolder + path;

			if (System.IO.File.Exists(binPath) == true)
			{
				if (System.IO.File.GetAttributes(binPath) == FileAttributes.Directory)
				{
					fileSz = GetDirectorySize( new DirectoryInfo(binPath));
				}
				else if (Path.GetExtension(binPath) == ".inf")
				{
					// .inf の場合はディレクトリとして扱う(ただし、ディレクトリ名は .inf ファイルを除いたもの)
					path = Path.GetDirectoryName(path) ?? "";
					fileSz = GetDirectorySize( new DirectoryInfo( Path.GetDirectoryName(binPath) ));
				}
				else
				{
					FileInfo fi = new(binPath);
					fileSz = fi.Length;
				}
			}

			UnitVersion version = new()
			{
				Name = unit,
				Attribute = int.Parse(parser.GetValue(unit, "Attribute")),
				Path = path ?? "",
				FuserPath = parser.GetValue(unit, "FuserPath"),
				CurVersion = parser.GetValue(unit, "Version"),
				NewVersion = newVer,
				Parent = module,
				Size = fileSz,
				UnitGroup = UnitLink.units.Where(x => x.components.Find(y => y == unit) != null).FirstOrDefault()?.name
			};
			versions.Add(version);

			//return version;
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
		public async Task<ReactiveCollection<UnitVersion>?> CreateVersionInfo(LcuInfo lcu, MachineInfo machine, ModuleInfo module, CancellationToken token)
		{
			bool ret;

			if (lcu.LcuCtrl == null)
			{
				return null;
			}

			Progress?.SetMessage($"{lcu.LcuCtrl.Name};{machine.Name};{module.Name} Get Update Info");

			// UpdateCommon.inf をLCUを経由して取得する
			if (module.UpdateInfo == null)
			{
				string tmpDir = Path.GetTempPath();

				string mcFile = Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;
				//string lcuFile = $"LCU_{module.Pos}/MCFiles/" + Define.UPDATE_INFO_FILE;
				string lcuFile = "/MCFiles/" + Define.UPDATE_INFO_FILE;

				//装置から UpdateCommon.inf を取得(lcuFile)してテンポラリに保存
				ret = await lcu.LcuCtrl.GetMachineFile(machine.Name, module.Pos, mcFile, lcuFile, tmpDir, token);

				if (ret == false)
				{
					AddLog($"{lcu.LcuCtrl.Name};{machine.Name};{module.Pos}={mcFile} Get error");
					return null;
				}
				// IniFilePaeser に読み込む
				module.UpdateInfo = new(tmpDir + Define.UPDATE_INFO_FILE);

				//テンポラリに生成したファイルを削除
				System.IO.File.Delete(tmpDir + Define.UPDATE_INFO_FILE);
			}
			IniFileParser parser = module.UpdateInfo;

			List<string> sec = parser.SectionCount();  //[Unit]セクションのリストを取得

			//バージョン情報を生成
			ReactiveCollection<UnitVersion> versions = [];

			// ユニット一覧作成時のオプション
			//   --matchUnit が指定されている場合は、UpdateCommon.inf に記載されているユニットのみを対象とする
			//   --diffOnly が指定されている場合は、バージョンが同一のユニットは対象外とする
			bool match = Options.GetOptionBool("--matchUnit", false);
			bool only = Options.GetOptionBool("--diffOnly", false);

			foreach (var unit in sec)
			{
				//バージョンアップの中に該当するユニットのインデックスを取得(ない場合は -1)
				int idx = -1;
				if (UpdateInfos != null)
				{
					idx = UpdateInfos.Select((v, i) => new { Value = v, Index = i }).FirstOrDefault(x => x.Value.Name == unit)?.Index ?? -1;
				}

				if (idx != -1 && UpdateInfos != null)
				{
					if (only == true && parser.GetValue(unit, "Version") == UpdateInfos[idx].Version)
					{
						//同じバージョンは対象外とするオプション指定の場合
						continue;
					}

					CreateUnitVersionList(versions, unit, UpdateInfos[idx].Version, module);
					/*
										if(version == null)
										{
											continue;
										}
										progCtrl?.SetMessage($"{lcu.LcuCtrl.Name};{machine.Name};{module.Name};{unit}={version.CurVersion}");
										versions.Add(version);
					*/
				}
				else
				{
					//アップデートデータ内に対象ユニットが無い場合
					if (match == false)
					{
						//存在するユニット以外も対象とする場合(--matchUnit==false or --matchUnit 未定義)
						CreateUnitVersionList(versions, unit, "N/A", module);
						/*
												if(version == null)
												{
													continue;
												}

												progCtrl?.SetMessage($"{lcu.LcuCtrl.Name};{machine.Name};{module.Name};{unit}={version.CurVersion}");
												versions.Add(version);
						*/
					}
				}
			}
			return versions;
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
			/*
			TreeViewItems = new ReactiveCollection<LcuInfo>
			{
				new (){ Name = "localhost:9000", ItemType=MachineType.LCU},
				//new (){ Name = "ch-lcu33",       ItemType=MachineType.LCU},
			};
			*/
			//TreeView に項目が追加されたときの処理
			TreeViewItems.ObserveAddChanged().Subscribe(x => Debug.WriteLine(x.Name));

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
				Progress?.SetMessage($"reading {lcu.LcuCtrl.Name}");
				bool ret = await UpdateLcuInfo(lcu, token);
				lcu.IsSelected.Value = ret;
			}

			// 選択されていない(LCU情報が取得できない) LCU(Line)を削除
			List<LcuInfo> tmpList = TreeViewItems.Where(x => x.IsSelected.Value == false).ToList();
			foreach (var item in tmpList)
			{
				TreeViewItems.Remove(item);
			}

			AddLog("LoadLineInfo End");
			_lineInfoLoaded = true;

			return true;
		}

		/// <summary>
		/// LCUの情報を更新する
		/// </summary>
		/// <param name="LcuInfo">LCU情報</param>
		public async Task<bool> UpdateLcuInfo(LcuInfo lcu, CancellationToken token)
		{
			bool ping = await CheckComputer(lcu.LcuCtrl.Name.Split(':')[0], 3);
			if (ping == false)
			{
				AddLog($"[ERROR] {lcu.Name}=Access Fail");
				return false;
			}

			if (lcu.LcuCtrl.FtpUser == null)
			{
				// FTPアカウント情報を取得
				var str = await lcu.LcuCtrl.LCU_Command(FtpData.Command(), token);
				FtpData? data = FtpData.FromJson(str);
				if (data == null)
				{
					return false;
				}
				if (data.username == null || data.password == null)
				{
					return false;
				}
				string password = FtpData.GetPasswd(data.username, data.password);

				// FTPアカウント情報を設定
				lcu.FtpUser = data.username;
				lcu.FtpPassword = password;
				lcu.LcuCtrl.FtpUser = lcu.FtpUser;
				lcu.LcuCtrl.FtpPassword = lcu.FtpPassword;

				// LCU バージョン取得
				str = await lcu.LcuCtrl.LCU_Command(LcuVersion.Command(), token);
				IList<LcuVersion>? versionInfo = LcuVersion.FromJson(str);
				if (versionInfo == null)
				{
					AddLog($"[ERROR] {lcu.Name}=LcuVersion Get Error");
					return false;
				}
				lcu.Version = versionInfo.First(x => x.itemName == "Fuji LCU Communication Server Service").itemVersion;

				Progress?.SetMessage($"LCU:{lcu.LcuCtrl.Name}=Version({lcu.Version})");

				//ディスク情報
				lcu.DiskSpace = await LcuDiskChkCmd(lcu);

				if (lcu.DiskSpace < UpdateDataSize)
				{
					// アップデートデータを転送するだけのディスク容量がない
					lcu.IsSelected.Value = false;
					lcu.IsUpdateOk = false;
					AddLog($"[ERROR] {lcu.Name}=DiskSpace Error({lcu.DiskSpace}<{UpdateDataSize})");
				}
			}

			//装置情報が未取得の場合
			if (lcu.Children.Count == 0)
			{
				// Machine 情報を登録
				XmlSerializer serializer = new(typeof(LineInfo));
				string response = await lcu.LcuCtrl.LCU_HttpGet("lines");

				if (response.Contains("errorCode"))
				{
					AddLog($"[ERROR] {lcu.Name};get lines error");
					return false;
				}

				LineInfo? lineInfo = (LineInfo)serializer.Deserialize(new StringReader(response));
				if (lineInfo == null)
				{
					return false;
				}
				if (lineInfo.Line == null)
				{
					return false;
				}
				if (lineInfo.Line.Machines == null)
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
							Progress?.SetMessage($"Module={module.DispModule}");
							baseInfo.Children.Add(moduleItem);
							machine.Children.Add(moduleItem);

							var ret = await CreateVersionInfo(lcu, machine, moduleItem, token);
							if (ret != null)
							{
								moduleItem.UnitVersions = ret;
							}
							else
							{
								moduleItem.IsSelected.Value = false;
							}
						}
					}
					lcu.Children.Add(machine);
				}
			}
			return true;
		}

		/// <summary>
		/// 転送進捗表示ダイアログを表示する
		/// </summary>
		/// <returns></returns>
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

	}
}
