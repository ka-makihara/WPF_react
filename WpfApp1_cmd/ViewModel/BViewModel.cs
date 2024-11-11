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
        private readonly LcuCtrl lcuCtrl = new();

        public BViewModel()
        {
            LcuCommandTest.Subscribe(_ => LcuCommandTestExec());
        }

        private async void LcuCommandTestExec()
        {
            bool ret = await lcuCtrl.LCU_Version("localhost:9000");

            Console.WriteLine("LcuCommandTestExec");
        }
    }
}
