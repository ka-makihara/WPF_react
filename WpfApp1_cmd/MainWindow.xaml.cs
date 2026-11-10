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
using WpfApp1_cmd.Services;

namespace WpfApp1_cmd
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
		private readonly MainWindowViewModel viewModel;
        private FlowDocument? lastLogDocument;

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

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            ((App)Application.Current).ThemeSettings.Select((ThemePreference)item.Tag);
            item.GetBindingExpression(MenuItem.IsCheckedProperty)?.UpdateTarget();
        }

        private void logMessage_TextChanged(object sender, TextChangedEventArgs e)
        {
            var log = (RichTextBox)sender;
            // Theme changes also raise TextChanged, without replacing the log document.
            if (ReferenceEquals(lastLogDocument, log.Document))
                return;

            lastLogDocument = log.Document;
            log.ScrollToEnd();
        }

    }
}