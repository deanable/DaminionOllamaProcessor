// DaminionOllamaApp/ViewModels/DaminionCollectionTaggerViewModel.cs
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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    public class DaminionCollectionTaggerViewModel : INotifyPropertyChanged
    {
        // --- Fields ---
        private readonly SettingsService _settingsService;
        private DaminionApiClient? _daminionClient;

        private bool _isLoggedIn;
        private string _daminionStatus = "Not logged in. Please configure Daminion settings and click Login.";
        private ObservableCollection<QueryTypeDisplayItem> _queryTypes;
        private QueryTypeDisplayItem? _selectedQueryType;
        private ObservableCollection<DaminionQueueItem> _daminionFilesToProcess;
        private bool _isLoadingItems;
        private bool _isProcessingDaminionQueue;
        private CancellationTokenSource? _daminionCts;

        // --- Properties ---
        public AppSettings Settings { get; }

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

        public DaminionCollectionTaggerViewModel(AppSettings settings, SettingsService settingsService)
        {
            Settings = settings;
            _settingsService = settingsService;

            _daminionFilesToProcess = new ObservableCollection<DaminionQueueItem>();
            _queryTypes = new ObservableCollection<QueryTypeDisplayItem>
            {
                new QueryTypeDisplayItem { DisplayName = "Unflagged Items", QueryLine = "1,7179;41,1", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Flagged Items", QueryLine = "1,7179;41,2", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Rejected Items", QueryLine = "1,7179;41,3", Operators = "1,any;41,any" }
            };
            _selectedQueryType = _queryTypes.FirstOrDefault();

            LoginCommand = new RelayCommand(async param => await LoginAsync(), param => CanLogin());
            LoadItemsByQueryCommand = new RelayCommand(async param => await LoadItemsByQueryAsync(), param => CanLoadItemsByQuery());
            StartDaminionQueueCommand = new RelayCommand(async param => await StartDaminionQueueProcessingAsync(), param => CanStartDaminionQueue());
            StopDaminionQueueCommand = new RelayCommand(param => StopDaminionQueueProcessing(), param => CanStopDaminionQueue());
        }

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
            if (string.IsNullOrWhiteSpace(Settings.DaminionServerUrl) ||
                string.IsNullOrWhiteSpace(Settings.DaminionUsername))
            {
                DaminionStatus = "Error: Daminion server URL or username is not configured in settings.";
                return;
            }

            _daminionClient = new DaminionApiClient();
            DaminionStatus = $"Logging in to {Settings.DaminionServerUrl}...";
            IsLoggedIn = false;

            try
            {
                bool success = await _daminionClient.LoginAsync(
                    Settings.DaminionServerUrl,
                    Settings.DaminionUsername,
                    Settings.DaminionPassword);

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
                        IsLoadingItems = false;
                        return;
                    }

                    DaminionStatus = $"{searchResult.MediaItems.Count} item(s) found. Fetching paths...";
                    var itemIds = searchResult.MediaItems.Select(item => item.Id).ToList();

                    if (!itemIds.Any())
                    {
                        DaminionStatus = $"No item IDs found to fetch paths for query: '{SelectedQueryType.DisplayName}'.";
                        IsLoadingItems = false;
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
                        DaminionFilesToProcess = new ObservableCollection<DaminionQueueItem>(tempQueueItems);
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
            _daminionCts = new CancellationTokenSource();
            var token = _daminionCts.Token;

            DaminionStatus = "Starting Daminion queue processing...";
            int successCount = 0;
            int failureCount = 0;

            var itemsToProcessThisRun = DaminionFilesToProcess
                .Where(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath))
                .ToList();
            if (!itemsToProcessThisRun.Any())
            {
                DaminionStatus = "No valid items with paths to process in the Daminion queue.";
                IsProcessingDaminionQueue = false;
                return;
            }

            string descTagGuid = Settings.DaminionDescriptionTagGuid;
            string keywordsTagGuid = Settings.DaminionKeywordsTagGuid;
            string categoriesTagGuid = Settings.DaminionCategoriesTagGuid;

            if (string.IsNullOrWhiteSpace(descTagGuid) || string.IsNullOrWhiteSpace(keywordsTagGuid) || string.IsNullOrWhiteSpace(categoriesTagGuid))
            {
                DaminionStatus = "Error: Target Daminion Tag GUIDs (Description, Keywords, Categories) are not configured in AppSettings. Please configure them.";
                System.Diagnostics.Debug.WriteLine("Missing Daminion Tag GUIDs for metadata update. Check AppSettings values.");
                IsProcessingDaminionQueue = false;
                return;
            }

            try
            {
                foreach (var item in itemsToProcessThisRun)
                {
                    if (token.IsCancellationRequested)
                    {
                        item.Status = ProcessingStatus.Cancelled;
                        item.StatusMessage = "Daminion queue stopped by user.";
                        break;
                    }

                    item.Status = ProcessingStatus.Processing;
                    UpdateOverallDaminionStatus($"Processing item: {item.FileName} (ID: {item.DaminionItemId})");

                    string aiResponse = string.Empty;
                    bool aiSuccess = false;

                    try
                    {
                        if (string.IsNullOrEmpty(item.FilePath))
                        {
                            throw new InvalidOperationException("File path is missing.");
                        }

                        byte[] imageBytes = await File.ReadAllBytesAsync(item.FilePath, token);
                        item.StatusMessage = $"Sending to {Settings.SelectedAiProvider}...";

                        // --- AI Provider Switch ---
                        if (Settings.SelectedAiProvider == AiProvider.Ollama)
                        {
                            var ollamaClient = new OllamaApiClient(Settings.OllamaServerUrl);
                            var ollamaResponse = await ollamaClient.AnalyzeImageAsync(Settings.OllamaModelName, Settings.OllamaPrompt, imageBytes);
                            if (ollamaResponse != null && ollamaResponse.Done)
                            {
                                aiResponse = ollamaResponse.Response;
                                aiSuccess = true;
                            }
                            else
                            {
                                throw new Exception($"Ollama API error: {ollamaResponse?.Response ?? "Empty response."}");
                            }
                        }
                        else // AiProvider.OpenRouter
                        {
                            var routerClient = new OpenRouterApiClient(Settings.OpenRouterApiKey, Settings.OpenRouterHttpReferer);
                            string base64Image = Convert.ToBase64String(imageBytes);
                            string? routerResponse = await routerClient.AnalyzeImageAsync(Settings.OpenRouterModelName, Settings.OllamaPrompt, base64Image);
                            if (!string.IsNullOrEmpty(routerResponse) && !routerResponse.StartsWith("Error:"))
                            {
                                aiResponse = routerResponse;
                                aiSuccess = true;
                            }
                            else
                            {
                                throw new Exception($"OpenRouter API error: {routerResponse ?? "Empty response."}");
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        item.StatusMessage = $"AI processing error: {ex.Message}";
                        System.Diagnostics.Debug.WriteLine($"AI error for {item.FileName}: {ex}");
                        aiSuccess = false;
                    }

                    if (token.IsCancellationRequested) throw new OperationCanceledException();

                    if (!aiSuccess)
                    {
                        item.Status = ProcessingStatus.Error;
                        failureCount++;
                        continue;
                    }

                    item.StatusMessage = "Parsing AI response...";
                    ParsedOllamaContent parsedContent = OllamaResponseParser.ParseLlavaResponse(aiResponse);

                    item.StatusMessage = "Updating local file and Daminion...";
                    var operations = new List<DaminionUpdateOperation>();

                    if (!string.IsNullOrWhiteSpace(parsedContent.Description))
                    {
                        operations.Add(new DaminionUpdateOperation { Guid = descTagGuid, Value = parsedContent.Description, Id = 0, Remove = false });
                    }
                    if (parsedContent.Keywords.Any())
                    {
                        foreach (var keyword in parsedContent.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                        {
                            operations.Add(new DaminionUpdateOperation { Guid = keywordsTagGuid, Value = keyword, Id = 0, Remove = false });
                        }
                    }
                    if (parsedContent.Categories.Any())
                    {
                        foreach (var category in parsedContent.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                        {
                            operations.Add(new DaminionUpdateOperation { Guid = categoriesTagGuid, Value = category, Id = 0, Remove = false });
                        }
                    }

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
                        item.StatusMessage = "Local file processed; no new metadata from AI to update in Daminion.";
                        successCount++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                UpdateOverallDaminionStatus("Daminion queue processing cancelled by user.");
                foreach (var item in DaminionFilesToProcess.Where(i => i.Status == ProcessingStatus.Processing))
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Cancelled during queue processing.";
                }
            }
            catch (Exception ex)
            {
                UpdateOverallDaminionStatus($"An error occurred during Daminion queue processing: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error in StartDaminionQueueProcessingAsync: {ex}");
                foreach (var item in DaminionFilesToProcess.Where(i => i.Status == ProcessingStatus.Processing))
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = "Queue processing error.";
                }
            }
            finally
            {
                IsProcessingDaminionQueue = false;
                _daminionCts?.Dispose();
                _daminionCts = null;
                successCount = DaminionFilesToProcess.Count(i => i.Status == ProcessingStatus.Processed);
                failureCount = DaminionFilesToProcess.Count(i => i.Status == ProcessingStatus.Error);
                int cancelledCount = DaminionFilesToProcess.Count(i => i.Status == ProcessingStatus.Cancelled);
                string finalSummary = $"Daminion queue finished. Successful: {successCount}, Failures: {failureCount}, Cancelled: {cancelledCount}.";
                UpdateOverallDaminionStatus(finalSummary);
            }
        }

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
            });
        }
    }
}