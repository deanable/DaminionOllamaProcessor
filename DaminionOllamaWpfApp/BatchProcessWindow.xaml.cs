// DaminionOllamaWpfApp/BatchProcessWindow.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.IO;
// Remove Microsoft.Win32 if OpenFileDialog is no longer used for folder Browse
// using Microsoft.Win32; 

// Add this using statement for FolderBrowserDialog
using System.Windows.Forms; // You'll need to add a reference to System.Windows.Forms.dll

// You will need these later for the actual batch processing logic
using DaminionOllamaInteractionLib.Ollama; // For ParsedOllamaContent
using DaminionOllamaInteractionLib.Services;       // For ImageMetadataService
using System.Threading.Tasks;              // For Task
using System.Threading;                    // For CancellationTokenSource
using System.Linq;                         // For LINQ operations like .Any()

namespace DaminionOllamaWpfApp
{
    /// <summary>
    /// Represents a single item in the batch processing queue.
    /// Tracks the processing status and details for individual files.
    /// </summary>
    public class BatchProcessItem // Consider INotifyPropertyChanged if updating existing items
    {
        /// <summary>
        /// Gets or sets the full path to the file being processed.
        /// </summary>
        public string? FilePath { get; set; }
        
        /// <summary>
        /// Gets or sets the current processing status (e.g., "Pending", "Processing", "Completed", "Error").
        /// </summary>
        public string? Status { get; set; }
        
        /// <summary>
        /// Gets or sets additional details about the processing result or any errors encountered.
        /// </summary>
        public string? Details { get; set; }
    }

    /// <summary>
    /// Window for batch processing local image files with AI-generated metadata.
    /// This window provides functionality to:
    /// 1. Select a folder containing images
    /// 2. Configure processing options (file extensions, subfolder inclusion)
    /// 3. Process multiple images in sequence with AI analysis
    /// 4. Update image metadata (EXIF, IPTC, XMP) with AI-generated content
    /// 5. Display real-time progress and results
    /// </summary>
    public partial class BatchProcessWindow : Window
    {
        #region Properties
        /// <summary>
        /// Observable collection of batch processing items that serves as the data source for the results list.
        /// Updates automatically as items are processed, providing real-time feedback to the user.
        /// </summary>
        public ObservableCollection<BatchProcessItem> ProcessResults { get; set; }
        
        /// <summary>
        /// Cancellation token source for stopping the batch processing operation.
        /// Allows users to cancel long-running operations gracefully.
        /// </summary>
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Client for interacting with the Ollama API for AI image analysis.
        /// Initialized with the server URL from MainWindow configuration.
        /// </summary>
        private OllamaApiClient? _ollamaClient; // Needs to be initialized with URL from MainWindow or config
        // ImageMetadataService is used per file, so instantiated inside the loop.
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the BatchProcessWindow.
        /// Sets up the UI data binding and initializes the processing results collection.
        /// </summary>
        public BatchProcessWindow()
        {
            InitializeComponent();
            ProcessResults = new ObservableCollection<BatchProcessItem>();
            ResultsListView.ItemsSource = ProcessResults;
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Handles the Browse Folder button click event.
        /// Opens a folder browser dialog to allow users to select the directory containing images to process.
        /// </summary>
        /// <param name="sender">The button that triggered the event.</param>
        /// <param name="e">Event arguments.</param>
        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder containing images to process";
                dialog.ShowNewFolderButton = false;
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FolderPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        /// <summary>
        /// Handles the Start Batch Processing button click event.
        /// Validates input parameters and initiates the batch processing operation.
        /// </summary>
        /// <param name="sender">The button that triggered the event.</param>
        /// <param name="e">Event arguments.</param>
        private async void StartBatchButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate required inputs
            string folderPath = FolderPathTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                MessageBox.Show("Please select a valid folder path.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ollamaModel = ModelTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ollamaModel))
            {
                MessageBox.Show("Please enter an Ollama model name.", "Missing Model", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ollamaPrompt = PromptTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ollamaPrompt))
            {
                MessageBox.Show("Please enter a prompt for image analysis.", "Missing Prompt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get processing options
            string extensionsPattern = ExtensionsTextBox.Text?.Trim() ?? "*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.gif";
            bool includeSubfolders = IncludeSubfoldersCheckBox.IsChecked ?? false;

            // Initialize cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            // Update UI state for processing
            SetUiForStartOfBatch();

            try
            {
                // Start the batch processing operation
                await ProcessLocalFolderAsync(folderPath, extensionsPattern, includeSubfolders, ollamaModel, ollamaPrompt, _cancellationTokenSource.Token);
                SetUiForEndOfBatch(false, "Batch processing completed successfully.");
            }
            catch (OperationCanceledException)
            {
                SetUiForEndOfBatch(true, "Batch processing was cancelled by user.");
            }
            catch (Exception ex)
            {
                SetUiForEndOfBatch(true, $"Batch processing failed: {ex.Message}");
                MessageBox.Show($"An error occurred during batch processing: {ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Stop Batch Processing button click event.
        /// Cancels the ongoing batch processing operation.
        /// </summary>
        /// <param name="sender">The button that triggered the event.</param>
        /// <param name="e">Event arguments.</param>
        private void StopBatchButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            StatusTextBlock.Text = "Stopping batch processing...";
            StopBatchButton.IsEnabled = false;
        }
        #endregion

        #region UI State Management
        /// <summary>
        /// Updates the UI state when batch processing starts.
        /// Disables input controls and enables the stop button.
        /// </summary>
        private void SetUiForStartOfBatch()
        {
            StartBatchButton.IsEnabled = false;
            StopBatchButton.IsEnabled = true;
            FolderPathTextBox.IsEnabled = false;
            BrowseFolderButton.IsEnabled = false;
            ModelTextBox.IsEnabled = false;
            PromptTextBox.IsEnabled = false;
            ExtensionsTextBox.IsEnabled = false;
            IncludeSubfoldersCheckBox.IsEnabled = false;
            
            // Clear previous results
            ProcessResults.Clear();
            StatusTextBlock.Text = "Batch processing started...";
        }

        /// <summary>
        /// Updates the UI state when batch processing ends.
        /// Re-enables input controls and provides final status message.
        /// </summary>
        /// <param name="wasCancelledOrError">True if the operation was cancelled or failed, false if completed successfully.</param>
        /// <param name="endMessage">Final status message to display.</param>
        private void SetUiForEndOfBatch(bool wasCancelledOrError, string endMessage)
        {
            StartBatchButton.IsEnabled = true;
            StopBatchButton.IsEnabled = false;
            FolderPathTextBox.IsEnabled = true;
            BrowseFolderButton.IsEnabled = true;
            ModelTextBox.IsEnabled = true;
            PromptTextBox.IsEnabled = true;
            ExtensionsTextBox.IsEnabled = true;
            IncludeSubfoldersCheckBox.IsEnabled = true;
            
            StatusTextBlock.Text = endMessage;
        }
        #endregion

        #region Batch Processing Logic
        /// <summary>
        /// Processes all images in the specified folder with AI-generated metadata.
        /// This method handles the complete workflow:
        /// 1. Discovers image files based on extension patterns
        /// 2. Initializes the Ollama client for AI processing
        /// 3. Processes each image file individually
        /// 4. Updates metadata using ImageMetadataService
        /// 5. Provides progress feedback and error handling
        /// </summary>
        /// <param name="folderPath">Path to the folder containing images to process.</param>
        /// <param name="extensionsPattern">File extension pattern (e.g., "*.jpg;*.png").</param>
        /// <param name="includeSubfolders">Whether to include files from subfolders.</param>
        /// <param name="ollamaModel">Name of the Ollama model to use for analysis.</param>
        /// <param name="ollamaPrompt">Prompt text to send to the AI model.</param>
        /// <param name="token">Cancellation token for stopping the operation.</param>
        private async Task ProcessLocalFolderAsync(string folderPath, string extensionsPattern, bool includeSubfolders,
                                                 string ollamaModel, string ollamaPrompt, CancellationToken token)
        {
            // Parse extension patterns and discover files
            var extensions = extensionsPattern.Split(';')
                .Select(ext => ext.Trim().TrimStart('*'))
                .Where(ext => !string.IsNullOrEmpty(ext))
                .ToArray();

            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var imageFiles = new List<string>();

            // Collect all matching image files
            foreach (var extension in extensions)
            {
                var pattern = "*" + extension;
                try
                {
                    var files = Directory.GetFiles(folderPath, pattern, searchOption);
                    imageFiles.AddRange(files);
                }
                catch (Exception ex)
                {
                    // Log error but continue with other extensions
                    var errorItem = new BatchProcessItem
                    {
                        FilePath = $"Extension: {extension}",
                        Status = "Error",
                        Details = $"Failed to search for files: {ex.Message}"
                    };
                    ProcessResults.Add(errorItem);
                }
            }

            if (!imageFiles.Any())
            {
                StatusTextBlock.Text = "No image files found matching the specified criteria.";
                return;
            }

            // Initialize Ollama client
            // TODO: Get the Ollama server URL from application settings or configuration
            string ollamaServerUrl = "http://localhost:11434"; // Default Ollama URL
            _ollamaClient = new OllamaApiClient(ollamaServerUrl);

            // Verify Ollama connection
            bool isConnected = await _ollamaClient.CheckConnectionAsync();
            if (!isConnected)
            {
                throw new Exception("Cannot connect to Ollama server. Please ensure Ollama is running.");
            }

            StatusTextBlock.Text = $"Found {imageFiles.Count} image files. Processing...";

            // Process each image file
            int processedCount = 0;
            int successCount = 0;
            int errorCount = 0;

            foreach (var filePath in imageFiles)
            {
                token.ThrowIfCancellationRequested();

                processedCount++;
                StatusTextBlock.Text = $"Processing {processedCount} of {imageFiles.Count}: {Path.GetFileName(filePath)}";

                var processItem = new BatchProcessItem
                {
                    FilePath = filePath,
                    Status = "Processing",
                    Details = "Analyzing image..."
                };
                ProcessResults.Add(processItem);

                try
                {
                    // Read image file
                    byte[] imageBytes = await File.ReadAllBytesAsync(filePath, token);

                    // Send to Ollama for analysis
                    processItem.Details = "Sending to Ollama for analysis...";
                    var ollamaResponse = await _ollamaClient.AnalyzeImageAsync(ollamaModel, ollamaPrompt, imageBytes);

                    if (ollamaResponse == null || string.IsNullOrEmpty(ollamaResponse.Response))
                    {
                        processItem.Status = "Error";
                        processItem.Details = "No response received from Ollama";
                        errorCount++;
                        continue;
                    }

                    // Parse AI response
                    processItem.Details = "Parsing AI response...";
                    var parsedContent = OllamaResponseParser.ParseLlavaResponse(ollamaResponse.Response);

                    // Update image metadata
                    processItem.Details = "Updating image metadata...";
                    using (var metadataService = new ImageMetadataService(filePath))
                    {
                        metadataService.Read();
                        metadataService.PopulateFromOllamaContent(parsedContent);
                        metadataService.Save();
                    }

                    // Update final status
                    processItem.Status = "Completed";
                    processItem.Details = $"Successfully processed. Description: {parsedContent.Description?.Substring(0, Math.Min(50, parsedContent.Description.Length ?? 0))}...";
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    processItem.Status = "Cancelled";
                    processItem.Details = "Processing was cancelled";
                    throw; // Re-throw to stop the entire operation
                }
                catch (Exception ex)
                {
                    processItem.Status = "Error";
                    processItem.Details = $"Error: {ex.Message}";
                    errorCount++;
                }
            }

            // Update final status
            StatusTextBlock.Text = $"Batch processing completed. Success: {successCount}, Errors: {errorCount}, Total: {processedCount}";
        }
        #endregion
    }
}