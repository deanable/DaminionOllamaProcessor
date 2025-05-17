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
using DaminionOllamaWpfApp.Services;       // For ImageMetadataService
using System.Threading.Tasks;              // For Task
using System.Threading;                    // For CancellationTokenSource
using System.Linq;                         // For LINQ operations like .Any()

namespace DaminionOllamaWpfApp
{
    public class BatchProcessItem // Consider INotifyPropertyChanged if updating existing items
    {
        public string? FilePath { get; set; }
        public string? Status { get; set; }
        public string? Details { get; set; }
    }

    public partial class BatchProcessWindow : Window
    {
        public ObservableCollection<BatchProcessItem> ProcessResults { get; set; }
        private CancellationTokenSource? _cancellationTokenSource;

        // Assuming you have OllamaApiClient and ImageMetadataService ready to be used
        // You might pass these from MainWindow or initialize them here if needed.
        // For now, we'll assume they are accessible or created within ProcessLocalFolderAsync.
        private OllamaApiClient? _ollamaClient; // Needs to be initialized with URL from MainWindow or config
        // ImageMetadataService is used per file, so instantiated inside the loop.

        public BatchProcessWindow()
        {
            InitializeComponent();
            ProcessResults = new ObservableCollection<BatchProcessItem>();
            ResultsListView.ItemsSource = ProcessResults;
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                // You can set a description for the dialog
                dialog.Description = "Select a folder containing images to process";
                // You can set the initial selected path if desired
                // if (!string.IsNullOrWhiteSpace(LocalFolderPathTextBox.Text) && Directory.Exists(LocalFolderPathTextBox.Text))
                // {
                //    dialog.SelectedPath = LocalFolderPathTextBox.Text;
                // }

                DialogResult result = dialog.ShowDialog(); // This uses System.Windows.Forms.DialogResult

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    LocalFolderPathTextBox.Text = dialog.SelectedPath;
                    Console.WriteLine($"[BatchProcessWindow] Selected folder: {dialog.SelectedPath}");
                }
                else
                {
                    Console.WriteLine("[BatchProcessWindow] Folder selection cancelled or failed.");
                }
            }
        }

        private async void StartBatchButton_Click(object sender, RoutedEventArgs e)
        {
            OverallStatusTextBlock.Text = "Starting batch process...";
            StartBatchButton.IsEnabled = false;
            StopBatchButton.IsEnabled = true;
            BatchProgressBar.Visibility = Visibility.Visible;
            BatchProgressBar.Value = 0;
            ProcessResults.Clear();

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Ollama details from MainWindow or dedicated TextBoxes in BatchProcessWindow
            // For this example, I'll assume they might come from MainWindow's TextBoxes
            // This is a simplification; you'd ideally have dedicated input fields in BatchProcessWindow
            // or pass these settings when creating BatchProcessWindow.
            string ollamaUrl = Application.Current.MainWindow is MainWindow mw ? mw.OllamaUrlTextBox.Text : "http://localhost:11434";
            string modelName = Application.Current.MainWindow is MainWindow mw2 ? mw2.OllamaModelTextBox.Text : "llava";
            string prompt = Application.Current.MainWindow is MainWindow mw3 ? mw3.OllamaPromptTextBox.Text : "Describe this image in detail and provide relevant categories and keywords.";

            if (BatchModeTabControl.SelectedItem == LocalFolderTab) // Assuming x:Name="LocalFolderTab" on the TabItem
            {
                string folderPath = LocalFolderPathTextBox.Text;
                string extensionsInput = FileExtensionsTextBox.Text;
                bool includeSubfolders = IncludeSubfoldersCheckBox.IsChecked == true;

                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    System.Windows.MessageBox.Show("Please select a valid folder path.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    OverallStatusTextBlock.Text = "Error: Invalid folder path.";
                    SetUiForEndOfBatch(true, "Invalid folder path.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(extensionsInput))
                {
                    System.Windows.MessageBox.Show("Please specify file extensions (e.g., *.jpg;*.png).", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    OverallStatusTextBlock.Text = "Error: File extensions missing.";
                    SetUiForEndOfBatch(true, "File extensions missing.");
                    return;
                }

                // Initialize Ollama client here if not already done, or ensure it's configured
                _ollamaClient = new OllamaApiClient(ollamaUrl);

                await ProcessLocalFolderAsync(folderPath, extensionsInput, includeSubfolders, modelName, prompt, token);
            }
            else if (BatchModeTabControl.SelectedItem == DaminionBatchTab)
            {
                OverallStatusTextBlock.Text = "Daminion batch processing not yet implemented.";
                ProcessResults.Add(new BatchProcessItem { FilePath = "Daminion Collection", Status = "Pending Implementation", Details = "Daminion collection processing logic to be added." });
                SetUiForEndOfBatch(false, "Daminion batch processing not yet implemented.");
            }
            else
            {
                OverallStatusTextBlock.Text = "No batch mode selected or unknown tab.";
                SetUiForEndOfBatch(true, "No batch mode selected.");
            }
        }

        private void StopBatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                OverallStatusTextBlock.Text = "Stopping batch process...";
                Console.WriteLine("[BatchProcessWindow] Cancellation requested by user.");
                _cancellationTokenSource.Cancel();
                StopBatchButton.IsEnabled = false; // Disable immediately to prevent multiple clicks
            }
        }

        private void SetUiForEndOfBatch(bool wasCancelledOrError, string endMessage)
        {
            StartBatchButton.IsEnabled = true;
            StopBatchButton.IsEnabled = false; // Always disable stop when process ends or is stopped
            BatchProgressBar.Visibility = wasCancelledOrError ? Visibility.Visible : Visibility.Collapsed; // Keep progress if error/cancelled
            if (wasCancelledOrError)
            {
                BatchProgressBar.Value = BatchProgressBar.Maximum; // Or set to a specific error indication
            }
            OverallStatusTextBlock.Text = endMessage;
            Console.WriteLine($"[BatchProcessWindow] Batch process ended. Message: {endMessage}");
        }

        // --- Actual Processing Logic for Local Files ---
        private async Task ProcessLocalFolderAsync(string folderPath, string extensionsPattern, bool includeSubfolders,
                                                 string ollamaModel, string ollamaPrompt, CancellationToken token)
        {
            Console.WriteLine($"[BatchProcessWindow] Starting ProcessLocalFolderAsync for path: {folderPath}");
            List<string> filesToProcess = new List<string>();
            SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            string[] patterns = extensionsPattern.Split(';')
                                .Select(p => p.Trim())
                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                .ToArray();

            if (!patterns.Any())
            {
                ProcessResults.Add(new BatchProcessItem { FilePath = folderPath, Status = "Error", Details = "No valid file patterns provided." });
                SetUiForEndOfBatch(true, "Error: No valid file patterns.");
                return;
            }

            try
            {
                foreach (string pattern in patterns)
                {
                    filesToProcess.AddRange(Directory.EnumerateFiles(folderPath, pattern, searchOption));
                }
            }
            catch (Exception ex)
            {
                ProcessResults.Add(new BatchProcessItem { FilePath = folderPath, Status = "Error", Details = $"Error enumerating files: {ex.Message}" });
                SetUiForEndOfBatch(true, $"Error finding files: {ex.Message}");
                Console.Error.WriteLine($"[BatchProcessWindow] Error enumerating files: {ex.Message}");
                return;
            }

            if (!filesToProcess.Any())
            {
                ProcessResults.Add(new BatchProcessItem { FilePath = folderPath, Status = "Info", Details = "No matching files found to process." });
                SetUiForEndOfBatch(false, "No matching files found.");
                Console.WriteLine("[BatchProcessWindow] No matching files found.");
                return;
            }

            BatchProgressBar.Maximum = filesToProcess.Count;
            BatchProgressBar.Value = 0;
            int filesProcessed = 0;
            int filesSucceeded = 0;
            int filesFailed = 0;

            foreach (string filePath in filesToProcess)
            {
                if (token.IsCancellationRequested)
                {
                    ProcessResults.Add(new BatchProcessItem { FilePath = filePath, Status = "Cancelled", Details = "Batch process was cancelled by user." });
                    Console.WriteLine($"[BatchProcessWindow] Processing cancelled for file: {filePath}");
                    break;
                }

                var itemResult = new BatchProcessItem { FilePath = filePath, Status = "Processing..." };
                ProcessResults.Add(itemResult);
                ResultsListView.ScrollIntoView(itemResult); // Auto-scroll
                OverallStatusTextBlock.Text = $"Processing: {Path.GetFileName(filePath)} ({filesProcessed + 1} of {filesToProcess.Count})";
                Console.WriteLine($"[BatchProcessWindow] Processing file: {filePath}");

                ImageMetadataService? editor = null; // Renamed from ImageMetadataWriter
                try
                {
                    editor = new ImageMetadataService(filePath); // Use your new class
                    // editor.Read(); // Optionally read and display existing if needed, but for batch might skip

                    byte[] imageBytes = await File.ReadAllBytesAsync(filePath, token);
                    if (token.IsCancellationRequested) { itemResult.Status = "Cancelled"; itemResult.Details = "Cancelled before Ollama."; break; }

                    Console.WriteLine($"[BatchProcessWindow] Sending to Ollama: {Path.GetFileName(filePath)}");
                    OllamaGenerateResponse? ollamaApiResponse = await _ollamaClient.AnalyzeImageAsync(ollamaModel, ollamaPrompt, imageBytes);
                    if (token.IsCancellationRequested) { itemResult.Status = "Cancelled"; itemResult.Details = "Cancelled after Ollama call attempt."; break; }


                    if (ollamaApiResponse == null || !ollamaApiResponse.Done || string.IsNullOrEmpty(ollamaApiResponse.Response))
                    {
                        itemResult.Status = "Failed (Ollama)";
                        itemResult.Details = $"Ollama analysis error: {ollamaApiResponse?.Response ?? "Null API response."}";
                        Console.Error.WriteLine($"[BatchProcessWindow] Ollama error for {filePath}: {itemResult.Details}");
                        filesFailed++;
                    }
                    else
                    {
                        Console.WriteLine($"[BatchProcessWindow] Ollama success for {filePath}. Parsing response...");
                        ParsedOllamaContent parsedOllamaData = OllamaResponseParser.ParseLlavaResponse(ollamaApiResponse.Response);

                        if (!parsedOllamaData.SuccessfullyParsed && string.IsNullOrEmpty(parsedOllamaData.Description))
                        {
                            parsedOllamaData.Description = $"Ollama (parsing issues or minimal content): {ollamaApiResponse.Response.Substring(0, Math.Min(ollamaApiResponse.Response.Length, 100))}...";
                        }

                        Console.WriteLine($"[BatchProcessWindow] Writing metadata for {filePath}...");
                        editor.Description = parsedOllamaData.Description;
                        editor.Keywords = new List<string>(parsedOllamaData.Keywords);
                        editor.Categories = new List<string>(parsedOllamaData.Categories);
                        // editor.ExifImageDescription = parsedOllamaData.Description; // If you choose to set this

                        editor.Save(); // This will use the Magick.NET logic

                        itemResult.Status = "Success";
                        itemResult.Details = $"Desc: {parsedOllamaData.Description?.Substring(0, Math.Min(parsedOllamaData.Description.Length, 30))}... KW: {parsedOllamaData.Keywords.Count}, Cat: {parsedOllamaData.Categories.Count}";
                        Console.WriteLine($"[BatchProcessWindow] Metadata written successfully for {filePath}");
                        filesSucceeded++;
                    }
                }
                catch (OperationCanceledException)
                {
                    itemResult.Status = "Cancelled";
                    itemResult.Details = "Operation cancelled during processing.";
                    Console.WriteLine($"[BatchProcessWindow] Operation cancelled for file: {filePath}");
                    break;
                }
                catch (Exception ex)
                {
                    itemResult.Status = "Failed (Error)";
                    itemResult.Details = ex.Message.Substring(0, Math.Min(ex.Message.Length, 100));
                    Console.Error.WriteLine($"[BatchProcessWindow] Error processing file {filePath}: {ex.Message}\n{ex.StackTrace}");
                    filesFailed++;
                }
                finally
                {
                    editor?.Dispose();
                    filesProcessed++;
                    BatchProgressBar.Value = filesProcessed;
                }
            }

            string summaryMessage;
            if (token.IsCancellationRequested)
            {
                summaryMessage = $"Batch process cancelled. Processed: {filesProcessed - 1}, Succeeded: {filesSucceeded}, Failed: {filesFailed}.";
            }
            else
            {
                summaryMessage = $"Batch process complete. Total: {filesToProcess.Count}, Succeeded: {filesSucceeded}, Failed: {filesFailed}.";
            }
            SetUiForEndOfBatch(token.IsCancellationRequested || filesFailed > 0, summaryMessage);
            Console.WriteLine($"[BatchProcessWindow] {summaryMessage}");
        }

        // private async Task ProcessDaminionCollectionAsync(string collectionIdentifier, CancellationToken token)
        // { ... To be implemented later ... }
    }
}