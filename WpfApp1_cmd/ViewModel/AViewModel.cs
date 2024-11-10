using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1_cmd.Command;

namespace WpfApp1_cmd.ViewModel
{
    public class AViewModel : ViewModelBase
    {
        private int clickCount = 0;

        public int ClickCount
        {
            get { return clickCount; }
            set
            {
                if (clickCount != value)
                {
                    clickCount = value;
                    OnPropertyChanged("ClickCount");
                }
            }
        }

        public DelegateCommand CountCommand { get; }

        public AViewModel()
        {
            CountCommand = new DelegateCommand(CountExecute);
        }
        private void CountExecute()
        {
            ClickCount++;
        }
    }
}
