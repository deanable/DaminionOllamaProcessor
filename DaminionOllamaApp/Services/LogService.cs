using System;
using System.IO;

namespace DaminionOllamaApp.Services
{
    public class LogService : IDisposable
    {
        private static readonly string AppName = "DaminionOllamaApp";
        private readonly string _logFilePath;
        private readonly StreamWriter _writer;
        private bool _disposed = false;

        public LogService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appSpecificFolder = Path.Combine(appDataPath, AppName);
            Directory.CreateDirectory(appSpecificFolder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string logFileName = $"log-{timestamp}.txt";
            _logFilePath = Path.Combine(appSpecificFolder, logFileName);
            _writer = new StreamWriter(_logFilePath, append: false) { AutoFlush = true };
            Log($"Log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            _writer.WriteLine(logEntry);
        }

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