using System;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace DaminionOllamaApp.Services
{
    /// <summary>
    /// Centralized error handling service for consistent error management across the application.
    /// </summary>
    public class ErrorHandlingService
    {
        private readonly ILogger _logger;

        public ErrorHandlingService(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Handles exceptions with logging and user notification.
        /// </summary>
        /// <param name="exception">The exception to handle.</param>
        /// <param name="context">Context information about where the error occurred.</param>
        /// <param name="showToUser">Whether to show the error to the user.</param>
        public void HandleException(Exception exception, string context, bool showToUser = true)
        {
            _logger.Error(exception, "Error in {Context}: {Message}", context, exception.Message);

            if (showToUser)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var userMessage = GetUserFriendlyMessage(exception);
                    MessageBox.Show(userMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Executes an action with automatic error handling.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="context">Context information.</param>
        /// <param name="onError">Optional callback for error handling.</param>
        public async Task ExecuteWithErrorHandlingAsync(Func<Task> action, string context, Action<Exception>? onError = null)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                HandleException(ex, context);
                onError?.Invoke(ex);
            }
        }

        private string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                HttpRequestException => "Network connection error. Please check your internet connection and try again.",
                TaskCanceledException => "The operation timed out. Please try again.",
                UnauthorizedAccessException => "Access denied. Please check your credentials.",
                ArgumentException => "Invalid input provided. Please check your settings.",
                _ => $"An unexpected error occurred: {exception.Message}"
            };
        }
    }
}