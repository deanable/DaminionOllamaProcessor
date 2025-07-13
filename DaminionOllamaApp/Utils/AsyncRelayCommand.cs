// DaminionOllamaApp/Utils/AsyncRelayCommand.cs
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DaminionOllamaApp.Utils
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object?, Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
} 