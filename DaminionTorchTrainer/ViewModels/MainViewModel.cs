using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion;
using DaminionTorchTrainer.Models;
using DaminionTorchTrainer.Services;
using Serilog;
using System.Windows.Media;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TorchSharp;

namespace DaminionTorchTrainer.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DaminionApiClient _daminionClient;
        private readonly ImageProcessor _imageProcessor;
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
        private int _maxItems = 1000;
        private string _exportStatus = "";
        
        private int _extractionProgress = 0;
        private int _extractionTotal = 0;
        private string _extractionStatus = "";
        private bool _autoExportOnnx = true;
        
        private string _localImageFolder = "";
        private bool _includeSubfolders = true;
        private DataSourceType _selectedDataSource = DataSourceType.API;
        
        public MainViewModel()
        {
            _daminionClient = new DaminionApiClient();
            _imageProcessor = new ImageProcessor();
            _dataExtractor = new DaminionDataExtractor(_daminionClient, _imageProcessor);
            _localDataExtractor = new LocalImageDataExtractor(_imageProcessor);
            _trainingConfig = new TrainingConfig();
            _currentProgress = new TrainingProgress();

            LoadSettingsFromRegistry();

            ConnectCommand = new AsyncRelayCommand(ConnectToDaminionAsync, () => !IsConnected && !IsTraining);
            ExtractDataCommand = new AsyncRelayCommand(ExtractDataAsync, () => CanExtractData());
            StartTrainingCommand = new AsyncRelayCommand(StartTrainingAsync, () => CanStartTraining());
            StopTrainingCommand = new RelayCommand(() => StopTraining(), () => IsTraining);
            ExportOnnxCommand = new AsyncRelayCommand(ExportOnnxModelAsync, () => !IsTraining && CurrentDataset != null);
            BrowseFolderCommand = new RelayCommand(BrowseLocalFolder);
        }

        #region Properties

        public string Status { get => _status; set => SetProperty(ref _status, value); }
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }
        public bool IsTraining { get => _isTraining; set => SetProperty(ref _isTraining, value); }
        public bool IsExtracting { get => _isExtracting; set => SetProperty(ref _isExtracting, value); }
        public int ExtractionProgress { get => _extractionProgress; set => SetProperty(ref _extractionProgress, value); }
        public int ExtractionTotal { get => _extractionTotal; set => SetProperty(ref _extractionTotal, value); }
        public string ExtractionStatus { get => _extractionStatus; set => SetProperty(ref _extractionStatus, value); }
        public bool AutoExportOnnx { get => _autoExportOnnx; set => SetProperty(ref _autoExportOnnx, value); }
        public TrainingConfig TrainingConfig { get => _trainingConfig; set => SetProperty(ref _trainingConfig, value); }
        public TrainingProgress CurrentProgress { get => _currentProgress; set => SetProperty(ref _currentProgress, value); }
        public TrainingDataset? CurrentDataset { get => _currentDataset; set => SetProperty(ref _currentDataset, value); }
        public string DaminionUrl { get => _daminionUrl; set => SetProperty(ref _daminionUrl, value); }
        public string DaminionUsername { get => _daminionUsername; set => SetProperty(ref _daminionUsername, value); }
        public string DaminionPassword { get => _daminionPassword; set => SetProperty(ref _daminionPassword, value); }
        public string SearchQuery { get => _searchQuery; set => SetProperty(ref _searchQuery, value); }
        public int MaxItems { get => _maxItems; set => SetProperty(ref _maxItems, value); }
        public string ExportStatus { get => _exportStatus; set => SetProperty(ref _exportStatus, value); }
        public string LocalImageFolder { get => _localImageFolder; set => SetProperty(ref _localImageFolder, value); }
        public bool IncludeSubfolders { get => _includeSubfolders; set => SetProperty(ref _includeSubfolders, value); }
        public DataSourceType SelectedDataSource { get => _selectedDataSource; set => SetProperty(ref _selectedDataSource, value); }

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand ExtractDataCommand { get; }
        public ICommand StartTrainingCommand { get; }
        public ICommand StopTrainingCommand { get; }
        public ICommand ExportOnnxCommand { get; }
        public ICommand BrowseFolderCommand { get; }

        #endregion

        #region Methods

        private async Task ConnectToDaminionAsync()
        {
            try
            {
                Status = "Connecting to Daminion...";
                _daminionClient.SetApiBaseUrl(DaminionUrl);
                
                if (string.IsNullOrEmpty(DaminionUsername) || string.IsNullOrEmpty(DaminionPassword))
                {
                    Status = "Username and password are required.";
                    return;
                }

                var loginSuccess = await _daminionClient.LoginAsync(DaminionUrl, DaminionUsername, DaminionPassword);
                
                if (!loginSuccess)
                {
                    Status = "Failed to authenticate with Daminion.";
                    return;
                }

                IsConnected = true;
                Status = "Connected to Daminion successfully.";
            }
            catch (Exception ex)
            {
                Status = $"Error connecting: {ex.Message}";
            }
        }

        private async Task ExtractDataAsync()
        {
            if (IsExtracting) return;

            try
            {
                IsExtracting = true;
                var progressCallback = new Action<int, int, string>((current, total, message) =>
                {
                    ExtractionProgress = current;
                    ExtractionTotal = total;
                    ExtractionStatus = message;
                });

                if (SelectedDataSource == DataSourceType.API)
                {
                    Status = "Extracting data from Daminion API...";
                    CurrentDataset = await Task.Run(() => _dataExtractor.ExtractTrainingDataAsync(SearchQuery, MaxItems, progressCallback));
                    Status = $"Extracted {CurrentDataset.Samples.Count} samples from Daminion API.";
                }
                else
                {
                    Status = "Extracting data from local images...";
                    CurrentDataset = await Task.Run(() => _localDataExtractor.ExtractTrainingDataAsync(LocalImageFolder, IncludeSubfolders, MaxItems, progressCallback));
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

                _trainer = new TorchSharpTrainer(TrainingConfig, (progress) =>
                {
                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => CurrentProgress = progress);
                    }
                    else
                    {
                        CurrentProgress = progress;
                    }
                });

                var results = await Task.Run(() => _trainer.TrainAsync(CurrentDataset, _cancellationTokenSource.Token));
                Status = $"Training completed. Final accuracy: {results.FinalValidationAccuracy:F4}";

                if (AutoExportOnnx && _trainer != null)
                {
                    await ExportOnnxModelAsync();
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
            _cancellationTokenSource?.Cancel();
        }

        private async Task ExportOnnxModelAsync()
        {
            if (_trainer == null)
            {
                ExportStatus = "No trained model available for export.";
                return;
            }
            try
            {
                ExportStatus = "Exporting model to ONNX...";
                var onnxPath = await _trainer.ExportToOnnxAsync();
                ExportStatus = $"Model exported successfully to: {onnxPath}";
            }
            catch (Exception ex)
            {
                ExportStatus = $"Error exporting model: {ex.Message}";
            }
        }

        private void BrowseLocalFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Image Folder" };
            if (dialog.ShowDialog() == true)
            {
                LocalImageFolder = dialog.FolderName;
            }
        }

        private bool CanExtractData()
        {
            if (IsTraining || IsExtracting) return false;
            return SelectedDataSource == DataSourceType.API ? IsConnected : Directory.Exists(LocalImageFolder);
        }

        private bool CanStartTraining()
        {
            return !IsTraining && CurrentDataset != null && CurrentDataset.Samples.Any();
        }

        private void LoadSettingsFromRegistry()
        {
            // Placeholder for loading settings
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
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
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
