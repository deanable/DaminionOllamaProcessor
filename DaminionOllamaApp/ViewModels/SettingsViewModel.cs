// DaminionOllamaApp/ViewModels/SettingsViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;         // For RelayCommand
using DaminionOllamaInteractionLib;    // For DaminionApiClient
using DaminionOllamaInteractionLib.Daminion; // For DaminionGetTagsResponse, DaminionTag
using System;
using System.Collections.Generic;      // For EqualityComparer
using System.ComponentModel;
using System.Linq;                     // For FirstOrDefault
using System.Runtime.CompilerServices; // For CallerMemberName
using System.Threading.Tasks;
using System.Windows;                  // For Application.Current.Dispatcher
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings; // This is the live settings object being edited

        private bool _isDiscoveringGuids;
        private string _discoveryStatusMessage = string.Empty;

        public AppSettings Settings
        {
            get => _settings;
            set { SetProperty(ref _settings, value); }
        }

        public bool IsDiscoveringGuids
        {
            get => _isDiscoveringGuids;
            private set // Private setter controlled by the discovery process
            {
                if (SetProperty(ref _isDiscoveringGuids, value))
                {
                    // Ensure command's CanExecute state is re-evaluated on the UI thread
                    Application.Current.Dispatcher.Invoke(() =>
                        (DiscoverTagGuidsCommand as RelayCommand)?.RaiseCanExecuteChanged()
                    );
                }
            }
        }

        public string DiscoveryStatusMessage
        {
            get => _discoveryStatusMessage;
            private set { SetProperty(ref _discoveryStatusMessage, value); }
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DiscoverTagGuidsCommand { get; }

        // Action to be called by the View to close itself
        public Action? CloseAction { get; set; }
        // Action to be called by the View to set the password in the PasswordBox
        public Action<string>? UpdatePasswordBoxAction { get; set; }


        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            // Load a fresh copy for editing. If "Save" isn't hit, these changes are not persisted.
            // The AppSettings object itself implements INotifyPropertyChanged for its properties.
            _settings = _settingsService.LoadSettings();
            // Public 'Settings' property allows binding directly to this instance.

            SaveCommand = new RelayCommand(param => SaveSettings());
            CloseCommand = new RelayCommand(param => Close());
            DiscoverTagGuidsCommand = new RelayCommand(async param => await DiscoverTagGuidsAsync(), param => CanDiscoverTagGuids());
        }

        private void SaveSettings()
        {
            _settingsService.SaveSettings(Settings); // Save the current state of the Settings object
            CloseAction?.Invoke();
        }

        private void Close()
        {
            CloseAction?.Invoke();
        }

        public void SetDaminionPassword(string password)
        {
            if (Settings != null)
            {
                Settings.DaminionPassword = password;
            }
        }

        private bool CanDiscoverTagGuids()
        {
            return !IsDiscoveringGuids &&
                   Settings != null &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionServerUrl) &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionUsername) &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionPassword); // Password might be optional but needed for login
        }

        private async Task DiscoverTagGuidsAsync()
        {
            if (!CanDiscoverTagGuids()) return;

            IsDiscoveringGuids = true;
            DiscoveryStatusMessage = "Connecting to Daminion...";

            // DaminionApiClient implements IDisposable
            using (var tempDaminionClient = new DaminionApiClient())
            {
                try
                {
                    bool loginSuccess = await tempDaminionClient.LoginAsync(
                        Settings.DaminionServerUrl,
                        Settings.DaminionUsername,
                        Settings.DaminionPassword);

                    if (loginSuccess)
                    {
                        DiscoveryStatusMessage = "Login successful. Fetching all tags...";
                        DaminionGetTagsResponse? tagsResponse = await tempDaminionClient.GetTagsAsync();

                        if (tagsResponse != null && tagsResponse.Success && tagsResponse.Data != null)
                        {
                            var tags = tagsResponse.Data;
                            int foundCount = 0;
                            List<string> notFoundNames = new List<string>();

                            // Discover Description Tag (looking for common Daminion system names)
                            var descTag = tags.FirstOrDefault(t =>
                                t.Name.Equals("Description", StringComparison.OrdinalIgnoreCase) ||
                                t.Name.Equals("Caption", StringComparison.OrdinalIgnoreCase) || // Common alternative
                                t.Name.Equals("Image Description", StringComparison.OrdinalIgnoreCase)); // Another common one
                            if (descTag != null) { Settings.DaminionDescriptionTagGuid = descTag.Guid; foundCount++; }
                            else { notFoundNames.Add("Description/Caption"); }

                            // Discover Keywords Tag
                            var keywordsTag = tags.FirstOrDefault(t => t.Name.Equals("Keywords", StringComparison.OrdinalIgnoreCase));
                            if (keywordsTag != null) { Settings.DaminionKeywordsTagGuid = keywordsTag.Guid; foundCount++; }
                            else { notFoundNames.Add("Keywords"); }

                            // Discover Categories Tag
                            var categoriesTag = tags.FirstOrDefault(t => t.Name.Equals("Categories", StringComparison.OrdinalIgnoreCase));
                            if (categoriesTag != null) { Settings.DaminionCategoriesTagGuid = categoriesTag.Guid; foundCount++; }
                            else { notFoundNames.Add("Categories"); }

                            // Discover Flag Tag
                            var flagTag = tags.FirstOrDefault(t => t.Name.Equals("Flag", StringComparison.OrdinalIgnoreCase));
                            if (flagTag != null) { Settings.DaminionFlagTagGuid = flagTag.Guid; foundCount++; }
                            else { notFoundNames.Add("Flag"); }

                            if (foundCount > 0)
                            {
                                string discoveryReport = $"Discovered {foundCount} GUID(s). ";
                                if (notFoundNames.Any())
                                {
                                    discoveryReport += $"Could not find: {string.Join(", ", notFoundNames)}. ";
                                }
                                DiscoveryStatusMessage = discoveryReport + "Review and Save.";
                            }
                            else
                            {
                                DiscoveryStatusMessage = "Could not automatically discover any common tag GUIDs. Please enter them manually.";
                            }
                        }
                        else
                        {
                            DiscoveryStatusMessage = $"Failed to fetch tags: {tagsResponse?.Error ?? "Unknown error after login."}";
                        }
                    }
                    else
                    {
                        DiscoveryStatusMessage = "Login to Daminion failed for discovery. Check credentials/URL in settings.";
                    }
                }
                catch (Exception ex)
                {
                    DiscoveryStatusMessage = $"Error during GUID discovery: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"GUID Discovery Exception: {ex}");
                }
                finally
                {
                    IsDiscoveringGuids = false;
                }
            } // tempDaminionClient will be disposed here
        }

        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
    }
}