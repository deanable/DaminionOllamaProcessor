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
    /// <summary>
    /// View model responsible for managing the AI-powered tagging workflow for Daminion collection items.
    /// This class handles the complete process of:
    /// 1. Connecting to Daminion DAM system
    /// 2. Querying and loading items from the collection
    /// 3. Processing images with AI services (Ollama or OpenRouter)
    /// 4. Updating metadata back to the Daminion system
    /// 
    /// The workflow supports batch processing with progress tracking and error handling.
    /// </summary>
    public class DaminionCollectionTaggerViewModel : INotifyPropertyChanged
    {
        #region Private Fields
        /// <summary>
        /// Service for loading and saving application settings.
        /// </summary>
        private readonly SettingsService _settingsService;
        
        /// <summary>
        /// Client for interacting with the Daminion API.
        /// Handles authentication, item querying, and metadata updates.
        /// </summary>
        private DaminionApiClient? _daminionClient;

        /// <summary>
        /// Indicates whether the user is currently authenticated with Daminion.
        /// </summary>
        private bool _isLoggedIn;
        
        /// <summary>
        /// Current status message for the Daminion connection and operations.
        /// </summary>
        private string _daminionStatus = "Not logged in. Please configure Daminion settings and click Login.";
        
        /// <summary>
        /// Collection of available query types for loading items from Daminion.
        /// </summary>
        private ObservableCollection<QueryTypeDisplayItem> _queryTypes;
        
        /// <summary>
        /// Currently selected query type for loading items.
        /// </summary>
        private QueryTypeDisplayItem? _selectedQueryType;
        
        /// <summary>
        /// Collection of Daminion items queued for processing.
        /// </summary>
        private ObservableCollection<DaminionQueueItem> _daminionFilesToProcess;
        
        /// <summary>
        /// Indicates whether items are currently being loaded from Daminion.
        /// </summary>
        private bool _isLoadingItems;
        
        /// <summary>
        /// Indicates whether the processing queue is currently running.
        /// </summary>
        private bool _isProcessingDaminionQueue;
        
        /// <summary>
        /// Cancellation token source for stopping the processing queue.
        /// </summary>
        private CancellationTokenSource? _daminionCts;
        #endregion

        #region Public Properties
        /// <summary>
        /// Reference to the application settings containing configuration for:
        /// - Daminion server connection details
        /// - AI service preferences (Ollama/OpenRouter)
        /// - Default prompts and processing options
        /// </summary>
        public AppSettings Settings { get; }

        /// <summary>
        /// Gets or sets whether the user is authenticated with Daminion.
        /// Updates command availability when changed.
        /// </summary>
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { SetProperty(ref _isLoggedIn, value); UpdateCommandStates(); }
        }

        /// <summary>
        /// Gets or sets the current status message for Daminion operations.
        /// Displayed in the UI to inform users of current state.
        /// </summary>
        public string DaminionStatus
        {
            get => _daminionStatus;
            set { SetProperty(ref _daminionStatus, value); }
        }

        /// <summary>
        /// Gets the collection of available query types for loading items from Daminion.
        /// Each query type represents a different way to filter and retrieve items.
        /// </summary>
        public ObservableCollection<QueryTypeDisplayItem> QueryTypes
        {
            get => _queryTypes;
            set { SetProperty(ref _queryTypes, value); }
        }

        /// <summary>
        /// Gets or sets the currently selected query type.
        /// Updates command availability when changed.
        /// </summary>
        public QueryTypeDisplayItem? SelectedQueryType
        {
            get => _selectedQueryType;
            set 
            { 
                SetProperty(ref _selectedQueryType, value);
                UpdateCommandStates();
                
                // Update the settings with the selected query type for persistence
                if (value != null)
                {
                    Settings.DaminionQueryType = value.QueryType;
                    Settings.DaminionQueryLine = value.QueryLine;
                }
            }
        }

        /// <summary>
        /// Gets the collection of Daminion items queued for AI processing.
        /// Each item represents a media file with its current processing status.
        /// </summary>
        public ObservableCollection<DaminionQueueItem> DaminionFilesToProcess
        {
            get => _daminionFilesToProcess;
            set 
            { 
                SetProperty(ref _daminionFilesToProcess, value);
                UpdateCommandStates();
            }
        }

        /// <summary>
        /// Gets or sets whether items are currently being loaded from Daminion.
        /// Disables certain commands during loading to prevent conflicts.
        /// </summary>
        public bool IsLoadingItems
        {
            get => _isLoadingItems;
            set { SetProperty(ref _isLoadingItems, value); UpdateCommandStates(); }
        }

        /// <summary>
        /// Gets or sets whether the processing queue is currently running.
        /// Controls the availability of start/stop commands.
        /// </summary>
        public bool IsProcessingDaminionQueue
        {
            get => _isProcessingDaminionQueue;
            set { SetProperty(ref _isProcessingDaminionQueue, value); UpdateCommandStates(); }
        }
        #endregion

        #region Commands
        /// <summary>
        /// Command to authenticate with the Daminion server.
        /// </summary>
        public ICommand LoginCommand { get; }
        
        /// <summary>
        /// Command to load items from Daminion based on the selected query type.
        /// </summary>
        public ICommand LoadItemsByQueryCommand { get; }
        
        /// <summary>
        /// Command to start processing the queue of loaded Daminion items.
        /// </summary>
        public ICommand StartDaminionQueueCommand { get; }
        
        /// <summary>
        /// Command to stop the currently running processing queue.
        /// </summary>
        public ICommand StopDaminionQueueCommand { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the DaminionCollectionTaggerViewModel.
        /// Sets up the initial state and available query types.
        /// </summary>
        /// <param name="settings">Application settings instance.</param>
        /// <param name="settingsService">Service for persisting settings.</param>
        public DaminionCollectionTaggerViewModel(AppSettings settings, SettingsService settingsService)
        {
            Settings = settings;
            _settingsService = settingsService;
            
            // Initialize collections
            _daminionFilesToProcess = new ObservableCollection<DaminionQueueItem>();
            _queryTypes = new ObservableCollection<QueryTypeDisplayItem>();
            
            // Set up available query types for different search scenarios
            InitializeQueryTypes();
            
            // Initialize commands with their respective handlers and can-execute predicates
            LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
            LoadItemsByQueryCommand = new AsyncRelayCommand(LoadItemsByQueryAsync, CanLoadItemsByQuery);
            StartDaminionQueueCommand = new AsyncRelayCommand(StartDaminionQueueProcessingAsync, CanStartDaminionQueue);
            StopDaminionQueueCommand = new RelayCommand(param => StopDaminionQueueProcessing(), CanStopDaminionQueue);
        }
        #endregion

        #region Private Methods - Command State Management
        /// <summary>
        /// Updates the can-execute state of all commands based on current conditions.
        /// Called whenever a property changes that affects command availability.
        /// </summary>
        private void UpdateCommandStates()
        {
            // Trigger re-evaluation of can-execute predicates
            (LoginCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (LoadItemsByQueryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (StartDaminionQueueCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (StopDaminionQueueCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Initializes the available query types for loading items from Daminion.
        /// Each query type represents a different search scenario.
        /// </summary>
        private void InitializeQueryTypes()
        {
            _queryTypes.Add(new QueryTypeDisplayItem 
            { 
                DisplayName = "All Items", 
                QueryType = "all", 
                QueryLine = "" 
            });
            _queryTypes.Add(new QueryTypeDisplayItem 
            { 
                DisplayName = "Items without Description", 
                QueryType = "no_description", 
                QueryLine = "Description is empty" 
            });
            _queryTypes.Add(new QueryTypeDisplayItem 
            { 
                DisplayName = "Items without Keywords", 
                QueryType = "no_keywords", 
                QueryLine = "Keywords is empty" 
            });
            _queryTypes.Add(new QueryTypeDisplayItem 
            { 
                DisplayName = "Items without Categories", 
                QueryType = "no_categories", 
                QueryLine = "Categories is empty" 
            });
            _queryTypes.Add(new QueryTypeDisplayItem 
            { 
                DisplayName = "Custom Query", 
                QueryType = "custom", 
                QueryLine = Settings.DaminionQueryLine 
            });
        }
        #endregion

        #region Command Handlers - Login
        /// <summary>
        /// Determines if the login command can be executed.
        /// </summary>
        /// <returns>True if login is possible, false otherwise.</returns>
        private bool CanLogin() => !IsLoggedIn && !IsLoadingItems && !IsProcessingDaminionQueue;

        /// <summary>
        /// Handles the login process to authenticate with Daminion.
        /// Validates settings and establishes a connection to the server.
        /// </summary>
        private async Task LoginAsync()
        {
            // Validate required settings
            if (string.IsNullOrWhiteSpace(Settings.DaminionServerUrl) ||
                string.IsNullOrWhiteSpace(Settings.DaminionUsername) ||
                string.IsNullOrWhiteSpace(Settings.DaminionPassword))
            {
                DaminionStatus = "Please configure Daminion settings first.";
                return;
            }

            try
            {
                DaminionStatus = "Logging in...";
                
                // Initialize the API client if not already done
                _daminionClient ??= new DaminionApiClient();
                
                // Attempt to authenticate with the server
                bool loginSuccess = await _daminionClient.LoginAsync(
                    Settings.DaminionServerUrl, 
                    Settings.DaminionUsername, 
                    Settings.DaminionPassword);

                if (loginSuccess)
                {
                    IsLoggedIn = true;
                    DaminionStatus = "Successfully logged in to Daminion.";
                    
                    // Restore previously selected query type if available
                    var savedQueryType = QueryTypes.FirstOrDefault(q => q.QueryType == Settings.DaminionQueryType);
                    if (savedQueryType != null)
                    {
                        SelectedQueryType = savedQueryType;
                    }
                }
                else
                {
                    DaminionStatus = "Login failed. Please check your credentials.";
                }
            }
            catch (Exception ex)
            {
                DaminionStatus = $"Login error: {ex.Message}";
            }
        }
        #endregion

        #region Command Handlers - Load Items
        /// <summary>
        /// Determines if the load items command can be executed.
        /// </summary>
        /// <returns>True if items can be loaded, false otherwise.</returns>
        private bool CanLoadItemsByQuery() => SelectedQueryType != null && IsLoggedIn && !IsLoadingItems && !IsProcessingDaminionQueue;

        /// <summary>
        /// Loads items from Daminion based on the selected query type.
        /// Retrieves media items and their metadata for processing.
        /// </summary>
        private async Task LoadItemsByQueryAsync()
        {
            if (_daminionClient == null || SelectedQueryType == null)
                return;

            try
            {
                IsLoadingItems = true;
                DaminionStatus = "Loading items from Daminion...";
                
                // Clear existing items
                DaminionFilesToProcess.Clear();

                // Execute the search query
                var response = await _daminionClient.SearchMediaItemsAsync(
                    queryLine: SelectedQueryType.QueryLine,
                    pageSize: 100,
                    pageIndex: 0);

                if (response?.Items != null)
                {
                    // Get absolute file paths for the items
                    var itemIds = response.Items.Select(item => item.Id).ToList();
                    var pathResult = await _daminionClient.GetAbsolutePathsAsync(itemIds);

                    // Create queue items with file paths
                    foreach (var item in response.Items)
                    {
                        var filePath = pathResult.PathMappings.ContainsKey(item.Id) 
                            ? pathResult.PathMappings[item.Id] 
                            : null;

                        var queueItem = new DaminionQueueItem
                        {
                            Id = item.Id,
                            FileName = item.FileName,
                            FilePath = filePath,
                            Status = ProcessingStatus.Unprocessed,
                            StatusMessage = "Ready for processing"
                        };

                        // Add to the processing queue
                        DaminionFilesToProcess.Add(queueItem);
                    }

                    DaminionStatus = $"Loaded {DaminionFilesToProcess.Count} items from Daminion.";
                }
                else
                {
                    DaminionStatus = "No items found matching the query.";
                }
            }
            catch (Exception ex)
            {
                DaminionStatus = $"Error loading items: {ex.Message}";
            }
            finally
            {
                IsLoadingItems = false;
            }
        }
        #endregion

        #region Command Handlers - Queue Processing
        /// <summary>
        /// Determines if the queue processing can be started.
        /// </summary>
        /// <returns>True if processing can start, false otherwise.</returns>
        private bool CanStartDaminionQueue() => DaminionFilesToProcess.Any(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath)) && IsLoggedIn && !IsProcessingDaminionQueue && !IsLoadingItems;

        /// <summary>
        /// Starts the batch processing of queued Daminion items.
        /// Processes each item with AI services and updates metadata in Daminion.
        /// </summary>
        private async Task StartDaminionQueueProcessingAsync()
        {
            if (_daminionClient == null)
                return;

            try
            {
                // Initialize cancellation token for stopping the process
                _daminionCts = new CancellationTokenSource();
                IsProcessingDaminionQueue = true;
                
                UpdateOverallDaminionStatus("Processing started...");

                // Get items that need processing
                var itemsToProcess = DaminionFilesToProcess
                    .Where(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) 
                                && !string.IsNullOrEmpty(f.FilePath))
                    .ToList();

                int processedCount = 0;
                int totalCount = itemsToProcess.Count;

                // Process each item individually
                foreach (var item in itemsToProcess)
                {
                    // Check for cancellation
                    if (_daminionCts.Token.IsCancellationRequested)
                    {
                        item.Status = ProcessingStatus.Cancelled;
                        item.StatusMessage = "Processing cancelled";
                        break;
                    }

                    try
                    {
                        // Update item status
                        item.Status = ProcessingStatus.Processing;
                        item.StatusMessage = "Processing...";
                        
                        UpdateOverallDaminionStatus($"Processing item {processedCount + 1} of {totalCount}: {item.FileName}");

                        // Validate file exists
                        if (!File.Exists(item.FilePath))
                        {
                            item.Status = ProcessingStatus.Error;
                            item.StatusMessage = "File not found";
                            continue;
                        }

                        // Read image data
                        byte[] imageBytes = await File.ReadAllBytesAsync(item.FilePath, _daminionCts.Token);

                        // Process with AI service
                        string aiResponse = await ProcessWithAIService(imageBytes, _daminionCts.Token);
                        
                        // Parse the AI response
                        var parsedContent = OllamaResponseParser.ParseLlavaResponse(aiResponse);
                        
                        // Update metadata in Daminion
                        await UpdateDaminionMetadata(item.Id, parsedContent);

                        // Update item status
                        item.Status = ProcessingStatus.Completed;
                        item.StatusMessage = "Successfully processed";
                        
                        processedCount++;
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = ProcessingStatus.Cancelled;
                        item.StatusMessage = "Processing cancelled";
                        break;
                    }
                    catch (Exception ex)
                    {
                        item.Status = ProcessingStatus.Error;
                        item.StatusMessage = $"Error: {ex.Message}";
                    }
                }

                // Update final status
                if (_daminionCts.Token.IsCancellationRequested)
                {
                    UpdateOverallDaminionStatus($"Processing cancelled. {processedCount} of {totalCount} items processed.");
                }
                else
                {
                    UpdateOverallDaminionStatus($"Processing completed. {processedCount} of {totalCount} items processed successfully.");
                }
            }
            catch (Exception ex)
            {
                UpdateOverallDaminionStatus($"Processing error: {ex.Message}");
            }
            finally
            {
                IsProcessingDaminionQueue = false;
                _daminionCts?.Dispose();
                _daminionCts = null;
            }
        }

        /// <summary>
        /// Processes an image with the configured AI service (Ollama or OpenRouter).
        /// </summary>
        /// <param name="imageBytes">The image data to process.</param>
        /// <param name="cancellationToken">Token for canceling the operation.</param>
        /// <returns>The AI-generated response text.</returns>
        private async Task<string> ProcessWithAIService(byte[] imageBytes, CancellationToken cancellationToken)
        {
            if (Settings.UseOpenRouter)
            {
                // Use OpenRouter service
                var openRouterClient = new OpenRouterClient(Settings.OpenRouterApiKey);
                var response = await openRouterClient.AnalyzeImageAsync(
                    Settings.OpenRouterModel, 
                    Settings.DaminionProcessingPrompt, 
                    imageBytes, 
                    cancellationToken);
                return response?.Content ?? "No response from OpenRouter";
            }
            else
            {
                // Use Ollama service
                var ollamaClient = new OllamaApiClient(Settings.OllamaServerUrl);
                var response = await ollamaClient.AnalyzeImageAsync(
                    Settings.OllamaModel, 
                    Settings.DaminionProcessingPrompt, 
                    imageBytes);
                return response?.Response ?? "No response from Ollama";
            }
        }

        /// <summary>
        /// Updates the metadata for a Daminion item with the parsed AI content.
        /// </summary>
        /// <param name="itemId">The ID of the item to update.</param>
        /// <param name="content">The parsed AI-generated content.</param>
        private async Task UpdateDaminionMetadata(long itemId, ParsedOllamaContent content)
        {
            if (_daminionClient == null)
                return;

            var operations = new List<DaminionUpdateOperation>();

            // Update description if available
            if (!string.IsNullOrWhiteSpace(content.Description))
            {
                operations.Add(new DaminionUpdateOperation
                {
                    TagGuid = Settings.DaminionDescriptionTagGuid,
                    Operation = "set",
                    Value = content.Description
                });
            }

            // Update keywords if available
            if (content.Keywords?.Any() == true)
            {
                operations.Add(new DaminionUpdateOperation
                {
                    TagGuid = Settings.DaminionKeywordsTagGuid,
                    Operation = "set",
                    Value = string.Join(", ", content.Keywords)
                });
            }

            // Update categories if available
            if (content.Categories?.Any() == true)
            {
                operations.Add(new DaminionUpdateOperation
                {
                    TagGuid = Settings.DaminionCategoriesTagGuid,
                    Operation = "set",
                    Value = string.Join(", ", content.Categories)
                });
            }

            // Execute the updates
            if (operations.Any())
            {
                await _daminionClient.UpdateItemMetadataAsync(new List<long> { itemId }, operations);
            }
        }

        /// <summary>
        /// Updates the overall status message for the processing operation.
        /// </summary>
        /// <param name="message">The status message to display.</param>
        private void UpdateOverallDaminionStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DaminionStatus = message;
            });
        }

        /// <summary>
        /// Determines if the queue processing can be stopped.
        /// </summary>
        /// <returns>True if processing can be stopped, false otherwise.</returns>
        private bool CanStopDaminionQueue() => IsProcessingDaminionQueue;

        /// <summary>
        /// Stops the currently running queue processing operation.
        /// </summary>
        private void StopDaminionQueueProcessing()
        {
            _daminionCts?.Cancel();
            UpdateOverallDaminionStatus("Stopping processing...");
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        /// <summary>
        /// Event raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Sets a property value and raises the PropertyChanged event if the value has changed.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="storage">Reference to the backing field.</param>
        /// <param name="value">The new value to set.</param>
        /// <param name="propertyName">The name of the property (automatically provided).</param>
        /// <returns>True if the value was changed, false otherwise.</returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}