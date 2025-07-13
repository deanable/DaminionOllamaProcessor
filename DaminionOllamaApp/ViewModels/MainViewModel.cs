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
    /// <summary>
    /// The main view model that serves as the central hub for the application.
    /// This class coordinates between different tagging modules and manages the global application state.
    /// It implements the MVVM pattern and provides access to:
    /// - Local file tagging functionality
    /// - Daminion collection tagging functionality  
    /// - Metadata tidy-up operations
    /// - Application settings management
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Commands
        /// <summary>
        /// Command to open the settings window for configuring application preferences.
        /// </summary>
        public ICommand OpenSettingsCommand { get; }
        
        /// <summary>
        /// Command to gracefully exit the application.
        /// </summary>
        public ICommand ExitCommand { get; }
        #endregion

        #region ViewModels
        /// <summary>
        /// View model for handling local file tagging operations.
        /// Allows users to process images from local file system with AI-generated metadata.
        /// </summary>
        public LocalFileTaggerViewModel LocalFileTaggerVM { get; }
        
        /// <summary>
        /// View model for handling Daminion collection tagging operations.
        /// Interfaces with Daminion DAM system to process cataloged images.
        /// </summary>
        public DaminionCollectionTaggerViewModel DaminionCollectionTaggerVM { get; }
        
        /// <summary>
        /// View model for metadata cleanup and organization operations.
        /// Provides tools to standardize and clean up existing metadata.
        /// </summary>
        public MetadataTidyUpViewModel MetadataTidyUpVM { get; }
        #endregion

        #region Properties
        /// <summary>
        /// The single source of truth for application settings.
        /// Contains configuration for Daminion, Ollama, OpenRouter, and other application preferences.
        /// This instance is shared across all ViewModels to ensure consistency.
        /// </summary>
        public AppSettings AppSettings { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the MainViewModel.
        /// Sets up the application's core components and loads configuration.
        /// </summary>
        public MainViewModel()
        {
            // Load application settings from storage (typically JSON file)
            var settingsService = new SettingsService();
            AppSettings = settingsService.LoadSettings();

            // Initialize commands with their respective handlers
            OpenSettingsCommand = new RelayCommand(param => OpenSettingsWindow());
            ExitCommand = new RelayCommand(param => ExitApplication());

            // Initialize child ViewModels with shared settings and services
            // Each VM gets the same AppSettings instance to ensure consistency
            LocalFileTaggerVM = new LocalFileTaggerViewModel(AppSettings, settingsService);
            DaminionCollectionTaggerVM = new DaminionCollectionTaggerViewModel(AppSettings, settingsService);
            MetadataTidyUpVM = new MetadataTidyUpViewModel(AppSettings, settingsService);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Opens the settings window as a modal dialog.
        /// Handles the configuration of application settings including:
        /// - Daminion server connection details
        /// - Ollama/OpenRouter AI service configuration
        /// - Default prompts and processing options
        /// </summary>
        private void OpenSettingsWindow()
        {
            // Create settings view model with current settings
            var settingsViewModel = new SettingsViewModel(this.AppSettings);
            
            // Create and configure the settings window
            var settingsWindow = new SettingsWindow
            {
                Owner = Application.Current.MainWindow, // Set parent for proper modal behavior
                DataContext = settingsViewModel
            };

            // Define the save/close action for the settings window
            settingsViewModel.CloseAction = () =>
            {
                // Persist the modified settings to storage
                var settingsService = new SettingsService();
                settingsService.SaveSettings(this.AppSettings);
                settingsWindow.Close();
            };

            // Configure password box handling (WPF PasswordBox requires special handling)
            settingsViewModel.UpdatePasswordBoxAction = (pwd) => settingsWindow.SetPasswordBox(pwd);
            settingsViewModel.UpdatePasswordBoxAction?.Invoke(settingsViewModel.Settings.DaminionPassword);

            // Show the settings window as a modal dialog
            settingsWindow.ShowDialog();
        }

        /// <summary>
        /// Gracefully shuts down the application.
        /// Ensures all resources are properly cleaned up before exit.
        /// </summary>
        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        /// <summary>
        /// Event raised when a property value changes.
        /// Required for WPF data binding to work properly.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}