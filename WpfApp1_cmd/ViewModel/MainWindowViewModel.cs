using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WpfApp1_cmd.Command;

namespace WpfApp1_cmd.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase activeView = null;

        public ViewModelBase ActiveView
        {
            get { return activeView; }
            set
            {
                if (activeView != value) {
                    activeView = value;
                    OnPropertyChanged("ActiveView");
                }
            }
        }

        private Dictionary<string, ViewModelBase> viewModeTable { get; }


        private bool flag = true;
        public bool Flag
        {
            get { return flag; }
            set { flag = value; ButtonCommand.DelegateCanExecute(); }
        }

        public DelegateCommand ButtonCommand { get; }
        public DelegateCommand<string> ScreenTransitionCommand { get; }
        public ReactiveCommand ButtonCommand2 { get; } = new ReactiveCommand();

        public MainWindowViewModel()
        {
            ButtonCommand = new DelegateCommand(async () =>
            {
                Flag = false;
                await Task.Delay(5000);
                Flag = true;
            }, canExecuteCommand);

            ScreenTransitionCommand = new DelegateCommand<string>(screenTransitionExecute);

            viewModeTable = new Dictionary<string, ViewModelBase>
            {
                { "AView", new AViewModel() },
                { "BView", new BViewModel() },
                { "CView", new CViewModel() }
            };
            ActiveView = viewModeTable["AView"];

            ButtonCommand2.Subscribe(async () =>
            {
                Flag = false;
                await Task.Delay(1000);
                Flag = true;
            });
        }

        private bool canExecuteCommand()
        {
            return Flag;
        }

        private void screenTransitionExecute(string screenName)
        {
            ActiveView = viewModeTable[screenName];
            /*
            switch (screenName)
            {
                case "AView":
                    ActiveView = new AViewModel();
                    break;
                case "BView":
                    ActiveView = new BViewModel();
                    break;
                case "CView":
                    ActiveView = new CViewModel();
                    break;
                default:
                    break;
            }
            */
        }
    }
}
