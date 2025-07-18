using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DaminionOllamaApp.Utils
{
    /// <summary>
    /// Enhanced async command with cancellation support and error handling.
    /// </summary>
    public class AsyncCommand : ICommand
    {
        private readonly Func<CancellationToken, Task> _execute;
        private readonly Func<bool>? _canExecute;
        private readonly Action<Exception>? _onError;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isExecuting;

        public AsyncCommand(
            Func<CancellationToken, Task> execute, 
            Func<bool>? canExecute = null,
            Action<Exception>? onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            _cancellationTokenSource = new CancellationTokenSource();
            RaiseCanExecuteChanged();

            try
            {
                await _execute(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
            finally
            {
                _isExecuting = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                RaiseCanExecuteChanged();
            }
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}