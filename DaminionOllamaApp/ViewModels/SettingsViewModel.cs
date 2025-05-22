// DaminionOllamaApp/ViewModels/SettingsViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils; // For RelayCommand
using System.ComponentModel;
using System.Windows.Input;
using System; // Required for Action

namespace DaminionOllamaApp.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

        public AppSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged(nameof(Settings));
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        // Action to be called by the View to close itself
        public Action? CloseAction { get; set; }
        // Action to be called by the View to set the password in the PasswordBox
        public Action<string>? UpdatePasswordBoxAction { get; set; }


        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            Settings = _settingsService.LoadSettings();

            // The DaminionPassword property in AppSettings will be loaded,
            // but we need to explicitly tell the PasswordBox to update.
            // This will be triggered after the View is loaded and DataContext is set.
            // We'll call UpdatePasswordBoxAction?.Invoke(Settings.DaminionPassword); from where we create this VM and show the window.

            SaveCommand = new RelayCommand(param => SaveSettings());
            CloseCommand = new RelayCommand(param => Close());
        }

        private void SaveSettings()
        {
            _settingsService.SaveSettings(Settings);
            CloseAction?.Invoke(); // Close the window after saving
        }

        private void Close()
        {
            CloseAction?.Invoke();
        }

        // Called from the View's code-behind (PasswordBox.PasswordChanged event)
        public void SetDaminionPassword(string password)
        {
            if (Settings != null)
            {
                Settings.DaminionPassword = password;
                // No OnPropertyChanged needed here for the PasswordBox itself,
                // as Settings.DaminionPassword will notify if its setter is called.
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}