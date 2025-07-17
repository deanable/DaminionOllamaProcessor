using System;
using System.IO;

namespace DaminionOllamaApp.Services
{
    /// <summary>
    /// Provides simple file-based logging functionality for the application.
    /// Creates a new log file in the application's logs directory for each session.
    /// </summary>
    public class LogService : IDisposable
    {
        /// <summary>
        /// The full path to the log file.
        /// </summary>
        private readonly string _logFilePath;
        /// <summary>
        /// The StreamWriter used to write log entries to the file.
        /// </summary>
        private readonly StreamWriter _writer;
        /// <summary>
        /// Indicates whether the object has been disposed.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogService"/> class, creating a new log file.
        /// </summary>
        public LogService()
        {
            // Use the application's base directory for logs
            string appRoot = AppDomain.CurrentDomain.BaseDirectory;
            string logsFolder = Path.Combine(appRoot, "logs");
            Directory.CreateDirectory(logsFolder); // Ensure the logs directory exists
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string logFileName = $"log-{timestamp}.txt";
            _logFilePath = Path.Combine(logsFolder, logFileName);
            _writer = new StreamWriter(_logFilePath, append: false) { AutoFlush = true };
            Log($"Log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        /// <summary>
        /// Writes a log entry with a timestamp to the log file.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            _writer.WriteLine(logEntry);
        }

        /// <summary>
        /// Disposes the log service and releases file resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _writer?.Dispose();
                _disposed = true;
            }
        }
    }
} 