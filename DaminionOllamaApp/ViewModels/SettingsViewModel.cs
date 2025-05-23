// DaminionOllamaApp/ViewModels/SettingsViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;         // For RelayCommand
using DaminionOllamaInteractionLib;    // For DaminionApiClient & OllamaApiClient
using DaminionOllamaInteractionLib.Daminion; // For DaminionGetTagsResponse, DaminionTag
using DaminionOllamaInteractionLib.Ollama;   // For OllamaListTagsResponse, OllamaModelInfo
using System;
using System.Collections.Generic;      // For EqualityComparer
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices; // For CallerMemberName
using System.Threading.Tasks;
using System.Windows;                  // For Application.Current.Dispatcher
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

        // Daminion GUID Discovery
        private bool _isDiscoveringGuids;
        private string _discoveryStatusMessage = string.Empty;

        // Ollama Settings
        private string _ollamaConnectionStatus = "Ollama connection not verified.";
        private bool _isVerifyingOllamaConnection;
        private bool _isFetchingOllamaModels;
        private ObservableCollection<string> _ollamaModels;

        // Daminion Connection Test
        private string _daminionConnectionTestStatus = "Daminion connection not verified.";
        private bool _isVerifyingDaminionConnectionTest;

        public AppSettings Settings
        {
            get => _settings;
            set { SetProperty(ref _settings, value); }
        }

        // Daminion GUID Discovery Properties
        public bool IsDiscoveringGuids
        {
            get => _isDiscoveringGuids;
            private set
            {
                if (SetProperty(ref _isDiscoveringGuids, value))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        (DiscoverTagGuidsCommand as RelayCommand)?.RaiseCanExecuteChanged());
                }
            }
        }

        public string DiscoveryStatusMessage
        {
            get => _discoveryStatusMessage;
            private set { SetProperty(ref _discoveryStatusMessage, value); }
        }

        // Ollama Settings Properties
        public string OllamaConnectionStatus
        {
            get => _ollamaConnectionStatus;
            private set { SetProperty(ref _ollamaConnectionStatus, value); }
        }

        public bool IsVerifyingOllamaConnection
        {
            get => _isVerifyingOllamaConnection;
            private set
            {
                if (SetProperty(ref _isVerifyingOllamaConnection, value))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        (VerifyOllamaConnectionCommand as RelayCommand)?.RaiseCanExecuteChanged());
                }
            }
        }

        public bool IsFetchingOllamaModels
        {
            get => _isFetchingOllamaModels;
            private set { SetProperty(ref _isFetchingOllamaModels, value); }
        }

        public ObservableCollection<string> OllamaModels
        {
            get => _ollamaModels;
            private set { SetProperty(ref _ollamaModels, value); }
        }

        public string? SelectedOllamaModelName
        {
            get => Settings?.OllamaModelName;
            set
            {
                if (Settings != null && Settings.OllamaModelName != value)
                {
                    Settings.OllamaModelName = value ?? string.Empty;
                    OnPropertyChanged(nameof(SelectedOllamaModelName));
                }
            }
        }

        // Daminion Connection Test Properties
        public string DaminionConnectionTestStatus
        {
            get => _daminionConnectionTestStatus;
            private set { SetProperty(ref _daminionConnectionTestStatus, value); }
        }

        public bool IsVerifyingDaminionConnectionTest
        {
            get => _isVerifyingDaminionConnectionTest;
            private set
            {
                if (SetProperty(ref _isVerifyingDaminionConnectionTest, value))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        (TestDaminionConnectionCommand as RelayCommand)?.RaiseCanExecuteChanged());
                }
            }
        }

        // Commands
        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DiscoverTagGuidsCommand { get; }
        public ICommand VerifyOllamaConnectionCommand { get; } // For Ollama
        public ICommand TestDaminionConnectionCommand { get; } // For Daminion

        // Actions for View Interaction
        public Action? CloseAction { get; set; }
        public Action<string>? UpdatePasswordBoxAction { get; set; }

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            _settings = _settingsService.LoadSettings();
            _ollamaModels = new ObservableCollection<string>();

            SaveCommand = new RelayCommand(param => SaveSettings());
            CloseCommand = new RelayCommand(param => Close());
            DiscoverTagGuidsCommand = new RelayCommand(async param => await DiscoverTagGuidsAsync(), param => CanDiscoverTagGuids());
            VerifyOllamaConnectionCommand = new RelayCommand(async param => await VerifyAndFetchOllamaModelsAsync(), param => CanVerifyOllamaConnection());
            TestDaminionConnectionCommand = new RelayCommand(async param => await TestDaminionConnectionAsync(), param => CanTestDaminionConnection());
        }

        private void SaveSettings()
        {
            _settingsService.SaveSettings(Settings);
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
                Application.Current.Dispatcher.Invoke(() => {
                    (DiscoverTagGuidsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (TestDaminionConnectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                });
            }
        }

        // Daminion GUID Discovery Methods
        private bool CanDiscoverTagGuids()
        {
            return !IsDiscoveringGuids && Settings != null &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionServerUrl) &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionUsername) &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionPassword);
        }

        private async Task DiscoverTagGuidsAsync()
        {
            if (!CanDiscoverTagGuids()) return;
            IsDiscoveringGuids = true;
            DiscoveryStatusMessage = "Connecting to Daminion to discover GUIDs...";

            using (var tempDaminionClient = new DaminionApiClient())
            {
                try
                {
                    bool loginSuccess = await tempDaminionClient.LoginAsync(Settings.DaminionServerUrl, Settings.DaminionUsername, Settings.DaminionPassword);
                    if (loginSuccess)
                    {
                        DiscoveryStatusMessage = "Login successful. Fetching all tags...";
                        DaminionGetTagsResponse? tagsResponse = await tempDaminionClient.GetTagsAsync();
                        if (tagsResponse != null && tagsResponse.Success && tagsResponse.Data != null)
                        {
                            var tags = tagsResponse.Data;
                            int foundCount = 0;
                            List<string> notFoundNames = new List<string>();

                            var descTag = tags.FirstOrDefault(t => t.Name.Equals("Description", StringComparison.OrdinalIgnoreCase) || t.Name.Equals("Caption", StringComparison.OrdinalIgnoreCase) || t.Name.Equals("Image Description", StringComparison.OrdinalIgnoreCase));
                            if (descTag != null) { Settings.DaminionDescriptionTagGuid = descTag.Guid; foundCount++; } else { notFoundNames.Add("Description/Caption"); }

                            var keywordsTag = tags.FirstOrDefault(t => t.Name.Equals("Keywords", StringComparison.OrdinalIgnoreCase));
                            if (keywordsTag != null) { Settings.DaminionKeywordsTagGuid = keywordsTag.Guid; foundCount++; } else { notFoundNames.Add("Keywords"); }

                            var categoriesTag = tags.FirstOrDefault(t => t.Name.Equals("Categories", StringComparison.OrdinalIgnoreCase));
                            if (categoriesTag != null) { Settings.DaminionCategoriesTagGuid = categoriesTag.Guid; foundCount++; } else { notFoundNames.Add("Categories"); }

                            var flagTag = tags.FirstOrDefault(t => t.Name.Equals("Flag", StringComparison.OrdinalIgnoreCase));
                            if (flagTag != null) { Settings.DaminionFlagTagGuid = flagTag.Guid; foundCount++; } else { notFoundNames.Add("Flag"); }

                            string report = $"Discovered {foundCount} GUID(s).";
                            if (notFoundNames.Any()) report += $" Could not find: {string.Join(", ", notFoundNames)}.";
                            DiscoveryStatusMessage = report + " Review and Save.";
                        }
                        else { DiscoveryStatusMessage = $"Failed to fetch tags: {tagsResponse?.Error ?? "Unknown error"}."; }
                    }
                    else { DiscoveryStatusMessage = "Login failed for discovery. Check credentials/URL."; }
                }
                catch (Exception ex) { DiscoveryStatusMessage = $"Error during GUID discovery: {ex.Message}"; System.Diagnostics.Debug.WriteLine($"GUID Discovery Exception: {ex}"); }
                finally { IsDiscoveringGuids = false; }
            }
        }

        // Ollama Connection and Model Fetching Methods
        private bool CanVerifyOllamaConnection()
        {
            return !IsVerifyingOllamaConnection && Settings != null && !string.IsNullOrWhiteSpace(Settings.OllamaServerUrl);
        }

        private async Task VerifyAndFetchOllamaModelsAsync()
        {
            if (!CanVerifyOllamaConnection() || Settings == null) return;
            IsVerifyingOllamaConnection = true;
            IsFetchingOllamaModels = false;
            OllamaConnectionStatus = $"Verifying Ollama connection to {Settings.OllamaServerUrl}...";

            // Clear previous models on new verification attempt
            Application.Current.Dispatcher.Invoke(() => OllamaModels.Clear());


            OllamaApiClient? tempOllamaClient = null; // Declare outside try to use in finally if needed for dispose, though 'using' is better
            try
            {
                tempOllamaClient = new OllamaApiClient(Settings.OllamaServerUrl); // Constructor takes URL
                using (tempOllamaClient) // Ensure disposal
                {
                    bool connected = await tempOllamaClient.CheckConnectionAsync();
                    if (connected)
                    {
                        OllamaConnectionStatus = "Ollama server connected. Fetching models...";
                        IsFetchingOllamaModels = true;
                        OllamaListTagsResponse? modelsResponse = await tempOllamaClient.ListLocalModelsAsync();
                        if (modelsResponse != null && modelsResponse.Models != null)
                        {
                            foreach (var modelInfo in modelsResponse.Models.OrderBy(m => m.Name))
                            {
                                OllamaModels.Add(modelInfo.Name);
                            }
                            OllamaConnectionStatus = $"{OllamaModels.Count} Ollama models found.";
                            if (!string.IsNullOrWhiteSpace(Settings.OllamaModelName) && OllamaModels.Contains(Settings.OllamaModelName))
                            {
                                SelectedOllamaModelName = Settings.OllamaModelName;
                            }
                            else if (OllamaModels.Any())
                            {
                                SelectedOllamaModelName = OllamaModels.FirstOrDefault();
                            }
                        }
                        else { OllamaConnectionStatus = "Connected, but failed to fetch models or no models found."; }
                        IsFetchingOllamaModels = false;
                    }
                    else { OllamaConnectionStatus = "Failed to connect to Ollama server. Check URL and ensure server is running."; }
                }
            }
            catch (ArgumentException ex) // From OllamaApiClient constructor if URL is bad
            {
                OllamaConnectionStatus = $"Error: Invalid Ollama Server URL - {ex.Message}";
            }
            catch (Exception ex)
            {
                OllamaConnectionStatus = $"Error interacting with Ollama: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Ollama Interaction Exception: {ex}");
            }
            finally
            {
                IsVerifyingOllamaConnection = false;
                IsFetchingOllamaModels = false;
            }
        }

        // Daminion Connection Test Methods
        private bool CanTestDaminionConnection()
        {
            return !IsVerifyingDaminionConnectionTest && Settings != null &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionServerUrl) &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionUsername) &&
                   !string.IsNullOrWhiteSpace(Settings.DaminionPassword);
        }

        private async Task TestDaminionConnectionAsync()
        {
            if (!CanTestDaminionConnection() || Settings == null) return;
            IsVerifyingDaminionConnectionTest = true;
            DaminionConnectionTestStatus = $"Testing Daminion connection to {Settings.DaminionServerUrl}...";

            using (var testDaminionClient = new DaminionApiClient())
            {
                try
                {
                    bool loginSuccess = await testDaminionClient.LoginAsync(Settings.DaminionServerUrl, Settings.DaminionUsername, Settings.DaminionPassword);
                    DaminionConnectionTestStatus = loginSuccess ? "Daminion connection successful!" : "Daminion login failed. Check credentials/URL. See Output window for DaminionApiClient logs.";
                }
                catch (Exception ex)
                {
                    DaminionConnectionTestStatus = $"Daminion connection error: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"Daminion Connection Test Exception: {ex}");
                }
                finally
                {
                    IsVerifyingDaminionConnectionTest = false;
                }
            }
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