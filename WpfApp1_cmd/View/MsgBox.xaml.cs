using MaterialDesignThemes.Wpf;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1_cmd.View
{
    /// <summary>
    /// MsgBox.xaml の相互作用ロジック
    /// </summary>
    public partial class MsgBox : UserControl
    {
        public ReactiveCommand Command { get; } = new ReactiveCommand();
        public MsgBox()
        {
            InitializeComponent();
        }

        public static Task<object?> Show(string title, string text, string detail, string info, int ty, string identifier)
        {
            var dialog = new MsgBox();
            BitmapImage icon;

            MsgDlgType iconType = (MsgDlgType)(ty & 0xF0);

            switch ( iconType )
            {
                case MsgDlgType.ICON_ERROR:   icon = new BitmapImage(new Uri("pack://application:,,,/Resources/error_32.png")); break;
                case MsgDlgType.ICON_WARNING: icon = new BitmapImage(new Uri("pack://application:,,,/Resources/warning_32.png")); break;
                case MsgDlgType.ICON_INFO:    icon = new BitmapImage(new Uri("pack://application:,,,/Resources/info_32.png")); break;
                case MsgDlgType.ICON_CAUTION: icon = new BitmapImage(new Uri("pack://application:,,,/Resources/caution_32.png")); break;
                default:
                    icon = new BitmapImage(new Uri("pack://application:,,,/Resources/info_32.png")); break;
            }

            dialog.DataContext = new { DialogTitle = title, DialogText = text, DialogDetail = detail, DialogInfo=info, DialogIcon=icon };

            
            MsgDlgType btnType = (MsgDlgType)(ty & 0x0F);

            Button btnOk     = dialog.Buttons.Children[0] as Button;
            Button btnYes    = dialog.Buttons.Children[1] as Button;
            Button btnNo     = dialog.Buttons.Children[2] as Button;
            Button btnCancel = dialog.Buttons.Children[3] as Button;

            if( ((ty & 0x0F) & (int)MsgDlgType.OK) == 0)
            {
                dialog.Buttons.Children.Remove(btnOk);
            }
            if( ((ty & 0x0F) & (int)MsgDlgType.YES) == 0)
            {
                dialog.Buttons.Children.Remove(btnYes);
            }
            if( ((ty & 0x0F) & (int)MsgDlgType.NO) == 0)
            {
                dialog.Buttons.Children.Remove(btnNo);
            }
            if (((ty & 0x0F) & (int)MsgDlgType.CANCEL) == 0)
            {
                dialog.Buttons.Children.Remove(btnCancel);
            }

            return DialogHost.Show(dialog,identifier);
        }
    }
}
