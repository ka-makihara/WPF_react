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
using WpfApp1_cmd.Services;
using System.Net.Http;
using YamlDotNet.Serialization;
using System.Diagnostics.Eventing.Reader;

namespace WpfApp1_cmd.ViewModel
{
	public class MainWindowViewModel : ViewModelBase
	{
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
		private readonly WpfLcuCtrlLib.IFileTransferClient _fileTransferClient;
		private readonly WpfLcuCtrlLib.ILcuWebApiClient? _webApiClient;
		private readonly ModuleBackupService _moduleBackupService = new();
		private readonly LcuUnitFileUploadService _lcuUnitFileUploadService;
		private readonly LcuFileUploadResultPresenter _lcuFileUploadResultPresenter = new();
		private readonly ModuleUpdateService _moduleUpdateService = new(new TransferProgressCalculator());
		private readonly PostMcFileTransferService _postMcFileTransferService = new();
		private readonly PostMcFileTransferResultPresenter _postMcFileTransferResultPresenter = new();
		private readonly McAccountProvider _mcAccountProvider = new();
		private readonly NeximLcuDirectoryService _neximLcuDirectoryService =
			new(new OracleNeximLcuRepository());
		private readonly UpdateInspectionDiagnosticService _updateInspectionDiagnosticService = new();
		private readonly ModuleTransferInspectionService _moduleTransferInspectionService = new();
		private readonly BackupDataService _backupDataService = new();
		private readonly UnitVersionListService _unitVersionListService = new();
		private readonly UpdateCommonService _updateCommonService = new();
		private readonly LcuPeripheralFileDownloadService _lcuPeripheralFileDownloadService = new();
		private readonly TransferSelectionSummaryBuilder _transferSelectionSummaryBuilder = new();
		private readonly TransferSelectionTreeBuilder _transferSelectionTreeBuilder = new();
		private readonly TransferSelectionGroupBuilder _transferSelectionGroupBuilder = new();
		private readonly UpdateFolderSelectionSnapshotBuilder _updateFolderSelectionSnapshotBuilder = new();
		private readonly TransferPreflightValidator _transferPreflightValidator = new();
		private readonly TransferPreflightResultPresenter _transferPreflightResultPresenter = new();
		private readonly TransferStartConfirmationPresenter _transferStartConfirmationPresenter = new();
		private readonly TransferCompletionResultPresenter _transferCompletionResultPresenter = new();
		private readonly UnitVersionIndexLookupBuilder _unitVersionIndexLookupBuilder = new();
		private readonly SelectedModulePreparationService _selectedModulePreparationService = new();
		private readonly TreeSelectionPolicy _treeSelectionPolicy = new();
		private readonly TreeSelectionDiagnosticBuilder _treeSelectionDiagnosticBuilder = new();
		private readonly ViewCacheKeyRemovalBuilder _viewCacheKeyRemovalBuilder = new();
		private readonly LcuLineInfoService _lcuLineInfoService = new();
		private readonly LcuFolderUploadProgressPresenter _lcuFolderUploadProgressPresenter =
			new(new TransferProgressCalculator());
		private readonly UpdateDataSourceService _updateDataSourceService;
		private readonly PeripheralArchiveExtractor _peripheralArchiveExtractor;
		private readonly UpdateDataLoadService _updateDataLoadService = new();
		private readonly LcuReachabilityService _lcuReachabilityService = new();
		private readonly LcuNetworkCheckService _lcuNetworkCheckService;
		private readonly LcuLineTreeItemBuilder _lcuLineTreeItemBuilder =
			new(new LcuLineTreeService());
		private readonly LcuLineTreeService _lcuLineTreeService = new();
		private readonly LineInfoLoadWaiter _lineInfoLoadWaiter = new();
		private readonly TransferExecutionService _transferExecutionService = new();
		private readonly TransferWorkBuilder _transferWorkBuilder = new();
		private readonly TransferPauseService _transferPauseService = new();
		private readonly OperationProgressMonitor _operationProgressMonitor = new();

		public MetroWindow Metro { get; set; } = Application.Current.MainWindow as MetroWindow;

		public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();
		public CancellationToken Token => Cts.Token;

		public ReactiveCommand WindowLoadedCommand { get; } = new ReactiveCommand();
		public ReactiveCommand<System.ComponentModel.CancelEventArgs> WindowClosingCommand { get; } = new();
		private bool _isShutdownConfirmed;

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
		public ICommand ClearLogCommand { get; }
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

			Debug.WriteLine(str);
			if (LogWriter != null)
			{
				LogWriter.WriteLine(str);
			}
		}

		private void AddCommunicationLog(string message)
		{
			string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [COMM] {message}";
			Application.Current?.Dispatcher.BeginInvoke(
				new Action(() => AddLog(entry)));
		}

		private static void CloseDialog(string identifier)
		{
			if (DialogHost.IsDialogOpen(identifier))
			{
				DialogHost.Close(identifier);
			}
		}

		// ツリービューのアイテム
		public StatusBarViewModel StatusBar { get; } = new();
		private ReactiveCollection<LcuInfo> treeViewItems = [];
		public ReactiveCollection<LcuInfo> TreeViewItems
		{
			get => treeViewItems;
			set
			{
				treeViewItems = value;
				StatusBar.SetItems(value);
				OnPropertyChanged();
			}
		}

		// ツリービューの選択項目に対応するビューモデル
		private Dictionary<string, ViewModelBase> viewModeTable;
		public Dictionary<string, ViewModelBase> ViewModeTable { get => viewModeTable; set => viewModeTable = value; }

		// ツリーの選択項目
		private CheckableItem? selectedItem;
		public CheckableItem? SelectedItem
		{
			get => selectedItem;
			set
			{
				selectedItem = value;
				StatusBar.SetSelection(value);
			}
		}

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
		private string DataFolder
		{
			get => StatusBar.DataFolder;
			set => StatusBar.DataFolder = value;
		}
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
		/// アプリ起動時のログを出力する
		/// </summary>
		private async Task StartupLogAsync()
		{
			string resultPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\UnitTransferResult";
			DateTime dt = DateTime.Now;
			string logPath = $"{resultPath}\\Update_{dt.ToString("yyyyMMdd")}.txt";
			LogWriter = await Task.Run(() =>
			{
				Directory.CreateDirectory(resultPath);
				return new StreamWriter(logPath);
			});

			LogWriter.WriteLine("Start Application");
		}

		/// <summary>
		/// 起動時、Windowsのロード後に呼ばれる
		/// </summary>
		private async void WindowLoadedExec()
		{
			bool bRet;

			//ログファイル開始
			await StartupLogAsync();

			// Config.json の読み込み(ユニットのリンク情報、デフォルト)
			await ReadConfigAsync();

			//アップデートデータの読み込み
			await LoadUpdateInfoAsync();

			//ライン情報の読み込み
			if( ErrorInfo.ErrCode == ErrorCode.OK)
			{
				bool lineInfoLoaded = await LoadLineInfo();
				if (!lineInfoLoaded)
				{
					CanTransferStartFlag.Value = false;
				}
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
		private async Task<bool> LoadLineInfo()
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadLineInformation);
			using CancellationTokenSource loadCancellation = new();
			CancelTokenSrc = loadCancellation;
			CancellationToken token = loadCancellation.Token;
			ProgressDialogController? progress = null;

			try
			{
				progress = await Metro.ShowProgressAsync(Resource.ReadLineInformation, "");
				Progress = progress;
				progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・
				progress.SetCancelable(true); // キャンセルボタンを表示する

				Task<bool> loadTask = LoadLineInfo(token);
				LineInfoLoadWaitResult loadResult = await _lineInfoLoadWaiter.WaitAsync(
					loadTask,
					() => progress.IsCanceled,
					() =>
					{
						loadCancellation.Cancel();
						Debug.WriteLine("---------------------- Cancel");
					});
				if (loadResult.WasCancelled || !loadResult.Succeeded)
				{
					return false;
				}
			}
			finally
			{
				try
				{
					OnPropertyChanged(nameof(TreeViewItems));
					if (progress != null)
					{
						await progress.CloseAsync();
					}
				}
				finally
				{
					if (ReferenceEquals(CancelTokenSrc, loadCancellation))
					{
						CancelTokenSrc = null;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Peripheral.bin を展開する
		/// </summary>
		public int CallDecompExe(string path)
		{
			string tmpDir = @"C:\DecompBin";// Path.GetTempPath();
			DataFolder = tmpDir;
			string applicationDirectory =
				Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
				?? throw new InvalidOperationException("The application directory could not be determined.");
			return _peripheralArchiveExtractor.Extract(path, tmpDir, applicationDirectory);
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
					LcuDiskSpaceResult diskSpace = await LcuDiskChkCmd(lcu);
					LcuDiskSpaceCheckPresentation presentation =
						LcuInformationService.CreateDiskSpacePresentation(
							diskSpace,
							lcu.Name,
							ErrorInfo.ErrCode,
							Resource.Error,
							Resource.Confirm,
							ErrorInfo.GetErrMessage());
					foreach (string message in presentation.LogMessages)
					{
						AddLog(message);
					}

					if (presentation.IsError)
					{
						await MsgBox.Show(
							presentation.DialogTitle,
							presentation.DialogMessage,
							presentation.DialogDetail,
							"",
							(int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR),
							"MainWindow");
						return;
					}

					await MsgBox.Show(
						presentation.DialogTitle,
						presentation.DialogMessage,
						presentation.DialogDetail,
						"",
						(int)(MsgDlgType.OK | MsgDlgType.ICON_INFO),
						"MainWindow");
				}
			}
		}

		/// <summary>
		///  アイテムの選択状態をデバッグ表示
		/// </summary>
		private void ShowItems()
		{
			TreeSelectionLcuInput[] lcus = new TreeSelectionLcuInput[TreeViewItems.Count];
			for (int lcuIndex = 0; lcuIndex < lcus.Length; lcuIndex++)
			{
				LcuInfo lcu = TreeViewItems[lcuIndex];
				TreeSelectionMachineInput[] machines =
					new TreeSelectionMachineInput[lcu.Children.Count];
				for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
				{
					MachineInfo machine = lcu.Children[machineIndex];
					TreeSelectionModuleInput[] modules =
						new TreeSelectionModuleInput[machine.Children.Count];
					for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
					{
						ModuleInfo module = machine.Children[moduleIndex];
						TreeSelectionUnitInput[] units =
							new TreeSelectionUnitInput[module.UnitVersions.Count];
						for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
						{
							UnitVersion unit = module.UnitVersions[unitIndex];
							units[unitIndex] = new(unit.Name, unit.IsSelected.Value);
						}
						modules[moduleIndex] = new(
							module.Name,
							module.IsSelected.Value,
							units);
					}
					machines[machineIndex] = new(
						machine.Name,
						machine.IsSelected.Value,
						modules);
				}
				lcus[lcuIndex] = new(lcu.Name, lcu.IsSelected.Value, machines);
			}

			foreach (string message in _treeSelectionDiagnosticBuilder.Build(lcus))
			{
				Debug.WriteLine(message);
			}
		}

		/// <summary>
		///  ユニットの連携情報を読み込む(デフォルトはexeのリソースから)
		/// </summary>
		private static async Task ReadConfigAsync()
		{
			Config.ReadConfig();

			// オプションでユニットリンクファイルが指定されている場合は、読み込んで更新する
			string path = ArgOptions.GetOption("--config", "");
			if (path != "")
			{
				UnitLinkConfigurationService service = new();
				IReadOnlyList<UnitLinkConfigurationEntry> overrides =
					await service.LoadOverridesAsync(path);
				foreach (UnitLinkConfigurationEntry overrideEntry in overrides)
				{
					UnitLink? existing = Config.units.FirstOrDefault(unit =>
						UnitLinkConfigurationMatcher.HasSameName(
							unit.name,
							overrideEntry.Name));
					if (existing != null)
					{
						existing.components = overrideEntry.Components.ToList();
						continue;
					}

					Config.units.Add(new UnitLink
					{
						name = overrideEntry.Name.ToDictionary(
							pair => pair.Key,
							pair => (object)pair.Value,
							StringComparer.Ordinal),
						components = overrideEntry.Components.ToList(),
						mode = overrideEntry.Mode,
						files = overrideEntry.Files.ToList()
					});
				}
			}

		}

		/// <summary>
		///  アップデートデータ情報の読み込み
		/// </summary>
		/// <returns></returns>
		private async Task<bool> LoadUpdateInfoAsync()
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadingUnitInfo);
			CancellationToken token = Token;
			token.ThrowIfCancellationRequested();
			string applicationDirectory =
				Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
				?? throw new InvalidOperationException("The application directory could not be determined.");
			UpdateDataSourceResult source = await _updateDataSourceService.PrepareAsync(
				ArgOptions.GetOption("--dataFolder", ""),
				applicationDirectory,
				@"C:\DecompBin",
				Define.MC_PERIPHERAL_PATH,
				Define.UPDATE_INFO_FILE,
				token);
			token.ThrowIfCancellationRequested();
			DataFolder = source.DataFolder;
			string infoFile = source.UpdateCommonPath;
			UpdateDataSourceDiagnostic archiveDiagnostic =
				_updateDataSourceService.CreateArchivePreparationDiagnostic(
					source.ArchivePreparation,
					source.ArchivePath);
			if (archiveDiagnostic.ArchiveSize is long archiveSize)
			{
				UpdateDataSize = archiveSize;
			}
			if (archiveDiagnostic.ErrorCode is ErrorCode errorCode)
			{
				ErrorInfo.ErrCode = errorCode;
			}
			if (archiveDiagnostic.LogMessage != null)
			{
				AddLog(archiveDiagnostic.LogMessage);
			}

			IsFileMenuEnabled = true;
			UpdatePathGroupDefinition[] pathGroupDefinitions =
				_updateDataLoadService.BuildPathGroupDefinitions(
					CreateUpdatePathGroupInputSnapshots(token),
					token).ToArray();
			string dataFolder = DataFolder;
			UpdateDataLoadResult loadResult = await Task.Run(
				() => _updateDataLoadService.Load(
					infoFile,
					dataFolder,
					pathGroupDefinitions,
					token),
				token);
			LogMissingUpdateCommonFiles(loadResult.MissingFiles);
			UpdateDataLoadPresentation loadPresentation =
				_updateDataLoadService.CreateLoadPresentation(
					loadResult,
					infoFile,
					ErrorInfo.GetErrMessage(ErrorCode.UPDATE_UNDEFINED_ERROR));
			if (loadPresentation.Outcome == UpdateDataLoadPresentationOutcome.ReadFailed)
			{
				AddLog(loadPresentation.LogMessage!);
				ErrorInfo.ErrCode = loadPresentation.ErrorCode!.Value;
				return false;
			}

			if (loadPresentation.Outcome == UpdateDataLoadPresentationOutcome.Loaded)
			{
				if (loadPresentation.DataSize is long dataSize)
				{
					UpdateDataSize = dataSize;
				}
				UpdateDataInfos = CreateUpdateInfoCollection(
					_updateDataLoadService.CreateUpdateInfoProjections(
						loadResult.Entries,
						token),
					token);
				UpdatePaths = CreateUnitPathCollection(
					_updateDataLoadService.CreatePathGroupProjections(
						loadResult.PathGroups,
						token),
					token);
			}

			if (loadPresentation.LogMessage != null)
			{
				AddLog(loadPresentation.LogMessage);
			}
			if (loadPresentation.ErrorCode is ErrorCode loadErrorCode)
			{
				ErrorInfo.ErrCode = loadErrorCode;
			}
			return (ErrorInfo.ErrCode == ErrorCode.OK);
		}

		/// <summary>
		/// コンストラクタ
		/// </summary>
		public MainWindowViewModel(
			IDialogCoordinator dialogCoordinator,
			WpfLcuCtrlLib.IFileTransferClient? fileTransferClient = null,
			WpfLcuCtrlLib.ILcuWebApiClient? webApiClient = null)
		{
			_dialogCoordinator = dialogCoordinator;
			ClearLogCommand = new DelegateCommand(() => LogData = string.Empty);
			_fileTransferClient = fileTransferClient
				?? new WpfLcuCtrlLib.FluentFtpFileTransferClient(AddCommunicationLog);
			_webApiClient = webApiClient;
			_lcuNetworkCheckService = new(_lcuReachabilityService);
			_peripheralArchiveExtractor = new(
				new McAccountPeripheralArchiveImageExtractor(),
				directory => Environment.CurrentDirectory = directory);
			_updateDataSourceService = new(
				Path.Exists,
				path => new FileInfo(path).Length,
				() => new DriveInfo("C:\\").AvailableFreeSpace,
				CallDecompExe);
			_lcuUnitFileUploadService = new LcuUnitFileUploadService(
				new UpdateFolderSelectionService(),
				new LcuFolderUploadService(_fileTransferClient));

			//Windowのロード時(ライン情報のよみこみ)、	クローズ時(ファイルの後始末)のコマンド
			WindowLoadedCommand.Subscribe(() => WindowLoadedExec());
			WindowClosingCommand.Subscribe(args =>
			{
				args.Cancel = !_isShutdownConfirmed;
				if (args.Cancel && QuitApplicationCommand.CanExecute())
				{
					QuitApplicationCommand.Execute();
				}
			});

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
			StartTransferCommand = CanTransferStartFlag
				.CombineLatest(StatusBar.HasTransferSelection, (canStart, hasSelection) => canStart && hasSelection)
				.ToReactiveCommandSlim();
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
		private Task<NeximLcuDirectoryResult> GetLcuTargetsFromNeximAsync(
			CancellationToken cancellationToken = default)
		{
			NeximDatabaseSettings databaseSettings =
				_mcAccountProvider.GetNeximDatabaseSettings(ArgOptions.GetOption);
			return _neximLcuDirectoryService.LoadActiveLcusAsync(
				databaseSettings,
				line => AddLog(_lcuLineInfoService
					.CreateDirectoryLineDiagnostic(line)
					.Message),
				computer => AddLog(_lcuLineInfoService
					.CreateDirectoryComputerDiagnostic(computer)
					.Message),
				cancellationToken);
		}

		/// <summary>
		///  TreeView で右クリックメニューでの実行
		/// </summary>
		/// <param name="x"></param>
		private async void TreeViewMenu(object x)
		{
			CheckableItem item = x as CheckableItem;

			Debug.WriteLine($"TreeViewMenu:{item.Name}");

			NeximLcuDirectoryResult directoryResult = await GetLcuTargetsFromNeximAsync();
			LcuLineInfoDirectoryPresentation directoryPresentation =
				_lcuLineInfoService.CreateDirectoryPresentation(directoryResult);
			foreach (LcuLineInfoDiagnostic diagnostic in directoryPresentation.Diagnostics)
			{
				AddLog(diagnostic.Message);
			}
			if (!directoryPresentation.Succeeded)
			{
				return;
			}

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
					TreeSelectionDecision machineDecision = _treeSelectionPolicy.Evaluate(
						machine.Status,
						chk);
					if (!machineDecision.PropagateToChildren)
					{
						item.IsSelected.Value = machineDecision.Selection;
						break;
					}
					UpdateChildren(machine.Children, machineDecision.Selection);
					if(ActiveView is MachineViewModel vm)
					{
						vm.IsAllSelected.Value = machineDecision.Selection;
					}
					break;
				case ModuleInfo module:
					TreeSelectionDecision moduleDecision = _treeSelectionPolicy.Evaluate(
						module.Status,
						chk);
					if (!moduleDecision.PropagateToChildren)
					{
						item.IsSelected.Value = moduleDecision.Selection;
						break;
					}
					UpdateChildren(module.UnitVersions, moduleDecision.Selection);
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
		private void ClearSelectedItems(CancellationToken cancellationToken = default)
		{
			foreach (var lcu in TreeViewItems)
			{
				cancellationToken.ThrowIfCancellationRequested();
				ClearSelection(lcu, cancellationToken);
			}
		}

		private void ClearSelection(
			CheckableItem item,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			item.IsSelected.Value = false;

			switch (item)
			{
				case LcuInfo lcu:
				foreach (var machine in lcu.Children)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ClearSelection(machine, cancellationToken);
				}
				break;
			case MachineInfo machine:
				foreach (var module in machine.Children)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ClearSelection(module, cancellationToken);
				}
				break;
			case ModuleInfo module:
				foreach (var unit in module.UnitVersions)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ClearSelection(unit, cancellationToken);
				}
				break;
			}
		}

		/// <summary>
		/// ビューをクリアする
		/// </summary>
		/// <param name="viewName"></param>
		private void ClearView(
			string viewName,
			CancellationToken cancellationToken = default)
		{
			IReadOnlyList<string> keysToRemove = _viewCacheKeyRemovalBuilder.Build(
				viewModeTable.Keys,
				viewName,
				cancellationToken);
			foreach (string key in keysToRemove)
			{
				cancellationToken.ThrowIfCancellationRequested();
				viewModeTable.Remove(key);
			}
		}

		private ReactiveCollection<MachineInfo> FindFirstLcuMachines(string? lcuName)
		{
			foreach (LcuInfo lcu in TreeViewItems)
			{
				if (lcu.Name == lcuName)
				{
					return lcu.Children;
				}
			}

			throw new InvalidOperationException("Sequence contains no matching element");
		}

		private ReactiveCollection<ModuleInfo> FindFirstMachineModules(string? machineName)
		{
			foreach (LcuInfo lcu in TreeViewItems)
			{
				foreach (MachineInfo machine in lcu.Children)
				{
					if (machine.Name == machineName)
					{
						return machine.Children;
					}
				}
			}

			throw new InvalidOperationException("Sequence contains no matching element");
		}

		/// <summary>
		///  ユニットアップデートデータのフォルダを選択して読み込む
		/// </summary>
		public async void FileOpenCmd()
		{
			try
			{
				await FileOpenCmdAsync();
			}
			catch (OperationCanceledException)
			{
				AddLog("Update data load canceled.");
			}
		}

		private async Task FileOpenCmdAsync()
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadingUnitInfo);
			CancellationToken cancellationToken = Token;
			cancellationToken.ThrowIfCancellationRequested();
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

					BackupDataTarget[] targets = new BackupDataTarget[TreeViewItems.Count];
					for (int lcuIndex = 0; lcuIndex < targets.Length; lcuIndex++)
					{
						cancellationToken.ThrowIfCancellationRequested();
						LcuInfo lcu = TreeViewItems[lcuIndex];
						BackupDataMachine[] machines = new BackupDataMachine[lcu.Children.Count];
						for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
						{
							cancellationToken.ThrowIfCancellationRequested();
							MachineInfo machineItem = lcu.Children[machineIndex];
							string[] modules = new string[machineItem.Children.Count];
							for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
							{
								cancellationToken.ThrowIfCancellationRequested();
								modules[moduleIndex] = machineItem.Children[moduleIndex].Name ?? "";
							}
							machines[machineIndex] = new BackupDataMachine(
								machineItem.Name ?? "",
								modules);
						}
						targets[lcuIndex] = new BackupDataTarget(lcu.Name ?? "", machines);
					}
					string selectedFolder = infoFilePath;
					BackupDataCandidate? candidate = await Task.Run(
						() => _backupDataService.CreateCandidate(
							selectedFolder,
							Define.UPDATE_INFO_FILE,
							Define.MC_PERIPHERAL_PATH,
							targets),
						cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();
					BackupDataCandidatePresentation candidatePresentation =
						_backupDataService.CreatePresentation(
							candidate,
							infoFilePath,
							Resource.Error,
							Resource.Info,
							ErrorInfo.GetErrMessage(ErrorCode.UPDATE_UNDEFINED_ERROR),
							ErrorInfo.GetErrMessage(ErrorCode.BACKUP_DATA_MISSMATCH));
					if (candidatePresentation.ErrorCode is ErrorCode candidateErrorCode)
					{
						if (candidatePresentation.LogMessage != null)
						{
							AddLog(candidatePresentation.LogMessage);
						}
						ErrorInfo.ErrCode = candidateErrorCode;
						if (candidatePresentation.IsError)
						{
							await MsgBox.Show(
								candidatePresentation.DialogTitle!,
								candidatePresentation.DialogMessage!,
								candidatePresentation.DialogDetail!,
								candidatePresentation.DialogInformation!,
								(int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR),
								"MainWindow");
						}
						return;
					}

					// バックアップデータかどうかをチェック(バックアップデータは一行目に情報が追加されている)
					if (candidate == null)
					{
						throw new InvalidOperationException(
							"Candidate presentation did not reject a missing UpdateCommon.inf.");
					}
					BackupDataSelectionResult backupInfo = candidate.Metadata;
					string line = backupInfo.LcuName ?? "";
					string machine = backupInfo.MachineName ?? "";
					string module = backupInfo.ModuleName ?? "";
					BackupDataTargetMatchResult? backupTarget = candidate.Target;
					if (candidatePresentation.Outcome == BackupDataCandidatePresentationOutcome.TargetMismatch)
					{
						if (candidatePresentation.LogMessage != null)
						{
							AddLog(candidatePresentation.LogMessage);
						}
						ErrorInfo.ErrCode = candidatePresentation.ErrorCode!.Value;
						await MsgBox.Show(
							candidatePresentation.DialogTitle!,
							candidatePresentation.DialogMessage!,
							candidatePresentation.DialogDetail!,
							candidatePresentation.DialogInformation!,
							(int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR),
							"MainWindow");
						return;
					}

					if (candidatePresentation.Outcome ==
						BackupDataCandidatePresentationOutcome.BackupDataConfirmationRequired)
					{
						object ret = await MsgBox.Show(
							candidatePresentation.DialogTitle!,
							candidatePresentation.DialogMessage!,
							candidatePresentation.DialogDetail!,
							candidatePresentation.DialogInformation!,
							(int)(MsgDlgType.OK_CANCEL | MsgDlgType.ICON_INFO),
							"MainWindow");
						if (!_backupDataService.ShouldLoadBackupData((string)ret))
						{
							return;
						}
					}
					UpdateCommonInfoReadResult? backupInfoResult =
						await ReadUpdateCommonAsync(
							candidate.UpdateCommonPath,
							candidate.DataFolder,
							cancellationToken);
					if (backupInfoResult == null)
					{
						await MsgBox.Show(
							Resource.Error,
							$"ErrorCode=0x{ErrorInfo.ErrCode:X}",
							ErrorInfo.GetErrMessage(),
							"UpdateCommon.inf could not be read.",
							(int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR),
							"MainWindow");
						return;
					}
					cancellationToken.ThrowIfCancellationRequested();
					ReactiveCollection<UpdateInfo> backupUpdateInfos = CreateUpdateInfoCollection(
						_updateDataLoadService.CreateUpdateInfoProjections(
							backupInfoResult.Entries,
							cancellationToken),
						cancellationToken);
					UpdateVersionViewModel updateVersionView = new(backupUpdateInfos);
					cancellationToken.ThrowIfCancellationRequested();

					// Commit the loaded update data as one non-cancellable UI-thread phase.
					infoFilePath = candidate.DataFolder;
					DataFolder = infoFilePath;
					IsBackupedData = candidate.Metadata.IsBackupData;
					UpdateDataSize = backupInfoResult.DataSize;
					UpdateDataInfos = backupUpdateInfos;
					viewModeTable["UpdateVersionView"] = updateVersionView;
					SelectedItem = null;
					ActiveView = updateVersionView;
					AddLog($"Load BackupData={infoFilePath}");

					//アップデートデータが変更されたので、 ツリーアイテムの選択をクリアする
					ClearSelectedItems();

					//モジュールビュー,ビューデータを削除
					ClearView("ModuleView");
					foreach (LcuInfo targetLcu in TreeViewItems)
					{
						foreach (MachineInfo targetMachine in targetLcu.Children)
						{
							foreach (ModuleInfo targetModule in targetMachine.Children)
							{
								targetModule.UnitVersions.Clear();
							}
						}
					}

					if (backupTarget is { Outcome: BackupDataTargetMatch.Found } resolvedTarget)
					{
						// モジュールビューを再生成する
						LcuInfo lcuInfo = TreeViewItems[resolvedTarget.LcuIndex];
						MachineInfo machineInfo = lcuInfo.Children[resolvedTarget.MachineIndex];
						ModuleInfo moduleInfo = machineInfo.Children[resolvedTarget.ModuleIndex];
						if (moduleInfo != null)
						{
							// モジュールからバージョン情報を取得して、モジュールビューを生成する
							moduleInfo.IsSelected.Value = true;

							BackupModulePreparationResult modulePreparation;
							try
							{
								modulePreparation = await _backupDataService.PrepareTargetModuleAsync(
									cancellationToken => GetModuleUpdateInfo(
										lcuInfo,
										machineInfo,
										moduleInfo,
										cancellationToken),
									cancellationToken => CreateVersionInfoAsync(
										lcuInfo,
										machineInfo,
										moduleInfo,
										cancellationToken),
									IsAdmin,
									isBackupedData => IsBackupedData = isBackupedData,
									cancellationToken);
							}
							catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
							{
								moduleInfo.IsSelected.Value = false;
								SelectedItem = null;
								ActiveView = updateVersionView;
								AddLog("Backup data load canceled.");
								return;
							}
							if (modulePreparation.Outcome == BackupModulePreparationOutcome.UpdateInfoUnavailable)
							{
								//アップデート情報の取得に失敗
								AddLog($"GetModuleUpdateInfo Fail:{lcuInfo.Name}-{machineInfo.Name}-{moduleInfo.Name}");
								ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
								await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", $"{line}:{machine}:{module}", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
								return;
							}

							if (modulePreparation.VersionInfoCreated)
							{
								string viewName = moduleInfo.GetViewName();
								viewModeTable.Add(viewName, new ModuleViewModel(moduleInfo, UpdateDataInfos));

								moduleInfo.IsSelected.Value = Utility.CheckSelectableUnitSelectionState(moduleInfo.UnitVersions);

								//該当のツリービューを展開する
								lcuInfo.IsExpanded.Value = true;
								machineInfo.IsExpanded.Value = true;
								moduleInfo.IsExpanded.Value = true;
							}
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
					ActiveView = viewModeTable["UpdateVersionView"];
				}
			}
		}

		/// <summary>
		///  LCU の接続状態を確認する
		/// </summary>
		async void LcuNetworkChkCmd()
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadLineInformation);
			CheckableItem item = SelectedItem;
			
			if( item is LcuInfo lcu)
			{
				LcuNetworkCheckResult checkResult = await _lcuNetworkCheckService.CheckAsync(
					lcu.LcuCtrl.Name.Split(':')[0],
					new LcuCtrlInformationGateway(lcu.LcuCtrl),
					message => Debug.WriteLine(message));
				LcuNetworkCheckPresentation presentation =
					LcuNetworkCheckService.CreatePresentation(
						checkResult,
						Resource.Error,
						Resource.Info,
						ErrorInfo.GetErrMessage(ErrorCode.LCU_CONNECT_ERROR),
						checkResult.Version);
				if (presentation.ErrorCode is ErrorCode errorCode)
				{
					ErrorInfo.ErrCode = errorCode;
				}

				if (presentation.ShowDialog)
				{
					object result = await MsgBox.Show(
						presentation.DialogTitle!,
						presentation.Message!,
						presentation.Detail!,
						"",
						presentation.IsError
							? (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR)
							: (int)MsgDlgType.OK,
						"MainWindow");
					if (presentation.IsError)
					{
						if ((string)result == "OK")
						{
							Debug.WriteLine("Click OK");
						}
						else
						{
							Debug.WriteLine("Click out");
						}
					}
				}
			}
		}

		/// <summary>
		/// LCU のディスク容量を確認する
		/// </summary>
		public async Task<LcuDiskSpaceResult> LcuDiskChkCmd(
			LcuInfo lcu,
			CancellationToken cancellationToken = default)
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadLineInformation);
			ArgumentNullException.ThrowIfNull(lcu);
			AddLog(LcuUpdatePreparationService.CreateDiskSpaceCheckStartedMessage(lcu.Name));
			LcuInformationService service = new(new LcuCtrlInformationGateway(lcu.LcuCtrl));
			LcuDiskSpaceResult result = await service.GetDiskSpaceAsync(cancellationToken);
			if (!result.IsAvailable)
			{
				ErrorInfo.ErrCode = ErrorCode.LCU_DISK_INFO_ERROR;
				AddLog(LcuUpdatePreparationService.CreateDiskInfoErrorMessage(lcu.Name));
			}
			return result;
		}

		/// <summary>
		/// アップデートパスのリストを作成する
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public ReactiveCollection<UnitPath> CreateUpdatePathList(
			string path,
			CancellationToken cancellationToken = default)
		{
			IReadOnlyList<UpdatePathGroup> groups = _updateDataLoadService.BuildPathGroups(
				path,
				DataFolder,
				_updateDataLoadService.BuildPathGroupDefinitions(
					CreateUpdatePathGroupInputSnapshots(cancellationToken),
					cancellationToken),
				cancellationToken);
			return CreateUnitPathCollection(
				_updateDataLoadService.CreatePathGroupProjections(
					groups,
					cancellationToken),
				cancellationToken);
		}

		private static UpdatePathGroupInput[] CreateUpdatePathGroupInputSnapshots(
			CancellationToken cancellationToken = default)
		{
			UpdatePathGroupInput[] inputs = new UpdatePathGroupInput[Config.units.Count];
			for (int index = 0; index < inputs.Length; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UnitLink link = Config.units[index];
				string[] components = new string[link.components.Count];
				for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					components[componentIndex] = link.components[componentIndex];
				}
				KeyValuePair<string, string>[] names =
					new KeyValuePair<string, string>[link.name.Count];
				int nameIndex = 0;
				foreach (KeyValuePair<string, object> entry in link.name)
				{
					cancellationToken.ThrowIfCancellationRequested();
					names[nameIndex++] = new KeyValuePair<string, string>(
						entry.Key,
						entry.Value?.ToString() ?? "");
				}
				inputs[index] = new UpdatePathGroupInput(
					components,
					names,
					link.mode);
			}

			cancellationToken.ThrowIfCancellationRequested();
			return inputs;
		}

		private static ReactiveCollection<UnitPath> CreateUnitPathCollection(
			IReadOnlyList<UpdatePathGroupProjection> groups,
			CancellationToken cancellationToken = default)
		{
			ReactiveCollection<UnitPath> unitPaths = [];
			foreach (UpdatePathGroupProjection group in groups)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UnitPath unitPath = new() { GroupName = group.GroupName };
				foreach (UpdatePathFileProjection file in group.Files)
				{
					cancellationToken.ThrowIfCancellationRequested();
					unitPath.AddUnitFile("", (
						file.Name,
						file.Path,
						file.FusePath));
				}
				if (unitPath.units?.Count > 0)
				{
					unitPaths.Add(unitPath);
				}
			}
			cancellationToken.ThrowIfCancellationRequested();
			return unitPaths;
		}

		/// <summary>
		///  UpdateCommon.inf を読み込み、画面へ反映する前の結果を返す
		/// </summary>
		/// <param name="path">UpdateCommon.inf ファイルのパス</param>
		/// <returns></returns>
		private async Task<UpdateCommonInfoReadResult?> ReadUpdateCommonAsync(
			string path,
			string dataFolder,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			IReadOnlyList<UpdatePathGroupDefinition> unitLinks =
				_updateDataLoadService.BuildPathGroupDefinitions(
					CreateUpdatePathGroupInputSnapshots(cancellationToken),
					cancellationToken);
			IReadOnlyDictionary<string, string> unitGroupsByComponent =
				_updateDataLoadService.BuildUnitGroupLookup(unitLinks, cancellationToken);

			UpdateCommonInfoReadResult result = await Task.Run(
				() => _updateDataLoadService.ReadUpdateCommon(
					path,
					dataFolder,
					unit => unitGroupsByComponent.GetValueOrDefault(unit),
					cancellationToken),
				cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (result.ErrorMessage != null)
			{
				AddLog($"Read Error:{result.ErrorMessage}");
				ErrorInfo.ErrCode = ErrorCode.UPDATE_READ_ERROR;
				return null;
			}

			LogMissingUpdateCommonFiles(result.MissingFiles);
			return result;
		}

		private void LogMissingUpdateCommonFiles(IReadOnlyList<string> missingFiles)
		{
			foreach (string filePath in missingFiles)
			{
				AddLog($"[WARNING] Update file not found (update data UpdateCommon.inf): {filePath}");
			}
		}

		private static ReactiveCollection<UpdateInfo> CreateUpdateInfoCollection(
			IReadOnlyList<UpdateInfoProjection> entries,
			CancellationToken cancellationToken = default)
		{
			ReactiveCollection<UpdateInfo> updates = [];
			foreach (UpdateInfoProjection entry in entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				updates.Add(new UpdateInfo
				{
					Name = entry.Name,
					Attribute = entry.Attribute,
					Path = entry.Path,
					Version = entry.Version,
					FuserPath = entry.FuserPath,
					IsVisibled = entry.IsVisible,
					UnitGroup = entry.UnitGroup
				});
			}

			cancellationToken.ThrowIfCancellationRequested();
			return updates;
		}

		/// <summary>
		/// ping によるネットワーク接続確認
		/// </summary>
		/// <param name="name">コンピューター名</param>
		/// <param name="count">繰り返し回数</param>
		/// <returns></returns>
		/// <summary>
		/// フォルダのサイズを取得する
		/// </summary>
		/// <param name="dirInfo">サイズを取得するフォルダ</param>
		/// <returns>フォルダのサイズ（バイト）</returns>
		public static long GetDirectorySize(DirectoryInfo dirInfo)
		{
			return UpdateDataLoadService.GetDirectorySize(dirInfo);
		}

		/// <summary>
		/// アップデートデータのサイズを取得する
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static long GetPeripheralSize(string path)
		{
			return UpdateDataLoadService.GetPeripheralSize(path);
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
			TransferSelectionLcu[] selectionTree = CreateTransferSelectionTree(Token);
			TransferSelectionGroupInput[] selectionGroupInputs =
				new TransferSelectionGroupInput[Config.units.Count];
			for (int index = 0; index < selectionGroupInputs.Length; index++)
			{
				Token.ThrowIfCancellationRequested();
				UnitLink link = Config.units[index];
				string[] components = new string[link.components.Count];
				for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
				{
					Token.ThrowIfCancellationRequested();
					components[componentIndex] = link.components[componentIndex];
				}
				selectionGroupInputs[index] = new TransferSelectionGroupInput(components, link.mode);
			}
			TransferSelectionGroup[] selectionGroups = await Task.Run(
				() => _transferSelectionGroupBuilder.Build(selectionGroupInputs, Token).ToArray(),
				Token);
			string dataFolder = DataFolder;
			TransferSelectionSummary summary = await Task.Run(
				() => _transferSelectionSummaryBuilder.Build(
					selectionTree,
					selectionGroups,
					dataFolder,
					Token),
				Token);
			TransferCount = summary.Paths.Count;
			TransferedCount = 0;
			TransferErrorCount = 0;
			TransferSuccessCount = 0;
			string? backupPath = ArgOptions.GetOption("--backup");

			foreach (string path in summary.Paths)
			{
				Debug.WriteLine($"Transfer Path={path}");
			}

			TransferPreflightOutcome preflight = _transferPreflightValidator.Validate(
				summary);
			TransferPreflightPresentation preflightPresentation =
				_transferPreflightResultPresenter.Create(
					preflight,
					Resource.Confirm,
					Resource.NoTargetMachine,
					Resource.TargetSelect);
			if (preflightPresentation.ShowDialog)
			{
				await MsgBox.Show(
					preflightPresentation.DialogTitle!,
					preflightPresentation.Message!,
					preflightPresentation.Detail!,
					"",
					(int)(MsgDlgType.OK | MsgDlgType.ICON_CAUTION),
					"MainWindow");
				return;
			}

			TransferStartConfirmationPresentation confirmation =
				_transferStartConfirmationPresenter.Create(
					summary.ModuleCount,
					Resource.Confirm,
					Resource.QuestionTransferStart);
			DialogTitle = confirmation.DialogTitle;
			DialogText = confirmation.DialogText;

			object? r = await DialogHostShowCoordinator.ShowAsync(
				"MainWindow",
				() => DialogHost.Show(new MyMessageBox(), "MainWindow"));

			if (!_transferStartConfirmationPresenter.ShouldStart(r))
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

			// --backup 指定時のみ実行し、未指定ならそのまま更新へ進む。
			BackupOutcome backupOutcome = BackupOutcome.Succeeded;
			string? backupFailureMessage = null;
			if (!string.IsNullOrWhiteSpace(backupPath))
			{
				Progress = await Metro.ShowProgressAsync(Resource.BackupUnitSoft, "");
				Progress.SetCancelable(true); // キャンセルボタンを表示する
				Progress.SetIndeterminate();// 進捗(?)をそれらしく流す・・・

				IsTransfering = true;
				//Task backUpTask = Task.Run(() => { Task<bool> task1 = BackupUnitData(backupPath, token); });
				Task<bool> backUpTask = BackupUnitData(backupPath, token);
				bool isBackupCancel = await _operationProgressMonitor.WaitAsync(
					backUpTask,
					() => IsTransfering,
					() => Progress.IsCanceled,
					async () =>
					{
						CanTransferStartFlag.Value = true;
						await Progress.CloseAsync();
					},
					StopTransferExecute,
					async () =>
					{
						Progress = await Metro.ShowProgressAsync(Resource.BackupUnitSoft, "");
						Progress.SetCancelable(true);
						Progress.SetIndeterminate();
						CanTransferStartFlag.Value = true;
					},
					() => { });
				if (isBackupCancel)
				{
					try
					{
						await backUpTask;
					}
					catch (Exception ex)
					{
						AddLog($"[BACKUP] Error while stopping: {ex.Message}");
					}
					backupOutcome = BackupOutcome.Cancelled;
					backupFailureMessage = Resource.BackupCanceled;
				}
				else
				{
					await Progress.CloseAsync();
					try
					{
						bool backupSucceeded = await backUpTask;
						bool hasBackupError = ErrorInfo.ErrCode != ErrorCode.OK;
						BackupExecutionResult backupResult = _moduleBackupService.ResolveResult(
							backupSucceeded,
							hasBackupError,
							hasBackupError ? ErrorInfo.GetErrMessage() : null,
							Resource.BackupRequiredForTransfer);
						if (backupResult.Outcome != BackupOutcome.Succeeded)
						{
							backupOutcome = backupResult.Outcome;
							backupFailureMessage = backupResult.FailureMessage;
						}
					}
					catch (Exception ex)
					{
						AddLog($"[BACKUP] Error: {ex.Message}");
						backupOutcome = BackupOutcome.Failed;
						backupFailureMessage = ex.Message;
					}
				}

				BackupResultPresentation backupPresentation = _moduleBackupService.CreateResultPresentation(
					backupOutcome,
					Resource.Error,
					Resource.BackupCanceled,
					Resource.BackupRequiredForTransfer,
					backupFailureMessage);
				if (backupPresentation.ShowDialog)
				{
					CanTransferStartFlag.Value = true;
					CanAppQuitFlag.Value = true;
					await MsgBox.Show(
						backupPresentation.DialogTitle!,
						backupPresentation.Message!,
						backupPresentation.Detail!,
						"",
						(int)(MsgDlgType.OK | (backupPresentation.IsCancelled
							? MsgDlgType.ICON_CAUTION
							: MsgDlgType.ICON_ERROR)),
						"MainWindow");
				}
			}

			Func<Task<bool>>? startUpdate = UpdateExecutionGate.CreateUpdateOperation(
				backupOutcome,
				() => TransferExecute(token));
			if (startUpdate == null)
			{
				return;
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
				Task task = startUpdate();

				bool isCancel = await _operationProgressMonitor.WaitAsync(
					task,
					() => IsTransfering,
					() => Progress.IsCanceled,
					async () =>
					{
						CanTransferStartFlag.Value = true;
						await Progress.CloseAsync();
					},
					StopTransferExecute,
					async () =>
					{
						Progress = await Metro.ShowProgressAsync(Resource.UnitSoftTransfer, "");
						Progress.SetCancelable(true);
						Progress.Minimum = 0;
						Progress.Maximum = 1000;
						CanTransferStartFlag.Value = true;
						AddLog("[Transfer] Restart");
					},
					() => AddLog("[Transfer] Cancel"));
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
			TransferCompletionResultPresentation completionPresentation =
				_transferCompletionResultPresenter.Create(
					TransferSuccessCount,
					TransferErrorCount,
					Resource.Info,
					Resource.Success,
					Resource.Fail,
					Resource.TransferInformation);
			var showResult = await MsgBox.Show(completionPresentation.DialogTitle,
									completionPresentation.SuccessMessage,
									completionPresentation.FailureMessage,
									completionPresentation.InformationMessage,
									(int)(MsgDlgType.YES_NO | MsgDlgType.ICON_INFO), "MainWindow");
			if (_transferCompletionResultPresenter.ShouldOpenResult((string)showResult))
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
			bool IsAdmin = ArgOptions.GetOption("--mode", "user") == "administrator";

			List<SelectedModulePreparationWork> moduleWork = [];
			foreach (LcuInfo lcu in TreeViewItems)
			{
				token.ThrowIfCancellationRequested();
				if (lcu.Children == null || lcu.LcuCtrl == null || lcu.IsSelected.Value == false)
				{
					continue;
				}

				foreach (MachineInfo machine in lcu.Children)
				{
					token.ThrowIfCancellationRequested();
					if (machine.Children == null
						|| machine.Name == null
						|| machine.IsSelected.Value == false)
					{
						continue;
					}

					foreach (ModuleInfo module in machine.Children)
					{
						token.ThrowIfCancellationRequested();
						if (module.IsSelected.Value == false || module.UnitVersions == null)
						{
							continue;
						}

						moduleWork.Add(new SelectedModulePreparationWork(
							lcu.Name,
							machine.Name,
							module.Name,
							module.UnitVersions.Count > 0,
							cancellationToken => GetModuleUpdateInfo(
								lcu,
								machine,
								module,
								cancellationToken),
							cancellationToken => CreateVersionInfoAsync(
								lcu,
								machine,
								module,
								cancellationToken),
							cancellationToken => CountUpdateFilesAsync(
								module.UnitVersions,
								cancellationToken),
							async cancellationToken =>
							{
								bool[] selections = new bool[module.UnitVersions.Count];
								for (int index = 0; index < selections.Length; index++)
								{
									cancellationToken.ThrowIfCancellationRequested();
									selections[index] = module.UnitVersions[index].IsSelected.Value == true;
								}
								return await Task.Run(() =>
								{
									long count = 0;
									foreach (bool isSelected in selections)
									{
										cancellationToken.ThrowIfCancellationRequested();
										if (isSelected)
										{
											count++;
										}
									}

									return count;
								}, cancellationToken);
							}));
					}
				}
			}
			token.ThrowIfCancellationRequested();
			SelectedModulePreparationWork[] modules = moduleWork.ToArray();

			SelectedModulePreparationResult result =
				await _selectedModulePreparationService.PrepareAsync(
					modules,
					IsAdmin,
					(module, selectedUnitCount) =>
						Debug.WriteLine(
							$"[LoadModulesUpdateCommon] {module.LcuName};{module.MachineName};{module.ModuleName}={selectedUnitCount}"),
					token);

			if (result.Outcome == SelectedModulePreparationOutcome.UpdateInfoUnavailable)
			{
				SelectedModulePreparationWork failed = result.FailedWork!;
				AddLog($"GetModuleUpdateInfo Fail:{failed.LcuName}-{failed.MachineName}-{failed.ModuleName}");
				ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
			}
			else if (result.Outcome == SelectedModulePreparationOutcome.VersionInfoUnavailable)
			{
				ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
			}

			return result.Outcome == SelectedModulePreparationOutcome.Ready
				? result.TransferFileCount
				: 0;
		}
		/// <summary>
		/// データ転送
		/// </summary>
		/// <returns></returns>
		public async Task<bool> TransferExecute(CancellationToken token)
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.UnitSoftTransfer);
			//FTP転送のサイズを取得
			// UpdateData の全データ× LCU の数
			//   (選択されているもの以外のデータも転送する。-> ユニット毎に選択/非選択を考慮すると手間なので)
			//      A というユニットでは未選択でも Bでは選択されている場合がある、など)
			//    ※実際のFTPサイズは　UpDateCommon.inf ファイルを除いたサイズとなるが、総サイズに対して微量なので、無視する
			int selectedLcuCount = 0;
			foreach (LcuInfo lcu in TreeViewItems)
			{
				token.ThrowIfCancellationRequested();
				if (lcu.IsSelected.Value != false)
				{
					selectedLcuCount++;
				}
			}

			string dataFolder = DataFolder;
			token.ThrowIfCancellationRequested();
			long updateDataSize = await Task.Run(
				() => UpdateDataLoadService.GetDirectorySize(
					new DirectoryInfo(dataFolder + "\\Fuji"),
					token),
				token);
			token.ThrowIfCancellationRequested();
			FtpTransferSize = _transferSelectionSummaryBuilder.CalculateFtpTransferSize(
				updateDataSize,
				selectedLcuCount);
			FtpTransferedSize = 0;

			TransferLcuNode<LcuInfo, MachineInfo, ModuleInfo>[] transferNodes =
				new TransferLcuNode<LcuInfo, MachineInfo, ModuleInfo>[TreeViewItems.Count];
			for (int lcuIndex = 0; lcuIndex < transferNodes.Length; lcuIndex++)
			{
				token.ThrowIfCancellationRequested();
				LcuInfo lcu = TreeViewItems[lcuIndex];
				TransferMachineNode<MachineInfo, ModuleInfo>[] machines =
					new TransferMachineNode<MachineInfo, ModuleInfo>[lcu.Children.Count];
				for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
				{
					token.ThrowIfCancellationRequested();
					MachineInfo machine = lcu.Children[machineIndex];
					TransferModuleNode<ModuleInfo>[] modules =
						new TransferModuleNode<ModuleInfo>[machine.Children.Count];
					for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
					{
						token.ThrowIfCancellationRequested();
						ModuleInfo module = machine.Children[moduleIndex];
						modules[moduleIndex] = new TransferModuleNode<ModuleInfo>(
							module.Name,
							module.IsSelected.Value,
							module.UnitVersions != null,
							module);
					}

					machines[machineIndex] = new TransferMachineNode<MachineInfo, ModuleInfo>(
						machine.Name,
						machine.Children != null,
						machine.IsSelected.Value,
						machine,
						modules);
				}

				transferNodes[lcuIndex] = new TransferLcuNode<LcuInfo, MachineInfo, ModuleInfo>(
					lcu.Name,
					lcu.IsSelected.Value,
					lcu.Children != null,
					lcu.LcuCtrl != null,
					lcu,
					machines);
			}
			token.ThrowIfCancellationRequested();
			IReadOnlyList<TransferLcuWork> work = await Task.Run(
				() => _transferWorkBuilder.Build(
					transferNodes,
					uploadFiles: null,
					UploadModuleFiles,
					token),
				token);

			TransferExecutionResult result = await _transferExecutionService.ExecuteAsync(
				work,
				LoadModulesUpdateCommon,
				moduleCount =>
				{
					TransferCount = moduleCount;
					TransferedCount = 0;
				},
				lcuName => AddLog($"[Transfer] {lcuName}=Skip"),
				(lcuName, machineName) => AddLog($"[Transfer] {lcuName};{machineName}=Skip"),
				(lcuName, machineName, moduleName) => AddLog($"[Transfer] {lcuName};{machineName};{moduleName}=Skip"),
				(lcuName, machineName, moduleName) => AddLog($"[Transfer] {lcuName};{machineName};{moduleName}=Start"),
				(lcuName, machineName, moduleName) => AddLog($"[Transfer] {lcuName};{machineName};{moduleName}=End"),
				token);

			IsTransfering = false;
			return result.IsSuccess;
		}

		/// <summary>
		/// 転送済みのファイルをチェックする
		/// </summary>
		/// <param name="files">転送ファイル一覧(転送先から再取得)</param>
		/// <param name="versions">アップデートデータ</param>
		private void LogTransferredFilePresence(IReadOnlyList<TransferredFilePresence> results)
		{
			IReadOnlyList<InspectionDiagnostic> diagnostics =
				_updateInspectionDiagnosticService.CreateTransferredFilePresenceDiagnostics(results);
			foreach (InspectionDiagnostic diagnostic in diagnostics)
			{
				Debug.WriteLine(diagnostic.Message);
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
			return ModuleTransferInspectionService.CompareFiles(file1, file2);
		}

		/// <summary>
		///  転送中止
		/// </summary>
		private async Task<bool> StopTransferExecute()
		{
			AddLog("[Transfer] Command=Stop");

			DialogTitle = Resource.Confirm;// "確認";
			DialogText = Resource.QuitTransferMsg;// "転送を中止しますか？";

			var r = await DialogHostShowCoordinator.ShowAsync(
				"MainWindow",
				() => DialogHost.Show(new MyMessageBox(), "MainWindow"));

			if (!_transferStartConfirmationPresenter.ShouldStopTransfer(r))
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
			using IDisposable status = StatusBar.BeginOperation(Resource.BackupUnitSoft);
			BackupSelectionLcu[] selectionTree = new BackupSelectionLcu[TreeViewItems.Count];
			for (int lcuIndex = 0; lcuIndex < selectionTree.Length; lcuIndex++)
			{
				token.ThrowIfCancellationRequested();
				LcuInfo lcu = TreeViewItems[lcuIndex];
				BackupSelectionMachine[] machines = new BackupSelectionMachine[lcu.Children.Count];
				for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
				{
					token.ThrowIfCancellationRequested();
					MachineInfo machine = lcu.Children[machineIndex];
					BackupSelectionModule[] modules = new BackupSelectionModule[machine.Children.Count];
					for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
					{
						token.ThrowIfCancellationRequested();
						ModuleInfo module = machine.Children[moduleIndex];
						modules[moduleIndex] = new BackupSelectionModule(
							module.Name,
							module.IPAddress,
							module.IsSelected.Value != false);
					}

					machines[machineIndex] = new BackupSelectionMachine(
						machine.Name,
						machine.Children != null,
						machine.IsSelected.Value != false,
						modules);
				}

				selectionTree[lcuIndex] = new BackupSelectionLcu(
					lcu.Name,
					lcu.Children != null,
					lcu.LcuCtrl != null,
					lcu.IsSelected.Value != false,
					machines);
			}
			token.ThrowIfCancellationRequested();
			IReadOnlyList<ModuleBackupSelection> selectedModules = await Task.Run(
				() => _moduleBackupService.BuildSelections(selectionTree, token),
				token);
			List<ModuleBackupWorkItem<(LcuInfo Lcu, MachineInfo Machine, ModuleInfo Module, string IPAddress)>> workItems =
				new(selectedModules.Count);
			foreach (ModuleBackupSelection selection in selectedModules)
			{
				token.ThrowIfCancellationRequested();
				LcuInfo lcu = TreeViewItems[selection.LcuIndex];
				MachineInfo machine = lcu.Children[selection.MachineIndex];
				ModuleInfo module = machine.Children[selection.ModuleIndex];
				workItems.Add(new ModuleBackupWorkItem<(LcuInfo Lcu, MachineInfo Machine, ModuleInfo Module, string IPAddress)>(
					selection.LcuName!,
					selection.MachineName,
					selection.ModuleName!,
					selection.IPAddress!,
					(lcu, machine, module, selection.IPAddress!)));
			}
			token.ThrowIfCancellationRequested();

			ModuleBackupBatchResult<(LcuInfo Lcu, MachineInfo Machine, ModuleInfo Module, string IPAddress)> result =
				await _moduleBackupService.ExecuteBatchAsync(
					workItems,
					path,
					(workItem, backupPath, cancellationToken) =>
						DownloadModuleFiles(
							workItem.Context.Lcu,
							workItem.Context.Machine,
							workItem.Context.Module,
							backupPath,
							cancellationToken),
					async (workItem, cancellationToken) =>
					{
						bool canContinue = await WaitTransferState(cancellationToken);
						if (!canContinue)
						{
							AddLog($"[Backup] {workItem.LcuName};{workItem.MachineName};{workItem.ModuleName}=Transfer Stop");
						}
						return canContinue;
					},
					workItem => AddLog($"[BACKUP] {workItem.LcuName};{workItem.MachineName};{workItem.ModuleName}=Backup Start"),
					workItem => AddLog($"[BACKUP] {workItem.LcuName};{workItem.MachineName};{workItem.ModuleName}=Backup End"),
					token);

			IsTransfering = false;
			return result.Outcome == ModuleBackupBatchOutcome.Succeeded;
		}

		/// <summary>
		/// 転送ユニット数をカウントする(進捗表示用)
		/// </summary>
		public int GetTransferUnitCount()
		{
			return _transferSelectionSummaryBuilder.CountSelectedUnits(
				CreateTransferSelectionTree());
		}

		private TransferSelectionLcu[] CreateTransferSelectionTree(
			CancellationToken cancellationToken = default)
		{
			TransferSelectionLcuInput[] lcus = new TransferSelectionLcuInput[TreeViewItems.Count];
			for (int lcuIndex = 0; lcuIndex < lcus.Length; lcuIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				LcuInfo lcu = TreeViewItems[lcuIndex];
				TransferSelectionMachineInput[] machines =
					new TransferSelectionMachineInput[lcu.Children.Count];
				for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					MachineInfo machine = lcu.Children[machineIndex];
					TransferSelectionModuleInput[] modules =
						new TransferSelectionModuleInput[machine.Children.Count];
					for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
					{
						cancellationToken.ThrowIfCancellationRequested();
						ModuleInfo module = machine.Children[moduleIndex];
						TransferSelectionUnitInput[] units =
							new TransferSelectionUnitInput[module.UnitVersions.Count];
						for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
						{
							cancellationToken.ThrowIfCancellationRequested();
							UnitVersion unit = module.UnitVersions[unitIndex];
							units[unitIndex] = new TransferSelectionUnitInput(
								unit.IsSelected.Value,
								unit.Name,
								unit.Path,
								unit.FuserPath,
								unit.IsSupported,
								unit.IsUpdateProhibited);
						}
						modules[moduleIndex] = new TransferSelectionModuleInput(
							module.IsSelected.Value,
							units);
					}
					machines[machineIndex] = new TransferSelectionMachineInput(
						machine.IsSelected.Value,
						modules);
				}
				lcus[lcuIndex] = new TransferSelectionLcuInput(
					lcu.IsSelected.Value,
					machines);
			}

			cancellationToken.ThrowIfCancellationRequested();
			return _transferSelectionTreeBuilder.Build(lcus, cancellationToken).ToArray();
		}

		/// <summary>
		/// モジュール成功後にLCUのMCFiles配下を空にする。
		/// </summary>
		/// <param name="lcu">LCU情報</param>
		/// <param name="token">キャンセルトークン</param>
		/// <returns></returns>
		private async Task<bool> ClearModuleMcFilesAsync(
			LcuInfo lcu, MachineInfo machine, ModuleInfo module,
			CancellationToken token)
		{
			AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=MCFiles cleanup Start");
			try
			{
				await new LcuFolderUploadService(_fileTransferClient).ClearModuleFilesAsync(
					lcu.LcuCtrl.Name.Split(':')[0],
					lcu.FtpUser ?? "",
					lcu.FtpPassword ?? "",
					token);
				AddLog($"[INFO] {lcu.Name};{machine.Name};{module.Name}=MCFiles cleanup completed (all contents removed)");
				return true;
			}
			catch (Exception exception) when (
				exception is FluentFTP.Exceptions.FtpException or
				IOException or UnauthorizedAccessException or InvalidOperationException or
				ArgumentException or System.Net.Sockets.SocketException)
			{
				ErrorInfo.ErrCode = ErrorCode.FTP_TRANSFER_ERROR;
				AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}=MCFiles cleanup failed: {exception.Message}");
				return false;
			}
		}

		/// <summary>LCUに対してユニットデータを転送する。</summary>
		public async Task<bool> UploadUnitFilesToLcu(LcuInfo lcu, CancellationToken token)
		{
			string lcuRoot = $"/MCFiles";

			UpdateFolderMachineSnapshot[] machineSnapshots = new UpdateFolderMachineSnapshot[lcu.Children.Count];

			for (int machineIndex = 0; machineIndex < machineSnapshots.Length; machineIndex++)
			{
				token.ThrowIfCancellationRequested();
				MachineInfo machine = lcu.Children[machineIndex];
				UpdateFolderModuleSnapshot[] modules =
					new UpdateFolderModuleSnapshot[machine.Children.Count];
				for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
				{
					token.ThrowIfCancellationRequested();
					ModuleInfo module = machine.Children[moduleIndex];
					UpdateFolderUnitSnapshot[] units =
						new UpdateFolderUnitSnapshot[module.UnitVersions.Count];
					for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
					{
						token.ThrowIfCancellationRequested();
						UnitVersion unit = module.UnitVersions[unitIndex];
						units[unitIndex] = new UpdateFolderUnitSnapshot(
							unit.IsSelected.Value,
							unit.Path,
							unit.FuserPath);
					}
					modules[moduleIndex] = new UpdateFolderModuleSnapshot(
						module.IsSelected.Value,
						units);
				}
				machineSnapshots[machineIndex] = new UpdateFolderMachineSnapshot(
					machine.IsSelected.Value,
					modules);
			}
			UpdateFolderFileInfo[] updateDataFileSnapshots = [];
			if (UpdateDataInfos != null)
			{
				updateDataFileSnapshots = new UpdateFolderFileInfo[UpdateDataInfos.Count];
				for (int index = 0; index < updateDataFileSnapshots.Length; index++)
				{
					token.ThrowIfCancellationRequested();
					UpdateInfo info = UpdateDataInfos[index];
					updateDataFileSnapshots[index] = new UpdateFolderFileInfo(info.Path, info.FuserPath);
				}
			}
			token.ThrowIfCancellationRequested();
			UpdateFolderSelectionSnapshot selection = await Task.Run(
				() => _updateFolderSelectionSnapshotBuilder.Build(
					machineSnapshots,
					updateDataFileSnapshots,
					token),
				token);

			//対象ファイルを LCU にアップロード
			AddLog($"[Transfer] {lcu.Name}=UploadFiles Start");
			LcuUnitFileUploadResult result = await _lcuUnitFileUploadService.UploadSelectedAsync(
				lcu.LcuCtrl.Name.Split(":")[0],
				lcu.FtpUser ?? "",
				lcu.FtpPassword ?? "",
				selection.Machines,
				selection.UpdateDataFiles,
				DataFolder,
				lcuRoot,
				progress => OnLcuFolderUploaded(lcu, progress),
				async cancellationToken =>
				{
					bool canContinue = await WaitTransferState(cancellationToken);
					if (!canContinue)
					{
						AddLog($"[Transfer] {lcu.Name}=Stop");
					}
					return canContinue;
				},
				token);
			LcuFileUploadPresentation presentation =
				_lcuFileUploadResultPresenter.Create(result, lcu.Name ?? "", token);
			foreach (string message in presentation.LogMessages)
			{
				AddLog(message);
			}
			if (presentation.Cancelled)
			{
				return false;
			}

			if (!presentation.Succeeded)
			{
				ErrorInfo.ErrCode = ErrorCode.LCU_UPLOAD_ERROR;
				await MsgBox.Show(Resource.Error, $"ErrorCode=0x{ErrorInfo.ErrCode:X}", $"{ErrorInfo.GetErrMessage()}", $"{lcu.Name}", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
				return false;
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
		public async Task<bool> DownloadModuleFiles(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string backupPath, CancellationToken token)
		{
			if (UpdateDataInfos == null)
			{
				return false;
			}

			Progress?.SetMessage($"[Backup] {lcu.Name};{machine.Name};{module.Name}");
			ModuleBackupExecutionOutcome outcome = await _moduleBackupService.ExecuteModuleAsync(
				cancellationToken => GetModuleBackupUpdateInfo(lcu, machine, module, cancellationToken),
				() =>
				{
					IniFileParser updateInfo = module.MachineUpdateInfo
						?? throw new InvalidOperationException(
							$"Machine update information is unavailable for module '{module.Name}'.");
					UpdateCommonPathEntry[] updateInfoPaths =
						updateInfo.GetPathList()
							.Select(path => new UpdateCommonPathEntry(
								path.name,
								path.path,
								path.fuserPath))
							.ToArray();
					IReadOnlyList<string> backupPaths = _moduleBackupService.BuildUpdatePaths(updateInfoPaths, token);
					return new ModuleBackupRequest(
						lcu.Name,
						machine.Name,
						module.Name,
						module.IPAddress,
						module.Pos,
						backupPath,
						backupPaths,
						$"/{Define.MC_PERIPHERAL_PATH}{Define.UPDATE_INFO_FILE}",
						"/MCFiles/",
						Define.TXT_ENCODING);
				},
				new LcuModuleBackupGateway(lcu.LcuCtrl),
				token);
			switch (outcome)
			{
				case ModuleBackupExecutionOutcome.Succeeded:
					return true;
				case ModuleBackupExecutionOutcome.UpdateInfoUnavailable:
					AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=GetModuleUpdateInfo Error");
					ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
					break;
				case ModuleBackupExecutionOutcome.FolderCreationFailed:
					AddLog($"[DownLoad] {lcu.Name}=CreateFtpFolders Error");
					ErrorInfo.ErrCode = ErrorCode.LCU_CREATE_FOLDER_ERROR;
					break;
				case ModuleBackupExecutionOutcome.DownloadFailed:
					AddLog($"[DownLoad] {lcu.Name};{machine.Name};{module.Name}=GetMachineFiles Error");
					ErrorInfo.ErrCode = ErrorCode.LCU_DOWNLOAD_ERROR;
					break;
			}
			return false;
		}

		/// <summary>
		/// プログレスダイアログ表示中のキャンセルの確認
		/// </summary>
		private Task<bool> WaitTransferState(CancellationToken token) =>
			_transferPauseService.WaitUntilResumedAsync(
				() => CanTransferStartFlag.Value,
				token);


		/// <summary>
		/// アップデート対象のファイルを取得する
		/// </summary>
		/// <returns></returns>
		private async Task<List<UpdateFileGroupResult>> CreateUpdateFileListAsync(
			ReactiveCollection<UnitVersion> unitVersions,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (UpdateDataInfos == null)
			{
				return [];
			}

			UpdateFileGroup[] groups = new UpdateFileGroup[Config.units.Count];
			for (int index = 0; index < groups.Length; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UnitLink group = Config.units[index];
				string[] names = new string[group.name.Count];
				int nameIndex = 0;
				foreach (object value in group.name.Values)
				{
					cancellationToken.ThrowIfCancellationRequested();
					names[nameIndex++] = value.ToString()!;
				}
				groups[index] = new UpdateFileGroup(
					group.GetName(),
					names,
					group.components,
					group.mode);
			}

			UpdateFileVersion[] versions = new UpdateFileVersion[unitVersions.Count];
			for (int index = 0; index < versions.Length; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UnitVersion item = unitVersions[index];
				versions[index] = new UpdateFileVersion(
					item.Name,
					item.IsSelected.Value,
					item.Path,
					item.FuserPath,
					item.UnitGroup);
			}

			UpdateFilePath[] updatePaths = new UpdateFilePath[UpdatePaths.Count];
			for (int index = 0; index < updatePaths.Length; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UnitPath path = UpdatePaths[index];
				UpdateFileEntry[]? entries = null;
				if (path.units != null)
				{
					entries = new UpdateFileEntry[path.units.Count];
					for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
					{
						cancellationToken.ThrowIfCancellationRequested();
						(string? name, string? unitPath, string? upath) unit = path.units[entryIndex];
						entries[entryIndex] = new UpdateFileEntry(unit.name, unit.unitPath, unit.upath);
					}
				}
				updatePaths[index] = new UpdateFilePath(path.GroupName, entries);
			}

			string dataFolder = DataFolder;
			return await Task.Run(
					() => UpdateFileListBuilder.Create(
						groups,
						versions,
						updatePaths,
						dataFolder,
						cancellationToken),
					cancellationToken);
		}

		private async Task<int> CountUpdateFilesAsync(
			ReactiveCollection<UnitVersion> unitVersions,
			CancellationToken cancellationToken)
		{
			List<UpdateFileGroupResult> groups =
					await CreateUpdateFileListAsync(unitVersions, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			return await Task.Run(
					() => UpdateFileListBuilder.CountFiles(groups, cancellationToken),
					cancellationToken);
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
		public bool CheckTransferedFiles(
			LcuInfo lcu,
			MachineInfo machine,
			ModuleInfo module,
			UnitPath fileGroup,
			List<string> mc,
			CancellationToken cancellationToken = default)
		{
			ModuleInspectionUnit[] units = new ModuleInspectionUnit[fileGroup.units.Count];
			for (int index = 0; index < units.Length; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				(string? name, string? path, string? upath) unit = fileGroup.units[index];
				units[index] = new ModuleInspectionUnit(unit.name!, unit.path, unit.upath);
			}
			IReadOnlyList<ModuleFileInspectionResult> results = _moduleTransferInspectionService.InspectFiles(
				mc,
				units,
				DataFolder,
				Path.GetTempPath() + "MCFiles",
				cancellationToken);

			return LogModuleFileInspectionResults(lcu, machine, module, results, cancellationToken);
		}

		private bool LogModuleFileInspectionResults(
			LcuInfo lcu,
			MachineInfo machine,
			ModuleInfo module,
			IReadOnlyList<ModuleFileInspectionResult> results,
			CancellationToken cancellationToken = default)
		{
			ModuleInspectionDiagnostics diagnostics =
				_updateInspectionDiagnosticService.CreateModuleDiagnostics(
					lcu.Name ?? "",
					machine.Name ?? "",
					module.Name ?? "",
					results,
					cancellationToken);
			foreach (InspectionDiagnostic diagnostic in diagnostics.Entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (diagnostic.Destination == InspectionDiagnosticDestination.Debug)
				{
					Debug.WriteLine(diagnostic.Message);
				}
				else
				{
					AddLog(diagnostic.Message);
				}
			}
			return diagnostics.AllFilesMatch;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="updateCommonFile">UpdateCommon.inf</param>
		/// <param name="unitVersions"></param>
		/// <exception cref="NotImplementedException"></exception>
		private void CheckUpdateCommon(
			UpdateCommonVersionInspectionResult inspection,
			string updateCommonFile,
			CancellationToken cancellationToken = default)
		{
			IReadOnlyList<InspectionDiagnostic> diagnostics =
				_updateInspectionDiagnosticService.CreateUpdateCommonDiagnostics(
					inspection,
					updateCommonFile,
					cancellationToken);
			foreach (InspectionDiagnostic diagnostic in diagnostics)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (diagnostic.Destination == InspectionDiagnosticDestination.Debug)
				{
					Debug.WriteLine(diagnostic.Message);
				}
				else
				{
					AddLog(diagnostic.Message);
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
			LcuUnitFileUploadResult result = await _lcuUnitFileUploadService.UploadAsync(
					lcu.LcuCtrl.Name.Split(":")[0],
					lcu.FtpUser ?? "",
					lcu.FtpPassword ?? "",
					folders,
					srcRoot,
					lcuRoot,
					progress => OnLcuFolderUploaded(lcu, progress),
					cancellationToken => WaitTransferState(cancellationToken),
					token);
			LcuFileUploadPresentation presentation =
				_lcuFileUploadResultPresenter.Create(
					result,
					lcu.Name ?? "",
					token,
					includeOutcomeMessages: false);
			foreach (string message in presentation.LogMessages)
			{
				AddLog(message);
			}
			return presentation.Succeeded;
		}

		private async Task OnLcuFolderUploaded(LcuInfo lcu, LcuFolderUploadProgress progress)
		{
			FtpTransferedSize = progress.TotalTransferredBytes;
			LcuFolderUploadPresentation presentation =
				_lcuFolderUploadProgressPresenter.Create(
					lcu.Name ?? "",
					progress,
					FtpTransferSize);
			Progress?.SetMessage(presentation.Message);
			Progress?.SetProgress(presentation.Progress);
			await Task.Delay(100, CancellationToken.None);
			Debug.WriteLine(presentation.DebugMessage);
		}


		public async Task<bool> PostMcFilesCommand(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string group, List<string> mc, List<string> lc, CancellationToken token)
		{
			MachineFtpCredentials credentials = _mcAccountProvider.GetCredentials();

			PostMcFileTransferResult result = await _postMcFileTransferService.SendAsync(
				new PostMcFileTransferRequest(
					module.IPAddress,
					module.Pos,
					credentials.User,
					credentials.Password,
					mc,
					lc),
				new LcuPostMcFileTransferGateway(lcu.LcuCtrl),
				token);

			PostMcFileTransferPresentation presentation =
				_postMcFileTransferResultPresenter.Create(
					result,
					lcu.Name ?? "",
					machine.Name ?? "",
					module.Name ?? "",
					group,
					ErrorInfo.GetErrMessage(ErrorCode.FTP_ACCOUNT_ERROR));
			if (presentation.Succeeded)
			{
				return true;
			}

			if (presentation.ErrorCode is ErrorCode errorCode)
			{
				ErrorInfo.ErrCode = errorCode;
			}
			if (presentation.CountTransferError)
			{
				TransferErrorCount++;
			}
			foreach (string message in presentation.LogMessages)
			{
				AddLog(message);
			}
			return false;
		}

		public void UpdateProgress(long transfered, long total, long cnt)
		{
			if (Progress != null)
			{
				ModuleTransferProgressPresentation presentation =
					_moduleUpdateService.CreateProgress(
						transfered,
						total,
						TransferedCount,
						cnt);
				Debug.WriteLine(presentation.DebugMessage);
				Progress?.SetProgress(presentation.Progress);
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

			if (module.UnitVersions == null)
			{
				return false;
			}
			UnitVersion[] unitVersionSnapshot = new UnitVersion[module.UnitVersions.Count];
			string?[] unitVersionNames = new string?[module.UnitVersions.Count];
			for (int index = 0; index < module.UnitVersions.Count; index++)
			{
				token.ThrowIfCancellationRequested();
				UnitVersion unitVersion = module.UnitVersions[index];
				unitVersionSnapshot[index] = unitVersion;
				unitVersionNames[index] = unitVersion.Name;
			}
			UnitVersionIndexLookup unitVersionLookup =
				_unitVersionIndexLookupBuilder.Build(unitVersionNames, token);
			int transferredCountAtStart = TransferSuccessCount;

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

			//(転送データの)バージョン情報からパスのみを取り出してリスト化
			List<UpdateFileGroupResult> unitFiles =
				await CreateUpdateFileListAsync(module.UnitVersions, token);

			//LCUに転送したファイルを装置に送信する
			MachineFtpCredentials credentials = _mcAccountProvider.GetCredentials();

			if (!credentials.IsAvailable)
			{
				// モジュールにアクセスするためのFTP情報が取得できない(mcAccount.Dll が無い、など)
				ErrorInfo.ErrCode = ErrorCode.FTP_ACCOUNT_ERROR;
				AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}={ErrorInfo.GetErrMessage()}");
				return false;
			}

			ModuleUpdateVersion[] updateVersions = new ModuleUpdateVersion[unitVersionSnapshot.Length];
			UpdateCommonVersionUnit[] updateCommonUnits =
				new UpdateCommonVersionUnit[unitVersionSnapshot.Length];
			for (int index = 0; index < unitVersionSnapshot.Length; index++)
			{
				token.ThrowIfCancellationRequested();
				UnitVersion version = unitVersionSnapshot[index];
				updateVersions[index] = new ModuleUpdateVersion(
					version.Name,
					version.Path,
					version.FuserPath,
					version.CurVersion,
					version.NewVersion);
				updateCommonUnits[index] = new UpdateCommonVersionUnit(version.Name, version.CurVersion);
			}
			ModuleUpdateTransferPreparation preparation = await Task.Run(
				() => _moduleUpdateService.BuildTransferPreparation(
					unitFiles,
					updateVersions,
					DataFolder,
					lcuRoot,
					token),
				token);

			bool inspectionSucceeded = true;
			WpfLcuCtrlLib.FileTransferRequest[] stagedFiles = preparation.Plans
				.SelectMany(plan => plan.Units)
				.SelectMany(unit => new[] { unit.Path, unit.FusePath })
				.OfType<string>()
				.Where(path => path.Length > 0)
				.Select(path => new WpfLcuCtrlLib.FileTransferRequest(
					path,
					lcuRoot + "/" + Path.GetRelativePath(DataFolder, path).Replace('\\', '/')))
				.DistinctBy(file => file.RemotePath)
				.ToArray();
			if (stagedFiles.Length > 0)
			{
				try
				{
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=UploadFiles Start");
					Progress?.SetMessage($"Uploading {machine.Name};{module.Name}");
					await new LcuFolderUploadService(_fileTransferClient).UploadModuleFilesAsync(
						lcu.LcuCtrl.Name.Split(':')[0], lcu.FtpUser ?? "", lcu.FtpPassword ?? "",
						stagedFiles, token);
					AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name}=UploadFiles End");
				}
				catch (Exception exception) when (
					exception is FluentFTP.Exceptions.FtpException or
					IOException or UnauthorizedAccessException or InvalidOperationException or
					ArgumentException or System.Net.Sockets.SocketException)
				{
					ErrorInfo.ErrCode = ErrorCode.FTP_TRANSFER_ERROR;
					AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}=UploadFiles failed: {exception.Message}");
					return false;
				}
			}
			List<ModuleUpdateVersion> transferredVersions = [];
			ModuleUpdateTransferResult transferResult = await _moduleUpdateService.ExecuteAsync(
				preparation.Plans,
				preparation.Versions,
				DataFolder,
				lines,
				async (fileGroup, cancellationToken) =>
				{
					bool canStart = await WaitTransferState(cancellationToken);
					if (!canStart)
					{
						AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{fileGroup.GroupName}=NG:Transfer Stop");
						return false;
					}
					Progress?.SetMessage($"Transfering {fileGroup.GroupName}");
					return true;
				},
				(fileGroup, machineFiles, lcuFiles, cancellationToken) =>
					PostMcFilesCommand(
						lcu,
						machine,
						module,
						fileGroup.GroupName,
						machineFiles.ToList(),
						lcuFiles.ToList(),
						cancellationToken),
				(_, count) =>
				{
					TransferSuccessCount += count;
					TransferedCount += count;
					UpdateProgress(TransferedCount, TransferCount, count);
				},
				async (fileGroup, stateChanges) =>
				{
					foreach (ModuleUpdateStateChange change in stateChanges)
					{
						token.ThrowIfCancellationRequested();
						int? unitVersionIndex = unitVersionLookup.FindIndex(change.Name);
						UnitVersion? unitVersion = unitVersionIndex is int index
							? unitVersionSnapshot[index]
							: null;
						if (unitVersion != null)
						{
							unitVersion.IsUpdated.Value = true;
						}

						if (change.IsUpdateCommonVersionChanged)
						{
							if (unitVersionIndex is int updatedIndex)
							{
								transferredVersions.Add(updateVersions[updatedIndex]);
								updateCommonUnits[updatedIndex] = new UpdateCommonVersionUnit(
									updateCommonUnits[updatedIndex].Name,
									change.NewVersion);
							}
							AddLog($"[Transfer] {lcu.Name};{machine.Name};{module.Name};{change.Name}=OK:{change.CurrentVersion}->{change.NewVersion}");
							AddLog($"[File] {change.Name} Path={change.Path} FuserPath={change.FuserPath}");
						}
					}
					if (ArgOptions.GetOptionBool(
						"--inspection",
						Config.Options.GetDefaultOption("inspection", false)))
					{
						List<string> machineFiles = fileGroup.MachineFiles.ToList();
						List<string> lcuFiles = fileGroup.LcuFiles.ToList();
						ModuleInspectionUnit[] inspectionUnits = new ModuleInspectionUnit[fileGroup.Units.Count];
						for (int index = 0; index < fileGroup.Units.Count; index++)
						{
							token.ThrowIfCancellationRequested();
							UpdateFileListEntry unit = fileGroup.Units[index];
							inspectionUnits[index] = new ModuleInspectionUnit(
								unit.Name,
								unit.Path,
								unit.FusePath);
						}
						string inspectionFolder = Path.GetTempPath() + "MCFiles\\";
						ModuleTransferInspectionResult inspectionResult =
							await _moduleTransferInspectionService.InspectAsync(
								new ModuleTransferInspectionRequest(
									machineFiles,
									lcuFiles,
									inspectionUnits,
									DataFolder,
									inspectionFolder),
								new ModuleTransferInspectionContext(machine.Name, module.IPAddress, module.Pos),
								new LcuModuleTransferInspectionGateway(lcu.LcuCtrl),
								token);
						if (!inspectionResult.DownloadSucceeded)
						{
							inspectionSucceeded = false;
							AddLog($"[INSPECTION] {lcu.Name};{machine.Name};{module.Name};{fileGroup.GroupName}=NG:GetMcFile Inspection Error");
						}
						else
						{
							inspectionSucceeded &= LogModuleFileInspectionResults(
								lcu,
								machine,
								module,
								inspectionResult.FileResults,
								token);
						}
					}
				},
				token);
			ret = transferResult.IsSuccess && inspectionSucceeded;
			TransferErrorCount += transferResult.FailedGroupCount;

			lines = UpdateCommonService.SetTransferredFilePaths(lines, transferredVersions, token);

			// 転送成功ファイルがある場合だけUpdateCommon.infを更新する。
			bool inspectionEnabled = ArgOptions.GetOptionBool(
				"--inspection",
				Config.Options.GetDefaultOption("inspection", false));
			ModuleUpdateCommonDeploymentResult deploymentResult =
				await _updateCommonService.DeployIfNeededAsync(
					new ModuleUpdateCommonDeploymentRequest(
						transferredCountAtStart,
						TransferSuccessCount,
						new UpdateCommonDeploymentRequest(
							machine.Name,
							module.IPAddress,
							module.Pos,
							Define.MC_PERIPHERAL_PATH,
							lines,
							Path.GetTempPath(),
							Path.Combine(Path.GetTempPath(), "MCFiles"),
							Define.UPDATE_INFO_FILE,
							Define.TXT_ENCODING,
							inspectionEnabled,
							inspectionEnabled),
						updateCommonUnits),
					new LcuUpdateCommonDeploymentGateway(lcu.LcuCtrl),
					token);
			if (deploymentResult.WasDeployed)
			{
				if (deploymentResult.UploadSucceeded)
				{
					module.UpdateStrings = lines;
					module.UpdateInfo = new IniFileParser(
						Path.Combine(Path.GetTempPath(), Define.UPDATE_INFO_FILE));
				}
				ret = ret && deploymentResult.UploadSucceeded;
				if (!deploymentResult.UploadSucceeded)
				{
					ErrorInfo.ErrCode = ErrorCode.FTP_TRANSFER_ERROR;
					AddLog($"[ERROR] {lcu.Name};{machine.Name};{module.Name}=UpdateCommon.inf upload failed");
				}

				if (inspectionEnabled && !deploymentResult.InspectionDownloadSucceeded)
				{
					ret = false;
					AddLog($"[INSPECTION] {lcu.Name};{machine.Name};{module.Name}=NG:Inspection Error(UpdateCommon.inf)");
				}

				if (deploymentResult.Inspection != null)
				{
					string inspectionFilePath = Path.Combine(
						Path.GetTempPath(),
						Define.MC_PERIPHERAL_PATH,
						Define.UPDATE_INFO_FILE);
					CheckUpdateCommon(deploymentResult.Inspection, inspectionFilePath, token);
					ret = ret
						&& deploymentResult.Inspection.FileExists
						&& deploymentResult.Inspection.ReadError == null
						&& !deploymentResult.Inspection.Entries.Any(
							entry => entry.Status == UpdateCommonVersionStatus.Mismatched);
				}
			}

			//for Debug
			//Directory.Delete(tmpDir, recursive: true);
			if( ArgOptions.GetOptionBool("--inspection",Config.Options.GetDefaultOption("inspection",false)) == true)
			{
				string getPath = Path.GetTempPath() + "MCFiles\\";
				TransferredUnitFile[] inspectedUnits = new TransferredUnitFile[unitVersionSnapshot.Length];
				for (int index = 0; index < unitVersionSnapshot.Length; index++)
				{
					token.ThrowIfCancellationRequested();
					UnitVersion version = unitVersionSnapshot[index];
					inspectedUnits[index] = new TransferredUnitFile(
						version.Name,
						version.Path,
						version.FuserPath,
						version.IsSelected.Value);
				}
				IReadOnlyList<TransferredFilePresence> presenceResults =
					await Task.Run(
						() => _moduleTransferInspectionService.InspectTransferredDirectory(
							getPath,
							inspectedUnits,
							token),
						token);
				LogTransferredFilePresence(presenceResults);
			}

			if (ret && (stagedFiles.Length > 0 || deploymentResult.WasDeployed))
			{
				ret = await ClearModuleMcFilesAsync(lcu, machine, module, token);
			}
			else if (!ret)
			{
				AddLog($"[INFO] {lcu.Name};{machine.Name};{module.Name}=MCFiles cleanup skipped (transfer or inspection failed; retained for investigation)");
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
		private async Task<bool> GetMcPeripheralFile(LcuInfo lcu, MachineInfo machine, ModuleInfo module, string peripheralFile, string targetPath, CancellationToken token)
		{
			return await _lcuPeripheralFileDownloadService.DownloadAsync(
				new LcuPeripheralFileDownloadRequest(
					lcu.LcuId,
					machine.Name,
					module.IPAddress,
					module.Pos,
					Define.MC_PERIPHERAL_PATH,
					peripheralFile,
					targetPath),
				new LcuPeripheralFileDownloadGateway(lcu.LcuCtrl),
				token);
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
								ReactiveCollection<MachineInfo> machines =
									FindFirstLcuMachines(item.Name);
								viewModeTable.Add(viewName, new LcuViewModel((LcuInfo)item, machines));
								IsLcuMenuEnabled = true;
								CanExecuteLcuCommand.Value = true;
								break;
							case MachineType.Machine:
								ReactiveCollection<ModuleInfo> modules =
									FindFirstMachineModules(item.Name);

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
								TaskCompletionSource progressDialogOpened = new( TaskCreationOptions.RunContinuationsAsynchronously);
								Task<object?> dlg = DialogHostShowCoordinator.ShowAsync(
									"MainWindow",
									() => DialogHost.Show(
										new WaitProgress(Resource.ReadingUnitInfo),
										"MainWindow",
										new DialogOpenedEventHandler(
											(_, _) => { progressDialogOpened.TrySetResult(); })));
								Task firstDialogEvent = await Task.WhenAny( progressDialogOpened.Task, dlg);

								if (firstDialogEvent == dlg) {
									await dlg;
								}
								else {
									await progressDialogOpened.Task;
								}

								var token = new CancellationToken();
								bool ret = await GetModuleUpdateInfo(lcu, machineInfo, moduleInfo, token);

								CloseDialog("MainWindow");
								await dlg;

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
								bool bRet = await CreateVersionInfoAsync( lcu, machineInfo, moduleInfo, token);
								if (bRet == true)
								{
									moduleInfo.ToolTipText = $"{moduleInfo.UnitVersions.Count} units";
								}

								viewModeTable.Add(viewName, new ModuleViewModel((ModuleInfo)item, UpdateDataInfos));
								moduleInfo.IsSelected.Value = Utility.CheckSelectableUnitSelectionState(moduleInfo.UnitVersions);
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
		/// LCUからモジュールのアップデート情報(UpdateCommon.inf)を取得する
		/// </summary>
		/// <param name="lcu"></param>
		/// <param name="machine"></param>
		/// <param name="module"></param>
		/// <returns></returns>
		public Task<bool> GetModuleUpdateInfo(
			LcuInfo lcu,
			MachineInfo machine,
			ModuleInfo module,
			CancellationToken token) =>
			LoadModuleUpdateInfoAsync(lcu, machine, module, forceMachineReload: false, token);

		private async Task<bool> GetModuleBackupUpdateInfo(
			LcuInfo lcu,
			MachineInfo machine,
			ModuleInfo module,
			CancellationToken token)
		{
			if (module.MachineUpdateInfo != null)
			{
				return true;
			}

			if (module.UpdateInfoLoaded == UnitInfoLoadStatus.LoadedMachine
				&& module.UpdateInfo != null)
			{
				module.MachineUpdateInfo = module.UpdateInfo;
				module.MachineUpdateStrings = module.UpdateStrings;
				return true;
			}

			return await LoadModuleUpdateInfoAsync(
				lcu,
				machine,
				module,
				forceMachineReload: true,
				token);
		}

		private async Task<bool> LoadModuleUpdateInfoAsync(
			LcuInfo lcu,
			MachineInfo machine,
			ModuleInfo module,
			bool forceMachineReload,
			CancellationToken token)
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadingUnitInfo);
			string tmpDir = Path.GetTempPath() + "MCFiles\\";
			ModuleUpdateInfoLoadResult result = await _moduleUpdateService.LoadUpdateInfoAsync(
				forceMachineReload
					|| module.UpdateInfo == null
					|| module.UpdateInfoLoaded == UnitInfoLoadStatus.NotLoaded,
				tmpDir,
				Define.MC_PERIPHERAL_PATH,
				Define.UPDATE_INFO_FILE,
				Define.TXT_ENCODING,
				(targetPath, cancellationToken) => GetMcPeripheralFile(
					lcu,
					machine,
					module,
					Define.UPDATE_INFO_FILE,
					targetPath,
					cancellationToken),
				token);
			if (result.Outcome == ModuleUpdateInfoLoadOutcome.DownloadFailed || result.Outcome == ModuleUpdateInfoLoadOutcome.FileNotFound)
			{
				ErrorInfo.ErrCode = ErrorCode.UPDATE_UNDEFINED_ERROR;
				module.UpdateInfoLoaded = UnitInfoLoadStatus.LoadError;
				return false;
			}

			if (result.Outcome == ModuleUpdateInfoLoadOutcome.Loaded)
			{
				IniFileParser parser = result.Parser
					?? throw new InvalidOperationException("Loaded UpdateCommon.inf parser is missing.");
				string[] lines = result.Lines
					?? throw new InvalidOperationException("Loaded UpdateCommon.inf lines are missing.");
				module.MachineUpdateInfo = parser;
				module.MachineUpdateStrings = lines;
				if (!forceMachineReload)
				{
					module.UpdateInfo = parser;
					module.UpdateStrings = lines;
					module.UpdateInfoLoaded = UnitInfoLoadStatus.LoadedMachine;
				}
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
		public async Task<bool> CreateVersionInfoAsync( LcuInfo lcu, MachineInfo machine, ModuleInfo module, CancellationToken token)
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadingUnitInfo);
			token.ThrowIfCancellationRequested();
			// 1. 前提チェック: LcuCtrl 未接続、またはアップデートデータ未指定の場合は終了
			if (lcu.LcuCtrl == null || UpdateDataInfos == null)
			{
				return false;
			}

			// 2. UpdateCommon.inf の取得元を決定する
			// ユニット一覧作成時のオプション
			//   --ignore_version が指定されている場合は、バージョンの違いを無視する
			bool ignoreVersion = ArgOptions.GetOptionBool("--ignore_version", Config.Options.GetDefaultOption("ignore_version",false));

			bool IsAdmin = ArgOptions.GetOption("--mode", "user") == "administrator";
			// module.UpdateInfo の有無・管理者モードか・バージョン無視かから、
			// LCU側情報 / アップデートデータ側情報のどちらを使うか(あるいは取得不可)を判定
			ModuleUpdateInfoDecision updateInfoDecision = _moduleUpdateService.DecideUpdateInfoSource( module.UpdateInfo != null, IsAdmin, ignoreVersion);

			if (updateInfoDecision.Source == ModuleUpdateInfoSource.Unavailable)
			{
				// ユーザーモードで UpdateCommon.inf が取得できない場合は、アップデートを中止する
				AddLog($"[ERROR] {lcu.LcuCtrl.Name};{machine.Name};{module.Name}={Define.UPDATE_INFO_FILE} Get error");
				return false;
			}

			// 3. パーサーの確定
			IniFileParser parser = module.UpdateInfo;
			string[]? stagedUpdateStrings = null;
			bool loadBaseUpdateInfo = module.UpdateInfoLoaded == UnitInfoLoadStatus.LoadedBase;
			if (updateInfoDecision.Source == ModuleUpdateInfoSource.UpdateData)
			{
				// Administrator モードで UpdateCommon.inf が取得できていない場合はアップデートデータと同じとする
				token.ThrowIfCancellationRequested();
				string updateInfoPath = DataFolder + "\\" + Define.MC_PERIPHERAL_PATH + Define.UPDATE_INFO_FILE;

				UpdateCommonSourceReadResult updateInfo = await Task.Run(
					() => _updateCommonService.Read(
						updateInfoPath,
						Define.TXT_ENCODING,
						token),
					token);
				token.ThrowIfCancellationRequested();
				if (updateInfo.Parser != null)
				{
					parser = updateInfo.Parser;
					stagedUpdateStrings = updateInfo.Lines;
				}
				loadBaseUpdateInfo = true;
			}

			if (parser == null) {
				throw new InvalidOperationException( $"Update information is unavailable for module '{module.Name}'.");
			}

			// 4. 入力スナップショットの構築
			// 4-1. アップデートデータ(名前・バージョン)のスナップショットを作成
			token.ThrowIfCancellationRequested();
			UpdateVersionInputSnapshot[] updateInfoSnapshot = new UpdateVersionInputSnapshot[UpdateDataInfos.Count];

			for (int index = 0; index < UpdateDataInfos.Count; index++)
			{
				token.ThrowIfCancellationRequested();
				UpdateInfo update = UpdateDataInfos[index];
				updateInfoSnapshot[index] = new UpdateVersionInputSnapshot(
					update.Name, update.Version, update.Path, update.FuserPath);
			}
			// 4-2. ユニットのグループリンク定義(Config.units)のスナップショットを作成
			UnitVersionGroupInputSnapshot[] unitLinkSnapshot =
				new UnitVersionGroupInputSnapshot[Config.units.Count];
			for (int index = 0; index < Config.units.Count; index++)
			{
				token.ThrowIfCancellationRequested();
				UnitLink link = Config.units[index];
				KeyValuePair<string, string>[] names = new KeyValuePair<string, string>[link.name.Count];
				int nameIndex = 0;
				foreach (KeyValuePair<string, object> entry in link.name)
				{
					token.ThrowIfCancellationRequested();
					names[nameIndex++] = new KeyValuePair<string, string>(
						entry.Key,
						entry.Value?.ToString() ?? "");
				}
				string[] components = new string[link.components.Count];
				for (int componentIndex = 0; componentIndex < link.components.Count; componentIndex++)
				{
					token.ThrowIfCancellationRequested();
					components[componentIndex] = link.components[componentIndex];
				}
				unitLinkSnapshot[index] = new UnitVersionGroupInputSnapshot(components, names);
			}
			// 4-3. 既存ユニット(module.UnitVersions)の選択状態をスナップショット化
			List<UnitGroupSelection> existingUnits = [];
			foreach (UnitVersion unit in module.UnitVersions)
			{
				token.ThrowIfCancellationRequested();
				existingUnits.Add(new UnitGroupSelection(unit.UnitGroup, unit.IsSelected.Value));
			}
			string machineType = module.Parent.MachineType;
			bool isModuleSelected = module.IsSelected.Value != false;
			bool isBackupData = IsBackupedData;
			// --fileExist: strict=更新ファイル無しは除外 / その他=表示のみ(選択不可)
			string fileExistMode = ArgOptions.GetOption("--fileExist", "") ?? "";
			UnitVersionMissingFileMode missingFileMode =
				string.Equals(fileExistMode, "strict", StringComparison.OrdinalIgnoreCase) ? UnitVersionMissingFileMode.Exclude
				: string.Equals(fileExistMode, "test", StringComparison.OrdinalIgnoreCase) ? UnitVersionMissingFileMode.Selectable
				: UnitVersionMissingFileMode.ListOnly;

			// 5〜6. ユニット一覧の作成(バージョン比較・グループ決定・行作成・ログ作成)
			UnitVersionListRequest listRequest = new(
				updateInfoSnapshot,
				unitLinkSnapshot,
				parser,
				existingUnits,
				machineType,
				DataFolder,
				updateInfoDecision.IgnoreVersion,
				isModuleSelected,
				isBackupData,
				loadBaseUpdateInfo,
				!IsAdmin,
				Define.NOT_UPDATE,
				missingFileMode,
				lcu.LcuCtrl.Name,
				machine.Name,
				module.Name);
			UnitVersionListResult projection = await Task.Run(
				() => _unitVersionListService.Create(listRequest, token),
				token);
			token.ThrowIfCancellationRequested();

			// 7. ログ出力(Preparationフェーズ分のみ)
			IReadOnlyList<UnitVersionPreparationDiagnostic> diagnostics = projection.Diagnostics;
			foreach (UnitVersionPreparationDiagnostic diagnostic in diagnostics)
			{
				token.ThrowIfCancellationRequested();
				if (diagnostic.Phase != UnitVersionPreparationDiagnosticPhase.Preparation)
				{
					continue;
				}
				if (diagnostic.Destination == UnitVersionDiagnosticDestination.Debug)
				{
					Debug.WriteLine(diagnostic.Message);
				}
				else
				{
					AddLog(diagnostic.Message);
				}
			}

			// 8. 新規ユニットバージョンの生成(projection.ItemsToCreate から UnitVersion を作成)
			List<UnitVersion> preparedVersions = [];
			foreach (UnitVersionPreparedDefinition definition in projection.ItemsToCreate)
			{
				token.ThrowIfCancellationRequested();
				UnitVersion version = new(definition.IsSelected)
				{
					Name = definition.Name,
					IsSupported = Config.IsUnitSupported(definition.Name),
					Attribute = definition.Attribute,
					Path = definition.Path!,
					FuserPath = definition.FuserPath!,
					CurVersion = definition.CurrentVersion,
					NewVersion = definition.NewVersion,
					Parent = module,
					Size = definition.Size,
					UnitGroup = definition.UnitGroup,
					IsUpdateFileMissing = definition.IsUpdateFileMissing
				};
				if (!version.IsSupported)
				{
					AddLog($"[INFO] {lcu.Name};{machine.Name};{module.Name}:{definition.Name}=unsupported");
				}
				preparedVersions.Add(version);
			}

			// 9. module.UpdateInfo 等の更新
			token.ThrowIfCancellationRequested();
			if (stagedUpdateStrings != null)
			{
				module.UpdateInfo = parser;
				module.UpdateStrings = stagedUpdateStrings;
				module.UpdateInfoLoaded = UnitInfoLoadStatus.LoadedBase;
			}
			foreach (UnitVersionPreparationDiagnostic diagnostic in diagnostics)
			{
				token.ThrowIfCancellationRequested();
				if (diagnostic.Phase == UnitVersionPreparationDiagnosticPhase.IniUpdate)
				{
					Debug.WriteLine(diagnostic.Message);
				}
			}
			_updateCommonService.ClearMissingFuserPaths(
				parser,
				projection.FuserPathsToClear,
				token);
			// 10. 生成したユニットバージョンを module.UnitVersions へ反映
			foreach (UnitVersion version in preparedVersions)
			{
				module.UnitVersions.Add(version);
				Debug.WriteLine(_unitVersionListService.CreateCreatedVersionMessage(
					module.Name,
					version.Name,
					version.CurVersion,
					version.NewVersion));
			}

			// 11. 既存ユニットのうち、選択解除すべきものを解除する
			foreach (int index in projection.ExistingUnitsToDeselect)
			{
				module.UnitVersions[index].IsSelected.Value = false;
			}

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
			token.ThrowIfCancellationRequested();
			AddLog("LoadLineInfo Start");

			// NeximDB より LCU のリストを取得(list=> Line名=LCU名)
			NeximLcuDirectoryResult directoryResult =
				await GetLcuTargetsFromNeximAsync(token);
			token.ThrowIfCancellationRequested();
			LcuLineInfoDirectoryPresentation directoryPresentation =
				_lcuLineInfoService.CreateDirectoryPresentation(directoryResult);
			foreach (LcuLineInfoDiagnostic diagnostic in directoryPresentation.Diagnostics)
			{
				AddLog(diagnostic.Message);
			}
			if (!directoryPresentation.Succeeded)
			{
				ErrorInfo.ErrCode = ErrorCode.LCU_LINEINFO_ERROR;
				return false;
			}
			IReadOnlyList<NeximLcuTarget> lcuTargets = directoryPresentation.Targets;

			ReactiveCollection<LcuInfo> lcuItems = [];
			foreach (NeximLcuTarget target in lcuTargets)
			{
				token.ThrowIfCancellationRequested();
				lcuItems.Add(new LcuInfo(
					target.LcuName,
					target.ComputerId,
					_fileTransferClient,
					_webApiClient,
					AddCommunicationLog)
				{
					Name = target.LineName,
					ItemType = MachineType.LCU
				});
			}
			token.ThrowIfCancellationRequested();
			TreeViewItems = lcuItems;

			// LCU, LCU下のライン 情報を取得
			string?[] supportedMachineTypes = Config.Options.machineType.Cast<string?>().ToArray();

			bool loadCompleted = await _lcuLineInfoService.LoadSequentiallyAsync(
				TreeViewItems.ToArray(),
				lcu => UpdateLcuInfo(lcu, token, supportedMachineTypes),
				lcu => Progress?.SetMessage(_lcuLineInfoService
					.CreateLoadStartedProgressMessage(lcu.LcuCtrl.Name)),
				(lcu, succeeded) => CompleteLcuLineInfoLoad(lcu, succeeded, token),
				(lcu, exception) =>
				{
					AddLog(_lcuLineInfoService
						.CreateLoadErrorDiagnostic(exception)
						.Message);
					Progress?.SetMessage(_lcuLineInfoService
						.CreateTargetLoadProgressMessage(lcu.LcuCtrl.Name, exception));
					lcu.ErrCode = ErrorCode.LCU_LINEINFO_ERROR;
					lcu.Status = ItemStatus.NG;
					lcu.IconToolTipText = Resource.ConnectionError;
				},
				(lcu, exception) =>
				{
					AddLog(_lcuLineInfoService
						.CreateLoadCancelledDiagnostic()
						.Message);
					Progress?.SetMessage(_lcuLineInfoService
						.CreateTargetLoadProgressMessage(lcu.LcuCtrl.Name, exception));
					lcu.IsSelected.Value = false;
					lcu.Status = ItemStatus.NG;
					lcu.IconToolTipText = Resource.ConnectionError;
				},
				token);
			if (!loadCompleted)
			{
				return false;
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
			return true;
		}

		private void CompleteLcuLineInfoLoad(
			LcuInfo lcu,
			bool succeeded,
			CancellationToken cancellationToken)
		{
			Debug.WriteLine(_lcuLineInfoService
				.CreateLoadResultDiagnostic(lcu.Name, succeeded)
				.Message);

			cancellationToken.ThrowIfCancellationRequested();
			LcuMachineSelection[] machineSelections = new LcuMachineSelection[lcu.Children.Count];
			for (int machineIndex = 0; machineIndex < lcu.Children.Count; machineIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				MachineInfo machine = lcu.Children[machineIndex];
				LcuModuleSelection[] moduleSelections = new LcuModuleSelection[machine.Children.Count];
				for (int moduleIndex = 0; moduleIndex < machine.Children.Count; moduleIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ModuleInfo module = machine.Children[moduleIndex];
					bool?[] unitSelections = new bool?[module.UnitVersions.Count];
					for (int unitIndex = 0; unitIndex < module.UnitVersions.Count; unitIndex++)
					{
						cancellationToken.ThrowIfCancellationRequested();
						unitSelections[unitIndex] = module.UnitVersions[unitIndex].IsSelected.Value;
					}

					moduleSelections[moduleIndex] = new LcuModuleSelection(
						module.IsSelected.Value,
						unitSelections);
				}

				machineSelections[machineIndex] = new LcuMachineSelection(
					machine.IsSelected.Value,
					moduleSelections);
			}

			LcuSelectionSummary selectionSummary = _lcuLineTreeService.CreateSelectionSummary(
				machineSelections,
				cancellationToken);
			AddLog(_lcuLineInfoService
				.CreateSelectionSummaryDiagnostic(lcu.Name, selectionSummary)
				.Message);
			LcuLoadCompletionState completionState =
				_lcuLineTreeService.CreateCompletionState(
					lcu.ErrCode != ErrorCode.OK,
					selectionSummary.SelectionState,
					lcu.ErrCode != ErrorCode.OK
						? ErrorInfo.GetErrMessage(lcu.ErrCode)
						: null,
					$"{lcu.Version}");
			lcu.IsSelected.Value = completionState.IsSelected;
			lcu.ToolTipText = completionState.ToolTip;
		}

		/// <summary>
		/// LCUの情報を取得する
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public async Task<bool> GetLineInfoFromLcu(
			LcuInfo lcu,
			CancellationToken token,
			IReadOnlyCollection<string?>? supportedMachineTypes = null)
		{
			using IDisposable status = StatusBar.BeginOperation(Resource.ReadLineInformation);
			LcuLineInfoResponseResult<LineInfo> responseResult =
				await _lcuLineInfoService.GetLineInfoAsync<LineInfo>(
					new LcuCtrlLineInfoGateway(lcu.LcuCtrl),
					lineInfo => lineInfo?.Line?.Machines != null,
					token);
			token.ThrowIfCancellationRequested();

			LcuLineInfoResponsePresentation responsePresentation =
				_lcuLineInfoService.CreateResponsePresentation(
					responseResult.Outcome,
					lcu.Name);
			foreach (LcuLineInfoDiagnostic diagnostic in responsePresentation.Diagnostics)
			{
				AddLog(diagnostic.Message);
			}

			if (!responsePresentation.Succeeded)
			{
				MarkLcuLineInfoError(lcu);
				await MsgBox.Show(Resource.Error, $"{lcu.Name}",$"{Resource.GetLineInfoError}","", (int)(MsgDlgType.OK | MsgDlgType.ICON_ERROR), "MainWindow");
				return false;
			}

			LineInfo lineInfo = responseResult.Data!;
			LcuLineTreePlan treePlan = await _lcuLineTreeItemBuilder.PopulateAsync(
				lcu,
				lineInfo,
				supportedMachineTypes ?? Config.Options.machineType.Cast<string?>().ToArray(),
				ArgOptions.GetOptionBool(
					"--ignore_machineType",
					Config.Options.GetDefaultOption("ignore_machineType", false)),
				progress => Progress?.SetMessage(
					_lcuLineInfoService.CreateTreeProgressMessage(progress)),
				token);

			// ライン情報はツリー構築が完了してから保持する
			lcu.LineInfo = lineInfo;

			foreach ((string? skippedMachine, string? machineType) in treePlan.SkippedMachines)
			{
				AddLog(_lcuLineInfoService
					.CreateSkippedMachineDiagnostic(lcu.Name, skippedMachine, machineType)
					.Message);
			}
			return true;
		}

		private void MarkLcuLineInfoError(LcuInfo lcu)
		{
			ErrorInfo.ErrCode = ErrorCode.LCU_LINEINFO_ERROR;
			lcu.ErrCode = ErrorCode.LCU_LINEINFO_ERROR;
			lcu.Status = ItemStatus.NG;
			lcu.IconToolTipText = Resource.ConnectionError;
		}

		/// <summary>
		/// LCUの情報を更新する
		/// </summary>
		/// <param name="LcuInfo">LCU情報</param>
		public async Task<bool> UpdateLcuInfo(
			LcuInfo lcu,
			CancellationToken token,
			IReadOnlyCollection<string?>? supportedMachineTypes = null)
		{
			LcuInformationService informationService = new( new LcuCtrlInformationGateway(lcu.LcuCtrl));
			LcuUpdatePreparationService preparationService = new( _lcuReachabilityService, informationService);

			LcuUpdatePreparationResult preparation = await preparationService.PrepareAsync(
				lcu.LcuCtrl.Name.Split(':')[0],
				lcu.LcuCtrl.FtpUser != null,
				UpdateDataSize,
				token,
				message => Debug.WriteLine(message),
				() => AddLog(LcuUpdatePreparationService.CreateDiskSpaceCheckStartedMessage(lcu.Name)));
			token.ThrowIfCancellationRequested();

			LcuUpdatePreparationPresentation presentation =
				LcuUpdatePreparationService.CreatePresentation(
					preparation,
					lcu.Name,
					lcu.LcuCtrl.Name,
					UpdateDataSize);
			foreach (LcuUpdatePreparationAction action in presentation.Actions)
			{
				switch (action.Type)
				{
					case LcuUpdatePreparationActionType.Log:
						AddLog(action.Message!);
						break;
					case LcuUpdatePreparationActionType.SetProgressMessage:
						Progress?.SetMessage(action.Message!);
						break;
					case LcuUpdatePreparationActionType.SetLcuErrorCode:
						lcu.ErrCode = action.ErrorCode!.Value;
						break;
					case LcuUpdatePreparationActionType.SetGlobalErrorCode:
						ErrorInfo.ErrCode = action.ErrorCode!.Value;
						break;
					case LcuUpdatePreparationActionType.MarkConnectionError:
					case LcuUpdatePreparationActionType.MarkDiskInfoError:
						lcu.Status = ItemStatus.NG;
						lcu.IconToolTipText = Resource.ConnectionError;
						break;
					case LcuUpdatePreparationActionType.ApplyFtpAccount:
						LcuFtpAccountResult ftpAccount = preparation.FtpAccount
							?? throw new InvalidOperationException(
								$"Preparation outcome '{preparation.Outcome}' requires FTP account data.");
						lcu.FtpUser = ftpAccount.Username;
						lcu.FtpPassword = ftpAccount.Password;
						lcu.LcuCtrl.FtpUser = lcu.FtpUser;
						lcu.LcuCtrl.FtpPassword = lcu.FtpPassword;
						break;
					case LcuUpdatePreparationActionType.ApplyVersion:
						lcu.Version = preparation.Version!.Version!;
						break;
					case LcuUpdatePreparationActionType.ApplyDiskSpace:
						lcu.DiskSpace = preparation.DiskSpace!.FreeSpace;
						break;
					case LcuUpdatePreparationActionType.MarkDiskSpaceInsufficient:
						lcu.IsUpdateOk = false;
						break;
					case LcuUpdatePreparationActionType.SetDiskSpaceErrorTooltip:
						lcu.IconToolTipText = Resource.DiskSpaceError;
						break;
					default:
						throw new ArgumentOutOfRangeException(
							nameof(action.Type),
							action.Type,
							"Unknown LCU update preparation action.");
				}
			}

			if (!presentation.CanContinue)
			{
				return false;
			}

			//装置情報が未取得の場合
			if (lcu.Children.Count == 0)
			{
				if (!await GetLineInfoFromLcu(lcu, token, supportedMachineTypes))
				{
					return false;
				}
				token.ThrowIfCancellationRequested();
			}
			return true;
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

			var r = await DialogHostShowCoordinator.ShowAsync(
				"MainWindow",
				() => DialogHost.Show(new MyMessageBox(), "MainWindow"));

			if (r == null || r.ToString() == "CANCEL")
			{
				CanAppQuitFlag.Value = true;
				CanTransferStartFlag.Value = true;
				return;
			}
			StreamWriter? logWriter = LogWriter;
			StatusBar.Dispose();
			LogWriter = null;
			if (logWriter != null)
			{
				await Task.Run(logWriter.Dispose);
			}

			//Peripheral.bin の展開フォルダを削除
			await Task.Run(() =>
			{
				if (Directory.Exists("C:\\DecompBin"))
				{
					Directory.Delete("C:\\DeCompBin", true);
				}
			});

			_isShutdownConfirmed = true;
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

			string resultData = await System.IO.File.ReadAllTextAsync(
				desktopPath,
				Encoding.UTF8);


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
