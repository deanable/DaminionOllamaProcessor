// DaminionOllamaApp/ViewModels/DaminionCollectionTaggerViewModel.cs
using DaminionOllamaApp.Models;      // For AppSettings, DaminionQueueItem, QueryTypeDisplayItem, ProcessingStatus
using DaminionOllamaApp.Services;    // For SettingsService, ProcessingService
using DaminionOllamaApp.Utils;       // For RelayCommand
using DaminionOllamaInteractionLib;  // For DaminionApiClient
using DaminionOllamaInteractionLib.Daminion; // For DaminionMediaItem, DaminionPathResult, DaminionUpdateOperation etc.
using DaminionOllamaInteractionLib.Ollama;   // For ParsedOllamaContent, OllamaResponseParser
using DaminionOllamaInteractionLib.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;          // For INotifyPropertyChanged
using System.IO;                    // For Path, File.ReadAllBytesAsync
using System.Linq;
using System.Runtime.CompilerServices; // For CallerMemberName
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    // Assuming QueryTypeDisplayItem is in DaminionOllamaApp.Models namespace
    // If not, adjust the 'using DaminionOllamaApp.Models;' statement or its definition location.

    public class DaminionCollectionTaggerViewModel : INotifyPropertyChanged
    {
        // --- Fields ---
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;
        private DaminionApiClient? _daminionClient;
        private readonly ProcessingService _processingService;

        private bool _isLoggedIn;
        private string _daminionStatus = "Not logged in. Please configure Daminion settings and click Login.";

        private ObservableCollection<QueryTypeDisplayItem> _queryTypes;
        private QueryTypeDisplayItem? _selectedQueryType;

        private ObservableCollection<DaminionQueueItem> _daminionFilesToProcess;
        private bool _isLoadingItems;
        private bool _isProcessingDaminionQueue;
        private CancellationTokenSource? _daminionCts;

        // --- Properties ---
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { SetProperty(ref _isLoggedIn, value); UpdateCommandStates(); }
        }

        public string DaminionStatus
        {
            get => _daminionStatus;
            set { SetProperty(ref _daminionStatus, value); }
        }

        public ObservableCollection<QueryTypeDisplayItem> QueryTypes
        {
            get => _queryTypes;
            set { SetProperty(ref _queryTypes, value); }
        }

        public QueryTypeDisplayItem? SelectedQueryType
        {
            get => _selectedQueryType;
            set
            {
                if (SetProperty(ref _selectedQueryType, value))
                {
                    if (DaminionFilesToProcess != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => DaminionFilesToProcess.Clear());
                    }
                    UpdateCommandStates();
                }
            }
        }

        public ObservableCollection<DaminionQueueItem> DaminionFilesToProcess
        {
            get => _daminionFilesToProcess;
            set
            {
                if (SetProperty(ref _daminionFilesToProcess, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        public bool IsLoadingItems
        {
            get => _isLoadingItems;
            set { SetProperty(ref _isLoadingItems, value); UpdateCommandStates(); }
        }

        public bool IsProcessingDaminionQueue
        {
            get => _isProcessingDaminionQueue;
            set { SetProperty(ref _isProcessingDaminionQueue, value); UpdateCommandStates(); }
        }

        // --- Commands ---
        public ICommand LoginCommand { get; }
        public ICommand LoadItemsByQueryCommand { get; }
        public ICommand StartDaminionQueueCommand { get; }
        public ICommand StopDaminionQueueCommand { get; }

        // --- Constructor ---
        public DaminionCollectionTaggerViewModel()
        {
            _settingsService = new SettingsService();
            _currentSettings = _settingsService.LoadSettings();
            _processingService = new ProcessingService();

            _daminionFilesToProcess = new ObservableCollection<DaminionQueueItem>(); // Initialize backing field
            _queryTypes = new ObservableCollection<QueryTypeDisplayItem> // Initialize backing field
            {
                new QueryTypeDisplayItem { DisplayName = "Unflagged Items", QueryLine = "1,7179;41,1", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Flagged Items", QueryLine = "1,7179;41,2", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Rejected Items", QueryLine = "1,7179;41,3", Operators = "1,any;41,any" }
            };
            _selectedQueryType = _queryTypes.FirstOrDefault(); // Initialize backing field

            // Initialize Commands
            LoginCommand = new RelayCommand(async param => await LoginAsync(), param => CanLogin());
            LoadItemsByQueryCommand = new RelayCommand(async param => await LoadItemsByQueryAsync(), param => CanLoadItemsByQuery());
            StartDaminionQueueCommand = new RelayCommand(async param => await StartDaminionQueueProcessingAsync(), param => CanStartDaminionQueue());
            StopDaminionQueueCommand = new RelayCommand(param => StopDaminionQueueProcessing(), param => CanStopDaminionQueue());
        }

        // --- Command Methods & Helpers ---
        private void UpdateCommandStates()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (LoadItemsByQueryCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StartDaminionQueueCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StopDaminionQueueCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }

        private bool CanLogin() => !IsLoggedIn && !IsLoadingItems && !IsProcessingDaminionQueue;
        private async Task LoginAsync()
        {
            _currentSettings = _settingsService.LoadSettings();
            if (string.IsNullOrWhiteSpace(_currentSettings.DaminionServerUrl) ||
                string.IsNullOrWhiteSpace(_currentSettings.DaminionUsername))
            {
                DaminionStatus = "Error: Daminion server URL or username is not configured in settings.";
                return;
            }

            _daminionClient = new DaminionApiClient();
            DaminionStatus = $"Logging in to {_currentSettings.DaminionServerUrl}...";
            IsLoggedIn = false;

            try
            {
                bool success = await _daminionClient.LoginAsync(
                    _currentSettings.DaminionServerUrl,
                    _currentSettings.DaminionUsername,
                    _currentSettings.DaminionPassword);

                if (success)
                {
                    IsLoggedIn = true;
                    DaminionStatus = "Logged in successfully. Select a query type and load items.";
                }
                else
                {
                    DaminionStatus = "Login failed. Check credentials and server URL. See console for details.";
                    IsLoggedIn = false;
                }
            }
            catch (Exception ex)
            {
                DaminionStatus = $"Login error: {ex.Message}";
                IsLoggedIn = false;
                System.Diagnostics.Debug.WriteLine($"Daminion Login Exception: {ex}");
            }
        }

        private bool CanLoadItemsByQuery() => SelectedQueryType != null && IsLoggedIn && !IsLoadingItems && !IsProcessingDaminionQueue;
        private async Task LoadItemsByQueryAsync()
        {
            if (SelectedQueryType == null || _daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                DaminionStatus = "Cannot load items: No query type selected or not logged in.";
                return;
            }

            IsLoadingItems = true;
            DaminionStatus = $"Loading items for query: '{SelectedQueryType.DisplayName}'...";
            Application.Current.Dispatcher.Invoke(() => DaminionFilesToProcess.Clear());

            try
            {
                DaminionSearchMediaItemsResponse? searchResult = await _daminionClient.SearchMediaItemsAsync(
                    SelectedQueryType.QueryLine,
                    SelectedQueryType.Operators,
                    pageSize: 1000);

                if (searchResult != null && searchResult.Success && searchResult.MediaItems != null)
                {
                    if (!searchResult.MediaItems.Any())
                    {
                        DaminionStatus = $"No items found for query: '{SelectedQueryType.DisplayName}'.";
                        IsLoadingItems = false; // Ensure flag is reset
                        return;
                    }

                    DaminionStatus = $"{searchResult.MediaItems.Count} item(s) found. Fetching paths...";
                    var itemIds = searchResult.MediaItems.Select(item => item.Id).ToList();

                    if (!itemIds.Any())
                    {
                        DaminionStatus = $"No item IDs found to fetch paths for query: '{SelectedQueryType.DisplayName}'.";
                        IsLoadingItems = false; // Ensure flag is reset
                        return;
                    }

                    DaminionPathResult pathResult = await _daminionClient.GetAbsolutePathsAsync(itemIds);

                    if (pathResult.Success && pathResult.Paths != null)
                    {
                        var tempQueueItems = new List<DaminionQueueItem>();
                        foreach (var daminionItem in searchResult.MediaItems)
                        {
                            string displayName = !string.IsNullOrWhiteSpace(daminionItem.Name) ? daminionItem.Name :
                                                 (!string.IsNullOrWhiteSpace(daminionItem.FileName) ? daminionItem.FileName : $"Item {daminionItem.Id}");

                            if (pathResult.Paths.TryGetValue(daminionItem.Id.ToString(), out string? filePath) && !string.IsNullOrEmpty(filePath))
                            {
                                tempQueueItems.Add(new DaminionQueueItem(daminionItem.Id, displayName) { FilePath = filePath });
                            }
                            else
                            {
                                tempQueueItems.Add(new DaminionQueueItem(daminionItem.Id, displayName)
                                {
                                    Status = ProcessingStatus.Error,
                                    StatusMessage = $"Path not found for item ID {daminionItem.Id}."
                                });
                            }
                        }
                        DaminionFilesToProcess = new ObservableCollection<DaminionQueueItem>(tempQueueItems); // Assign new collection
                        DaminionStatus = $"{DaminionFilesToProcess.Count(f => f.Status != ProcessingStatus.Error)} items loaded with paths for query '{SelectedQueryType.DisplayName}'. Ready to process.";
                    }
                    else
                    {
                        DaminionStatus = $"Found {searchResult.MediaItems.Count} items, but failed to get their paths: {pathResult.ErrorMessage}";
                        var tempErrorItems = new List<DaminionQueueItem>();
                        foreach (var daminionItem in searchResult.MediaItems)
                        {
                            string displayName = !string.IsNullOrWhiteSpace(daminionItem.Name) ? daminionItem.Name :
                                                (!string.IsNullOrWhiteSpace(daminionItem.FileName) ? daminionItem.FileName : $"Item {daminionItem.Id}");
                            tempErrorItems.Add(new DaminionQueueItem(daminionItem.Id, displayName)
                            {
                                Status = ProcessingStatus.Error,
                                StatusMessage = $"Failed to retrieve file path. API Error: {pathResult.ErrorMessage}"
                            });
                        }
                        DaminionFilesToProcess = new ObservableCollection<DaminionQueueItem>(tempErrorItems);
                    }
                }
                else
                {
                    DaminionStatus = $"Failed to search items for query '{SelectedQueryType.DisplayName}': {searchResult?.Error ?? "Unknown API error."}";
                }
            }
            catch (Exception ex)
            {
                DaminionStatus = $"Error loading items by query: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in LoadItemsByQueryAsync: {ex}");
            }
            finally
            {
                IsLoadingItems = false;
            }
        }

        private bool CanStartDaminionQueue() => DaminionFilesToProcess.Any(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath)) && IsLoggedIn && !IsProcessingDaminionQueue && !IsLoadingItems;
        private async Task StartDaminionQueueProcessingAsync()
        {
            if (!IsLoggedIn || _daminionClient == null)
            {
                DaminionStatus = "Cannot start: Not logged in to Daminion.";
                return;
            }

            IsProcessingDaminionQueue = true;
            _currentSettings = _settingsService.LoadSettings();
            _daminionCts = new CancellationTokenSource();
            var token = _daminionCts.Token;

            DaminionStatus = "Starting Daminion queue processing...";
            int successCount = 0;
            int failureCount = 0;

            var itemsToProcessThisRun = DaminionFilesToProcess
                .Where(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath) && !f.FilePath.Contains("(dummy path)"))
                .ToList();

            if (!itemsToProcessThisRun.Any())
            {
                DaminionStatus = "No valid items with paths to process in the Daminion queue.";
                IsProcessingDaminionQueue = false;
                return;
            }

            string descTagGuid = _currentSettings.DaminionDescriptionTagGuid;
            string keywordsTagGuid = _currentSettings.DaminionKeywordsTagGuid;
            string categoriesTagGuid = _currentSettings.DaminionCategoriesTagGuid;
            // string flagTagGuid = _currentSettings.DaminionFlagTagGuid; // For later flag changes

            if (string.IsNullOrWhiteSpace(descTagGuid) || string.IsNullOrWhiteSpace(keywordsTagGuid) || string.IsNullOrWhiteSpace(categoriesTagGuid))
            {
                DaminionStatus = "Error: Target Daminion Tag GUIDs (Description, Keywords, Categories) are not configured in AppSettings. Please configure them.";
                System.Diagnostics.Debug.WriteLine("Missing Daminion Tag GUIDs for metadata update. Check AppSettings values.");
                IsProcessingDaminionQueue = false;
                return;
            }

            try // Added main try block for the processing loop
            {
                foreach (var item in itemsToProcessThisRun)
                {
                    if (token.IsCancellationRequested)
                    {
                        item.Status = ProcessingStatus.Cancelled;
                        item.StatusMessage = "Daminion queue stopped by user.";
                        break; // Break from foreach loop
                    }

                    item.Status = ProcessingStatus.Processing;
                    item.StatusMessage = "Sending to Ollama...";
                    UpdateOverallDaminionStatus($"Processing item: {item.FileName} (ID: {item.DaminionItemId})");

                    ParsedOllamaContent? parsedContentForDaminion = null;
                    bool ollamaAndLocalWriteSuccess = false;

                    try // Inner try for individual item processing (Ollama + Local Write)
                    {
                        if (string.IsNullOrEmpty(item.FilePath))
                        {
                            item.StatusMessage = "Error: File path is missing.";
                            System.Diagnostics.Debug.WriteLine($"Missing file path for Daminion item {item.FileName} (ID: {item.DaminionItemId})");
                            // ollamaAndLocalWriteSuccess remains false
                        }
                        else
                        {
                            byte[] imageBytes = await File.ReadAllBytesAsync(item.FilePath, token);
                            var ollamaClient = new OllamaApiClient(_currentSettings.OllamaServerUrl);
                            var ollamaResponse = await ollamaClient.AnalyzeImageAsync(_currentSettings.OllamaModelName, _currentSettings.OllamaPrompt, imageBytes);

                            if (token.IsCancellationRequested) throw new OperationCanceledException();

                            if (ollamaResponse != null && ollamaResponse.Done && !string.IsNullOrWhiteSpace(ollamaResponse.Response))
                            {
                                parsedContentForDaminion = OllamaResponseParser.ParseLlavaResponse(ollamaResponse.Response);
                                if (parsedContentForDaminion.SuccessfullyParsed)
                                {
                                    item.StatusMessage = "Ollama processing complete. Writing to local file...";
                                    var metadataService = new ImageMetadataService(item.FilePath);
                                    metadataService.Read();
                                    metadataService.PopulateFromOllamaContent(parsedContentForDaminion);
                                    metadataService.Save();
                                    item.StatusMessage = "Local metadata written. Updating Daminion server...";
                                    ollamaAndLocalWriteSuccess = true;
                                }
                                else
                                {
                                    item.StatusMessage = "Ollama processing complete, but parsing response failed.";
                                }
                            }
                            else
                            {
                                item.StatusMessage = $"Ollama API error or empty response: {ollamaResponse?.Response?.Substring(0, Math.Min(ollamaResponse.Response?.Length ?? 0, 100)) ?? "N/A"}";
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        item.StatusMessage = $"Ollama processing or local write error: {ex.Message}";
                        System.Diagnostics.Debug.WriteLine($"Ollama/LocalWrite error for {item.FileName}: {ex}");
                        // ollamaAndLocalWriteSuccess remains false
                    }

                    if (token.IsCancellationRequested) throw new OperationCanceledException(); // Check again before Daminion update

                    if (!ollamaAndLocalWriteSuccess || parsedContentForDaminion == null)
                    {
                        item.Status = ProcessingStatus.Error; // Ensure status is Error if we continue
                        failureCount++;
                        continue; // Move to next item in the foreach loop
                    }

                    // Update Daminion server metadata
                    UpdateOverallDaminionStatus($"Updating Daminion for: {item.FileName}");
                    var operations = new List<DaminionUpdateOperation>();

                    if (!string.IsNullOrWhiteSpace(parsedContentForDaminion.Description))
                    {
                        operations.Add(new DaminionUpdateOperation { Guid = descTagGuid, Value = parsedContentForDaminion.Description, Id = 0, Remove = false });
                    }
                    if (parsedContentForDaminion.Keywords.Any())
                    {
                        foreach (var keyword in parsedContentForDaminion.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                        {
                            operations.Add(new DaminionUpdateOperation { Guid = keywordsTagGuid, Value = keyword, Id = 0, Remove = false });
                        }
                    }
                    if (parsedContentForDaminion.Categories.Any())
                    {
                        foreach (var category in parsedContentForDaminion.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                        {
                            operations.Add(new DaminionUpdateOperation { Guid = categoriesTagGuid, Value = category, Id = 0, Remove = false });
                        }
                    }

                    // Placeholder for changing flag status in Daminion (e.g., from "Unflagged" to "Processed")
                    // This logic would be added here if desired, using the flagTagGuid and relevant value IDs.

                    if (operations.Any())
                    {
                        var updateResult = await _daminionClient.UpdateItemMetadataAsync(new List<long> { item.DaminionItemId }, operations);
                        if (updateResult != null && updateResult.Success)
                        {
                            item.Status = ProcessingStatus.Processed;
                            item.StatusMessage = "Processed and Daminion metadata updated.";
                            successCount++;
                        }
                        else
                        {
                            item.Status = ProcessingStatus.Error;
                            item.StatusMessage = $"Daminion server update failed: {updateResult?.Error ?? "Unknown error"}";
                            failureCount++;
                        }
                    }
                    else
                    {
                        item.Status = ProcessingStatus.Processed;
                        item.StatusMessage = "Local file processed; no new metadata from Ollama to update in Daminion.";
                        successCount++;
                    }
                } // End foreach item
            } // End main try block
            catch (OperationCanceledException)
            {
                UpdateOverallDaminionStatus("Daminion queue processing cancelled by user.");
                // Items already processed or errored will keep their status.
                // Items that were in 'Processing' or 'Queued' and haven't been updated to Cancelled yet can be marked here.
                foreach (var item in DaminionFilesToProcess.Where(i => i.Status == ProcessingStatus.Processing || i.Status == ProcessingStatus.Queued))
                {
                    item.Status = ProcessingStatus.Cancelled; // Or Error if preferred for partially processed
                    item.StatusMessage = "Cancelled during queue processing.";
                }
            }
            catch (Exception ex)
            {
                UpdateOverallDaminionStatus($"An error occurred during Daminion queue processing: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error in StartDaminionQueueProcessingAsync: {ex}");
                foreach (var item in DaminionFilesToProcess.Where(i => i.Status == ProcessingStatus.Processing || i.Status == ProcessingStatus.Queued))
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = "Queue processing error.";
                }
            }
            finally
            {
                IsProcessingDaminionQueue = false; // This will call UpdateCommandStates via its setter
                _daminionCts?.Dispose();
                _daminionCts = null;
                // Recalculate counts based on final statuses
                successCount = DaminionFilesToProcess.Count(i => i.Status == ProcessingStatus.Processed);
                failureCount = DaminionFilesToProcess.Count(i => i.Status == ProcessingStatus.Error);
                int cancelledCount = DaminionFilesToProcess.Count(i => i.Status == ProcessingStatus.Cancelled);
                string finalSummary = $"Daminion queue finished. Successful: {successCount}, Failures: {failureCount}, Cancelled: {cancelledCount}.";
                UpdateOverallDaminionStatus(finalSummary);
            }
        } // End StartDaminionQueueProcessingAsync

        private void UpdateOverallDaminionStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DaminionStatus = message;
            });
        }

        private bool CanStopDaminionQueue() => IsProcessingDaminionQueue;
        private void StopDaminionQueueProcessing()
        {
            _daminionCts?.Cancel();
            UpdateOverallDaminionStatus("Daminion queue stop requested by user.");
            // IsProcessingDaminionQueue will be set to false in the finally block of StartDaminionQueueProcessingAsync
        }

        // --- INotifyPropertyChanged Implementation ---
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
            Application.Current.Dispatcher.Invoke(() => // Ensure PropertyChanged is raised on UI thread
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
    } // End DaminionCollectionTaggerViewModel class
} // End namespace