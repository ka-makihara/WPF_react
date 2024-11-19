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
        public const string LCU_ROOT_PATH = "\\LCU_1\\MCFiles";
    }
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
    }

}
