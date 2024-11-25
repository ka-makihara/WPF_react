using Reactive.Bindings;
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
        private ReactiveCollection<UpdateInfo> _updates;
        public ReactiveCollection<UpdateInfo> Updates
        {
            get => _updates;
            set
            {
                _updates = value;
                SetProperty(ref _updates, value);
            }
        }
        public UpdateVersionViewModel(ReactiveCollection<UpdateInfo> updates)
        {
            Updates = updates;
        }
        public void UpdateItems(ReactiveCollection<UpdateInfo> updates)
        {
            if( Updates != null)
            {
                Updates.Clear();
            }
            Updates = updates;
        }
    }
}
