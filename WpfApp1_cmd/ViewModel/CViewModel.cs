using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp1_cmd.ViewModel
{
    public class CViewModel : ViewModelBase
    {
        ObservableCollection<MySelectData> Items { get; set; } = [];
        public CViewModel()
        {
            Items.Add(new MySelectData { Name = "Item1" });
            Items.Add(new MySelectData { Name = "Item2" });
            Items.Add(new MySelectData { Name = "Item3" });

            _selectedItem = Items[0];
        } 
        private MySelectData _selectedItem;

        public MySelectData SelectedItem
        {
            get => _selectedItem;
        }
         private void ComboBox_Selected(object sender, RoutedEventArgs e)
        {

        }
    }
    public class MySelectData
    {
        public string Name { get; set; }
    }
}
