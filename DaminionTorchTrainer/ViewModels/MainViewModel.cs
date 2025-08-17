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
using System.IO;
using System.Linq;

namespace DaminionTorchTrainer.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DaminionApiClient _daminionClient;
        private readonly DaminionDataExtractor _dataExtractor;
        private readonly LocalImageDataExtractor _localDataExtractor;
        private readonly MLNetImageClassifierTrainer _mlNetTrainer;
        private CancellationTokenSource? _cancellationTokenSource;

        private string _status = "Ready";
        private bool _isConnected = false;
        private bool _isTraining = false;
        private bool _isExtracting = false;
        private TrainingDataset? _currentDataset;
        private string _daminionUrl = "http://localhost:8080";
        private string _daminionUsername = "";
        private string _daminionPassword = "";
        private string _searchQuery = "";
        private int _maxItems = 1000;
        
        private int _extractionProgress = 0;
        private int _extractionTotal = 0;
        private string _extractionStatus = "";
        
        private string _localImageFolder = "";
        private bool _includeSubfolders = true;
        private DataSourceType _selectedDataSource = DataSourceType.API;
        
        public MainViewModel()
        {
            // The ImageProcessor is no longer needed here, as ML.NET handles it.
            // We can't remove it completely yet as the old extractors still need it.
            // This will be cleaned up in a future step.
            var tempImageProcessor = new ImageProcessor();

            _daminionClient = new DaminionApiClient();
            _dataExtractor = new DaminionDataExtractor(_daminionClient, tempImageProcessor);
            _localDataExtractor = new LocalImageDataExtractor(tempImageProcessor);
            _mlNetTrainer = new MLNetImageClassifierTrainer();

            ConnectCommand = new AsyncRelayCommand(ConnectToDaminionAsync, () => !IsConnected && !IsTraining);
            ExtractDataCommand = new AsyncRelayCommand(ExtractDataAsync, () => CanExtractData());
            StartTrainingCommand = new AsyncRelayCommand(StartTrainingAsync, () => CanStartTraining());
            StopTrainingCommand = new RelayCommand(() => StopTraining(), () => IsTraining);
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
        public TrainingDataset? CurrentDataset { get => _currentDataset; set => SetProperty(ref _currentDataset, value); }
        public string DaminionUrl { get => _daminionUrl; set => SetProperty(ref _daminionUrl, value); }
        public string DaminionUsername { get => _daminionUsername; set => SetProperty(ref _daminionUsername, value); }
        public string DaminionPassword { get => _daminionPassword; set => SetProperty(ref _daminionPassword, value); }
        public string SearchQuery { get => _searchQuery; set => SetProperty(ref _searchQuery, value); }
        public int MaxItems { get => _maxItems; set => SetProperty(ref _maxItems, value); }
        public string LocalImageFolder { get => _localImageFolder; set => SetProperty(ref _localImageFolder, value); }
        public bool IncludeSubfolders { get => _includeSubfolders; set => SetProperty(ref _includeSubfolders, value); }
        public DataSourceType SelectedDataSource { get => _selectedDataSource; set => SetProperty(ref _selectedDataSource, value); }
        #endregion

        #region Commands
        public ICommand ConnectCommand { get; }
        public ICommand ExtractDataCommand { get; }
        public ICommand StartTrainingCommand { get; }
        public ICommand StopTrainingCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        #endregion

        #region Methods
        private async Task ConnectToDaminionAsync()
        {
            try
            {
                Status = "Connecting...";
                _daminionClient.SetApiBaseUrl(DaminionUrl);
                var loginSuccess = await _daminionClient.LoginAsync(DaminionUrl, DaminionUsername, DaminionPassword);
                IsConnected = loginSuccess;
                Status = loginSuccess ? "Connected." : "Failed to connect.";
            }
            catch (Exception ex) { Status = $"Error: {ex.Message}"; }
        }

        private async Task ExtractDataAsync()
        {
            if (IsExtracting) return;
            try
            {
                IsExtracting = true;
                Status = "Extracting data...";
                var progressCallback = new Action<int, int, string>((curr, total, msg) => { ExtractionProgress = curr; ExtractionTotal = total; ExtractionStatus = msg; });

                // HACK: The old data extractors still exist but we are not using them for training.
                // We will just use the local file extractor to get a list of files and labels.
                // This part of the code needs a major refactoring to align with the new ML.NET approach.
                // For now, we get the dataset to pass to the ML.NET trainer.
                CurrentDataset = await _localDataExtractor.ExtractTrainingDataAsync(LocalImageFolder, IncludeSubfolders, MaxItems, progressCallback);
                Status = $"Extracted {CurrentDataset.Samples.Count} samples.";
            }
            catch (Exception ex) { Status = $"Error: {ex.Message}"; }
            finally { IsExtracting = false; }
        }

        private async Task StartTrainingAsync()
        {
            if (CurrentDataset == null || !CurrentDataset.Samples.Any()) { Status = "No data to train on."; return; }
            try
            {
                IsTraining = true;
                _cancellationTokenSource = new CancellationTokenSource();
                Status = "Starting ML.NET Training...";

                var modelPath = await Task.Run(() => _mlNetTrainer.TrainModelAsync(CurrentDataset, (log) => {
                    Application.Current.Dispatcher.Invoke(() => Status = log);
                }), _cancellationTokenSource.Token);

                Status = $"ML.NET training complete. Model saved at {modelPath}";
            }
            catch (OperationCanceledException) { Status = "Training canceled."; }
            catch (Exception ex) { Status = $"Error: {ex.Message}"; }
            finally
            {
                IsTraining = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        private void StopTraining() => _cancellationTokenSource?.Cancel();

        private void BrowseLocalFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Image Folder" };
            if (dialog.ShowDialog() == true) LocalImageFolder = dialog.FolderName;
        }

        private bool CanExtractData() => !IsTraining && !IsExtracting && Directory.Exists(LocalImageFolder);
        private bool CanStartTraining() => !IsTraining && CurrentDataset != null && CurrentDataset.Samples.Any();

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public RelayCommand(Action execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);
        public async void Execute(object? parameter)
        {
            _isExecuting = true;
            try { await _execute(); }
            finally { _isExecuting = false; }
        }
    }
}
