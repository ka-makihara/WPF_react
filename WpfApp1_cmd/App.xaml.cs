using System.Configuration;
using System.Data;
using System.Windows;

namespace WpfApp1_cmd
{
    public enum MachineType
    {
        LCU = 0,
        Machine = 1,
        Base = 2,
        Module = 3,
    }

    public static class Define
    {
        public const string FTP_ROOT_PATH = "C:\\users\\ka.makihara\\docker\\docker_ftp\\data";
        public const string LCU_ROOT_PATH = "\\MCFiles";
        public const string MC_PERIPHERAL_PATH = "Fuji/System3/Program/Peripheral";
        public const string UPDATE_INFO_FILE = "/UpdateCommon.inf";
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
                            key = true;
                        }
                        else
                        {
                            optSwitches.Add(optKey);
                            key = false;
                        }
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
        public static bool GetOptionBool(string opt, bool value)
        {
            if (OptionsDic.ContainsKey(opt))
            {
                if (bool.TryParse(OptionsDic[opt], out bool result) == true)
                {
                    return bool.Parse(OptionsDic[opt]);
                }
            }
            return value;
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                Options.ParseArgs(e.Args);
            }
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }

}
