using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
		private readonly MainWindowViewModel viewModel;

        public MainWindow()
        {
			// xaml の　<Window.DataContext>　と同じ意味
			viewModel = new MainWindowViewModel(DialogCoordinator.Instance);
			DataContext = viewModel;

			InitializeComponent();
        }
        private void LaunchGitHubSite(object sender, RoutedEventArgs e)
        {
            // Launch the GitHub site...
        }

        private void DeployCupCakes(object sender, RoutedEventArgs e)
        {
            // deploy some CupCakes...
            lineView.IsEnabled = false;
        }

        /// <summary>
        ///  Log領域の最終行にスクロールする
        ///     イベントを Command に割り当てて ReactiveCommand(in ViewModel) で実行しようと考えたが例外が発生したので
        ///     ここで実装してしまった。画面のスクロールだけなので・・・
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void logMessage_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TextBox を最終行にスクロールする
            logMessage.ScrollToEnd();
        }

    }
}