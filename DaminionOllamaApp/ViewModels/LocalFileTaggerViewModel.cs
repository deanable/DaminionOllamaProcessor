// DaminionOllamaApp/ViewModels/LocalFileTaggerViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Serilog;
using System.IO;

namespace DaminionOllamaApp.ViewModels
{
    /// <summary>
    /// ViewModel for the "Local File Tagger" tab.
    /// Manages the state and logic for processing a queue of local image files.
    /// </summary>
    public class LocalFileTaggerViewModel : INotifyPropertyChanged
    {
        private static readonly ILogger Logger;
        static LocalFileTaggerViewModel()
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaminionOllamaApp", "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "localfiletaggerviewmodel.log");
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();
        }

        // --- Private Fields ---
        private string _currentOperationStatus = "Ready. Add files to begin.";
        private ObservableCollection<FileQueueItem> _filesToProcess;
        private bool _isProcessingQueue;
        private CancellationTokenSource? _cancellationTokenSource;
        private FileQueueItem? _selectedFile;
        private double _actualSpendUSD = -1;

        private readonly ProcessingService _processingService;
        private readonly SettingsService _settingsService;

        // --- Public Properties ---

        /// <summary>
        /// Holds the shared application settings, passed in from the MainViewModel.
        /// This allows the ViewModel to react to global settings changes.
        /// </summary>
        public AppSettings Settings { get; }

        // Add property for free tier alert
        public bool FreeTierExceededForSelectedModel
        {
            get
            {
                var modelName = Settings.SelectedAiProvider switch
                {
                    AiProvider.Gemma => Settings.GemmaModelName,
                    AiProvider.OpenRouter => Settings.OpenRouterModelName,
                    AiProvider.Ollama => Settings.OllamaModelName,
                    _ => null
                };
                if (string.IsNullOrEmpty(modelName)) return false;
                var usage = Settings.GetOrCreateModelUsage(modelName);
                return usage.FreeTierExceeded;
            }
        }

        public double ActualSpendUSD
        {
            get => _actualSpendUSD;
            set { _actualSpendUSD = value; OnPropertyChanged(nameof(ActualSpendUSD)); OnPropertyChanged(nameof(ShowActualSpendAlert)); }
        }

        public bool ShowActualSpendAlert
        {
            get
            {
                // Use actual spend if available, else fallback to estimate
                double freeTier = GetFreeTierForSelectedModel();
                return ActualSpendUSD >= 0 && ActualSpendUSD > freeTier;
            }
        }

        public ICommand RefreshActualSpendCommand { get; }

        /// <summary>
        /// A collection of files that are queued for processing. This is bound to the ListView in the UI.
        /// </summary>
        public ObservableCollection<FileQueueItem> FilesToProcess
        {
            get => _filesToProcess;
            set
            {
                _filesToProcess = value;
                OnPropertyChanged(nameof(FilesToProcess));
            }
        }

        /// <summary>
        /// The currently selected file in the ListView.
        /// </summary>
        public FileQueueItem? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (_selectedFile != value)
                {
                    _selectedFile = value;
                    OnPropertyChanged(nameof(SelectedFile));
                    // Notify that the "Remove Selected" command's executability might have changed.
                    ((RelayCommand)RemoveSelectedFileCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// A status message displayed to the user, indicating the current operation or result.
        /// </summary>
        public string CurrentOperationStatus
        {
            get => _currentOperationStatus;
            set
            {
                _currentOperationStatus = value;
                OnPropertyChanged(nameof(CurrentOperationStatus));
            }
        }

        /// <summary>
        /// A flag indicating whether the processing queue is currently active.
        /// Used to enable/disable UI controls.
        /// </summary>
        public bool IsProcessingQueue
        {
            get => _isProcessingQueue;
            set
            {
                if (_isProcessingQueue != value)
                {
                    _isProcessingQueue = value;
                    OnPropertyChanged(nameof(IsProcessingQueue));
                    // When this property changes, update the state of all related commands.
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged();
                        ((RelayCommand)StopQueueCommand).RaiseCanExecuteChanged();
                        ((RelayCommand)AddFilesCommand).RaiseCanExecuteChanged();
                        ((RelayCommand)RemoveSelectedFileCommand).RaiseCanExecuteChanged();
                        ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged();
                        ((RelayCommand)ClearAllFilesCommand).RaiseCanExecuteChanged();
                    });
                }
            }
        }

        // --- Commands ---
        public ICommand AddFilesCommand { get; }
        public ICommand StartQueueCommand { get; }
        public ICommand StopQueueCommand { get; }
        public ICommand RemoveSelectedFileCommand { get; }
        public ICommand ClearProcessedFilesCommand { get; }
        public ICommand ClearAllFilesCommand { get; }

        /// <summary>
        /// Initializes a new instance of the LocalFileTaggerViewModel.
        /// </summary>
        /// <param name="settings">The shared AppSettings instance.</param>
        /// <param name="settingsService">The service for loading/saving settings.</param>
        public LocalFileTaggerViewModel(AppSettings settings, SettingsService settingsService)
        {
            // Store the shared settings instance
            Settings = settings;
            _settingsService = settingsService;
            _filesToProcess = new ObservableCollection<FileQueueItem>();
            _processingService = new ProcessingService();

            // Initialize commands
            AddFilesCommand = new RelayCommand(param => AddFiles(), param => CanAddFiles());
            StartQueueCommand = new RelayCommand(async param => await StartQueueAsync(), param => CanStartQueue());
            StopQueueCommand = new RelayCommand(param => StopQueue(), param => CanStopQueue());
            RemoveSelectedFileCommand = new RelayCommand(param => RemoveSelectedFile(), param => CanRemoveSelectedFile());
            ClearProcessedFilesCommand = new RelayCommand(param => ClearProcessedFiles(), param => CanClearProcessedFiles());
            ClearAllFilesCommand = new RelayCommand(param => ClearAllFiles(), param => CanClearAllFiles());
            RefreshActualSpendCommand = new AsyncRelayCommand(_ => RefreshActualSpendAsync());
        }

        private bool CanAddFiles() => !IsProcessingQueue;
        private void AddFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.bmp; *.gif; *.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|All files (*.*)|*.*",
                Title = "Select Image Files"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                int filesAddedCount = 0;
                foreach (string filePath in openFileDialog.FileNames)
                {
                    if (!FilesToProcess.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        FilesToProcess.Add(new FileQueueItem(filePath));
                        filesAddedCount++;
                    }
                }
                CurrentOperationStatus = $"{filesAddedCount} file(s) added to the queue. {FilesToProcess.Count} total.";
                // Update command states after modifying the list
                ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ClearAllFilesCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged();
                LogFileQueueChange("AddFiles", new { FileCount = filesAddedCount });
            }
        }

        private bool CanStartQueue()
        {
            return !IsProcessingQueue && FilesToProcess.Any(f => f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error);
        }

        private async Task StartQueueAsync()
        {
            IsProcessingQueue = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            CurrentOperationStatus = "Processing queue...";
            int processedCount = 0;
            int errorCount = 0;

            var itemsToProcessThisRun = FilesToProcess
                                        .Where(f => f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error)
                                        .ToList();
            foreach (var item in itemsToProcessThisRun)
            {
                if (token.IsCancellationRequested)
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Queue stopped.";
                    break;
                }
                item.Status = ProcessingStatus.Queued;
                item.StatusMessage = "Waiting for processing...";

                // Use the shared Settings property directly
                await _processingService.ProcessLocalFileAsync(item, Settings, UpdateOverallStatus, token);

                if (item.Status == ProcessingStatus.Processed) processedCount++;
                else if (item.Status == ProcessingStatus.Error || item.Status == ProcessingStatus.Cancelled) errorCount++;
            }

            IsProcessingQueue = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            string summary = $"Queue finished. Processed: {processedCount}, Errors/Cancelled: {errorCount}.";
            CurrentOperationStatus = summary;
            UpdateOverallStatus(summary);
            ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged();
            LogFileQueueChange("StartQueueAsync", new { ProcessedCount = processedCount, ErrorCount = errorCount });
        }

        private void UpdateOverallStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentOperationStatus = message;
            });
        }

        private bool CanStopQueue() => IsProcessingQueue;
        private void StopQueue()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                CurrentOperationStatus = "Stop request received. Finishing current item then stopping...";
                _cancellationTokenSource.Cancel();
                LogFileQueueChange("StopQueue");
            }
        }

        private bool CanRemoveSelectedFile() => SelectedFile != null && !IsProcessingQueue;
        private void RemoveSelectedFile()
        {
            if (SelectedFile != null)
            {
                FilesToProcess.Remove(SelectedFile);
                SelectedFile = null; // Clear selection
                CurrentOperationStatus = "Selected file removed.";
                ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ClearAllFilesCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged();
                LogFileQueueChange("RemoveSelectedFile", SelectedFile);
            }
        }

        private bool CanClearProcessedFiles() => FilesToProcess.Any(f => f.Status == ProcessingStatus.Processed) && !IsProcessingQueue;
        private void ClearProcessedFiles()
        {
            var processedFiles = FilesToProcess.Where(f => f.Status == ProcessingStatus.Processed).ToList();
            foreach (var file in processedFiles)
            {
                FilesToProcess.Remove(file);
            }
            CurrentOperationStatus = $"{processedFiles.Count} processed file(s) cleared.";
            ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearAllFilesCommand).RaiseCanExecuteChanged();
            LogFileQueueChange("ClearProcessedFiles", new { Count = processedFiles.Count });
        }

        private bool CanClearAllFiles() => FilesToProcess.Any() && !IsProcessingQueue;
        private void ClearAllFiles()
        {
            int count = FilesToProcess.Count;
            FilesToProcess.Clear();
            SelectedFile = null; // Clear selection as well
            CurrentOperationStatus = $"{count} file(s) cleared from the queue.";
            ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearAllFilesCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged();
            LogFileQueueChange("ClearAllFiles", new { Count = count });
        }

        private async Task RefreshActualSpendAsync()
        {
            string provider = Settings.SelectedAiProvider switch
            {
                AiProvider.Gemma => "Google (Gemma/BigQuery)",
                AiProvider.OpenRouter => "OpenRouter",
                AiProvider.Ollama => "Ollama",
                _ => "Unknown Provider"
            };
            Logger.Information("Billing fetch initiated. Provider: {Provider}", provider);
            CurrentOperationStatus = $"Fetching spend from {provider}...";
            if (string.IsNullOrWhiteSpace(Settings.BigQueryProjectId) ||
                string.IsNullOrWhiteSpace(Settings.BigQueryDataset) ||
                string.IsNullOrWhiteSpace(Settings.BigQueryTable) ||
                string.IsNullOrWhiteSpace(Settings.GemmaServiceAccountJsonPath))
            {
                Logger.Warning("Billing fetch failed: Missing BigQuery settings.");
                ActualSpendUSD = -1;
                CurrentOperationStatus = $"Billing fetch failed: Missing BigQuery settings.";
                return;
            }
            var client = new BigQueryBillingClient(Settings.BigQueryProjectId, Settings.BigQueryDataset, Settings.BigQueryTable, Settings.GemmaServiceAccountJsonPath);
            try
            {
                double spend = await client.GetCurrentMonthSpendUSDAsync();
                ActualSpendUSD = spend;
                Logger.Information("Billing fetch succeeded. Spend: ${Spend:F2}", spend);
                CurrentOperationStatus = $"Current Google Cloud spend: ${spend:F2}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Billing fetch failed.");
                ActualSpendUSD = -1;
                CurrentOperationStatus = $"Billing fetch failed: {ex.Message}";
            }
        }

        private double GetFreeTierForSelectedModel()
        {
            var modelName = Settings.SelectedAiProvider switch
            {
                AiProvider.Gemma => Settings.GemmaModelName,
                AiProvider.OpenRouter => Settings.OpenRouterModelName,
                AiProvider.Ollama => Settings.OllamaModelName,
                _ => null
            };
            if (string.IsNullOrEmpty(modelName)) return 0;
            if (ModelPricingTable.Pricing.TryGetValue(modelName, out var pricing))
            {
                // Free tier is for input tokens; convert to $ using input token price
                return (pricing.FreeInputTokens / 1000.0) * pricing.PricePer1KInputTokens;
            }
            return 0;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Example: Log when files are added to the queue
        private void LogFileQueueChange(string action, object? details = null)
        {
            Logger.Information("File queue action: {Action}, Details: {@Details}", action, details);
        }
    }
}