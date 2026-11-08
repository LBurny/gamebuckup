using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GameBackup.Plugin.UI.Infrastructure
{
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isRunning;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public event EventHandler<Exception> ExecutionFailed;

        public bool IsRunning => _isRunning;

        public bool CanExecute(object parameter)
        {
            return !_isRunning && (_canExecute == null || _canExecute());
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isRunning = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                ExecutionFailed?.Invoke(this, ex);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
