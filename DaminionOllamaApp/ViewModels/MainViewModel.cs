// DaminionOllamaApp/ViewModels/MainViewModel.cs
using DaminionOllamaApp.Utils; // For RelayCommand
using DaminionOllamaApp.Views;
using System.ComponentModel;
using System.Windows; // Required for Application.Current
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ICommand OpenSettingsCommand { get; }
        public ICommand ExitCommand { get; }

        // This property will hold the ViewModel for the "Local File Tagger" tab
        public LocalFileTaggerViewModel LocalFileTaggerVM { get; }

        public MainViewModel()
        {
            OpenSettingsCommand = new RelayCommand(param => OpenSettingsWindow());
            ExitCommand = new RelayCommand(param => ExitApplication());

            // Initialize the ViewModel for the LocalFileTagger tab
            LocalFileTaggerVM = new LocalFileTaggerViewModel();
        }

        private void OpenSettingsWindow()
        {
            var settingsViewModel = new SettingsViewModel(); // Assumes SettingsViewModel is in this namespace or DaminionOllamaApp.ViewModels
            var settingsWindow = new SettingsWindow
            {
                Owner = Application.Current.MainWindow, // Set owner to the current main window
                DataContext = settingsViewModel
            };

            // Wire up the actions for the SettingsViewModel
            settingsViewModel.CloseAction = () => settingsWindow.Close();
            settingsViewModel.UpdatePasswordBoxAction = (pwd) => settingsWindow.SetPasswordBox(pwd);

            // Call the action to set the initial password in the PasswordBox
            // This should be done after the window's DataContext is set and it's about to be shown
            if (settingsViewModel.Settings != null) // Ensure settings are loaded
            {
                settingsViewModel.UpdatePasswordBoxAction?.Invoke(settingsViewModel.Settings.DaminionPassword);
            }

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