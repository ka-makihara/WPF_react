using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1_cmd.ViewModel
{
    public class CViewModel : ViewModelBase
    {
        private ObservableCollection<MySelectData> _items = [];
        public ObservableCollection<MySelectData> Items
        {
            get => _items;
            set => _items = value;
        }

        public ReactiveProperty<MySelectData> SelectedItem { get; } = new ReactiveProperty<MySelectData>();

        public CViewModel()
        {
            Items.Add(new MySelectData { Name = "Item1" });
            Items.Add(new MySelectData { Name = "Item2" });
            Items.Add(new MySelectData { Name = "Item3" });

            SelectedItem.Value = Items[0];
            SelectedItem.Subscribe(SelectedItem => SelectionChanged());
        } 
        private void SelectionChanged()
        {
            //MessageBox.Show($"SelectionChanged:{SelectedItem.Value.Name}");
        }
    }
    public class MySelectData
    {
        public string Name { get; set; }
    }
}
