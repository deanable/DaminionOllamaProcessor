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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    public class MetadataTidyUpViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;
        private DaminionApiClient? _daminionClient;

        private ObservableCollection<FileQueueItem> _filesToProcess;
        private string _currentOperationStatus = "Select processing mode and add files.";
        private bool _isCleaningQueue;
        private CancellationTokenSource? _cleanupCts;

        private bool _isLocalFilesMode = true;
        private bool _isDaminionCatalogMode;
        private bool _isDaminionLoggedIn;
        private string _daminionLoginStatus = "Not logged in.";
        private ObservableCollection<QueryTypeDisplayItem> _daminionQueryTypes;
        private QueryTypeDisplayItem? _selectedDaminionQueryType;
        private bool _isLoadingDaminionItems;

        private bool _splitCategories = true;
        private bool _trimDescriptionPrefix = true;
        private string _descriptionPrefixToTrim = "Okay, here's a detailed description of the image, broken down as requested:";

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
            set { if (SetProperty(ref _isCleaningQueue, value)) UpdateCommandStates(); }
        }

        public bool IsLocalFilesMode
        {
            get => _isLocalFilesMode;
            set
            {
                if (SetProperty(ref _isLocalFilesMode, value))
                {
                    if (_isLocalFilesMode) IsDaminionCatalogMode = false;
                    Application.Current.Dispatcher.Invoke(() => FilesToProcess.Clear());
                    CurrentOperationStatus = "Local files mode selected. Add files to tidy up.";
                    UpdateCommandStates();
                }
            }
        }

        public bool IsDaminionCatalogMode
        {
            get => _isDaminionCatalogMode;
            set
            {
                if (SetProperty(ref _isDaminionCatalogMode, value))
                {
                    if (_isDaminionCatalogMode) IsLocalFilesMode = false;
                    Application.Current.Dispatcher.Invoke(() => FilesToProcess.Clear());
                    CurrentOperationStatus = "Daminion catalog mode selected. Login and select a query.";
                    UpdateCommandStates();
                }
            }
        }

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

        public ICommand AddFilesCommand { get; }
        public ICommand DaminionLoginCommand { get; }
        public ICommand LoadDaminionItemsCommand { get; }
        public ICommand StartCleanupCommand { get; }
        public ICommand StopCleanupCommand { get; }

        public MetadataTidyUpViewModel()
        {
            _settingsService = new SettingsService();
            _currentSettings = _settingsService.LoadSettings();

            _filesToProcess = new ObservableCollection<FileQueueItem>();
            _daminionQueryTypes = new ObservableCollection<QueryTypeDisplayItem>
            {
                new QueryTypeDisplayItem { DisplayName = "Unflagged Items", QueryLine = "1,7179;41,1", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Flagged Items", QueryLine = "1,7179;41,2", Operators = "1,any;41,any" },
                new QueryTypeDisplayItem { DisplayName = "Rejected Items", QueryLine = "1,7179;41,3", Operators = "1,any;41,any" }
            };
            SelectedDaminionQueryType = _daminionQueryTypes.FirstOrDefault();

            AddFilesCommand = new RelayCommand(param => AddLocalFiles(), param => CanAddLocalFiles());
            DaminionLoginCommand = new RelayCommand(async param => await LoginToDaminionAsync(), param => CanLoginToDaminion());
            LoadDaminionItemsCommand = new RelayCommand(async param => await LoadDaminionItemsByQueryAsync(), param => CanLoadDaminionItems());
            StartCleanupCommand = new RelayCommand(async param => await StartCleanupAsync(), param => CanStartCleanup());
            StopCleanupCommand = new RelayCommand(param => StopCleanup(), param => CanStopCleanup());
        }

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

        private bool CanAddLocalFiles() => IsLocalFilesMode && !IsCleaningQueue && !IsLoadingDaminionItems;
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
                CurrentOperationStatus = $"{filesAddedCount} local file(s) added. {FilesToProcess.Count} total.";
                UpdateCommandStates();
            }
        }

        private bool CanLoginToDaminion() => IsDaminionCatalogMode && !IsDaminionLoggedIn && !IsLoadingDaminionItems && !IsCleaningQueue;
        private async Task LoginToDaminionAsync()
        {
            _currentSettings = _settingsService.LoadSettings();
            if (string.IsNullOrWhiteSpace(_currentSettings.DaminionServerUrl) ||
                string.IsNullOrWhiteSpace(_currentSettings.DaminionUsername))
            {
                DaminionLoginStatus = "Error: Daminion server URL or username is not configured.";
                return;
            }

            _daminionClient = new DaminionApiClient();
            DaminionLoginStatus = $"Logging in to Daminion: {_currentSettings.DaminionServerUrl}...";
            IsDaminionLoggedIn = false;

            try
            {
                bool success = await _daminionClient.LoginAsync(
                    _currentSettings.DaminionServerUrl,
                    _currentSettings.DaminionUsername,
                    _currentSettings.DaminionPassword);

                IsDaminionLoggedIn = success;
                DaminionLoginStatus = success ? "Daminion login successful. Select query and load items." : "Daminion login failed.";
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
                CurrentOperationStatus = "Cannot load: No query type selected or not logged in to Daminion.";
                return;
            }

            IsLoadingDaminionItems = true;
            CurrentOperationStatus = $"Loading Daminion items for: '{SelectedDaminionQueryType.DisplayName}'...";
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
                        CurrentOperationStatus = $"No Daminion items found for: '{SelectedDaminionQueryType.DisplayName}'.";
                    }
                    else
                    {
                        CurrentOperationStatus = $"{searchResult.MediaItems.Count} Daminion item(s) found. Fetching paths...";
                        var itemIds = searchResult.MediaItems.Select(item => item.Id).ToList();

                        if (!itemIds.Any())
                        {
                            CurrentOperationStatus = $"No Daminion item IDs to fetch paths for: '{SelectedDaminionQueryType.DisplayName}'.";
                        }
                        else
                        {
                            DaminionPathResult pathResult = await _daminionClient.GetAbsolutePathsAsync(itemIds);
                            if (pathResult.Success && pathResult.Paths != null)
                            {
                                foreach (var daminionItemFromSearch in searchResult.MediaItems)
                                {
                                    string displayName = !string.IsNullOrWhiteSpace(daminionItemFromSearch.Name) ? daminionItemFromSearch.Name :
                                                         (!string.IsNullOrWhiteSpace(daminionItemFromSearch.FileName) ? daminionItemFromSearch.FileName : $"Item {daminionItemFromSearch.Id}");

                                    if (pathResult.Paths.TryGetValue(daminionItemFromSearch.Id.ToString(), out string? filePath) && !string.IsNullOrEmpty(filePath))
                                    {
                                        FilesToProcess.Add(new FileQueueItem(filePath, displayName, daminionItemFromSearch.Id));
                                    }
                                    else
                                    {
                                        FilesToProcess.Add(new FileQueueItem(string.Empty, displayName, daminionItemFromSearch.Id)
                                        { Status = ProcessingStatus.Error, StatusMessage = $"Path not found for Daminion ID {daminionItemFromSearch.Id}." });
                                    }
                                }
                                CurrentOperationStatus = $"{FilesToProcess.Count(f => f.Status != ProcessingStatus.Error)} Daminion items loaded. Ready for cleanup.";
                            }
                            else
                            {
                                CurrentOperationStatus = $"Found {searchResult.MediaItems.Count} items, but failed to get paths: {pathResult.ErrorMessage}";
                                foreach (var daminionItemFromSearch in searchResult.MediaItems)
                                {
                                    string displayName = !string.IsNullOrWhiteSpace(daminionItemFromSearch.Name) ? daminionItemFromSearch.Name :
                                                        (!string.IsNullOrWhiteSpace(daminionItemFromSearch.FileName) ? daminionItemFromSearch.FileName : $"Item {daminionItemFromSearch.Id}");
                                    FilesToProcess.Add(new FileQueueItem(string.Empty, displayName, daminionItemFromSearch.Id)
                                    { Status = ProcessingStatus.Error, StatusMessage = $"Path retrieval failed. API Error: {pathResult.ErrorMessage}" });
                                }
                            }
                        }
                    }
                }
                else
                {
                    CurrentOperationStatus = $"Failed to search Daminion items: {searchResult?.Error ?? "Unknown API error."}";
                }
            }
            catch (Exception ex)
            {
                CurrentOperationStatus = $"Error loading Daminion items: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in LoadDaminionItemsByQueryAsync (TidyUpVM): {ex}");
            }
            finally
            {
                IsLoadingDaminionItems = false;
            }
        }

        private bool CanStartCleanup() => FilesToProcess.Any(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath)) && !IsCleaningQueue && !IsLoadingDaminionItems;

        // =======================================================================================
        // REFINED StartCleanupAsync METHOD
        // =======================================================================================
        private async Task StartCleanupAsync()
        {
            IsCleaningQueue = true;
            _currentSettings = _settingsService.LoadSettings();
            _cleanupCts = new CancellationTokenSource();
            var token = _cleanupCts.Token;

            CurrentOperationStatus = "Starting metadata cleanup...";
            int processedCount = 0;
            int errorCount = 0;

            var itemsToClean = FilesToProcess.Where(f => (f.Status == ProcessingStatus.Unprocessed || f.Status == ProcessingStatus.Error) && !string.IsNullOrEmpty(f.FilePath)).ToList();

            if (IsDaminionCatalogMode &&
                (string.IsNullOrWhiteSpace(_currentSettings.DaminionDescriptionTagGuid) ||
                 string.IsNullOrWhiteSpace(_currentSettings.DaminionKeywordsTagGuid) || // Ensure Keywords GUID is also checked if you plan to update it
                 string.IsNullOrWhiteSpace(_currentSettings.DaminionCategoriesTagGuid)))
            {
                CurrentOperationStatus = "Error: Key Daminion Tag GUIDs (Description, Keywords, Categories) are not set in AppSettings. Cannot update Daminion catalog.";
                IsCleaningQueue = false;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Starting cleanup. SplitCategories: {SplitCategories}, TrimDescriptionPrefix: {TrimDescriptionPrefix}, Prefix: '{DescriptionPrefixToTrim}'");

            try
            {
                foreach (var item in itemsToClean)
                {
                    if (token.IsCancellationRequested)
                    {
                        item.Status = ProcessingStatus.Cancelled;
                        item.StatusMessage = "Cleanup cancelled by user.";
                        break;
                    }

                    item.Status = ProcessingStatus.Processing;
                    item.StatusMessage = "";
                    UpdateOverallStatus($"Tidying: {item.FileName}");
                    System.Diagnostics.Debug.WriteLine($"Tidying: {item.FileName}");

                    bool changesMadeToLocalFile = false;
                    ImageMetadataService metadataService = new ImageMetadataService(item.FilePath);

                    try
                    {
                        metadataService.Read();
                        System.Diagnostics.Debug.WriteLine($"  Read metadata. Desc: '{metadataService.Description?.Substring(0, Math.Min(50, metadataService.Description?.Length ?? 0))}', Cats: {string.Join(";", metadataService.Categories ?? new List<string>())}");

                        // 1. Trim Description Prefix
                        if (TrimDescriptionPrefix && !string.IsNullOrEmpty(metadataService.Description))
                        {
                            string originalDesc = metadataService.Description;
                            string currentDesc = metadataService.Description;

                            string[] prefixesToTrim = {
                                DescriptionPrefixToTrim, // User-defined prefix first
                                "Okay, here’s a detailed description of the image, broken down as requested:",
                                "Okay, here's a detailed description of the image, broken down as requested:",
                                "Okay, here’s a detailed description of the image, categorized and with keywords as requested:",
                                "Here's a detailed description of the image:",
                                "Here’s a detailed description of the image:"
                            };

                            foreach (var prefix in prefixesToTrim.Where(p => !string.IsNullOrWhiteSpace(p))) // Only consider non-empty prefixes
                            {
                                if (currentDesc.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    currentDesc = currentDesc.TrimStart().Substring(prefix.Length);
                                    System.Diagnostics.Debug.WriteLine($"    Trimmed prefix '{prefix}' from description.");
                                    break;
                                }
                            }

                            string followUpPattern = "<br><br>**Description:**";
                            int followUpIndex = currentDesc.IndexOf(followUpPattern, StringComparison.OrdinalIgnoreCase);
                            if (followUpIndex != -1)
                            {
                                currentDesc = currentDesc.Substring(followUpIndex + followUpPattern.Length);
                                System.Diagnostics.Debug.WriteLine($"    Trimmed follow-up pattern '{followUpPattern}' from description.");
                            }

                            currentDesc = currentDesc.Trim();
                            if (currentDesc != originalDesc.Trim())
                            {
                                metadataService.Description = currentDesc;
                                changesMadeToLocalFile = true;
                                item.StatusMessage += "Description trimmed. ";
                                System.Diagnostics.Debug.WriteLine($"    Description changed. New: '{metadataService.Description?.Substring(0, Math.Min(50, metadataService.Description?.Length ?? 0))}'");
                            }
                        }

                        // 2. Split and Clean Categories
                        if (SplitCategories && metadataService.Categories != null && metadataService.Categories.Any())
                        {
                            System.Diagnostics.Debug.WriteLine($"    Original categories: [{string.Join("] | [", metadataService.Categories)}]");
                            var newCategoriesList = new List<string>();
                            bool anyCategoryStringWasSplit = false;

                            foreach (var catString in metadataService.Categories) // Iterate over each string entry in the list
                            {
                                if (string.IsNullOrWhiteSpace(catString)) continue;

                                if (catString.Contains(',')) // This specific string needs splitting
                                {
                                    anyCategoryStringWasSplit = true;
                                    var splitParts = catString.Split(',')
                                        .Select(s => s.Trim())
                                        .Select(s => s.Trim('*', ' '))
                                        .Select(s => s.Trim())      // Trim again after asterisk removal
                                        .Where(s => !string.IsNullOrWhiteSpace(s) &&
                                                    !(s.Contains(@"\") || s.Contains(@"/")) && // Filter out path-like strings (your clarification)
                                                    !Regex.IsMatch(s, @"^\d{4}$") &&
                                                    s.Length > 1)
                                        .ToList();
                                    newCategoriesList.AddRange(splitParts);
                                }
                                else // This string does not contain a comma, clean it individually
                                {
                                    string cleanedSingleCat = catString.Trim().Trim('*', ' ').Trim();
                                    if (!string.IsNullOrWhiteSpace(cleanedSingleCat) &&
                                        !(cleanedSingleCat.Contains(@"\") || cleanedSingleCat.Contains(@"/")) &&
                                        !Regex.IsMatch(cleanedSingleCat, @"^\d{4}$") &&
                                        cleanedSingleCat.Length > 1)
                                    {
                                        newCategoriesList.Add(cleanedSingleCat);
                                    }
                                    else if (anyCategoryStringWasSplit)
                                    {
                                        // If splitting happened elsewhere, and this was a "bad" token even before, it might indicate a change
                                    }
                                }
                            }

                            var distinctCleanedCategories = newCategoriesList
                                                            .Distinct(StringComparer.OrdinalIgnoreCase)
                                                            .OrderBy(c => c)
                                                            .ToList();

                            // Determine if a change actually occurred more reliably
                            var originalEffectiveCategories = metadataService.Categories
                               .SelectMany(c => c.Split(',')) // Split all original entries as if they were one big list
                               .Select(s => s.Trim().Trim('*', ' ').Trim())
                               .Where(s => !string.IsNullOrWhiteSpace(s) && !(s.Contains(@"\") || s.Contains(@"/")) && !Regex.IsMatch(s, @"^\d{4}$") && s.Length > 1)
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .OrderBy(c => c)
                               .ToList();

                            if (!originalEffectiveCategories.SequenceEqual(distinctCleanedCategories, StringComparer.OrdinalIgnoreCase))
                            {
                                metadataService.Categories = distinctCleanedCategories;
                                changesMadeToLocalFile = true;
                                item.StatusMessage += "Categories split/cleaned. ";
                                System.Diagnostics.Debug.WriteLine($"    Categories changed. New: [{string.Join("] | [", metadataService.Categories)}]");
                            }
                        }

                        if (changesMadeToLocalFile)
                        {
                            metadataService.Save();
                            item.StatusMessage += "Local file updated. ";
                            System.Diagnostics.Debug.WriteLine($"    Local file saved for {item.FileName}");
                        }
                        else
                        {
                            item.StatusMessage = string.IsNullOrWhiteSpace(item.StatusMessage) ? "No applicable tidy-up changes to local file." : item.StatusMessage;
                        }

                        // 3. If Daminion Catalog mode, changes were made, and client is ready -> Update Daminion
                        if (IsDaminionCatalogMode && item.DaminionItemId.HasValue && changesMadeToLocalFile && _daminionClient != null && _daminionClient.IsAuthenticated)
                        {
                            item.StatusMessage += "Updating Daminion...";
                            UpdateOverallStatus($"Updating Daminion for: {item.FileName}");
                            System.Diagnostics.Debug.WriteLine($"    Attempting Daminion update for {item.FileName} (ID: {item.DaminionItemId.Value})");

                            var operations = new List<DaminionUpdateOperation>();
                            if (!string.IsNullOrWhiteSpace(metadataService.Description) && !string.IsNullOrWhiteSpace(_currentSettings.DaminionDescriptionTagGuid))
                                operations.Add(new DaminionUpdateOperation { Guid = _currentSettings.DaminionDescriptionTagGuid, Value = metadataService.Description, Id = 0, Remove = false });

                            if (metadataService.Categories != null && metadataService.Categories.Any() && !string.IsNullOrWhiteSpace(_currentSettings.DaminionCategoriesTagGuid))
                            {
                                // IMPORTANT: This ADDS categories. If you want to REPLACE all existing categories for this item in Daminion,
                                // you would first need an operation to REMOVE ALL existing categories for this tag from this item.
                                // The Daminion API doc doesn't explicitly show a "remove all by GUID" for an item.
                                // It shows remove by ID (of tag value) or by text value (if ID=0 for Remove=true).
                                // For now, this will APPEND the cleaned categories. This might lead to duplicates if the item is processed multiple times
                                // or if the original combined string is not also removed.
                                // We will address "removing the original combined tag value" later if this append behavior is not desired.
                                System.Diagnostics.Debug.WriteLine($"      Adding to Daminion Categories ({_currentSettings.DaminionCategoriesTagGuid}): [{string.Join(" | ", metadataService.Categories)}]");
                                foreach (var category in metadataService.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                                    operations.Add(new DaminionUpdateOperation { Guid = _currentSettings.DaminionCategoriesTagGuid, Value = category, Id = 0, Remove = false });
                            }
                            // Add similar logic for Keywords if you implement keyword tidying

                            if (operations.Any())
                            {
                                var updateResult = await _daminionClient.UpdateItemMetadataAsync(new List<long> { item.DaminionItemId.Value }, operations);
                                if (updateResult != null && updateResult.Success)
                                {
                                    item.StatusMessage += "Daminion metadata updated.";
                                    System.Diagnostics.Debug.WriteLine($"      Daminion update successful for {item.FileName}.");
                                }
                                else
                                {
                                    item.StatusMessage += $"Daminion update failed: {updateResult?.Error ?? "Unknown"}.";
                                    item.Status = ProcessingStatus.Error;
                                    System.Diagnostics.Debug.WriteLine($"      Daminion update FAILED for {item.FileName}: {updateResult?.Error}");
                                }
                            }
                            else
                            {
                                item.StatusMessage += "No metadata operations to send to Daminion.";
                                System.Diagnostics.Debug.WriteLine($"      No operations to send to Daminion for {item.FileName}.");
                            }
                        }

                        item.StatusMessage = item.StatusMessage.Trim();
                        if (item.Status != ProcessingStatus.Error)
                        {
                            item.Status = ProcessingStatus.Processed;
                            if (string.IsNullOrWhiteSpace(item.StatusMessage) || item.StatusMessage == "Reading metadata.")
                                item.StatusMessage = changesMadeToLocalFile ? "Cleanup successful." : "No changes applied.";
                        }

                        if (item.Status == ProcessingStatus.Processed) processedCount++; else errorCount++;
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = ProcessingStatus.Cancelled; item.StatusMessage = "Cancelled during item processing.";
                        System.Diagnostics.Debug.WriteLine($"    Item {item.FileName} cancelled.");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        item.Status = ProcessingStatus.Error;
                        item.StatusMessage = $"Error cleaning file {item.FileName}: {ex.Message}";
                        System.Diagnostics.Debug.WriteLine($"    Error cleaning {item.FileName}: {ex}");
                        errorCount++; // Increment here as it's an item-specific error
                    }
                } // End foreach
            }
            catch (OperationCanceledException)
            {
                UpdateOverallStatus("Metadata cleanup cancelled by user.");
                // Update status for any items that were processing but didn't get set to Cancelled
                foreach (var item in FilesToProcess.Where(i => i.Status == ProcessingStatus.Processing || i.Status == ProcessingStatus.Queued))
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Queue cancelled.";
                }
            }
            catch (Exception ex)
            {
                UpdateOverallStatus($"An error occurred during metadata cleanup: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error in StartCleanupAsync loop: {ex}");
                foreach (var item in FilesToProcess.Where(i => i.Status == ProcessingStatus.Processing || i.Status == ProcessingStatus.Queued))
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = "Queue processing error.";
                }
            }
            finally
            {
                IsCleaningQueue = false;
                _cleanupCts?.Dispose();
                _cleanupCts = null;

                processedCount = FilesToProcess.Count(i => i.Status == ProcessingStatus.Processed);
                errorCount = FilesToProcess.Count(i => i.Status == ProcessingStatus.Error);
                int cancelledCount = FilesToProcess.Count(i => i.Status == ProcessingStatus.Cancelled);
                CurrentOperationStatus = $"Cleanup finished. Processed: {processedCount}, Errors: {errorCount}, Cancelled: {cancelledCount}.";
                System.Diagnostics.Debug.WriteLine(CurrentOperationStatus);
            }
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