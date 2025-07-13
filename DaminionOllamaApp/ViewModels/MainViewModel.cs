// DaminionOllamaApp/ViewModels/MainViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;
using DaminionOllamaApp.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ICommand OpenSettingsCommand { get; }
        public ICommand ExitCommand { get; }

        public LocalFileTaggerViewModel LocalFileTaggerVM { get; }
        public DaminionCollectionTaggerViewModel DaminionCollectionTaggerVM { get; }
        public MetadataTidyUpViewModel MetadataTidyUpVM { get; }

        // This is the single source of truth for settings
        public AppSettings AppSettings { get; }

        public MainViewModel()
        {
            // Load settings once when the application starts
            var settingsService = new SettingsService();
            AppSettings = settingsService.LoadSettings();

            OpenSettingsCommand = new RelayCommand(param => OpenSettingsWindow());
            ExitCommand = new RelayCommand(param => ExitApplication());

            // Pass the single AppSettings instance to all child ViewModels
            LocalFileTaggerVM = new LocalFileTaggerViewModel(AppSettings, settingsService);
            DaminionCollectionTaggerVM = new DaminionCollectionTaggerViewModel(AppSettings, settingsService);
            MetadataTidyUpVM = new MetadataTidyUpViewModel(AppSettings, settingsService);
        }

        private void OpenSettingsWindow()
        {
            // Pass the single AppSettings instance to the SettingsViewModel
            var settingsViewModel = new SettingsViewModel(this.AppSettings);
            var settingsWindow = new SettingsWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = settingsViewModel
            };

            // Define what happens when the settings window is closed/saved
            settingsViewModel.CloseAction = () =>
            {
                // Save the (potentially modified) settings object to disk
                var settingsService = new SettingsService();
                settingsService.SaveSettings(this.AppSettings);
                settingsWindow.Close();
            };

            settingsViewModel.UpdatePasswordBoxAction = (pwd) => settingsWindow.SetPasswordBox(pwd);
            settingsViewModel.UpdatePasswordBoxAction?.Invoke(settingsViewModel.Settings.DaminionPassword);

            settingsWindow.ShowDialog();
        }

        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}