using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WpfLcuCtrlLib;

namespace WpfApp1_cmd.ViewModel
{
    internal class UpdateVersionViewModel : ViewModelBase
    {

        private ObservableCollection<UpdateInfo> _updates;
        public ObservableCollection<UpdateInfo> Updates
        {
            get => _updates;
            set
            {
                _updates = value;
                SetProperty(ref _updates, value);
            }
        }
        public UpdateVersionViewModel(ObservableCollection<UpdateInfo> updates)
        {
            Updates = updates;
        }
        public void UpdateItems(ObservableCollection<UpdateInfo> updates)
        {
            if( Updates != null)
            {
                Updates.Clear();
            }
            Updates = updates;
        }
    }
}
