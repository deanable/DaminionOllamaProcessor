using System.Configuration;
using System.Data;
using System.Windows;
using DaminionOllamaApp.Services;

namespace DaminionOllamaApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static LogService? Logger { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Logger = new LogService();
            Logger.Log("Application started.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger?.Log("Application exiting.");
            Logger?.Dispose();
            base.OnExit(e);
        }
    }
}
