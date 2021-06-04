using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace UserTool.ViewModel
{
    class CommandBase : ICommand
    {
        private Action<object> _execute;
        private Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged;

        public CommandBase(Action<object> execute, Func<bool> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }
    }
}
