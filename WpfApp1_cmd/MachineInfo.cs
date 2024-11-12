using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WpfApp1_cmd.ViewModel;

namespace WpfApp1_cmd
{
    public class MachineInfo : ViewModelBase
    {
        private bool? _isSelected;
        private string? _name;
        private string? _ipAddress;
        private int _id;
        private int _pos;

        public bool? IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        public string? Name 
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        public string? IPAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int ID 
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        public int Pos
        {
            get => _pos;
            set => SetProperty(ref _pos, value);
        }
    }
}
