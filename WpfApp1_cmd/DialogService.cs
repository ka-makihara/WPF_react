using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using MahApps.Metro.Controls.Dialogs;
using MaterialDesignThemes.Wpf;

using WpfApp1_cmd.ViewModel;
using WpfApp1_cmd.View;

namespace WpfApp1_cmd
{
    public class DialogService : IDialogService
    {
        public readonly string _identifier;

        public DialogService(string identifier)
        {
            _identifier = identifier;
        }

        public async Task<bool> Question(string message)
        {
            WpfApp1_cmd.View.MessageDialog dialog = new()
            {
                DataContext = new MessageDialogViewModel
                {
                    Message = message,
                }
            };
            object result = await DialogHost.Show(dialog, _identifier);
            return (result is bool selectedResult) && selectedResult;
        }
    }
}
