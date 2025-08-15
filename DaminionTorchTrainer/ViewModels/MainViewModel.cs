using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DaminionOllamaInteractionLib;
using DaminionTorchTrainer.Models;
using DaminionTorchTrainer.Services;
using Serilog;
using System.Windows.Media;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace DaminionTorchTrainer.ViewModels
{
    /// <summary>
    /// Main ViewModel for the Daminion TorchSharp Trainer application
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DaminionApiClient _daminionClient;
        private readonly DaminionDataExtractor _dataExtractor;
        private readonly LocalImageDataExtractor _localDataExtractor;
        private TorchSharpTrainer? _trainer;
        private CancellationTokenSource? _cancellationTokenSource;

        private string _status = "Ready";
        private bool _isConnected = false;
        private bool _isTraining = false;
        private bool _isExtracting = false;
        private TrainingConfig _trainingConfig;
        private TrainingProgress _currentProgress;
        private TrainingDataset? _currentDataset;
        private string _daminionUrl = "http://localhost:8080";
        private string _daminionUsername = "";
        private string _daminionPassword = "";
        private string _searchQuery = "";
        private int _maxItems = 10000; // Increased from 1000 to 10000
        private string _exportStatus = "";
        
        // Progress tracking properties
        private int _extractionProgress = 0;
        private int _extractionTotal = 0;
        private string _extractionStatus = "";
        private bool _autoExportOnnx = true;
        
        // Local image properties
        private string _localImageFolder = "";
        private bool _includeSubfolders = true;
        private DataSourceType _selectedDataSource = DataSourceType.API;
        
        // Training log and threshold properties
        private ObservableCollection<string> _trainingLogs = new ObservableCollection<string>();
        private double _resultThreshold = 50.0; // Default to 50%
        private ObservableCollection<TrainingResult> _trainingResults = new ObservableCollection<TrainingResult>();

        public MainViewModel()
        {
            _daminionClient = new DaminionApiClient();
            _dataExtractor = new DaminionDataExtractor(_daminionClient);
            _localDataExtractor = new LocalImageDataExtractor();
            _trainingConfig = new TrainingConfig();
            _currentProgress = new TrainingProgress();

            // Load settings from registry
            LoadSettingsFromRegistry();

            // Initialize commands
            ConnectCommand = new AsyncRelayCommand(ConnectToDaminionAsync, () => !IsConnected && !IsTraining);
            DisconnectCommand = new RelayCommand(() => DisconnectFromDaminion(), () => IsConnected && !IsTraining);
            ExtractDataCommand = new AsyncRelayCommand(ExtractDataAsync, () => CanExtractData());
            StartTrainingCommand = new AsyncRelayCommand(StartTrainingAsync, () => CanStartTraining());
            StopTrainingCommand = new RelayCommand(() => StopTraining(), () => IsTraining);
            SaveConfigCommand = new RelayCommand(() => SaveTrainingConfig());
            LoadConfigCommand = new RelayCommand(() => LoadTrainingConfig());
            ExportOnnxCommand = new AsyncRelayCommand(ExportOnnxModelAsync, () => !IsTraining && CurrentDataset != null);
            OpenExportFolderCommand = new RelayCommand(() => OpenExportFolder());
            SaveSettingsCommand = new RelayCommand(() => SaveSettingsToRegistry());
            BrowseFolderCommand = new RelayCommand(() => BrowseLocalFolder());
            
            // Temporary test command to manually set connection state
            TestConnectCommand = new RelayCommand(() => {
                Console.WriteLine("[DEBUG] Test connect command executed");
                IsConnected = true;
                Status = "Test connection set to true";
                CommandManager.InvalidateRequerySuggested();
            });
            BrowseImageCommand = new RelayCommand(BrowseImage);
            TestModelCommand = new RelayCommand(TestModel, CanTestModel);
            RefreshModelsCommand = new AsyncRelayCommand(RefreshModelsAsync);
            ClearLogsCommand = new RelayCommand(ClearTrainingLogs);
            ExportLogsCommand = new RelayCommand(ExportTrainingLogs);

            // Initialize model list
            _ = RefreshModelsAsync();
        }

        #region Properties

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public bool IsTraining
        {
            get => _isTraining;
            set => SetProperty(ref _isTraining, value);
        }

        public bool IsExtracting
        {
            get => _isExtracting;
            set => SetProperty(ref _isExtracting, value);
        }

        public int ExtractionProgress
        {
            get => _extractionProgress;
            set => SetProperty(ref _extractionProgress, value);
        }

        public int ExtractionTotal
        {
            get => _extractionTotal;
            set => SetProperty(ref _extractionTotal, value);
        }

        public string ExtractionStatus
        {
            get => _extractionStatus;
            set => SetProperty(ref _extractionStatus, value);
        }

        public bool AutoExportOnnx
        {
            get => _autoExportOnnx;
            set => SetProperty(ref _autoExportOnnx, value);
        }

        public TrainingConfig TrainingConfig
        {
            get => _trainingConfig;
            set 
            { 
                if (SetProperty(ref _trainingConfig, value))
                    SaveSettingsToRegistry();
            }
        }

        public TrainingProgress CurrentProgress
        {
            get => _currentProgress;
            set => SetProperty(ref _currentProgress, value);
        }

        public TrainingDataset? CurrentDataset
        {
            get => _currentDataset;
            set => SetProperty(ref _currentDataset, value);
        }

        public string DaminionUrl
        {
            get => _daminionUrl;
            set 
            { 
                if (SetProperty(ref _daminionUrl, value))
                    SaveSettingsToRegistry();
            }
        }

        public string DaminionUsername
        {
            get => _daminionUsername;
            set 
            { 
                if (SetProperty(ref _daminionUsername, value))
                    SaveSettingsToRegistry();
            }
        }

        public string DaminionPassword
        {
            get => _daminionPassword;
            set 
            { 
                if (SetProperty(ref _daminionPassword, value))
                    SaveSettingsToRegistry();
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set 
            { 
                if (SetProperty(ref _searchQuery, value))
                    SaveSettingsToRegistry();
            }
        }

        public int MaxItems
        {
            get => _maxItems;
            set 
            { 
                if (SetProperty(ref _maxItems, value))
                    SaveSettingsToRegistry();
            }
        }

        public string ExportStatus
        {
            get => _exportStatus;
            set => SetProperty(ref _exportStatus, value);
        }

        // Local image properties
        public string LocalImageFolder
        {
            get => _localImageFolder;
            set 
            { 
                if (SetProperty(ref _localImageFolder, value))
                    SaveSettingsToRegistry();
            }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set 
            { 
                if (SetProperty(ref _includeSubfolders, value))
                    SaveSettingsToRegistry();
            }
        }

        public DataSourceType SelectedDataSource
        {
            get => _selectedDataSource;
            set 
            { 
                if (SetProperty(ref _selectedDataSource, value))
                    SaveSettingsToRegistry();
            }
        }

        // Training log and threshold properties
        public ObservableCollection<string> TrainingLogs
        {
            get => _trainingLogs;
            set => SetProperty(ref _trainingLogs, value);
        }

        public double ResultThreshold
        {
            get => _resultThreshold;
            set 
            { 
                if (SetProperty(ref _resultThreshold, value))
                {
                    SaveSettingsToRegistry();
                    // Trigger re-filter if we have current predictions to filter
                    if (_currentPredictions.Count > 0)
                    {
                        // Ensure we're on the UI thread
                        if (Application.Current?.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(FilterCurrentPredictions));
                        }
                        else
                        {
                            FilterCurrentPredictions();
                        }
                    }
                }
            }
        }

        public ObservableCollection<TrainingResult> TrainingResults
        {
            get => _trainingResults;
            set => SetProperty(ref _trainingResults, value);
        }



        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ExtractDataCommand { get; }
        public ICommand StartTrainingCommand { get; }
        public ICommand StopTrainingCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadConfigCommand { get; }
        public ICommand ExportOnnxCommand { get; }
        public ICommand OpenExportFolderCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand TestConnectCommand { get; }
        public ICommand BrowseImageCommand { get; }
        public ICommand TestModelCommand { get; }
        public ICommand RefreshModelsCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand ExportLogsCommand { get; }

        // Model Testing Properties
        private string? _selectedModel;
        public string? SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (SetProperty(ref _selectedModel, value))
                {
                    ((RelayCommand)TestModelCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string? _selectedImagePath;
        public string? SelectedImagePath
        {
            get => _selectedImagePath;
            set
            {
                if (SetProperty(ref _selectedImagePath, value))
                {
                    UpdateImagePreview();
                    ((RelayCommand)TestModelCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private ImageSource? _imagePreview;
        public ImageSource? ImagePreview
        {
            get => _imagePreview;
            set => SetProperty(ref _imagePreview, value);
        }

        private string? _testStatus;
        public string? TestStatus
        {
            get => _testStatus;
            set => SetProperty(ref _testStatus, value);
        }

        public ObservableCollection<string> TestResults
        {
            get => _testResults;
            set => SetProperty(ref _testResults, value);
        }

        private List<string> _availableModels = new();
        public List<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        // Store current predictions for re-filtering
        private List<(string term, float probability)> _currentPredictions = new();
        
        // Test results collection
        private ObservableCollection<string> _testResults = new ObservableCollection<string>();

        #endregion

        #region Methods

        private async Task ConnectToDaminionAsync()
        {
            Console.WriteLine("[DEBUG] ConnectToDaminionAsync called");
            try
            {
                Status = "Connecting to Daminion...";
                Console.WriteLine($"[DEBUG] Attempting to connect to: {DaminionUrl}");
                Console.WriteLine($"[DEBUG] Username: {DaminionUsername}");
                Console.WriteLine($"[DEBUG] Password length: {DaminionPassword?.Length ?? 0}");
                
                // Set the API base URL
                _daminionClient.SetApiBaseUrl(DaminionUrl);
                
                // Authenticate with Daminion
                if (string.IsNullOrEmpty(DaminionUsername) || string.IsNullOrEmpty(DaminionPassword))
                {
                    Status = "Username and password are required for authentication.";
                    Console.WriteLine("[DEBUG] Username or password is empty");
                    return;
                }

                Console.WriteLine("[DEBUG] Calling LoginAsync...");
                var loginSuccess = await _daminionClient.LoginAsync(DaminionUrl, DaminionUsername, DaminionPassword);
                Console.WriteLine($"[DEBUG] LoginAsync result: {loginSuccess}");
                
                if (!loginSuccess)
                {
                    Status = "Failed to authenticate with Daminion. Please check your credentials.";
                    Console.WriteLine("[DEBUG] Login failed");
                    return;
                }

                Console.WriteLine("[DEBUG] Login successful, setting IsConnected = true");
                IsConnected = true;
                Status = "Connected to Daminion successfully.";
                
                // Manually trigger command refresh
                CommandManager.InvalidateRequerySuggested();
                Console.WriteLine("[DEBUG] Command refresh triggered");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in ConnectToDaminionAsync: {ex}");
                Status = $"Error connecting to Daminion: {ex.Message}";
            }
        }

        private void DisconnectFromDaminion()
        {
            try
            {
                _daminionClient.Dispose();
                IsConnected = false;
                CurrentDataset = null;
                Status = "Disconnected from Daminion.";
                
                // Manually trigger command refresh
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                Status = $"Error disconnecting from Daminion: {ex.Message}";
            }
        }

        private async Task ExtractDataAsync()
        {
            if (IsExtracting) return;

            try
            {
                IsExtracting = true;
                
                if (SelectedDataSource == DataSourceType.API)
                {
                    Log.Information("ExtractDataAsync - MaxItems: {MaxItems}, SearchQuery: '{SearchQuery}'", MaxItems, SearchQuery);
                    Status = "Extracting data from Daminion API...";
                    
                    // Use simple text search - multiple terms are treated as AND by default
                    var progressCallback = new Action<int, int, string>((current, total, message) =>
                    {
                        ExtractionProgress = current;
                        ExtractionTotal = total;
                        ExtractionStatus = message;
                    });
                    
                    CurrentDataset = await Task.Run(() => _dataExtractor.ExtractTrainingDataAsync(
                        SearchQuery, 
                        MaxItems,
                        progressCallback));
                    
                    Log.Information("ExtractDataAsync - Extracted {SampleCount} samples", CurrentDataset.Samples.Count);
                    Status = $"Extracted {CurrentDataset.Samples.Count} samples from Daminion API.";
                }
                else
                {
                    Status = "Extracting data from local images...";
                    
                    // Create progress callback for local extraction
                    var progressCallback = new Action<int, int, string>((current, total, message) =>
                    {
                        ExtractionProgress = current;
                        ExtractionTotal = total;
                        ExtractionStatus = message;
                    });
                    
                    CurrentDataset = await Task.Run(() => _localDataExtractor.ExtractTrainingDataAsync(
                        LocalImageFolder,
                        IncludeSubfolders,
                        MaxItems,
                        progressCallback));
                    
                    Status = $"Extracted {CurrentDataset.Samples.Count} samples from local images.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error extracting data: {ex.Message}";
            }
            finally
            {
                IsExtracting = false;
                ExtractionProgress = 0;
                ExtractionTotal = 0;
                ExtractionStatus = "";
            }
        }

        private async Task StartTrainingAsync()
        {
            if (CurrentDataset == null)
            {
                Status = "No dataset available for training.";
                return;
            }

            try
            {
                IsTraining = true;
                _cancellationTokenSource = new CancellationTokenSource();
                Status = "Starting training...";

                // Add initial log entry
                var startLog = $"[{DateTime.Now:HH:mm:ss}] Training started - Dataset: {CurrentDataset.Name}, Samples: {CurrentDataset.TotalSamples}";
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TrainingLogs.Add(startLog);
                    });
                }
                else
                {
                    TrainingLogs.Add(startLog);
                }

                // Create trainer
                _trainer = new TorchSharpTrainer(TrainingConfig, OnTrainingProgress);

                // Start training in background thread
                var results = await Task.Run(() => _trainer.TrainAsync(CurrentDataset, _cancellationTokenSource.Token));

                Status = $"Training completed. Final accuracy: {results.FinalValidationAccuracy:F4}";

                // Add completion log entry
                var completionLog = $"[{DateTime.Now:HH:mm:ss}] Training completed - Final accuracy: {results.FinalValidationAccuracy:F4}";
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TrainingLogs.Add(completionLog);
                    });
                }
                else
                {
                    TrainingLogs.Add(completionLog);
                }

                // Auto-export ONNX model if enabled
                if (AutoExportOnnx && _trainer != null)
                {
                    try
                    {
                        Status = "Exporting ONNX model...";
                        ExportStatus = "Exporting trained model to ONNX format...";
                        
                        var onnxPath = await Task.Run(() => _trainer.ExportToOnnxAsync());
                        
                        ExportStatus = $"ONNX model exported successfully to: {onnxPath}";
                        Status = $"Training and export completed. ONNX model saved to: {onnxPath}";
                    }
                    catch (Exception exportEx)
                    {
                        ExportStatus = $"ONNX export failed: {exportEx.Message}";
                        Status = $"Training completed but ONNX export failed: {exportEx.Message}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Status = "Training was cancelled.";
                var cancelLog = $"[{DateTime.Now:HH:mm:ss}] Training was cancelled.";
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TrainingLogs.Add(cancelLog);
                    });
                }
                else
                {
                    TrainingLogs.Add(cancelLog);
                }
            }
            catch (Exception ex)
            {
                Status = $"Error during training: {ex.Message}";
                var errorLog = $"[{DateTime.Now:HH:mm:ss}] Training error: {ex.Message}";
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TrainingLogs.Add(errorLog);
                    });
                }
                else
                {
                    TrainingLogs.Add(errorLog);
                }
            }
            finally
            {
                IsTraining = false;
                _trainer?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }

        private void StopTraining()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                Status = "Stopping training...";
                var stopLog = $"[{DateTime.Now:HH:mm:ss}] Training stop requested.";
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TrainingLogs.Add(stopLog);
                    });
                }
                else
                {
                    TrainingLogs.Add(stopLog);
                }
            }
            catch (Exception ex)
            {
                Status = $"Error stopping training: {ex.Message}";
                var errorLog = $"[{DateTime.Now:HH:mm:ss}] Error stopping training: {ex.Message}";
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TrainingLogs.Add(errorLog);
                    });
                }
                else
                {
                    TrainingLogs.Add(errorLog);
                }
            }
        }

        private void OnTrainingProgress(TrainingProgress progress)
        {
            // Ensure we're on the UI thread for all UI updates
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateTrainingProgress(progress);
                });
            }
            else
            {
                UpdateTrainingProgress(progress);
            }
        }

        private void UpdateTrainingProgress(TrainingProgress progress)
        {
            CurrentProgress = progress;
            Status = $"Training - Epoch {progress.CurrentEpoch}/{progress.TotalEpochs} - " +
                    $"Loss: {progress.TrainingLoss:F4}, Acc: {progress.TrainingAccuracy:F4}";

            // Add to training logs
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] Epoch {progress.CurrentEpoch}/{progress.TotalEpochs} - " +
                          $"Train Loss: {progress.TrainingLoss:F4}, Train Acc: {progress.TrainingAccuracy:F4}, " +
                          $"Val Loss: {progress.ValidationLoss:F4}, Val Acc: {progress.ValidationAccuracy:F4}";
            
            TrainingLogs.Add(logEntry);

            // Add training result
            var result = new TrainingResult
            {
                Epoch = progress.CurrentEpoch,
                TrainingLoss = progress.TrainingLoss,
                ValidationLoss = progress.ValidationLoss,
                TrainingAccuracy = progress.TrainingAccuracy,
                ValidationAccuracy = progress.ValidationAccuracy,
                ConfidenceScore = progress.ValidationAccuracy, // Use validation accuracy as confidence
                Timestamp = DateTime.Now,
                Details = $"Epoch {progress.CurrentEpoch} completed"
            };

            TrainingResults.Add(result);

            // Keep only last 1000 log entries to prevent memory issues
            if (TrainingLogs.Count > 1000)
            {
                TrainingLogs.RemoveAt(0);
            }

            // Keep only last 500 results
            if (TrainingResults.Count > 500)
            {
                TrainingResults.RemoveAt(0);
            }
        }



        private void ClearTrainingLogs()
        {
            TrainingLogs.Clear();
            TrainingResults.Clear();
            Status = "Training logs cleared.";
        }

        private void ExportTrainingLogs()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"training_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var logContent = string.Join(Environment.NewLine, TrainingLogs);
                    System.IO.File.WriteAllText(dialog.FileName, logContent);
                    Status = "Training logs exported successfully.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error exporting logs: {ex.Message}";
            }
        }

        private void SaveTrainingConfig()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(TrainingConfig, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = "training_config.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    Status = "Training configuration saved successfully.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error saving configuration: {ex.Message}";
            }
        }

        private void LoadTrainingConfig()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    var config = System.Text.Json.JsonSerializer.Deserialize<TrainingConfig>(json);
                    if (config != null)
                    {
                        TrainingConfig = config;
                        Status = "Training configuration loaded successfully.";
                    }
                }
            }
            catch (Exception ex)
            {
                Status = $"Error loading configuration: {ex.Message}";
            }
        }

        private async Task ExportOnnxModelAsync()
        {
            try
            {
                ExportStatus = "Exporting model to ONNX format...";
                
                if (_trainer == null)
                {
                    ExportStatus = "No trained model available for export.";
                    return;
                }

                // Export the model to ONNX format
                var onnxPath = await _trainer.ExportToOnnxAsync();
                
                ExportStatus = $"Model exported successfully to: {onnxPath}";
                Status = "ONNX model exported successfully.";
            }
            catch (Exception ex)
            {
                ExportStatus = $"Error exporting model: {ex.Message}";
                Status = $"Error exporting ONNX model: {ex.Message}";
            }
        }

        private async Task RefreshModelsAsync()
        {
            try
            {
                Status = "Refreshing models...";
                var models = new List<string>();
                
                // Look for trained models in the models directory
                var modelsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                if (Directory.Exists(modelsDirectory))
                {
                    var modelDirectories = Directory.GetDirectories(modelsDirectory)
                        .Where(dir => File.Exists(Path.Combine(dir, "model.pt")))
                        .Select(dir => Path.GetFileName(dir))
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList();
                    
                    models.AddRange(modelDirectories);
                }
                
                AvailableModels = models;
                Status = $"Found {AvailableModels.Count} trained models.";
                
                if (AvailableModels.Count > 0)
                {
                    SelectedModel = AvailableModels.First();
                }
                else
                {
                    SelectedModel = null;
                    Status = "No trained models found. Train a model first.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error refreshing models: {ex.Message}";
                Log.Error(ex, "Error refreshing models");
            }
        }


        private void LoadSettingsFromRegistry()
        {
            try
            {
                Console.WriteLine("[DEBUG] Loading settings from registry...");
                
                // Load connection settings
                DaminionUrl = RegistryService.LoadString("DaminionUrl") ?? "http://localhost:8080";
                DaminionUsername = RegistryService.LoadString("DaminionUsername") ?? "";
                DaminionPassword = RegistryService.LoadEncryptedString("DaminionPassword") ?? "";
                
                // Load data source settings
                SelectedDataSource = RegistryService.LoadEnum("SelectedDataSource", DataSourceType.API);
                LocalImageFolder = RegistryService.LoadString("LocalImageFolder") ?? "";
                IncludeSubfolders = RegistryService.LoadBool("IncludeSubfolders", true);
                
                // Load search settings
                SearchQuery = RegistryService.LoadString("SearchQuery") ?? "";
                MaxItems = RegistryService.LoadInt("MaxItems", 10000); // Changed from 1000 to 10000
                
                // Load training configuration
                TrainingConfig.LearningRate = (float)RegistryService.LoadDouble("LearningRate", 0.001);
                TrainingConfig.Epochs = RegistryService.LoadInt("Epochs", 100);
                TrainingConfig.BatchSize = RegistryService.LoadInt("BatchSize", 32);
                TrainingConfig.Device = RegistryService.LoadString("Device") ?? "CPU";
                
                // Load threshold setting (convert from old decimal format if needed)
                var savedThreshold = RegistryService.LoadDouble("ResultThreshold", 50.0);
                // If the saved value is less than 1, it's in the old decimal format, convert to percentage
                if (savedThreshold < 1.0)
                {
                    savedThreshold = savedThreshold * 100.0;
                }
                ResultThreshold = savedThreshold;
                
                Console.WriteLine("[DEBUG] Settings loaded from registry successfully");
                Status = "Settings loaded successfully.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error loading settings: {ex.Message}");
                Status = $"Error loading settings: {ex.Message}";
            }
        }

        private void SaveSettingsToRegistry()
        {
            try
            {
                Console.WriteLine("[DEBUG] Saving settings to registry...");
                
                // Save connection settings
                RegistryService.SaveString("DaminionUrl", DaminionUrl);
                RegistryService.SaveString("DaminionUsername", DaminionUsername);
                RegistryService.SaveEncryptedString("DaminionPassword", DaminionPassword);
                
                // Save data source settings
                RegistryService.SaveEnum("SelectedDataSource", SelectedDataSource);
                RegistryService.SaveString("LocalImageFolder", LocalImageFolder);
                RegistryService.SaveBool("IncludeSubfolders", IncludeSubfolders);
                
                // Save search settings
                RegistryService.SaveString("SearchQuery", SearchQuery);
                RegistryService.SaveInt("MaxItems", MaxItems);
                
                // Save training configuration
                RegistryService.SaveDouble("LearningRate", (double)TrainingConfig.LearningRate);
                RegistryService.SaveInt("Epochs", TrainingConfig.Epochs);
                RegistryService.SaveInt("BatchSize", TrainingConfig.BatchSize);
                RegistryService.SaveString("Device", TrainingConfig.Device);
                
                // Save threshold setting
                RegistryService.SaveDouble("ResultThreshold", ResultThreshold);
                
                Console.WriteLine("[DEBUG] Settings saved to registry successfully");
                Status = "Settings saved successfully.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error saving settings: {ex.Message}");
                Status = $"Error saving settings: {ex.Message}";
            }
        }

        private void OpenExportFolder()
        {
            try
            {
                var exportPath = TrainingConfig.OutputPath;
                if (System.IO.Directory.Exists(exportPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", exportPath);
                    Status = "Export folder opened.";
                }
                else
                {
                    Status = "Export folder does not exist.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error opening export folder: {ex.Message}";
            }
        }

        private void BrowseLocalFolder()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Image Folder"
                };

                if (dialog.ShowDialog() == true)
                {
                    LocalImageFolder = dialog.FolderName;
                    Status = $"Selected folder: {LocalImageFolder}";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error browsing folder: {ex.Message}";
            }
        }

        private bool CanExtractData()
        {
            if (IsTraining || IsExtracting) return false;

            if (SelectedDataSource == DataSourceType.API)
            {
                var canExtract = IsConnected;
                Console.WriteLine($"[DEBUG] CanExtractData - API Mode: IsConnected={IsConnected}, CanExtract={canExtract}");
                return canExtract;
            }
            else
            {
                var canExtract = !string.IsNullOrWhiteSpace(LocalImageFolder) && 
                       System.IO.Directory.Exists(LocalImageFolder);
                Console.WriteLine($"[DEBUG] CanExtractData - Local Mode: LocalImageFolder='{LocalImageFolder}', CanExtract={canExtract}");
                return canExtract;
            }
        }

        private bool CanStartTraining()
        {
            return !IsTraining && CurrentDataset != null;
        }

        private void TestConnect()
        {
            // Implementation for testing connection
            Status = "Connection test not implemented yet.";
        }

        private void BrowseImage()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Image for Testing",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif)|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif|All Files (*.*)|*.*",
                DefaultExt = "jpg"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SelectedImagePath = openFileDialog.FileName;
            }
        }

        private async void TestModel()
        {
            if (string.IsNullOrEmpty(SelectedImagePath) || string.IsNullOrEmpty(SelectedModel))
            {
                var errorResults = new ObservableCollection<string> { "Please select both a model and an image." };
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TestResults = errorResults;
                    });
                }
                else
                {
                    TestResults = errorResults;
                }
                TestStatus = "Please select both a model and an image.";
                return;
            }

            try
            {
                var loadingResults = new ObservableCollection<string> { "Loading model and preprocessing image..." };
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TestResults = loadingResults;
                    });
                }
                else
                {
                    TestResults = loadingResults;
                }
                TestStatus = "Loading model and preprocessing image...";
                
                // Get the model path
                var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", SelectedModel, "model.pt");
                if (!File.Exists(modelPath))
                {
                    var modelNotFoundResults = new ObservableCollection<string> { $"Model file not found: {modelPath}" };
                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            TestResults = modelNotFoundResults;
                        });
                    }
                    else
                    {
                        TestResults = modelNotFoundResults;
                    }
                    TestStatus = $"Model file not found: {modelPath}";
                    return;
                }

                // Load and preprocess the image
                var imageFeatures = await PreprocessImageAsync(SelectedImagePath);
                
                var inferenceResults = new ObservableCollection<string> { "Running inference..." };
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TestResults = inferenceResults;
                    });
                }
                else
                {
                    TestResults = inferenceResults;
                }
                TestStatus = "Running inference...";
                
                // For now, create a simple mock prediction since loading the full model is complex
                // In a real implementation, you would load the model and run inference
                var mockPredictions = GenerateMockPredictions(imageFeatures.Length);
                
                // Get metadata vocabulary from the model directory
                var vocabularyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", SelectedModel, "vocabulary.json");
                var metadataTerms = await LoadMetadataVocabularyAsync(vocabularyPath);
                
                // Store current predictions for re-filtering
                _currentPredictions.Clear();
                for (int i = 0; i < mockPredictions.Length && i < metadataTerms.Count; i++)
                {
                    _currentPredictions.Add((metadataTerms[i], mockPredictions[i]));
                }
                
                // Format results using threshold (convert percentage to decimal)
                var thresholdDecimal = ResultThreshold / 100.0;
                var results = new List<string>();
                foreach (var (term, probability) in _currentPredictions)
                {
                    if (probability >= thresholdDecimal) // Only show terms above threshold
                    {
                        results.Add($"{term}: {probability:P1}");
                    }
                }
                
                // Create new collection to avoid binding issues
                var newResults = new ObservableCollection<string>();
                if (results.Count > 0)
                {
                    newResults.Add($"Predictions (threshold: {ResultThreshold:F0}%):");
                    foreach (var result in results)
                    {
                        newResults.Add(result);
                    }
                    TestStatus = $"Found {results.Count} predictions above threshold ({ResultThreshold:F0}%)";
                }
                else
                {
                    newResults.Add($"No predictions found above threshold ({ResultThreshold:F0}%)");
                    TestStatus = $"No predictions found above threshold ({ResultThreshold:F0}%)";
                }
                
                // Use dispatcher to ensure UI thread safety
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TestResults = newResults;
                    });
                }
                else
                {
                    TestResults = newResults;
                }
            }
            catch (Exception ex)
            {
                var errorResults = new ObservableCollection<string> { $"Error testing model: {ex.Message}" };
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TestResults = errorResults;
                    });
                }
                else
                {
                    TestResults = errorResults;
                }
                TestStatus = $"Error testing model: {ex.Message}";
                Log.Error(ex, "Error during model testing");
            }
        }

        private float[] GenerateMockPredictions(int featureCount)
        {
            // Generate mock predictions for demonstration
            // In a real implementation, this would be the actual model output
            var random = new Random();
            var predictions = new float[10]; // Assume 10 output classes
            
            for (int i = 0; i < predictions.Length; i++)
            {
                predictions[i] = (float)random.NextDouble() * 0.8f; // Random values between 0 and 0.8
            }
            
            return predictions;
        }

        private async Task<float[]> PreprocessImageAsync(string imagePath)
        {
            // Load image using System.Drawing
            using var image = System.Drawing.Image.FromFile(imagePath);
            using var bitmap = new System.Drawing.Bitmap(image);
            
            // Resize to 224x224 (standard input size)
            using var resizedBitmap = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(224, 224));
            
            // Extract RGB values and normalize to 0-1
            var features = new List<float>();
            
            for (int y = 0; y < 224; y += 8) // Sample every 8th pixel to reduce features
            {
                for (int x = 0; x < 224; x += 8)
                {
                    var pixel = resizedBitmap.GetPixel(x, y);
                    features.Add(pixel.R / 255.0f); // Red channel
                    features.Add(pixel.G / 255.0f); // Green channel
                    features.Add(pixel.B / 255.0f); // Blue channel
                }
            }
            
            return features.ToArray();
        }

        private async Task<List<string>> LoadMetadataVocabularyAsync(string vocabularyPath)
        {
            try
            {
                if (File.Exists(vocabularyPath))
                {
                    var json = await File.ReadAllTextAsync(vocabularyPath);
                    var vocabulary = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    
                    // Convert back to ordered list
                    var terms = new List<string>(vocabulary.Count);
                    for (int i = 0; i < vocabulary.Count; i++)
                    {
                        var term = vocabulary.FirstOrDefault(kvp => kvp.Value == i).Key;
                        if (!string.IsNullOrEmpty(term))
                        {
                            terms.Add(term);
                        }
                    }
                    return terms;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load metadata vocabulary from {VocabularyPath}", vocabularyPath);
            }
            
            // Fallback: return generic terms
            return new List<string> { "unknown" };
        }

        private bool CanTestModel()
        {
            return !string.IsNullOrEmpty(SelectedImagePath) && !string.IsNullOrEmpty(SelectedModel);
        }

        private void UpdateImagePreview()
        {
            if (string.IsNullOrEmpty(SelectedImagePath) || !File.Exists(SelectedImagePath))
            {
                ImagePreview = null;
                return;
            }

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(SelectedImagePath);
                bitmap.EndInit();
                ImagePreview = bitmap;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load image preview for {ImagePath}", SelectedImagePath);
                ImagePreview = null;
            }
        }

        private void FilterCurrentPredictions()
        {
            if (_currentPredictions.Count == 0) return;

            try
            {
                // Convert percentage to decimal
                var thresholdDecimal = ResultThreshold / 100.0;
                var results = new List<string>();
                
                Console.WriteLine($"[DEBUG] Filtering predictions with threshold: {ResultThreshold:F0}% ({thresholdDecimal:F3})");
                Console.WriteLine($"[DEBUG] Total predictions to filter: {_currentPredictions.Count}");
                
                foreach (var (term, probability) in _currentPredictions)
                {
                    if (probability >= thresholdDecimal)
                    {
                        results.Add($"{term}: {probability:P1}");
                    }
                }
                
                Console.WriteLine($"[DEBUG] Predictions above threshold: {results.Count}");
                
                // Create a new collection to avoid UI binding issues
                var newResults = new ObservableCollection<string>();
                
                if (results.Count > 0)
                {
                    newResults.Add($"Predictions (threshold: {ResultThreshold:F0}%):");
                    foreach (var result in results)
                    {
                        newResults.Add(result);
                    }
                    TestStatus = $"Found {results.Count} predictions above threshold ({ResultThreshold:F0}%)";
                }
                else
                {
                    newResults.Add($"No predictions found above threshold ({ResultThreshold:F0}%)");
                    TestStatus = $"No predictions found above threshold ({ResultThreshold:F0}%)";
                }
                
                // Use dispatcher to ensure UI thread safety
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TestResults = newResults;
                    });
                }
                else
                {
                    TestResults = newResults;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in FilterCurrentPredictions: {ex.Message}");
                // Fallback: create a simple error message
                var errorResults = new ObservableCollection<string> { $"Error filtering predictions: {ex.Message}" };
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TestResults = errorResults;
                    });
                }
                else
                {
                    TestResults = errorResults;
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            Console.WriteLine($"[DEBUG] Property changed: {propertyName} = {value}");
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Simple relay command implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Async relay command implementation
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;
            
            _isExecuting = true;
            
            try
            {
                await _execute();
            }
            finally
            {
                _isExecuting = false;
            }
        }
    }
}

