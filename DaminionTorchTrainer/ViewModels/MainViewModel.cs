using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DaminionOllamaInteractionLib;
using DaminionTorchTrainer.Models;
using DaminionTorchTrainer.Services;

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
        private string _searchOperators = "";
        private int _maxItems = 1000;
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

        public string SearchOperators
        {
            get => _searchOperators;
            set 
            { 
                if (SetProperty(ref _searchOperators, value))
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
            Console.WriteLine("[DEBUG] ExtractDataAsync called");
            try
            {
                IsExtracting = true;
                ExtractionProgress = 0;
                ExtractionTotal = 0;
                ExtractionStatus = "Initializing...";

                if (SelectedDataSource == DataSourceType.API)
                {
                    Console.WriteLine($"[DEBUG] ExtractDataAsync - MaxItems: {MaxItems}, SearchQuery: '{SearchQuery}'");
                    Status = "Extracting data from Daminion API...";
                    
                    // Create progress callback for API extraction
                    var progressCallback = new Action<int, int, string>((current, total, message) =>
                    {
                        ExtractionProgress = current;
                        ExtractionTotal = total;
                        ExtractionStatus = message;
                    });
                    
                    CurrentDataset = await Task.Run(() => _dataExtractor.ExtractTrainingDataAsync(
                        SearchQuery, 
                        SearchOperators, 
                        MaxItems,
                        progressCallback));
                    
                    Console.WriteLine($"[DEBUG] ExtractDataAsync - Extracted {CurrentDataset.Samples.Count} samples");
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

                // Create trainer
                _trainer = new TorchSharpTrainer(TrainingConfig, OnTrainingProgress);

                // Start training in background thread
                var results = await Task.Run(() => _trainer.TrainAsync(CurrentDataset, _cancellationTokenSource.Token));

                Status = $"Training completed. Final accuracy: {results.FinalValidationAccuracy:F4}";

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
            }
            catch (Exception ex)
            {
                Status = $"Error during training: {ex.Message}";
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
            }
            catch (Exception ex)
            {
                Status = $"Error stopping training: {ex.Message}";
            }
        }

        private void OnTrainingProgress(TrainingProgress progress)
        {
            CurrentProgress = progress;
            Status = $"Training - Epoch {progress.CurrentEpoch}/{progress.TotalEpochs} - " +
                    $"Loss: {progress.TrainingLoss:F4}, Acc: {progress.TrainingAccuracy:F4}";
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
                SearchOperators = RegistryService.LoadString("SearchOperators") ?? "";
                MaxItems = RegistryService.LoadInt("MaxItems", 1000);
                
                // Load training configuration
                TrainingConfig.LearningRate = (float)RegistryService.LoadDouble("LearningRate", 0.001);
                TrainingConfig.Epochs = RegistryService.LoadInt("Epochs", 100);
                TrainingConfig.BatchSize = RegistryService.LoadInt("BatchSize", 32);
                TrainingConfig.Device = RegistryService.LoadString("Device") ?? "CPU";
                
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
                RegistryService.SaveString("SearchOperators", SearchOperators);
                RegistryService.SaveInt("MaxItems", MaxItems);
                
                // Save training configuration
                RegistryService.SaveDouble("LearningRate", (double)TrainingConfig.LearningRate);
                RegistryService.SaveInt("Epochs", TrainingConfig.Epochs);
                RegistryService.SaveInt("BatchSize", TrainingConfig.BatchSize);
                RegistryService.SaveString("Device", TrainingConfig.Device);
                
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
