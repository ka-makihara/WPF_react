using Reactive.Bindings;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using WpfApp1_cmd.Models;

namespace WpfApp1_cmd
{
	public enum MachineType
	{
		LCU = 0,
		Machine = 1,
		Base = 2,
		Module = 3,
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
	public static class Options
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
		public static bool GetOptionBool(string opt)
		{
			if (OptionsDic.ContainsKey(opt))
			{
				if (bool.TryParse(OptionsDic[opt], out bool result) == true)
				{
					return bool.Parse(OptionsDic[opt]);
				}
			}
			// --opt=true/false 以外に　--opt だけの場合は true とする
			return HasSwitch(opt);
			//return value;
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

		private void Application_Startup(object sender, StartupEventArgs e)
		{
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

			if (e.Args.Length > 0)
			{
				Options.ParseArgs(e.Args);
			}
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
