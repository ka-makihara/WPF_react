using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfApp1_cmd.Command
{
    public class DelegateCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private readonly Action action;
        private readonly Func<bool> canExecute;
        public DelegateCommand(Action action, Func<bool> canExecute = default)
        {
            this.action = action;
            this.canExecute = canExecute;
        }
        public bool CanExecute(object? parameter)
        {
            return canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            action?.Invoke();
        }

        public void DelegateCanExecute()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        } 
    }
    public class DelegateCommand<T> : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private readonly Action<T> action;
        private readonly Func<T, bool> canExecute;
        public DelegateCommand(Action<T> action, Func<T, bool> canExecute = default)
        {
            this.action = action;
            this.canExecute = canExecute;
        }
        public bool CanExecute(object? parameter)
        {
            return canExecute?.Invoke((T)parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            action?.Invoke((T)parameter);
        }

        public void DelegateCanExecute()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
