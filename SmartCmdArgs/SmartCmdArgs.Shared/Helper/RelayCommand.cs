using System;
using System.Diagnostics;
using System.Windows.Input;

namespace SmartCmdArgs.Helper
{
    public class RelayCommand<T> : ICommand
    {
        #region Fields

        readonly Action<T> execute;
        readonly Predicate<T> canExecute;

        #endregion

        #region Constructors

        public RelayCommand(Action<T> execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            this.execute = execute;
            this.canExecute = canExecute;
        }
        #endregion

        #region ICommand Members

        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute((T)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            execute((T)parameter);
        }

        #endregion
    }

    public class RelayCommand : RelayCommand<object>
    {
        #region Constructors

        public RelayCommand(Action execute)
            : this(execute, null)
        {}

        public RelayCommand(Action execute, Predicate<object> canExecute)
            : base(x => execute(), canExecute) {}
        #endregion
    }

    public static class CommandExtensions
    {
        public static bool SafeExecute(this ICommand command, object obj = null)
        {
            if (!command.CanExecute(obj))
                return false;

            command.Execute(obj);
            return true;
        }
    }
}
