using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1_cmd.Command;

using WpfLcuCtrlLib;

namespace WpfApp1_cmd.ViewModel
{
    public class AViewModel : ViewModelBase
    {
        private int clickCount = 0;

        private IniFileParser? iniFileParser = null;

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

        public ReactiveCommand MouseEnterCommand { get; } = new ReactiveCommand();
        public ReactiveCommand MouseLeaveCommand { get; } = new ReactiveCommand();

        public AViewModel()
        {
            CountCommand = new DelegateCommand(CountExecute);

            MouseEnterCommand.Subscribe(_ => ClickCount++);
            MouseLeaveCommand.Subscribe(_ => ClickCount++);
        }
        private void CountExecute()
        {
            ClickCount++;
        }
    }
}
