// DaminionOllamaApp/ViewModels/MetadataTidyUpViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion;
using DaminionOllamaInteractionLib.Services; // For ImageMetadataService
using Microsoft.Win32;
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
    public class MetadataTidyUpViewModel : INotifyPropertyChanged
    {
        // --- Fields ---
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;
        private DaminionApiClient? _daminionClient;
        private readonly ImageMetadataService _imageMetadataService; // Used for reading/writing local file metadata

        private ObservableCollection<FileQueueItem> _filesToProcess;
        private string _currentOperationStatus = "Select processing mode and add files.";
        private bool _isCleaningQueue;
        private CancellationTokenSource? _cleanupCts;

        // Mode Management
        private bool _isLocalFilesMode = true; // Default to local files mode

        // Daminion Specific Fields
        private bool _isDaminionLoggedIn;
        private string _daminionLoginStatus = "Not logged in.";
        private ObservableCollection<QueryTypeDisplayItem> _daminionQueryTypes;
        private QueryTypeDisplayItem? _selectedDaminionQueryType;
        private bool _isLoadingDaminionItems;

        // Cleanup Options
        private bool _splitCategories = true;
        private bool _trimDescriptionPrefix = true;
        private string _descriptionPrefixToTrim = "Okay, here's a detailed description of the image, broken down as requested:";

        // --- Properties ---
        public ObservableCollection<FileQueueItem> FilesToProcess
        {
            get => _filesToProcess;
            set { SetProperty(ref _filesToProcess, value); }
        }

        public string CurrentOperationStatus
        {
            get => _currentOperationStatus;
            set { SetProperty(ref _currentOperationStatus, value); }
        }

        public bool IsCleaningQueue
        {
            get => _isCleaningQueue;
            set
            {
                if (SetProperty(ref _isCleaningQueue, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        // Mode Management Properties
        public bool IsLocalFilesMode
        {
            get => _isLocalFilesMode;
            set
            {
                if (SetProperty(ref _isLocalFilesMode, value))
                {
                    if (_isLocalFilesMode) IsDaminionCatalogMode = false; // Ensure exclusivity
                    FilesToProcess.Clear(); // Clear list when mode changes
                    CurrentOperationStatus = "Local files mode selected. Add files to tidy up.";
                    UpdateCommandStates();
                }
            }
        }

        private bool _isDaminionCatalogMode;
        public bool IsDaminionCatalogMode
        {
            get => _isDaminionCatalogMode;
            set
            {
                if (SetProperty(ref _isDaminionCatalogMode, value))
                {
                    if (_isDaminionCatalogMode) IsLocalFilesMode = false; // Ensure exclusivity
                    FilesToProcess.Clear(); // Clear list when mode changes
                    CurrentOperationStatus = "Daminion catalog mode selected. Login and select a query.";
                    UpdateCommandStates();
                }
            }
        }

        // Daminion Specific Properties
        public bool IsDaminionLoggedIn
        {
            get => _isDaminionLoggedIn;
            set { SetProperty(ref _isDaminionLoggedIn, value); UpdateCommandStates(); }
        }

        public string DaminionLoginStatus
        {
            get => _daminionLoginStatus;
            set { SetProperty(ref _daminionLoginStatus, value); }
        }

        public ObservableCollection<QueryTypeDisplayItem> DaminionQueryTypes
        {
            get => _daminionQueryTypes;
            set { SetProperty(ref _daminionQueryTypes, value); }
        }

        public QueryTypeDisplayItem? SelectedDaminionQueryType
        {
            get => _selectedDaminionQueryType;
            set
            {
                if (SetProperty(ref _selectedDaminionQueryType, value))
                {
                    Application.Current.Dispatcher.Invoke(() => FilesToProcess.Clear());
                    UpdateCommandStates();
                }
            }
        }

        public bool IsLoadingDaminionItems
        {
            get => _isLoadingDaminionItems;
            set { SetProperty(ref _isLoadingDaminionItems, value); UpdateCommandStates(); }
        }


        // Cleanup Option Properties
        public bool SplitCategories
        {
            get => _splitCategories;
            set { SetProperty(ref _splitCategories, value); }
        }

        public bool TrimDescriptionPrefix
        {
            get => _trimDescriptionPrefix;
            set { SetProperty(ref _trimDescriptionPrefix, value); }
        }

        public string DescriptionPrefixToTrim
        {
            get => _descriptionPrefixToTrim;
            set { SetProperty(ref _descriptionPrefixToTrim, value); }
        }

        // --- Commands ---
        public ICommand AddFilesCommand { get; } // For local files
        public ICommand DaminionLoginCommand { get; }
        public ICommand LoadDaminionItemsCommand { get; }
        public ICommand StartCleanupCommand { get; }
        public ICommand StopCleanupCommand { get; }

        // --- Constructor ---
        public MetadataTidyUpViewModel()
        {
            _settingsService = new SettingsService();
            _currentSettings = _settingsService.LoadSettings();
            _imageMetadataService = new ImageMetadataService(string.Empty); // Dummy path, will be replaced per file

            _filesToProcess = new ObservableCollection<FileQueueItem>();
            _daminionQueryTypes = new ObservableCollection<QueryTypeDisplayItem>
            {
                new QueryTypeDisplayItem { DisplayName = "Unflagged Items", QueryLine = "1,7179;41,1", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Flagged Items", QueryLine = "1,7179;41,2", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Rejected Items", QueryLine = "1,7179;41,3", Operators = "1,any;41,any" }
            };
            SelectedDaminionQueryType = DaminionQueryTypes.FirstOrDefault();

            AddFilesCommand = new RelayCommand(param => AddLocalFiles(), param => CanAddLocalFiles());
            DaminionLoginCommand = new RelayCommand(async param => await LoginToDaminionAsync(), param => CanLoginToDaminion());
            LoadDaminionItemsCommand = new RelayCommand(async param => await LoadDaminionItemsByQueryAsync(), param => CanLoadDaminionItems());
            StartCleanupCommand = new RelayCommand(async param => await StartCleanupAsync(), param => CanStartCleanup());
            StopCleanupCommand = new RelayCommand(param => StopCleanup(), param => CanStopCleanup());
        }

        // --- Command Methods & Helpers ---
        private void UpdateCommandStates()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                (AddFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DaminionLoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (LoadDaminionItemsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StartCleanupCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StopCleanupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }

        // Local Files Mode
        private bool CanAddLocalFiles() => IsLocalFilesMode && !IsCleaningQueue;
        private void AddLocalFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files (*.jpg; *.jpeg; *.png; *.bmp; *.gif; *.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|All files (*.*)|*.*",
                Title = "Select Image Files for Meta Tidy-up"
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
                CurrentOperationStatus = $"{filesAddedCount} local file(s) added to the cleanup queue. {FilesToProcess.Count} total.";
                UpdateCommandStates();
            }
        }

        // Daminion Catalog Mode
        private bool CanLoginToDaminion() => IsDaminionCatalogMode && !IsDaminionLoggedIn && !IsLoadingDaminionItems && !IsCleaningQueue;
        private async Task LoginToDaminionAsync()
        {
            _currentSettings = _settingsService.LoadSettings(); // Refresh Daminion settings
            if (string.IsNullOrWhiteSpace(_currentSettings.DaminionServerUrl) ||
                string.IsNullOrWhiteSpace(_currentSettings.DaminionUsername))
            {
                DaminionLoginStatus = "Error: Daminion server URL or username is not configured.";
                return;
            }

            _daminionClient = new DaminionApiClient();
            DaminionLoginStatus = $"Logging in to Daminion server {_currentSettings.DaminionServerUrl}...";
            IsDaminionLoggedIn = false;

            try
            {
                bool success = await _daminionClient.LoginAsync(
                    _currentSettings.DaminionServerUrl,
                    _currentSettings.DaminionUsername,
                    _currentSettings.DaminionPassword);

                if (success)
                {
                    IsDaminionLoggedIn = true;
                    DaminionLoginStatus = "Daminion login successful. Select a query and load items.";
                }
                else
                {
                    DaminionLoginStatus = "Daminion login failed. Check credentials/URL.";
                    IsDaminionLoggedIn = false;
                }
            }
            catch (Exception ex)
            {
                DaminionLoginStatus = $"Daminion login error: {ex.Message}";
                IsDaminionLoggedIn = false;
                System.Diagnostics.Debug.WriteLine($"Daminion Login Exception (TidyUpVM): {ex}");
            }
        }

        private bool CanLoadDaminionItems() => IsDaminionCatalogMode && IsDaminionLoggedIn && SelectedDaminionQueryType != null && !IsLoadingDaminionItems && !IsCleaningQueue;
        private async Task LoadDaminionItemsByQueryAsync()
        {
            if (SelectedDaminionQueryType == null || _daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                CurrentOperationStatus = "Cannot load items: No query type selected or not logged in to Daminion.";
                return;
            }

            IsLoadingDaminionItems = true;
            CurrentOperationStatus = $"Loading Daminion items for query: '{SelectedDaminionQueryType.DisplayName}'...";
            Application.Current.Dispatcher.Invoke(() => FilesToProcess.Clear());

            try
            {
                DaminionSearchMediaItemsResponse? searchResult = await _daminionClient.SearchMediaItemsAsync(
                    SelectedDaminionQueryType.QueryLine,
                    SelectedDaminionQueryType.Operators,
                    pageSize: 1000);

                if (searchResult != null && searchResult.Success && searchResult.MediaItems != null)
                {
                    if (!searchResult.MediaItems.Any())
                    {
                        CurrentOperationStatus = $"No Daminion items found for query: '{SelectedDaminionQueryType.DisplayName}'.";
                        IsLoadingDaminionItems = false;
                        return;
                    }

                    CurrentOperationStatus = $"{searchResult.MediaItems.Count} Daminion item(s) found. Fetching paths...";
                    var itemIds = searchResult.MediaItems.Select(item => item.Id).ToList();

                    if (!itemIds.Any())
                    {
                        CurrentOperationStatus = $"No Daminion item IDs found to fetch paths for query: '{SelectedDaminionQueryType.DisplayName}'.";
                        IsLoadingDaminionItems = false;
                        return;
                    }

                    DaminionPathResult pathResult = await _daminionClient.GetAbsolutePathsAsync(itemIds);

                    if (pathResult.Success && pathResult.Paths != null)
                    {
                        foreach (var daminionItemFromSearch in searchResult.MediaItems)
                        {
                            string displayName = !string.IsNullOrWhiteSpace(daminionItemFromSearch.Name) ? daminionItemFromSearch.Name :
                                                 (!string.IsNullOrWhiteSpace(daminionItemFromSearch.FileName) ? daminionItemFromSearch.FileName : $"Item {daminionItemFromSearch.Id}");

                            if (pathResult.Paths.TryGetValue(daminionItemFromSearch.Id.ToString(), out string? filePath) && !string.IsNullOrEmpty(filePath))
                            {
                                // Using the FileQueueItem constructor that takes DaminionItemId
                                FilesToProcess.Add(new FileQueueItem(filePath, displayName, daminionItemFromSearch.Id));
                            }
                            else
                            {
                                var errorItem = new FileQueueItem(string.Empty, displayName, daminionItemFromSearch.Id)
                                {
                                    Status = ProcessingStatus.Error,
                                    StatusMessage = $"Path not found for Daminion item ID {daminionItemFromSearch.Id}."
                                };
                                FilesToProcess.Add(errorItem);
                            }
                        }
                        CurrentOperationStatus = $"{FilesToProcess.Count(f => f.Status != ProcessingStatus.Error)} Daminion items loaded with paths. Ready for cleanup.";
                    }
                    else
                    {
                        CurrentOperationStatus = $"Found {searchResult.MediaItems.Count} Daminion items, but failed to get paths: {pathResult.ErrorMessage}";
                        foreach (var daminionItemFromSearch in searchResult.MediaItems)
                        {
                            string displayName = !string.IsNullOrWhiteSpace(daminionItemFromSearch.Name) ? daminionItemFromSearch.Name :
                                                (!string.IsNullOrWhiteSpace(daminionItemFromSearch.FileName) ? daminionItemFromSearch.FileName : $"Item {daminionItemFromSearch.Id}");
                            FilesToProcess.Add(new FileQueueItem(string.Empty, displayName, daminionItemFromSearch.Id)
                            {
                                Status = ProcessingStatus.Error,
                                StatusMessage = $"Failed to retrieve file path. API Error: {pathResult.ErrorMessage}"
                            });
                        }
                    }
                }
                else
                {
                    CurrentOperationStatus = $"Failed to search Daminion items for query '{SelectedDaminionQueryType.DisplayName}': {searchResult?.Error ?? "Unknown API error."}";
                }
            }
            catch (Exception ex)
            {
                CurrentOperationStatus = $"Error loading Daminion items by query: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in LoadDaminionItemsByQueryAsync (TidyUpVM): {ex}");
            }
            finally
            {
                IsLoadingDaminionItems = false;
            }
        }


        // Combined Cleanup Logic
        private bool CanStartCleanup() => FilesToProcess.Any(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath)) && !IsCleaningQueue;
        private async Task StartCleanupAsync()
        {
            IsCleaningQueue = true;
            _currentSettings = _settingsService.LoadSettings(); // Refresh settings
            _cleanupCts = new CancellationTokenSource();
            var token = _cleanupCts.Token;

            CurrentOperationStatus = "Starting metadata cleanup...";
            int cleanedCount = 0;
            int errorCount = 0;

            var itemsToClean = FilesToProcess.Where(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath)).ToList();

            foreach (var item in itemsToClean)
            {
                if (token.IsCancellationRequested)
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Cleanup cancelled by user.";
                    break;
                }

                item.Status = ProcessingStatus.Processing;
                item.StatusMessage = "Reading metadata...";
                UpdateOverallStatus($"Cleaning: {item.FileName}");

                bool changesMade = false;
                try
                {
                    // Use a new instance of ImageMetadataService for each file
                    var metadataService = new ImageMetadataService(item.FilePath);
                    metadataService.Read();

                    // Apply Description Trim
                    if (TrimDescriptionPrefix && !string.IsNullOrEmpty(DescriptionPrefixToTrim) && !string.IsNullOrEmpty(metadataService.Description))
                    {
                        if (metadataService.Description.StartsWith(DescriptionPrefixToTrim, StringComparison.OrdinalIgnoreCase))
                        {
                            string originalDesc = metadataService.Description;
                            metadataService.Description = originalDesc.Substring(DescriptionPrefixToTrim.Length).TrimStart();
                            if (originalDesc != metadataService.Description) changesMade = true;
                            item.StatusMessage = "Description prefix trimmed. ";
                        }
                    }

                    // Apply Category Split
                    if (SplitCategories && metadataService.Categories != null && metadataService.Categories.Any())
                    {
                        var originalCategories = new List<string>(metadataService.Categories);
                        var newCategories = new List<string>();
                        foreach (var catString in originalCategories)
                        {
                            if (!string.IsNullOrWhiteSpace(catString))
                            {
                                if (catString.Contains(','))
                                {
                                    newCategories.AddRange(catString.Split(',')
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                                    changesMade = true; // Assume change if it contained a comma and was split
                                }
                                else
                                {
                                    newCategories.Add(catString.Trim());
                                }
                            }
                        }
                        // Update only if there was a meaningful change after splitting and distinctive add
                        var distinctNewCategories = newCategories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        if (!originalCategories.SequenceEqual(distinctNewCategories, StringComparer.OrdinalIgnoreCase) || distinctNewCategories.Count != originalCategories.Count || changesMade) // Simplified check, might need better comparison
                        {
                            metadataService.Categories = distinctNewCategories;
                            changesMade = true; // Ensure it's true if categories changed
                            item.StatusMessage += "Categories split/tidied. ";
                        }
                    }

                    if (changesMade)
                    {
                        metadataService.Save();
                        item.StatusMessage += "Changes saved to local file. ";
                    }
                    else
                    {
                        item.StatusMessage += "No applicable changes made to local file. ";
                    }

                    // If it's a Daminion item, update Daminion server
                    if (IsDaminionCatalogMode && item.DaminionItemId.HasValue && _daminionClient != null && _daminionClient.IsAuthenticated && changesMade)
                    {
                        item.StatusMessage += "Updating Daminion server...";
                        UpdateOverallStatus($"Updating Daminion for: {item.FileName}");

                        var operations = new List<DaminionUpdateOperation>();
                        if (!string.IsNullOrWhiteSpace(metadataService.Description))
                            operations.Add(new DaminionUpdateOperation { Guid = _currentSettings.DaminionDescriptionTagGuid, Value = metadataService.Description, Id = 0, Remove = false });

                        if (metadataService.Keywords != null && metadataService.Keywords.Any()) // Assuming keywords were not directly modified by tidy but should be preserved if re-writing all. Or only add cleaned categories
                        {
                            // If you want to clear existing keywords first:
                            // operations.Add(new DaminionUpdateOperation { Guid = _currentSettings.DaminionKeywordsTagGuid, RemoveAll = true }); // Fictional RemoveAll, check Daminion API
                            foreach (var keyword in metadataService.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                                operations.Add(new DaminionUpdateOperation { Guid = _currentSettings.DaminionKeywordsTagGuid, Value = keyword, Id = 0, Remove = false });
                        }
                        if (metadataService.Categories != null && metadataService.Categories.Any())
                        {
                            // If you want to clear existing categories first:
                            // operations.Add(new DaminionUpdateOperation { Guid = _currentSettings.DaminionCategoriesTagGuid, RemoveAll = true }); // Fictional
                            foreach (var category in metadataService.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                                operations.Add(new DaminionUpdateOperation { Guid = _currentSettings.DaminionCategoriesTagGuid, Value = category, Id = 0, Remove = false });
                        }

                        if (operations.Any())
                        {
                            var updateResult = await _daminionClient.UpdateItemMetadataAsync(new List<long> { item.DaminionItemId.Value }, operations);
                            if (updateResult != null && updateResult.Success)
                            {
                                item.StatusMessage += "Daminion metadata updated.";
                            }
                            else
                            {
                                item.StatusMessage += $"Daminion server update failed: {updateResult?.Error ?? "Unknown"}.";
                                item.Status = ProcessingStatus.Error; // Mark as error if Daminion update fails
                            }
                        }
                        else
                        {
                            item.StatusMessage += "No metadata operations to send to Daminion.";
                        }
                    }

                    if (item.Status != ProcessingStatus.Error) // If not already marked as error
                    {
                        item.Status = ProcessingStatus.Processed; // Mark as processed if all steps passed or no Daminion update was needed
                        item.StatusMessage = item.StatusMessage.TrimEnd(' ');
                        if (item.StatusMessage.EndsWith("...")) item.StatusMessage = "Cleanup successful.";
                    }


                    if (item.Status == ProcessingStatus.Processed) cleanedCount++; else errorCount++;
                }
                catch (OperationCanceledException) { throw; } // Propagate to be caught by outer handler
                catch (Exception ex)
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"Error cleaning file: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"Error cleaning {item.FileName}: {ex}");
                    errorCount++;
                }
            } // End foreach

            IsCleaningQueue = false;
            _cleanupCts?.Dispose();
            _cleanupCts = null;
            CurrentOperationStatus = $"Cleanup finished. Processed: {cleanedCount}, Errors: {errorCount}, Cancelled: {FilesToProcess.Count(f => f.Status == ProcessingStatus.Cancelled)}.";
        }

        private void UpdateOverallStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(() => CurrentOperationStatus = message);
        }

        private bool CanStopCleanup() => IsCleaningQueue;

        private void StopCleanup()
        {
            _cleanupCts?.Cancel();
            CurrentOperationStatus = "Cleanup stop requested.";
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
    }
}