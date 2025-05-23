﻿using MaterialDesignColors.Recommended;
using Reactive.Bindings;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using WpfApp1_cmd.Models;
using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd
{
	public enum MachineType
	{
		LCU = 0,
		Machine = 1,
		Base = 2,
		Module = 3,
	}

	/// <summary>
	///  新ホストデーターベース仕様書・ModuleType一覧より
	///
	public enum ModuleType
	{
		NXT_M3          = 1,
		NXT_M6          = 2,
		NXT_M3_CONVEYOR = 11,
		NXT_M6_CONVEYOR = 12,
		NXT_M3S         = 101,
		NXT_M6S         = 102,
		NXT_M3_2        = 201,
		NXT_M6_2        = 202,
		NXT_M3_2_IHC    = 221,
		NXT_M6_2_IHC    = 222,
		NXT_M6_3I       = 232,
		NXT_M6_2SP      = 302,
		NXT_M3_2C       = 501,
		NXT_M6_2C       = 502,
		NXT_M3_2C_IHC   = 521,
		NXT_M6_2C_IHC   = 522,
		NXT_M3_3CI      = 531,
		NXT_M3_3        = 601,
		NXT_M6_3        = 602,
		NXT_M3_3C       = 701,
		NXT_M6_3C       = 702,
		NXT_M6_3L       = 902,
		NXT_M3_3S       = 1201,
		NXT_M3_3SE      = 1301,
		AIMEX_TWIN_ROBOT     = 1,
		AIMEX_SINGLE_ROBOT   = 2,
		AIMEX_TWIN_ROBOT2    = 101,
		AIMEX_SINGLE_ROBOT2  = 102,
		AIMEX_TWIN_ROBOT2S   = 401,
		AIMEX_SINGLE_ROBOT2S = 402,
		AIMEX_TWIN_ROBOT3C   = 801,
		AIMEX_SINGLE_ROBOT3C = 802,
		AIMEX_TWIN_ROBOT3    = 1101,
		AIMEX_SINGLE_ROBOT3  = 1102,
		NXT_H                = 1,
		NXT_HW               = 2,
		SFAB                 = 1001,
		SFAB_D               = 1002,
		SFAB_SH              = 1003,
		SFAB_A               = 9000,
		NXTR_1R              = 1,
		NXTR_2R              = 2,
		NXTR_2RV             = 1302,
		AIMEXR_TWIN_ROBOT    = 1,
		AIMEXR_SINGLE_ROBOT  = 2
	}

	[Flags]
	public enum MsgDlgType
	{
		OK = 1,
		CANCEL = 1 << 1,
		OK_CANCEL = OK | CANCEL,
		YES = 1 << 2,
		NO = 1 << 3,
		YES_NO = YES | NO,
		YES_NO_CANCEL = YES | NO | CANCEL,

		ICON_ERROR = 0x10,
		ICON_WARNING = ICON_ERROR << 1,
		ICON_INFO =  ICON_ERROR << 2,
		ICON_CAUTION = ICON_ERROR << 3,
		ICON_QUESTION = ICON_ERROR << 4,
	}

	public static class Define
	{
		public const string FTP_ROOT_PATH = "C:\\users\\ka.makihara\\docker\\docker_ftp\\data";
		public const string LCU_ROOT_PATH = "\\MCFiles";
		public const string MC_PERIPHERAL_PATH = "Fuji/System3/Program/Peripheral/";
		public const string UPDATE_INFO_FILE = "UpdateCommon.inf";
		public const string LOCAL_BACKUP_PATH = "C:\\users\\ka.makihara\\Backup";
		public const string TXT_ENCODING = "shift_jis";

		public const int NOT_UPDATE = 2;    // Attribute が 2 は更新しない(できない)
	}

	/// <summary>
	/// 起動時オプションを解析するクラス
	/// </summary>
	public static class ArgOptions
	{
		public static List<string> mainArgs = [];
		public static Dictionary<string, string> OptionsDic = [];
		public static List<string> optSwitches = [];

		public static void ParseArgs(string[] args)
		{
			int idx = 0;
			bool key = false;
			string optKey = "";

			while (idx < args.Length)
			{
				string arg = args[idx];
				if (arg.StartsWith("--"))
				{
					string[] opt = arg.Split("=");
					if (opt.Length == 2)
					{
						OptionsDic[opt[0]] = opt[1];
					}
					else
					{
						if (key == false)
						{
							optKey = opt[0];
						}
						else
						{
							optSwitches.Add(optKey);
							optKey = opt[0];
						}
						key = true;
					}
				}
				else
				{
					if (key == true)
					{
						OptionsDic[optKey] = arg;
						key = false;
					}
					else
					{
						mainArgs.Add(arg);
					}
				}
				idx++;
			}
			if(key == true)
			{
				optSwitches.Add(optKey);
			}
		}

		/// <summary>
		///  opt が指定されているか
		/// </summary>
		/// <param name="opt">オプション名</param>
		/// <returns></returns>
		public static bool HasSwitch(string opt)
		{
			return optSwitches.Contains(opt);
		}

		/// <summary>
		/// opt が定義されているのなら、その値を文字列として返す
		/// </summary>
		/// <param name="opt">オプション名</param>
		/// <param name="value">デフォルト値</param>
		/// <returns></returns>
		public static string GetOption(string opt, string value = null)
		{
			if (OptionsDic.ContainsKey(opt))
			{
				return OptionsDic[opt];
			}
			return value;
		}

		/// <summary>
		/// opt が定義されているのなら、その値数値としてを返す
		/// </summary>
		/// <param name="opt">オプション名</param>
		/// <param name="value">デフォルト値</param>
		/// <returns></returns>
		public static int GetOptionInt(string opt, int value)
		{
			if (OptionsDic.ContainsKey(opt))
			{
				if (int.TryParse(OptionsDic[opt], out int result) == true)
				{
					return int.Parse(OptionsDic[opt]);
				}
			}
			return value;
		}

		/// <summary>
		/// opt が定義されているのなら、その値をboolとして返す
		/// </summary>
		/// <param name="opt">オプション名</param>
		/// <param name="value">デフォルト値</param>
		/// <returns></returns>
		public static bool GetOptionBool(string opt, bool value=true)
		{
			if (OptionsDic.ContainsKey(opt))
			{
				if (bool.TryParse(OptionsDic[opt], out bool result) == true)
				{
					return bool.Parse(OptionsDic[opt]);
				}
			}
			// --opt=true/false 以外に　--opt だけの場合は true とする
			if (HasSwitch(opt))
			{
				return true;
			}
			else
			{
				//デフォルト値が指定されている場合はその値を返す
				return value;
			}
		}
	}

	public class Utility
	{
		public static long GetDirectorySize(DirectoryInfo dirinfo)
		{
			long size = 0;

			foreach (FileInfo fi in dirinfo.GetFiles())
			{
				size += fi.Length;
			}
			foreach (DirectoryInfo di in dirinfo.GetDirectories())
			{
				size += GetDirectorySize(di);
			}
			return size;
		}

		/// <summary>
		/// チェックボックスのチェック状態を取得
		/// </summary>
		/// <typeparam name="Type"></typeparam>
		/// <param name="list"></param>
		/// <returns></returns>
		public static bool? CheckState<Type>(ReactiveCollection<Type> list)
			where Type : CheckableItem
		{
			if(list.Count == 0)
			{
				return false;
			}

			int tc = list.Where(x => x.IsSelected.Value == true).Count();
			int fc = list.Where(x => x.IsSelected.Value == false).Count();

			if (tc == list.Count)
			{
				//全チェック
				return true;
			}
			else if (fc == list.Count)
			{
				//全、未チェック
				return false;
			}
			else
			{
				//一部チェック
				return null;
			}
		}

		public static bool? CheckState<Type>(List<Type> list)
			where Type : CheckableItem
		{
			int tc = list.Where(x => x.IsSelected.Value == true).Count();
			int fc = list.Where(x => x.IsSelected.Value == false).Count();

			if (tc == list.Count)
			{
				//全チェック
				return true;
			}
			else if (fc == list.Count)
			{
				//全、未チェック
				return false;
			}
			else
			{
				//一部チェック
				return null;
			}
		}
	}

	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		[DllImport("user32.dll")]
		private static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport("user32.dll")]
		private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
		[DllImport("user32.dll")]
		private static extern bool IsIconic(IntPtr hWnd);

		private static Mutex mutex;

		public static void ChangeCulture(string cultureName)
		{
			CultureInfo culture = new CultureInfo(cultureName);
			Thread.CurrentThread.CurrentCulture = culture;
			Thread.CurrentThread.CurrentUICulture = culture;

			// リソースを再読み込み
			foreach (Window window in Current.Windows)
			{
				if (window.DataContext is ViewModelBase viewModel)
				{
					viewModel.OnPropertyChanged(null);
				}
			}
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			if (e.Args.Length > 0)
			{
				ArgOptions.ParseArgs(e.Args);
			}

			// Windowsのカルチャを取得して設定(--langオプションが指定されている場合はその言語を設定)
			CultureInfo currentCulture = CultureInfo.InstalledUICulture;

			ChangeCulture( ArgOptions.GetOption("--lang", currentCulture.Name) );
			//ChangeCulture(currentCulture.Name);
            //ChangeCulture("ja-JP");
            //ChangeCulture("en-US");

			// 二重起動を防止
			bool createdNew;
			mutex = new Mutex(true, "WpfApp1_cmd", out createdNew);

			if( !createdNew)
			{
				Process prevProcess = GetPreviousProcess();
				if (prevProcess != null)
				{
					IntPtr hWnd = prevProcess.MainWindowHandle;
					if (IsIconic(hWnd))
					{
						ShowWindowAsync(hWnd, 9);
					}
					SetForegroundWindow(hWnd);
				}
				this.Shutdown();
				return;
			}
			// .NET(Core系)は デフォルトで shift-jis(sjis) に対応したエンコーディングプロバイダーが登録されていないので、追加する
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

			MainWindow mainWindow = new MainWindow();
			mainWindow.Show();
		}

		public static Process GetPreviousProcess()
		{
			Process curProcess = Process.GetCurrentProcess();
			string currentPath = curProcess.MainModule.FileName;

			Process[] processes = Process.GetProcessesByName(curProcess.ProcessName);

			foreach (Process checkProcess in processes)
			{
				if (checkProcess.Id != curProcess.Id)
				{
					string checkPath;
					try
					{
						checkPath = checkProcess.MainModule.FileName;
					}
					catch (System.ComponentModel.Win32Exception)
					{
						//アクセス権限がない場合は無視
						continue;
					}

					// プロセスのフルパス名を比較して同じアプリケーションかどうかを判定
					if (String.Compare(checkPath, currentPath, true) == 0)
					{
						return checkProcess;
					}
				}
			}
			return null;
		}

		public static string GetLcuRootPath(int id)
		{
			return $"LCU_{id}";
		}
	}
}
