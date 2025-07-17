// DaminionOllamaApp/Utils/AsyncRelayCommand.cs
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DaminionOllamaApp.Utils
{
    /// <summary>
    /// An ICommand implementation that supports asynchronous execution and disables itself while running.
    /// Useful for async operations in MVVM.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        /// <summary>
        /// The async function to execute when the command is invoked.
        /// </summary>
        private readonly Func<object?, Task> _execute;
        /// <summary>
        /// Predicate to determine if the command can execute.
        /// </summary>
        private readonly Func<bool>? _canExecute;
        /// <summary>
        /// Indicates whether the command is currently executing.
        /// </summary>
        private bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRelayCommand"/> class.
        /// </summary>
        /// <param name="execute">The async function to execute.</param>
        /// <param name="canExecute">Predicate to determine if the command can execute.</param>
        public AsyncRelayCommand(Func<object?, Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns>True if the command can execute; otherwise, false.</returns>
        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute());
        }

        /// <summary>
        /// Executes the command asynchronously.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
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

        /// <summary>
        /// Raises the CanExecuteChanged event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
} 