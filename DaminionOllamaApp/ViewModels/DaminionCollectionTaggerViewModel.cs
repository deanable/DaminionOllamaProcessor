// DaminionOllamaApp/ViewModels/DaminionCollectionTaggerViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils; // For RelayCommand
using DaminionOllamaInteractionLib; // For DaminionApiClient
using DaminionOllamaInteractionLib.Daminion; // For DaminionTag, DaminionTagValue etc.
using DaminionOllamaInteractionLib.Ollama;
using System;
using System.IO; // Add this line
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    public class CollectionDisplayItem // Helper class for displaying collections in UI
    {
        public long Id { get; set; } // This would be the DaminionTagValue.Id
        public string Name { get; set; } = string.Empty;
        public long ParentTagId { get; set; } // ID of the parent tag (e.g., the "Collections" tag itself)

        public override string ToString() => Name;
    }

    public class DaminionCollectionTaggerViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;
        private DaminionApiClient? _daminionClient;
        private readonly ProcessingService _processingService; // To reuse Ollama processing

        private bool _isLoggedIn;
        private string _daminionStatus = "Not logged in. Please configure Daminion settings and click Login.";
        private ObservableCollection<CollectionDisplayItem> _collections;
        private CollectionDisplayItem? _selectedCollection;
        private ObservableCollection<DaminionQueueItem> _daminionFilesToProcess;
        private bool _isLoadingCollections;
        private bool _isLoadingFiles;
        private bool _isProcessingDaminionQueue;
        private CancellationTokenSource? _daminionCts;

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; OnPropertyChanged(nameof(IsLoggedIn)); UpdateCommandStates(); }
        }

        public string DaminionStatus
        {
            get => _daminionStatus;
            set { _daminionStatus = value; OnPropertyChanged(nameof(DaminionStatus)); }
        }

        public ObservableCollection<CollectionDisplayItem> Collections
        {
            get => _collections;
            set
            {
                if (_collections != value)
                {
                    _collections = value;
                    OnPropertyChanged(nameof(Collections));
                }
            }
        }
        public CollectionDisplayItem? SelectedCollection
        {
            get => _selectedCollection;
            set
            {
                if (_selectedCollection != value)
                {
                    _selectedCollection = value;
                    OnPropertyChanged(nameof(SelectedCollection));
                    if (Application.Current.Dispatcher.CheckAccess())
                        DaminionFilesToProcess.Clear(); // Clear old files when collection changes
                    else
                        Application.Current.Dispatcher.Invoke(() => DaminionFilesToProcess.Clear());

                    UpdateCommandStates(); // Specifically for LoadItemsFromCollectionCommand
                }
            }
        }

        public ObservableCollection<DaminionQueueItem> DaminionFilesToProcess
        {
            get => _daminionFilesToProcess;
            set
            {
                if (_daminionFilesToProcess != value)
                {
                    _daminionFilesToProcess = value;
                    OnPropertyChanged(nameof(DaminionFilesToProcess));
                    // Crucially, do not call UpdateCommandStates() or individual RaiseCanExecuteChanged() here
                    // if this setter is called from the constructor before commands are initialized.
                }
            }
        }

        public bool IsLoadingCollections
        {
            get => _isLoadingCollections;
            set { _isLoadingCollections = value; OnPropertyChanged(nameof(IsLoadingCollections)); UpdateCommandStates(); }
        }
        public bool IsLoadingFiles
        {
            get => _isLoadingFiles;
            set { _isLoadingFiles = value; OnPropertyChanged(nameof(IsLoadingFiles)); UpdateCommandStates(); }
        }
        public bool IsProcessingDaminionQueue
        {
            get => _isProcessingDaminionQueue;
            set { _isProcessingDaminionQueue = value; OnPropertyChanged(nameof(IsProcessingDaminionQueue)); UpdateCommandStates(); }
        }

        public ICommand LoginCommand { get; }
        public ICommand LoadCollectionsCommand { get; }
        public ICommand LoadItemsFromCollectionCommand { get; }
        public ICommand StartDaminionQueueCommand { get; }
        public ICommand StopDaminionQueueCommand { get; }

        public DaminionCollectionTaggerViewModel()
        {
            _settingsService = new SettingsService();
            _currentSettings = _settingsService.LoadSettings();
            _processingService = new ProcessingService();

            Collections = new ObservableCollection<CollectionDisplayItem>();
            DaminionFilesToProcess = new ObservableCollection<DaminionQueueItem>();

            LoginCommand = new RelayCommand(async param => await LoginAsync(), param => CanLogin());
            // LoadCollectionsCommand is now primarily triggered after login, but can be made explicit if needed
            LoadCollectionsCommand = new RelayCommand(async param => await LoadCollectionsAsync(), param => CanLoadCollections());
            LoadItemsFromCollectionCommand = new RelayCommand(async param => await LoadItemsFromCollectionAsync(), param => CanLoadItemsFromCollection());
            StartDaminionQueueCommand = new RelayCommand(async param => await StartDaminionQueueProcessingAsync(), param => CanStartDaminionQueue());
            StopDaminionQueueCommand = new RelayCommand(param => StopDaminionQueueProcessing(), param => CanStopDaminionQueue());
        }

        private void UpdateCommandStates()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
                ((RelayCommand)LoadCollectionsCommand).RaiseCanExecuteChanged();
                ((RelayCommand)LoadItemsFromCollectionCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StartDaminionQueueCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopDaminionQueueCommand).RaiseCanExecuteChanged();
            });
        }

        private bool CanLogin() => !IsLoggedIn && !IsLoadingCollections && !IsLoadingFiles && !IsProcessingDaminionQueue;
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

            // Ensure IsLoggedIn is false before attempting login, to correctly reflect state
            IsLoggedIn = false;

            try
            {
                bool success = await _daminionClient.LoginAsync(
                    _currentSettings.DaminionServerUrl,
                    _currentSettings.DaminionUsername,
                    _currentSettings.DaminionPassword);

                if (success)
                {
                    IsLoggedIn = true; // Set this first
                    DaminionStatus = "Logged in successfully. Attempting to load collections...";
                    await LoadCollectionsAsync(); // Automatically load collections after login success
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
            // UpdateCommandStates is called by IsLoggedIn setter
        }

        private bool CanLoadCollections() => IsLoggedIn && !IsLoadingCollections && !IsLoadingFiles && !IsProcessingDaminionQueue;
        private async Task LoadCollectionsAsync()
        {
            if (_daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                DaminionStatus = "Cannot load collections: Not logged in.";
                UpdateCommandStates(); // Ensure buttons reflect this state
                return;
            }

            IsLoadingCollections = true;
            DaminionStatus = "Loading all tags to find 'Collections' parent tag...";

            // Use Dispatcher for UI collection modifications if called from non-UI thread,
            // though await should bring us back to UI thread if LoadCollectionsAsync was called from it.
            // For safety or if this method could be called from elsewhere:
            Application.Current.Dispatcher.Invoke(() => {
                Collections.Clear();
                DaminionFilesToProcess.Clear();
            });


            try
            {
                var allTagsResponse = await _daminionClient.GetTagsAsync();
                if (allTagsResponse == null || !allTagsResponse.Success || allTagsResponse.Data == null)
                {
                    DaminionStatus = "Failed to load tags from Daminion or no tags found.";
                    IsLoadingCollections = false; // Also triggers UpdateCommandStates
                    return;
                }

                // --- !!! IMPORTANT: IDENTIFY YOUR "COLLECTIONS" PARENT TAG HERE !!! ---
                // This is an example. You MUST determine how collections are represented in your Daminion.
                string collectionsParentTagName = "Collections"; // <<-- CONFIGURABLE or known name
                // string collectionsParentTagName = "Keywords"; // Example: if your "collections" are keywords
                // string collectionsParentTagName = "Categories"; // Example: if your "collections" are categories

                var collectionsParentTag = allTagsResponse.Data.FirstOrDefault(t =>
                    t.Name.Equals(collectionsParentTagName, StringComparison.OrdinalIgnoreCase) && t.Indexed); // Ensure it's an indexed tag to have values

                if (collectionsParentTag == null)
                {
                    DaminionStatus = $"Parent tag '{collectionsParentTagName}' (and indexed) not found. Cannot load collections.";
                    // Log available tags for debugging
                    System.Diagnostics.Debug.WriteLine("Available Daminion Tags for identifying parent collection tag:");
                    foreach (var tag_debug in allTagsResponse.Data.OrderBy(t => t.Name))
                    {
                        System.Diagnostics.Debug.WriteLine($"- Name: {tag_debug.Name}, ID: {tag_debug.Id}, GUID: {tag_debug.Guid}, DataType: {tag_debug.DataType}, Indexed: {tag_debug.Indexed}");
                    }
                    IsLoadingCollections = false; // Also triggers UpdateCommandStates
                    return;
                }

                DaminionStatus = $"Found parent tag '{collectionsParentTag.Name}' (ID: {collectionsParentTag.Id}). Loading its values (collections)...";

                // Fetch top-level values (parentValueId = 0) for this tag
                var tagValuesResponse = await _daminionClient.GetTagValuesAsync(collectionsParentTag.Id, pageSize: 2000, parentValueId: 0);

                if (tagValuesResponse != null && tagValuesResponse.Success && tagValuesResponse.Values != null)
                {
                    var tempCollections = new ObservableCollection<CollectionDisplayItem>();
                    foreach (var tagValue in tagValuesResponse.Values.OrderBy(tv => tv.Text))
                    {
                        tempCollections.Add(new CollectionDisplayItem
                        {
                            Id = tagValue.Id,
                            Name = tagValue.Text,
                            ParentTagId = collectionsParentTag.Id
                        });
                    }
                    Collections = tempCollections; // Replace the collection to ensure UI updates correctly
                    DaminionStatus = Collections.Any() ? $"{Collections.Count} collection(s) loaded under '{collectionsParentTag.Name}'. Select one." : $"No collections found under '{collectionsParentTag.Name}'.";
                }
                else
                {
                    DaminionStatus = $"Failed to load values for tag '{collectionsParentTag.Name}': {tagValuesResponse?.Error ?? "Unknown error from GetTagValuesAsync."}";
                }
            }
            catch (Exception ex)
            {
                DaminionStatus = $"Error loading collections: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in LoadCollectionsAsync: {ex}");
            }
            finally
            {
                IsLoadingCollections = false; // Also triggers UpdateCommandStates
            }
        }

        private bool CanLoadItemsFromCollection() => SelectedCollection != null && IsLoggedIn && !IsLoadingFiles && !IsLoadingCollections && !IsProcessingDaminionQueue;
        private async Task LoadItemsFromCollectionAsync()
        {
            if (SelectedCollection == null || _daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                DaminionStatus = "Cannot load items: No collection selected or not logged in.";
                return;
            }

            IsLoadingFiles = true;
            DaminionStatus = $"Loading items from '{SelectedCollection.Name}'...";
            Application.Current.Dispatcher.Invoke(() => DaminionFilesToProcess.Clear());


            try
            {
                // Construct queryLine: "{TagId},{TagValueId}"
                // Here, SelectedCollection.ParentTagId is the ID of the "Collections" tag,
                // and SelectedCollection.Id is the ID of the specific collection value.
                string queryLine = $"{SelectedCollection.ParentTagId},{SelectedCollection.Id}";

                System.Diagnostics.Debug.WriteLine($"Querying Daminion for items with queryLine: {queryLine}");

                // We need a method in DaminionApiClient to get items based on a query.
                // Let's assume DaminionApiClient has a method like:
                // GetMediaItemsByQueryAsync(string queryLine, int pageSize, int pageIndex)
                // This would wrap "GET /api/mediaItems/get"
                // For now, we'll use GetAbsolutePathsAsync with a placeholder for item IDs,
                // assuming another method `GetItemIdsByQueryAsync` would provide these.
                // THIS PART REQUIRES DaminionApiClient TO HAVE A METHOD TO GET ITEM IDs BY QUERY.

                // Placeholder: Simulate fetching item IDs first.
                // List<long> itemIds = await _daminionClient.GetItemIdsByQueryAsync(queryLine);
                // For this example, let's hardcode some IDs that you might get from a query.
                // In a real scenario, this list of IDs comes from Daminion.

                // --- This is a conceptual placeholder for fetching Item IDs ---
                // You would typically call an API endpoint like /api/mediaItems/get?queryLine={queryLine}
                // This endpoint returns an array of 'Item' objects, each having an 'id'.
                // Let's assume you have a method in your DaminionApiClient:
                // public async Task<List<long>> GetItemIdsByQueryAsync(string queryLine, int limit = 100)
                // For now, we'll just show the structure. This needs to be implemented in DaminionApiClient.

                DaminionStatus = "Fetching item IDs for collection... (Conceptual - Needs GetMediaItems API call)";
                await Task.Delay(100); // Simulate API call

                // Dummy Item IDs - replace with actual API call to get item IDs based on queryLine
                var dummyItemIds = new List<long>(); // { 1, 2, 3 }; // Example IDs

                if (true) // Replace 'true' with check if GetMediaItems API call successful
                {
                    // This part (GetMediaItems) needs to be implemented in DaminionApiClient
                    // It would call /api/mediaItems/get with the queryLine.
                    // The response contains `mediaItems` which is an array of `Item` objects.
                    // Each `Item` has an `id` and `fileName` or `name`.
                    DaminionStatus = "Conceptual: /api/mediaItems/get not yet implemented in DaminionApiClient.";
                    // Add placeholder items to show structure
                    // In reality, you iterate over items returned from /api/mediaItems/get
                    // DaminionFilesToProcess.Add(new DaminionQueueItem(itemId, itemName));
                }


                // For now, let's just use the dummy items previously in the placeholder logic to show UI.
                // This part will be replaced once you fetch actual item IDs and then their paths.
                DaminionFilesToProcess.Add(new DaminionQueueItem(101, "Image_A001.jpg") { FilePath = "C:/path/to/Image_A001.jpg (dummy path)", StatusMessage = "Path is dummy" });
                DaminionFilesToProcess.Add(new DaminionQueueItem(102, "Image_B002.png") { FilePath = "C:/path/to/Image_B002.png (dummy path)", StatusMessage = "Path is dummy" });
                DaminionFilesToProcess.Add(new DaminionQueueItem(103, "Photo_C003.tiff") { FilePath = "C:/path/to/Photo_C003.tiff (dummy path)", StatusMessage = "Path is dummy" });
                DaminionStatus = $"{DaminionFilesToProcess.Count} dummy items loaded for '{SelectedCollection.Name}'. Ready to process. (Actual path fetching needed)";


                // If you had actual itemIds, you would then fetch paths:
                // if (itemIds.Any())
                // {
                //     DaminionPathResult pathResult = await _daminionClient.GetAbsolutePathsAsync(itemIds);
                //     if (pathResult.Success && pathResult.Paths != null)
                //     {
                //         foreach (var itemId in itemIds)
                //         {
                //             if (pathResult.Paths.TryGetValue(itemId.ToString(), out string? filePath))
                //             {
                //                  // You'd also need the item's name/filename from the GetMediaItems call
                //                 DaminionFilesToProcess.Add(new DaminionQueueItem(itemId) { FilePath = filePath, FileName = $"Item {itemId}"});
                //             }
                //             else
                //             {
                //                 DaminionFilesToProcess.Add(new DaminionQueueItem(itemId) { Status = ProcessingStatus.Error, StatusMessage = "Path not found."});
                //             }
                //         }
                //         DaminionStatus = $"{DaminionFilesToProcess.Count} items loaded from '{SelectedCollection.Name}'. Ready to process.";
                //     }
                //     else
                //     {
                //         DaminionStatus = $"Failed to get paths for items: {pathResult.ErrorMessage}";
                //     }
                // }
                // else
                // {
                //     DaminionStatus = $"No item IDs found for query '{queryLine}'.";
                // }
            }
            catch (Exception ex)
            {
                DaminionStatus = $"Error loading items from collection: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in LoadItemsFromCollectionAsync: {ex}");
            }
            finally
            {
                IsLoadingFiles = false; // Also triggers UpdateCommandStates
            }
        }


        private bool CanStartDaminionQueue() => DaminionFilesToProcess.Any(f => f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && IsLoggedIn && !IsProcessingDaminionQueue && !IsLoadingFiles && !IsLoadingCollections;
        private async Task StartDaminionQueueProcessingAsync()
        {
            if (!IsLoggedIn || _daminionClient == null)
            {
                DaminionStatus = "Cannot start: Not logged in to Daminion.";
                return;
            }

            IsProcessingDaminionQueue = true;
            _currentSettings = _settingsService.LoadSettings(); // Refresh app settings (Ollama URL etc.)
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

            // Fetch necessary Daminion Tag GUIDs for updating metadata
            // These should ideally be configurable or reliably fetched
            // For now, placeholders - replace with actual GUIDs from your Daminion setup
            string descTagGuid = "YOUR_DESCRIPTION_TAG_GUID"; // e.g., from Daminion's standard "Description" tag
            string keywordsTagGuid = "YOUR_KEYWORDS_TAG_GUID";   // e.g., from Daminion's standard "Keywords" tag
            string categoriesTagGuid = "YOUR_CATEGORIES_TAG_GUID"; // e.g., from Daminion's standard "Categories" tag
            // You would fetch these from _daminionClient.GetTagsAsync() and match by name, then store the GUID.

            foreach (var item in itemsToProcessThisRun)
            {
                if (token.IsCancellationRequested)
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Daminion queue stopped by user.";
                    break;
                }

                item.Status = ProcessingStatus.Processing;
                item.StatusMessage = "Sending to Ollama...";
                UpdateOverallDaminionStatus($"Processing item: {item.FileName} (ID: {item.DaminionItemId})");

                // 1. Process with Ollama and write to local file
                // Around line 438
                await _processingService.ProcessLocalFileAsync(
                    new FileQueueItem(item.FilePath, item.FileName), // Use the new constructor
                    _currentSettings,
                    (progressMsg) => item.StatusMessage = progressMsg,
                    token);

                // Check status from ProcessLocalFileAsync (it updates the temporary FileQueueItem's status)
                // We need to get the parsed content back from ProcessLocalFileAsync or re-parse
                // For simplicity, let's assume ProcessLocalFileAsync could return ParsedOllamaContent
                // Or we re-do the Ollama call and parse part here if ProcessLocalFileAsync only writes to file
                // Modifying ProcessLocalFileAsync to return ParsedOllamaContent would be cleaner.

                // For now, let's assume the local file was updated and we need to get that info to Daminion.
                // This part is highly conceptual without knowing the output of ProcessLocalFileAsync.
                // We need the ParsedOllamaContent to update Daminion.

                if (item.StatusMessage.StartsWith("Error") || item.StatusMessage.Contains("Cancelled")) // A bit fragile check
                {
                    item.Status = item.StatusMessage.Contains("Cancelled") ? ProcessingStatus.Cancelled : ProcessingStatus.Error;
                    // StatusMessage is already set by ProcessLocalFileAsync callback
                    failureCount++;
                    continue; // Next item
                }

                // If ProcessLocalFileAsync was successful (wrote to local file)
                // Now, update Daminion server metadata.
                // This requires having the ParsedOllamaContent.
                // Let's simulate getting it again for clarity, though ideally ProcessLocalFileAsync would return it.
                ParsedOllamaContent? parsedContentForDaminion = null;
                try
                {
                    byte[] imageBytes = await File.ReadAllBytesAsync(item.FilePath, token);
                    var ollamaClient = new OllamaApiClient(_currentSettings.OllamaServerUrl);
                    var ollamaResponse = await ollamaClient.AnalyzeImageAsync(_currentSettings.OllamaModelName, _currentSettings.OllamaPrompt, imageBytes);
                    if (ollamaResponse != null && ollamaResponse.Done && !string.IsNullOrWhiteSpace(ollamaResponse.Response))
                    {
                        parsedContentForDaminion = OllamaResponseParser.ParseLlavaResponse(ollamaResponse.Response);
                    }
                }
                catch (Exception ex)
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"Failed to re-fetch Ollama content for Daminion update: {ex.Message}";
                    failureCount++;
                    continue;
                }

                if (parsedContentForDaminion == null || !parsedContentForDaminion.SuccessfullyParsed)
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = "Could not get parsed content for Daminion update.";
                    failureCount++;
                    continue;
                }

                item.StatusMessage = "Updating Daminion server...";
                UpdateOverallDaminionStatus($"Updating Daminion for: {item.FileName}");

                var operations = new List<DaminionUpdateOperation>();

                // Description
                if (!string.IsNullOrWhiteSpace(parsedContentForDaminion.Description))
                {
                    operations.Add(new DaminionUpdateOperation { Guid = descTagGuid, Value = parsedContentForDaminion.Description, Id = 0, Remove = false });
                }

                // Keywords (assuming Keywords tag in Daminion is multi-value)
                // For multi-value, Daminion might require clearing existing ones first if you want to overwrite.
                // Or, if it appends, you just add new ones.
                // This example just adds. You may need to refine based on Daminion's behavior for your Keywords tag.
                if (parsedContentForDaminion.Keywords.Any())
                {
                    // Optional: Add an operation to remove all existing keywords for this tag first
                    // operations.Add(new DaminionUpdateOperation { Guid = keywordsTagGuid, Id = 0, Remove = true }); // This might remove the tag itself or all its values, check API behavior
                    foreach (var keyword in parsedContentForDaminion.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                    {
                        operations.Add(new DaminionUpdateOperation { Guid = keywordsTagGuid, Value = keyword, Id = 0, Remove = false });
                    }
                }

                // Categories (similar to keywords)
                if (parsedContentForDaminion.Categories.Any())
                {
                    foreach (var category in parsedContentForDaminion.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
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
                    item.Status = ProcessingStatus.Processed; // Local file processed, but nothing to update in Daminion
                    item.StatusMessage = "Local file processed; no new metadata for Daminion update.";
                    successCount++; // Or handle as a different status
                }
            }

            IsProcessingDaminionQueue = false;
            _daminionCts?.Dispose();
            _daminionCts = null;
            string finalSummary = $"Daminion queue finished. Successful: {successCount}, Failures: {failureCount}.";
            UpdateOverallDaminionStatus(finalSummary);
            // UpdateCommandStates is called by IsProcessingDaminionQueue setter
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
        protected virtual void OnPropertyChanged(string propertyName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
    }
}