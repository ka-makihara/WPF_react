using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
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
using System.Windows.Shapes;
using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd.Windows
{
	/// <summary>
	/// TransferResultWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class TransferResultWindow : MetroWindow
	{
		public TransferResultWindow(string resultData)
		{
			InitializeComponent();
			DataContext = new TransferResultWindowViewModel(resultData);

			// ViewModel の CloseAction に Window の Close メソッドをバインド
			((TransferResultWindowViewModel)DataContext).CloseAction = () => this.Close();
		}

		private async void ClickMeOnClick(object sender, RoutedEventArgs e)
        {
            await this.ShowMessageAsync("Title Template Test", "Thx for using MahApps.Metro!!!");
        }

		public void Launch()
		{
			this.Owner = Application.Current.MainWindow;
			if (this.WindowState == WindowState.Minimized)
			{
				this.WindowState = WindowState.Normal;
			}
			this.Show();
		}
	}
}
