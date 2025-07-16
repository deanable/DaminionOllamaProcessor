// DaminionOllamaApp/ViewModels/SettingsViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion;
using DaminionOllamaInteractionLib.Ollama;
using DaminionOllamaInteractionLib.OpenRouter;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Serilog;
using System.IO;
using DaminionOllamaApp;

namespace DaminionOllamaApp.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private static readonly ILogger Logger;
        static SettingsViewModel()
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaminionOllamaApp", "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "settingsviewmodel.log");
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();
        }

        private AppSettings _settings;

        // --- Ollama Settings ---
        private string _ollamaConnectionStatus = "Ollama connection not verified.";
        private bool _isVerifyingOllamaConnection;
        private bool _isFetchingOllamaModels;
        private ObservableCollection<string> _ollamaModels;

        // --- OpenRouter Settings ---
        private string _openRouterConnectionStatus = "OpenRouter connection not verified.";
        private bool _isVerifyingOpenRouterConnection;
        private bool _isFetchingOpenRouterModels;
        private ObservableCollection<string> _openRouterModels;

        // --- Daminion Settings ---
        private bool _isDiscoveringGuids;
        private string _discoveryStatusMessage = string.Empty;
        private string _daminionConnectionTestStatus = "Daminion connection not verified.";
        private bool _isVerifyingDaminionConnectionTest;

        // --- Gemma Settings ---
        private ObservableCollection<string> _gemmaModels = new ObservableCollection<string>();
        private string _gemmaConnectionStatus = "Gemma connection not verified.";
        private bool _isVerifyingGemmaConnection;

        // --- Free Model Filter ---
        private bool _onlyShowFreeGeminiGemmaModels = false;
        public bool OnlyShowFreeGeminiGemmaModels
        {
            get => _onlyShowFreeGeminiGemmaModels;
            set
            {
                if (_onlyShowFreeGeminiGemmaModels != value)
                {
                    _onlyShowFreeGeminiGemmaModels = value;
                    OnPropertyChanged(nameof(OnlyShowFreeGeminiGemmaModels));
                }
            }
        }

        // --- GCP/BigQuery Billing Export Selectors ---
        public ObservableCollection<string> GcpProjects { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> BigQueryDatasets { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> BigQueryTables { get; } = new ObservableCollection<string>();

        private string _billingExportInstructions = string.Empty;
        public string BillingExportInstructions
        {
            get => _billingExportInstructions;
            set { _billingExportInstructions = value; OnPropertyChanged(nameof(BillingExportInstructions)); }
        }

        public AppSettings Settings
        {
            get => _settings;
            set { SetProperty(ref _settings, value); }
        }

        // --- Ollama Properties ---
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

        // --- OpenRouter Properties ---
        public string OpenRouterConnectionStatus
        {
            get => _openRouterConnectionStatus;
            private set { SetProperty(ref _openRouterConnectionStatus, value); }
        }

        public bool IsVerifyingOpenRouterConnection
        {
            get => _isVerifyingOpenRouterConnection;
            private set
            {
                if (SetProperty(ref _isVerifyingOpenRouterConnection, value))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        (VerifyOpenRouterConnectionCommand as RelayCommand)?.RaiseCanExecuteChanged());
                }
            }
        }

        public bool IsFetchingOpenRouterModels
        {
            get => _isFetchingOpenRouterModels;
            private set { SetProperty(ref _isFetchingOpenRouterModels, value); }
        }

        public ObservableCollection<string> OpenRouterModels
        {
            get => _openRouterModels;
            private set { SetProperty(ref _openRouterModels, value); }
        }

        public string? SelectedOpenRouterModelName
        {
            get => Settings?.OpenRouterModelName;
            set
            {
                if (Settings != null && Settings.OpenRouterModelName != value)
                {
                    Settings.OpenRouterModelName = value ?? string.Empty;
                    OnPropertyChanged(nameof(SelectedOpenRouterModelName));
                }
            }
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

        // --- Gemma Properties ---
        public ObservableCollection<string> GemmaModels
        {
            get => _gemmaModels;
            private set { SetProperty(ref _gemmaModels, value); }
        }

        public string? SelectedGemmaModelName
        {
            get => Settings?.GemmaModelName;
            set
            {
                if (Settings != null && Settings.GemmaModelName != value)
                {
                    Settings.GemmaModelName = value ?? string.Empty;
                    OnPropertyChanged(nameof(SelectedGemmaModelName));
                }
            }
        }

        public string GemmaConnectionStatus
        {
            get => _gemmaConnectionStatus;
            private set { SetProperty(ref _gemmaConnectionStatus, value); }
        }

        public bool IsVerifyingGemmaConnection
        {
            get => _isVerifyingGemmaConnection;
            private set
            {
                if (SetProperty(ref _isVerifyingGemmaConnection, value))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        (VerifyGemmaConnectionCommand as RelayCommand)?.RaiseCanExecuteChanged());
                }
            }
        }

        // --- Commands ---
        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DiscoverTagGuidsCommand { get; }
        public ICommand VerifyOllamaConnectionCommand { get; }
        public ICommand TestDaminionConnectionCommand { get; }
        public ICommand VerifyOpenRouterConnectionCommand { get; }
        public ICommand VerifyGemmaConnectionCommand { get; }
        public ICommand RefreshProjectsCommand { get; }
        public ICommand RefreshDatasetsCommand { get; }
        public ICommand RefreshTablesCommand { get; }
        public ICommand CheckBillingExportTableCommand { get; }

        // Actions for View Interaction
        public Action? CloseAction { get; set; }
        public Action<string>? UpdatePasswordBoxAction { get; set; }

        public SettingsViewModel(AppSettings settings)
        {
            _settings = settings;

            _ollamaModels = new ObservableCollection<string>();
            _openRouterModels = new ObservableCollection<string>();
            _gemmaModels = new ObservableCollection<string>();

            SaveCommand = new RelayCommand(param => CloseAction?.Invoke());
            CloseCommand = new RelayCommand(param => CloseAction?.Invoke());

            DiscoverTagGuidsCommand = new RelayCommand(async param => await DiscoverTagGuidsAsync(), param => CanDiscoverTagGuids());
            VerifyOllamaConnectionCommand = new RelayCommand(async param => await VerifyAndFetchOllamaModelsAsync(), param => CanVerifyOllamaConnection());
            TestDaminionConnectionCommand = new RelayCommand(async param => await TestDaminionConnectionAsync(), param => CanTestDaminionConnection());
            VerifyOpenRouterConnectionCommand = new RelayCommand(async param => await VerifyAndFetchOpenRouterModelsAsync(), param => CanVerifyOpenRouterConnection());
            VerifyGemmaConnectionCommand = new RelayCommand(async param => await VerifyAndFetchGemmaModelsAsync(), param => CanVerifyGemmaConnection());
            RefreshProjectsCommand = new AsyncRelayCommand(_ => RefreshProjectsAsync());
            RefreshDatasetsCommand = new AsyncRelayCommand(_ => RefreshDatasetsAsync());
            RefreshTablesCommand = new AsyncRelayCommand(_ => RefreshTablesAsync());
            CheckBillingExportTableCommand = new AsyncRelayCommand(_ => CheckBillingExportTableAsync());
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
                        else
                        {
                            DiscoveryStatusMessage = $"Failed to fetch tags: {tagsResponse?.Error ?? "Unknown error"}.";
                        }
                    }
                    else
                    {
                        DiscoveryStatusMessage = "Login failed for discovery. Check credentials/URL.";
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
            }
        }

        private bool CanVerifyOpenRouterConnection()
        {
            return !IsVerifyingOpenRouterConnection && Settings != null && !string.IsNullOrWhiteSpace(Settings.OpenRouterApiKey);
        }

        private async Task VerifyAndFetchOpenRouterModelsAsync()
        {
            if (!CanVerifyOpenRouterConnection() || Settings == null) return;

            IsVerifyingOpenRouterConnection = true;
            IsFetchingOpenRouterModels = true;
            OpenRouterConnectionStatus = "Verifying OpenRouter connection...";
            Application.Current.Dispatcher.Invoke(() => OpenRouterModels.Clear());

            try
            {
                using (var client = new OpenRouterApiClient(Settings.OpenRouterApiKey, Settings.OpenRouterHttpReferer))
                {
                    var modelsResponse = await client.ListModelsAsync();
                    if (modelsResponse?.Data != null)
                    {
                        var multimodalModels = modelsResponse.Data
                            .Where(m => m.Id != null && (m.Id.Contains("vision") || m.Id.Contains("claude-3") || 
                                                        m.Id.Contains("gpt-4") || m.Id.Contains("gemini")))
                            .OrderBy(m => m.Name)
                            .ToList();

                        foreach (var model in multimodalModels)
                        {
                            OpenRouterModels.Add(model.Id!);
                        }
                        OpenRouterConnectionStatus = $"{OpenRouterModels.Count} multimodal models found.";

                        if (!string.IsNullOrWhiteSpace(Settings.OpenRouterModelName) && OpenRouterModels.Contains(Settings.OpenRouterModelName))
                        {
                            SelectedOpenRouterModelName = Settings.OpenRouterModelName;
                        }
                        else if (OpenRouterModels.Any())
                        {
                            SelectedOpenRouterModelName = OpenRouterModels.FirstOrDefault();
                        }
                    }
                    else
                    {
                        OpenRouterConnectionStatus = "Failed to fetch models from OpenRouter. Check API Key.";
                    }
                }
            }
            catch (Exception ex)
            {
                OpenRouterConnectionStatus = $"Error: {ex.Message}";
                if (App.Logger != null) App.Logger.Log($"OpenRouter Verification Exception: {ex}");
                System.Diagnostics.Debug.WriteLine($"OpenRouter Verification Exception: {ex}");
            }
            finally
            {
                IsFetchingOpenRouterModels = false;
                IsVerifyingOpenRouterConnection = false;
            }
        }

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
            if (App.Logger != null) App.Logger.Log($"Verifying Ollama connection to {Settings.OllamaServerUrl}...");

            Application.Current.Dispatcher.Invoke(() => OllamaModels.Clear());

            try
            {
                using (var tempOllamaClient = new OllamaApiClient(Settings.OllamaServerUrl))
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
                        else
                        {
                            OllamaConnectionStatus = "Connected, but failed to fetch models or no models found.";
                        }
                        IsFetchingOllamaModels = false;
                    }
                    else
                    {
                        OllamaConnectionStatus = "Failed to connect to Ollama server. Check URL and ensure server is running.";
                    }
                }
            }
            catch (ArgumentException ex)
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

        private bool CanVerifyGemmaConnection()
        {
            return !IsVerifyingGemmaConnection && Settings != null && !string.IsNullOrWhiteSpace(Settings.GemmaServiceAccountJsonPath);
        }

        private async Task VerifyAndFetchGemmaModelsAsync()
        {
            if (!CanVerifyGemmaConnection() || Settings == null) return;
            IsVerifyingGemmaConnection = true;
            GemmaConnectionStatus = "Verifying Gemma credentials and loading models...";
            Application.Current.Dispatcher.Invoke(() => GemmaModels.Clear());
            try
            {
                string maskedPath = string.IsNullOrEmpty(Settings.GemmaServiceAccountJsonPath) ? "(empty)" : System.IO.Path.GetFileName(Settings.GemmaServiceAccountJsonPath);
                string logMsg = $"[Gemma] Credential/model list check: Service Account JSON: {maskedPath}";
                Logger.Information(logMsg);
                if (App.Logger != null) App.Logger.Log(logMsg);

                var client = new DaminionOllamaApp.Services.GemmaApiClient(Settings.GemmaServiceAccountJsonPath, ""); // No model name needed for listing
                string requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models (OAuth2 Bearer)";
                Logger.Information($"[Gemma] Request URL: {requestUrl}");
                if (App.Logger != null) App.Logger.Log($"[Gemma] Request URL: {requestUrl}");

                var models = await client.ListModelsAsync();
                Logger.Information($"[Gemma] ListModelsAsync returned {models?.Count ?? 0} models.");
                if (App.Logger != null) App.Logger.Log($"[Gemma] ListModelsAsync returned {models?.Count ?? 0} models.");

                // Filtering for free models if requested
                List<string> filteredModels = new List<string>();
                if (OnlyShowFreeGeminiGemmaModels && models != null)
                {
                    filteredModels = models.Where(m =>
                        m.StartsWith("models/gemini-1.5-pro") ||
                        m.StartsWith("models/gemini-1.5-flash") ||
                        m.StartsWith("models/gemma-2-9b-it") ||
                        m.StartsWith("models/gemma-2-27b-it")
                    ).ToList();
                }
                else if (models != null)
                {
                    filteredModels = models;
                }

                if (filteredModels.Count > 0)
                {
                    foreach (var model in filteredModels)
                    {
                        Logger.Information($"[Gemma] Model found: {model}");
                        if (App.Logger != null) App.Logger.Log($"[Gemma] Model found: {model}");
                        GemmaModels.Add(model);
                    }
                    GemmaConnectionStatus = $"{GemmaModels.Count} models found.";
                    // Only set SelectedGemmaModelName if the current setting is in the list, otherwise pick the first
                    if (!string.IsNullOrWhiteSpace(Settings.GemmaModelName) && GemmaModels.Contains(Settings.GemmaModelName))
                    {
                        SelectedGemmaModelName = Settings.GemmaModelName;
                    }
                    else if (GemmaModels.Any())
                    {
                        SelectedGemmaModelName = GemmaModels.FirstOrDefault();
                    }
                }
                else
                {
                    Logger.Warning("[Gemma] Failed to fetch models from Gemma. Check Service Account JSON or filter.");
                    if (App.Logger != null) App.Logger.Log("[Gemma] Failed to fetch models from Gemma. Check Service Account JSON or filter.");
                    GemmaConnectionStatus = "Failed to fetch models from Gemma. Check Service Account JSON or filter.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[Gemma] Exception during credential/model check: {ex.Message}");
                if (App.Logger != null) App.Logger.Log($"[Gemma] Exception during credential/model check: {ex}");
                GemmaConnectionStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsVerifyingGemmaConnection = false;
            }
        }

        private async Task RefreshProjectsAsync()
        {
            GcpProjects.Clear();
            if (string.IsNullOrWhiteSpace(Settings.GemmaServiceAccountJsonPath)) return;
            var projects = await GoogleCloudResourceHelper.ListProjectsAsync(Settings.GemmaServiceAccountJsonPath);
            foreach (var p in projects) GcpProjects.Add(p);
        }
        private async Task RefreshDatasetsAsync()
        {
            BigQueryDatasets.Clear();
            if (string.IsNullOrWhiteSpace(Settings.GemmaServiceAccountJsonPath) || string.IsNullOrWhiteSpace(Settings.BigQueryProjectId)) return;
            var datasets = await GoogleCloudResourceHelper.ListDatasetsAsync(Settings.BigQueryProjectId, Settings.GemmaServiceAccountJsonPath);
            foreach (var d in datasets) BigQueryDatasets.Add(d);
        }
        private async Task RefreshTablesAsync()
        {
            BigQueryTables.Clear();
            if (string.IsNullOrWhiteSpace(Settings.GemmaServiceAccountJsonPath) || string.IsNullOrWhiteSpace(Settings.BigQueryProjectId) || string.IsNullOrWhiteSpace(Settings.BigQueryDataset)) return;
            var tables = await GoogleCloudResourceHelper.ListTablesAsync(Settings.BigQueryProjectId, Settings.BigQueryDataset, Settings.GemmaServiceAccountJsonPath);
            foreach (var t in tables) BigQueryTables.Add(t);
        }
        private async Task CheckBillingExportTableAsync()
        {
            BillingExportInstructions = string.Empty;
            if (string.IsNullOrWhiteSpace(Settings.GemmaServiceAccountJsonPath) || string.IsNullOrWhiteSpace(Settings.BigQueryProjectId) || string.IsNullOrWhiteSpace(Settings.BigQueryDataset) || string.IsNullOrWhiteSpace(Settings.BigQueryTable))
            {
                BillingExportInstructions = "Please select a project, dataset, and table.";
                return;
            }
            bool isBilling = false;
            try
            {
                isBilling = await GoogleCloudResourceHelper.IsBillingExportTableAsync(Settings.BigQueryProjectId, Settings.BigQueryDataset, Settings.BigQueryTable, Settings.GemmaServiceAccountJsonPath);
            }
            catch
            {
                BillingExportInstructions = "Could not access table. Check permissions and selections.";
                return;
            }
            if (!isBilling)
            {
                BillingExportInstructions = "The selected table is not a GCP Billing Export table.\nTo enable billing export, go to the GCP Console > Billing > Reports > Export > Enable BigQuery Export.";
            }
            else
            {
                BillingExportInstructions = "Billing export table detected.";
            }
        }

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
                LogSettingChange(propertyName, GetType().GetProperty(propertyName)?.GetValue(this));
            });
        }

        // Example: Log when settings are changed
        private void LogSettingChange(string property, object? value)
        {
            Logger.Information("Setting changed: {Property} = {Value}", property, value);
        }
    }
}