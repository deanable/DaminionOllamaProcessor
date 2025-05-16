// DaminionOllamaWpfApp/MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; // For JsonException (though less likely to be caught here now)
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32; // For OpenFileDialog

using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion;
using DaminionOllamaInteractionLib.Ollama;
using DaminionOllamaWpfApp.Services; // Your ImageMetadataEditor namespace
using System.Net.Http; 

namespace DaminionOllamaWpfApp
{
    public partial class MainWindow : Window
    {
        private DaminionApiClient? _daminionClient;
        private OllamaApiClient? _ollamaClient;

        private string? _descriptionTagGuid;
        private string? _keywordsTagGuid;
        private string? _categoriesTagGuid;

        public MainWindow()
        {
            InitializeComponent();
            _daminionClient = new DaminionApiClient();
        }

        /// <summary>
        /// Updates the status text block with a message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="append"></param>
        private void UpdateStatus(string message, bool append = false)
        {
            if (Dispatcher.CheckAccess())
            {
                if (append)
                {
                    StatusTextBlock.Text += message + Environment.NewLine;
                }
                else
                {
                    StatusTextBlock.Text = message + Environment.NewLine;
                }
                // If StatusTextBlock is inside a ScrollViewer named StatusScrollViewer:
                // StatusScrollViewer.ScrollToEnd(); 
            }
            else
            {
                Dispatcher.Invoke(() => UpdateStatus(message, append));
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("---- LoginButton_Click: START ----");
            if (_daminionClient == null)
            {
                UpdateStatus("Error: Daminion client not initialized.");
                Console.WriteLine("---- LoginButton_Click: ERROR - _daminionClient is null ----");
                return;
            }
            Console.WriteLine("---- LoginButton_Click: _daminionClient is NOT null ----");

            string daminionUrl = DaminionUrlTextBox.Text;
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            Console.WriteLine($"---- LoginButton_Click: URL='{daminionUrl}', User='{username}' ----");

            if (string.IsNullOrWhiteSpace(daminionUrl) || string.IsNullOrWhiteSpace(username))
            {
                UpdateStatus("Please enter Daminion URL and Username. Password may be required.");
                Console.WriteLine("---- LoginButton_Click: ERROR - URL or Username is empty ----");
                return;
            }

            SetUiInteraction(false); // Disable UI
            UpdateStatus("Logging in to Daminion...");

            try
            {
                Console.WriteLine("---- LoginButton_Click: TRY block entered, BEFORE calling _daminionClient.LoginAsync ----");
                bool loginSuccess = await _daminionClient.LoginAsync(daminionUrl, username, password);
                Console.WriteLine($"---- LoginButton_Click: AFTER calling _daminionClient.LoginAsync, loginSuccess = {loginSuccess} ----");

                if (loginSuccess)
                {
                    UpdateStatus("Successfully logged in to Daminion.");
                    FetchTagsButton.IsEnabled = true;
                }
                else
                {
                    UpdateStatus("Failed to log in to Daminion. Check credentials and server URL. See console/debug output for details.");
                }
            }
            catch (ArgumentException argEx)
            {
                UpdateStatus($"Login input error: {argEx.Message}");
                Console.WriteLine($"---- LoginButton_Click: CATCH ArgumentException: {argEx.Message} ----");
            }
            catch (HttpRequestException httpEx)
            {
                UpdateStatus($"Login network error: {httpEx.Message}. Ensure Daminion server is accessible. Check Debug Output.");
                Console.WriteLine($"---- LoginButton_Click: CATCH HttpRequestException: {httpEx.Message} ----");
                if (httpEx.InnerException != null) Console.WriteLine($"---- LoginButton_Click: InnerHttpRequestException: {httpEx.InnerException.Message} ----");
            }
            catch (Exception ex)
            {
                UpdateStatus($"An unexpected error occurred during login: {ex.Message}. Check Debug Output.");
                Console.WriteLine($"---- LoginButton_Click: CATCH Exception: {ex.Message} ----");
                if (ex.InnerException != null) Console.WriteLine($"---- LoginButton_Click: InnerException: {ex.InnerException.Message} ----");
                Console.WriteLine($"---- LoginButton_Click: StackTrace: {ex.StackTrace} ----");
            }
            finally
            {
                SetUiInteraction(true); // Re-enable relevant parts of UI
                Console.WriteLine("---- LoginButton_Click: FINALLY block executed ----");
            }
            Console.WriteLine("---- LoginButton_Click: END ----");
        }

        private async void FetchTagsButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("---- FetchTagsButton_Click: START ----");
            if (_daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                UpdateStatus("Please login to Daminion first.");
                Console.WriteLine("---- FetchTagsButton_Click: ERROR - Not authenticated or client is null ----");
                return;
            }

            SetUiInteraction(false);
            UpdateStatus("Fetching Daminion tags...");

            try
            {
                List<DaminionTag>? tags = await _daminionClient.GetTagsAsync();
                if (tags != null && tags.Any())
                {
                    var sb = new StringBuilder("Fetched Daminion Tags:\n");
                    var descTag = tags.FirstOrDefault(t => t.Name.Equals("Description", StringComparison.OrdinalIgnoreCase));
                    var keywordsTag = tags.FirstOrDefault(t => t.Name.Equals("Keywords", StringComparison.OrdinalIgnoreCase));
                    var categoriesTag = tags.FirstOrDefault(t => t.Name.Equals("Categories", StringComparison.OrdinalIgnoreCase));

                    _descriptionTagGuid = descTag?.Guid;
                    _keywordsTagGuid = keywordsTag?.Guid;
                    _categoriesTagGuid = categoriesTag?.Guid;

                    sb.AppendLine($"Found Description Tag GUID: {_descriptionTagGuid ?? "NOT FOUND"} (Name: {descTag?.Name ?? "N/A"})");
                    sb.AppendLine($"Found Keywords Tag GUID: {_keywordsTagGuid ?? "NOT FOUND"} (Name: {keywordsTag?.Name ?? "N/A"})");
                    sb.AppendLine($"Found Categories Tag GUID: {_categoriesTagGuid ?? "NOT FOUND"} (Name: {categoriesTag?.Name ?? "N/A"})");

                    if (string.IsNullOrEmpty(_descriptionTagGuid) || string.IsNullOrEmpty(_keywordsTagGuid) || string.IsNullOrEmpty(_categoriesTagGuid))
                    {
                        sb.AppendLine("\nWARNING: One or more target tags (Description, Keywords, Categories) were not found by common names.");
                    }
                    else
                    {
                        sb.AppendLine("\nSuccessfully identified GUIDs for Description, Keywords, and Categories.");
                    }
                    UpdateStatus(sb.ToString());
                }
                else
                {
                    UpdateStatus("No tags returned from Daminion or an error occurred during fetching.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error fetching tags: {ex.Message}");
                Console.WriteLine($"---- FetchTagsButton_Click: CATCH Exception: {ex.Message} ----");
            }
            finally
            {
                SetUiInteraction(true);
                Console.WriteLine("---- FetchTagsButton_Click: FINALLY block executed ----");
            }
            Console.WriteLine("---- FetchTagsButton_Click: END ----");
        }

        private async void TestOllamaButton_Click(object sender, RoutedEventArgs e) // Or your renamed button
        {
            Console.WriteLine("---- TestOllamaButton_Click: START ----");
            string ollamaUrl = OllamaUrlTextBox.Text;
            string modelName = OllamaModelTextBox.Text;
            string prompt = OllamaPromptTextBox.Text;

            if (string.IsNullOrWhiteSpace(ollamaUrl) || string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(prompt))
            {
                UpdateStatus("Please enter Ollama URL, Model Name, and Prompt.");
                Console.WriteLine("---- TestOllamaButton_Click: ERROR - Ollama params missing ----");
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select Image for Metadata Processing",
                Filter = "Image Files|*.jpg;*.jpeg;*.tif;*.tiff;*.png|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedImagePath = openFileDialog.FileName;
                SetUiInteraction(false);
                UpdateStatus($"Processing file: {selectedImagePath}");
                Console.WriteLine($"[MainWindow] Selected image: {selectedImagePath}");

                ImageMetadataService? metadataService = null; // Use your new class name

                try
                {
                    // 1. Read existing metadata
                    UpdateStatus("Reading existing metadata...", true);
                    Console.WriteLine("[MainWindow] Reading existing metadata...");
                    metadataService = new ImageMetadataService(selectedImagePath); // Use your new class
                    metadataService.Read();

                    var sbExisting = new StringBuilder("--- Existing Metadata (from ImageMetadataService) ---\n");
                    sbExisting.AppendLine($"Description: {metadataService.Description ?? "N/A"}");
                    sbExisting.AppendLine($"Keywords: {(metadataService.Keywords.Any() ? string.Join("; ", metadataService.Keywords) : "N/A")}");
                    sbExisting.AppendLine($"Categories: {(metadataService.Categories.Any() ? string.Join("; ", metadataService.Categories) : "N/A")}");
                    // Add other properties from ImageMetadataService if you want to display them
                    UpdateStatus(sbExisting.ToString(), true);

                    // 2. Send image to Ollama
                    UpdateStatus("Sending image to Ollama for analysis...", true);
                    Console.WriteLine("[MainWindow] Sending image to Ollama...");
                    _ollamaClient = new OllamaApiClient(ollamaUrl);
                    byte[] imageBytes = await File.ReadAllBytesAsync(selectedImagePath);
                    OllamaGenerateResponse? ollamaApiResponse = await _ollamaClient.AnalyzeImageAsync(modelName, prompt, imageBytes);

                    if (ollamaApiResponse == null || !ollamaApiResponse.Done || string.IsNullOrEmpty(ollamaApiResponse.Response))
                    {
                        string errorMsg = $"Ollama analysis failed or returned empty/incomplete response. Ollama response: '{ollamaApiResponse?.Response ?? "Null API response."}'";
                        UpdateStatus(errorMsg, true);
                        Console.Error.WriteLine($"[MainWindow] {errorMsg}");
                        return;
                    }
                    UpdateStatus("Ollama analysis successful.", true);
                    Console.WriteLine("[MainWindow] Ollama analysis successful.");

                    // 3. Parse Ollama's response
                    Console.WriteLine("[MainWindow] Parsing Ollama response...");
                    ParsedOllamaContent parsedOllamaData = OllamaResponseParser.ParseLlavaResponse(ollamaApiResponse.Response);
                    // ... (your existing parsing result handling) ...

                    var sbOllama = new StringBuilder("--- Ollama Suggested Metadata ---\n");
                    sbOllama.AppendLine($"Description: {parsedOllamaData.Description ?? "N/A"}");
                    sbOllama.AppendLine($"Keywords: {(parsedOllamaData.Keywords.Any() ? string.Join("; ", parsedOllamaData.Keywords) : "N/A")}");
                    sbOllama.AppendLine($"Categories: {(parsedOllamaData.Categories.Any() ? string.Join("; ", parsedOllamaData.Categories) : "N/A")}");
                    UpdateStatus(sbOllama.ToString(), true);

                    // 4. Write new metadata to file
                    UpdateStatus("Writing Ollama metadata to image file...", true);
                    Console.WriteLine("[MainWindow] Populating ImageMetadataService with Ollama data and saving...");
                    metadataService.Description = parsedOllamaData.Description;
                    metadataService.Keywords = new List<string>(parsedOllamaData.Keywords); // Create new list
                    metadataService.Categories = new List<string>(parsedOllamaData.Categories); // Create new list
                                                                                                // metadataService.ExifImageDescription = parsedOllamaData.Description; // If you want to set this

                    metadataService.Save();
                    UpdateStatus("Metadata write attempt complete (via metadataService.Save()).", true);
                    Console.WriteLine("[MainWindow] metadataService.Save() called.");

                    // 5. Read to confirm write
                    UpdateStatus("Re-reading metadata to confirm changes...", true);
                    Console.WriteLine("[MainWindow] Re-reading metadata...");
                    metadataService.Read(); // Re-read from the modified file

                    var sbConfirmed = new StringBuilder("--- Confirmed Metadata (after write from ImageMetadataService) ---\n");
                    sbConfirmed.AppendLine($"Description: {metadataService.Description ?? "N/A"}");
                    sbConfirmed.AppendLine($"Keywords: {(metadataService.Keywords.Any() ? string.Join("; ", metadataService.Keywords) : "N/A")}");
                    sbConfirmed.AppendLine($"Categories: {(metadataService.Categories.Any() ? string.Join("; ", metadataService.Categories) : "N/A")}");
                    UpdateStatus(sbConfirmed.ToString(), true);
                    UpdateStatus("Process complete for file: " + selectedImagePath, true);
                    Console.WriteLine("[MainWindow] Metadata processing workflow complete for file.");
                }
                // ... (your existing catch blocks for ArgumentNullException, HttpRequestException, JsonException, MagickException, Exception) ...
                finally
                {
                    SetUiInteraction(true);
                    metadataService?.Dispose();
                    Console.WriteLine("---- TestOllamaButton_Click: FINALLY block executed ----");
                }
            }
            else
            {
                UpdateStatus("Image selection cancelled.", true);
                Console.WriteLine("---- TestOllamaButton_Click: Image selection cancelled ----");
            }
            Console.WriteLine("---- TestOllamaButton_Click: END ----");
        }

        private async void StartProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("---- StartProcessingButton_Click: START ----");
            // This method is for the Daminion API workflow, which is currently shelved.
            // We can re-enable and adapt it later.
            if (_daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                UpdateStatus("Error: Please log in to Daminion first.");
                Console.WriteLine("---- StartProcessingButton_Click: ERROR - Not authenticated or client null ----");
                return;
            }
            if (string.IsNullOrEmpty(_descriptionTagGuid) || string.IsNullOrEmpty(_keywordsTagGuid) || string.IsNullOrEmpty(_categoriesTagGuid))
            {
                UpdateStatus("Error: Daminion tag GUIDs are missing. Please fetch tags first.");
                Console.WriteLine("---- StartProcessingButton_Click: ERROR - Tag GUIDs missing ----");
                return;
            }
            if (string.IsNullOrWhiteSpace(DaminionItemIdTextBox.Text) || !long.TryParse(DaminionItemIdTextBox.Text, out long itemId))
            {
                UpdateStatus("Error: Please enter a valid Daminion Media Item ID.");
                Console.WriteLine("---- StartProcessingButton_Click: ERROR - Invalid Item ID ----");
                return;
            }

            string ollamaUrl = OllamaUrlTextBox.Text;
            string ollamaModel = OllamaModelTextBox.Text;
            string ollamaPrompt = OllamaPromptTextBox.Text;
            if (string.IsNullOrWhiteSpace(ollamaUrl) || string.IsNullOrWhiteSpace(ollamaModel) || string.IsNullOrWhiteSpace(ollamaPrompt))
            {
                UpdateStatus("Error: Please ensure Ollama URL, Model, and Prompt are set.");
                Console.WriteLine("---- StartProcessingButton_Click: ERROR - Ollama params missing ----");
                return;
            }

            SetUiInteraction(false);
            var processLog = new StringBuilder();
            UpdateStatus($"Starting Daminion item processing for ID: {itemId}...");
            Console.WriteLine($"[MainWindow] StartProcessing Daminion Item ID: {itemId}");

            try
            {
                processLog.AppendLine("Fetching image path from Daminion...");
                DaminionPathResult pathResult = await _daminionClient.GetAbsolutePathsAsync(new List<long> { itemId });

                string? imagePath = null; // Declare here to be accessible
                if (pathResult.Success && pathResult.Paths != null && pathResult.Paths.TryGetValue(itemId.ToString(), out imagePath))
                {
                    if (string.IsNullOrEmpty(imagePath))
                    {
                        processLog.AppendLine($"Image path for item {itemId} is empty or null from Daminion.");
                        UpdateStatus(processLog.ToString(), true); SetUiInteraction(true); return;
                    }
                    processLog.AppendLine($"Image path received: {imagePath}");
                }
                else
                {
                    processLog.AppendLine($"Failed to get image path for item {itemId}. Error: {pathResult?.ErrorMessage ?? "Path result indicated failure or paths dictionary was null."}");
                    UpdateStatus(processLog.ToString(), true); SetUiInteraction(true); return;
                }

                byte[] imageBytes;
                try
                {
                    processLog.AppendLine("Reading image file...");
                    if (imagePath == null) throw new InvalidOperationException("Image path became null unexpectedly."); // Should be caught by above logic
                    imageBytes = await File.ReadAllBytesAsync(imagePath);
                    processLog.AppendLine($"Image file read successfully ({imageBytes.Length} bytes).");
                }
                catch (Exception ex)
                {
                    processLog.AppendLine($"Failed to read image file at '{imagePath}': {ex.Message}. Ensure path is accessible.");
                    UpdateStatus(processLog.ToString(), true); SetUiInteraction(true); return;
                }

                processLog.AppendLine("Sending image to Ollama for analysis...");
                _ollamaClient = new OllamaApiClient(ollamaUrl);
                OllamaGenerateResponse? ollamaApiResponse = await _ollamaClient.AnalyzeImageAsync(ollamaModel, ollamaPrompt, imageBytes);

                if (ollamaApiResponse == null || !ollamaApiResponse.Done || string.IsNullOrEmpty(ollamaApiResponse.Response))
                {
                    processLog.AppendLine($"Ollama analysis failed or returned empty/incomplete. Response: '{ollamaApiResponse?.Response ?? "Null API response."}'");
                    if (ollamaApiResponse == null) { UpdateStatus(processLog.ToString(), true); SetUiInteraction(true); return; }
                }
                else { processLog.AppendLine("Ollama analysis successful."); }

                processLog.AppendLine("Parsing Ollama response...");
                ParsedOllamaContent parsedOllamaData = OllamaResponseParser.ParseLlavaResponse(ollamaApiResponse.Response ?? string.Empty);
                if (!parsedOllamaData.SuccessfullyParsed && !string.IsNullOrEmpty(ollamaApiResponse.Response))
                {
                    parsedOllamaData.Description = $"Ollama (unparsed): {ollamaApiResponse.Response.Substring(0, Math.Min(ollamaApiResponse.Response.Length, 200))}";
                }
                else if (!parsedOllamaData.SuccessfullyParsed && string.IsNullOrEmpty(ollamaApiResponse.Response))
                {
                    parsedOllamaData.Description = "Ollama: No content generated.";
                }

                processLog.AppendLine($"Parsed Description (snippet): {parsedOllamaData.Description?.Substring(0, Math.Min(parsedOllamaData.Description.Length, 100))}...");
                processLog.AppendLine($"Parsed Categories: {(parsedOllamaData.Categories.Any() ? string.Join("; ", parsedOllamaData.Categories) : "N/A")}");
                processLog.AppendLine($"Parsed Keywords: {(parsedOllamaData.Keywords.Any() ? string.Join("; ", parsedOllamaData.Keywords) : "N/A")}");

                processLog.AppendLine("Preparing to update Daminion metadata...");
                var operations = new List<DaminionUpdateOperation>();
                if (!string.IsNullOrWhiteSpace(parsedOllamaData.Description))
                    operations.Add(new DaminionUpdateOperation { Guid = _descriptionTagGuid!, Value = parsedOllamaData.Description, Id = 0, Remove = false });
                else
                    operations.Add(new DaminionUpdateOperation { Guid = _descriptionTagGuid!, Value = (parsedOllamaData.SuccessfullyParsed ? "" : "Ollama: No description generated"), Id = 0, Remove = false });


                if (parsedOllamaData.Categories.Any())
                    operations.Add(new DaminionUpdateOperation { Guid = _categoriesTagGuid!, Value = string.Join("; ", parsedOllamaData.Categories), Id = 0, Remove = false });
                else
                    operations.Add(new DaminionUpdateOperation { Guid = _categoriesTagGuid!, Value = (parsedOllamaData.SuccessfullyParsed ? "" : "Ollama: No categories generated"), Id = 0, Remove = false });

                if (parsedOllamaData.Keywords.Any())
                    operations.Add(new DaminionUpdateOperation { Guid = _keywordsTagGuid!, Value = string.Join("; ", parsedOllamaData.Keywords), Id = 0, Remove = false });
                else
                    operations.Add(new DaminionUpdateOperation { Guid = _keywordsTagGuid!, Value = (parsedOllamaData.SuccessfullyParsed ? "" : "Ollama: No keywords generated"), Id = 0, Remove = false });

                if (operations.Any())
                {
                    processLog.AppendLine("Updating Daminion...");
                    DaminionBatchChangeResponse? updateResult = await _daminionClient.UpdateItemMetadataAsync(new List<long> { itemId }, operations);
                    if (updateResult != null && updateResult.Success)
                    {
                        processLog.AppendLine("Daminion metadata updated successfully!");
                    }
                    else
                    {
                        processLog.AppendLine($"Failed to update Daminion metadata. Error: {updateResult?.Error ?? "Unknown error from update API."}");
                    }
                }
                else
                {
                    processLog.AppendLine("No valid metadata operations constructed to update Daminion.");
                }
                UpdateStatus(processLog.ToString(), true);
            }
            catch (Exception ex)
            {
                processLog.AppendLine($"An critical error occurred during Daminion processing: {ex.Message}\n{ex.StackTrace}");
                UpdateStatus(processLog.ToString(), true);
            }
            finally
            {
                SetUiInteraction(true);
                Console.WriteLine("---- StartProcessingButton_Click: FINALLY block executed ----");
            }
            Console.WriteLine("---- StartProcessingButton_Click: END ----");
        }


        private void SetUiInteraction(bool enable)
        {
            LoginButton.IsEnabled = enable;
            FetchTagsButton.IsEnabled = enable && (_daminionClient?.IsAuthenticated ?? false);
            TestOllamaButton.IsEnabled = enable;

            bool canStartProcessing = enable &&
                                    (_daminionClient?.IsAuthenticated ?? false) &&
                                    !string.IsNullOrEmpty(_descriptionTagGuid) && // Check all required GUIDs
                                    !string.IsNullOrEmpty(_keywordsTagGuid) &&
                                    !string.IsNullOrEmpty(_categoriesTagGuid) &&
                                    !string.IsNullOrWhiteSpace(DaminionItemIdTextBox.Text);
            StartProcessingButton.IsEnabled = canStartProcessing;

            DaminionUrlTextBox.IsEnabled = enable;
            UsernameTextBox.IsEnabled = enable;
            PasswordBox.IsEnabled = enable;
            OllamaUrlTextBox.IsEnabled = enable;
            OllamaModelTextBox.IsEnabled = enable;
            OllamaPromptTextBox.IsEnabled = enable;
            DaminionItemIdTextBox.IsEnabled = enable;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _daminionClient?.Dispose();
            _ollamaClient?.Dispose();
        }
    }
}