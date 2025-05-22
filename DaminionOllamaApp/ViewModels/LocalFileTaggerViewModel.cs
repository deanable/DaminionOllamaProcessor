// DaminionOllamaApp/ViewModels/LocalFileTaggerViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Collections.Generic; // For List

namespace DaminionOllamaApp.ViewModels
{
    public class LocalFileTaggerViewModel : INotifyPropertyChanged
    {
        private string _currentOperationStatus = "Ready. Add files to begin.";
        private ObservableCollection<FileQueueItem> _filesToProcess;
        private bool _isProcessingQueue;
        private CancellationTokenSource? _cancellationTokenSource;
        private FileQueueItem? _selectedFile; // To track the selected file

        private readonly ProcessingService _processingService;
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;

        public ObservableCollection<FileQueueItem> FilesToProcess
        {
            get => _filesToProcess;
            set
            {
                _filesToProcess = value;
                OnPropertyChanged(nameof(FilesToProcess));
            }
        }

        // Property for the ListView's SelectedItem
        public FileQueueItem? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (_selectedFile != value)
                {
                    _selectedFile = value;
                    OnPropertyChanged(nameof(SelectedFile));
                    // Notify that CanExecute for RemoveSelectedFileCommand might have changed
                    ((RelayCommand)RemoveSelectedFileCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string CurrentOperationStatus
        {
            get => _currentOperationStatus;
            set
            {
                _currentOperationStatus = value;
                OnPropertyChanged(nameof(CurrentOperationStatus));
            }
        }

        public bool IsProcessingQueue
        {
            get => _isProcessingQueue;
            set
            {
                if (_isProcessingQueue != value)
                {
                    _isProcessingQueue = value;
                    OnPropertyChanged(nameof(IsProcessingQueue));
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

        public ICommand AddFilesCommand { get; }
        public ICommand StartQueueCommand { get; }
        public ICommand StopQueueCommand { get; }
        // New Commands
        public ICommand RemoveSelectedFileCommand { get; }
        public ICommand ClearProcessedFilesCommand { get; }
        public ICommand ClearAllFilesCommand { get; }


        public LocalFileTaggerViewModel()
        {
            FilesToProcess = new ObservableCollection<FileQueueItem>();
            _processingService = new ProcessingService();
            _settingsService = new SettingsService();
            _currentSettings = _settingsService.LoadSettings();

            AddFilesCommand = new RelayCommand(param => AddFiles(), param => CanAddFiles());
            StartQueueCommand = new RelayCommand(async param => await StartQueueAsync(), param => CanStartQueue());
            StopQueueCommand = new RelayCommand(param => StopQueue(), param => CanStopQueue());

            // Initialize New Commands
            RemoveSelectedFileCommand = new RelayCommand(param => RemoveSelectedFile(), param => CanRemoveSelectedFile());
            ClearProcessedFilesCommand = new RelayCommand(param => ClearProcessedFiles(), param => CanClearProcessedFiles());
            ClearAllFilesCommand = new RelayCommand(param => ClearAllFiles(), param => CanClearAllFiles());
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
                ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ClearAllFilesCommand).RaiseCanExecuteChanged(); // List is no longer empty
                ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged(); // Status of items might allow this
            }
        }

        private bool CanStartQueue()
        {
            return !IsProcessingQueue && FilesToProcess.Any(f => f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error);
        }

        private async Task StartQueueAsync()
        {
            IsProcessingQueue = true;
            _currentSettings = _settingsService.LoadSettings();
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
                await _processingService.ProcessLocalFileAsync(item, _currentSettings, UpdateOverallStatus, token);
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
            ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged(); // Processed items might now exist
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
            }
        }

        // --- New Command Methods ---
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
            ((RelayCommand)StartQueueCommand).RaiseCanExecuteChanged(); // Re-evaluate if any processable files remain
            ((RelayCommand)ClearProcessedFilesCommand).RaiseCanExecuteChanged(); // Might now be disabled
            ((RelayCommand)ClearAllFilesCommand).RaiseCanExecuteChanged(); // List might now be empty
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
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // For properties that might affect command states, you might call RaiseCanExecuteChanged here
            // e.g., if FilesToProcess.Count changes, it affects CanClearAllFiles.
            // However, we are already calling RaiseCanExecuteChanged in the methods that modify the list.
        }
    }
}