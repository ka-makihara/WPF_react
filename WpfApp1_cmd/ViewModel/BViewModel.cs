using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WpfLcuCtrlLib;

namespace WpfApp1_cmd.ViewModel
{
    public class BViewModel : ViewModelBase
    {
        public ReactiveCommand LcuCommandTest { get; } = new ReactiveCommand();
        private readonly LcuCtrl lcuCtrl = new("localhost:9000");

        public BViewModel()
        {
            LcuCommandTest.Subscribe(_ => LcuCommandTestExec());
        }

        private async void LcuCommandTestExec()
        {
            Console.WriteLine("LcuCommandTestExec");
        }

        //public ReactiveProperty<MyCtrlData> MyData { get; } = new ReactiveProperty<MyCtrlData>(new MyCtrlData("Unit1", "1.0.0", "1.0.1"));
        private string _textValue = "Hello, World!";
        public string TextValue 
        {
            get => _textValue;
            set => _textValue=value;
        }
    }
}
