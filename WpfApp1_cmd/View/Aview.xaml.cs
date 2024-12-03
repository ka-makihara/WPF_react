using MaterialDesignThemes.Wpf;
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
    /// Aview.xaml の相互作用ロジック
    /// </summary>
    public partial class Aview : UserControl
    {
        public Aview()
        {
            InitializeComponent();
            CounterValue = 5;
        }

        public int CounterValue 
        {
            get { return (int)GetValue(CounterValueProperty); }
            set { SetValue(CounterValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for .  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CounterValueProperty =
            DependencyProperty.Register("CounterValue",
                typeof(int),
                typeof(Aview),
                new PropertyMetadata(0, OnCounterValueChanged));

        private static void OnCounterValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Aview control = d as Aview;
            if (control != null)
            {
                control.CounterValue = (int)e.NewValue;
            }
        }
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            CounterValue++;
            var r = DialogHost.Show(new MyMessageBox(),"DataGridView");
            await Task.Delay(5000);
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
    }
}
