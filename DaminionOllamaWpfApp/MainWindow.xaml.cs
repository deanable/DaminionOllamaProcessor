// MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; // <--- Added for JsonException
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32; // For OpenFileDialog

// These are for your class library types:
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion; // For Daminion DTOs
using DaminionOllamaInteractionLib.Ollama;
using System.Net.Http;  // For Ollama DTOs and Parser

namespace DaminionOllamaWpfApp
{
    public partial class MainWindow : Window
    {
        private DaminionApiClient? _daminionClient;
        private OllamaApiClient? _ollamaClient;

        // Store identified Daminion Tag GUIDs
        private string? _descriptionTagGuid;
        private string? _keywordsTagGuid;
        private string? _categoriesTagGuid;

        public MainWindow()
        {
            InitializeComponent();
            _daminionClient = new DaminionApiClient();
            // _ollamaClient will be initialized when Ollama URL is confirmed or before use.
        }

        private void UpdateStatus(string message, bool append = false)
        {
            if (append)
            {
                StatusTextBlock.Text += message + Environment.NewLine;
            }
            else
            {
                StatusTextBlock.Text = message + Environment.NewLine;
            }
            // Scroll to end if using a TextBox, for TextBlock it's less direct
        }

        // In DaminionOllamaWpfApp/MainWindow.xaml.cs

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("---- LoginButton_Click: START ----"); // <-- ADD THIS

            if (_daminionClient == null)
            {
                UpdateStatus("Error: Daminion client not initialized.");
                Console.WriteLine("---- LoginButton_Click: ERROR - _daminionClient is null ----"); // <-- ADD THIS
                return;
            }
            Console.WriteLine("---- LoginButton_Click: _daminionClient is NOT null ----"); // <-- ADD THIS

            string daminionUrl = DaminionUrlTextBox.Text;
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            Console.WriteLine($"---- LoginButton_Click: URL='{daminionUrl}', User='{username}' ----"); // <-- ADD THIS (Password intentionally omitted from this log line)

            if (string.IsNullOrWhiteSpace(daminionUrl) ||
                string.IsNullOrWhiteSpace(username) /* Password can be empty by design for some systems */)
            {
                UpdateStatus("Please enter Daminion URL and Username. Password may be required.");
                Console.WriteLine("---- LoginButton_Click: ERROR - URL or Username is empty ----"); // <-- ADD THIS
                return;
            }

            LoginButton.IsEnabled = false;
            FetchTagsButton.IsEnabled = false;
            StartProcessingButton.IsEnabled = false;
            UpdateStatus("Logging in to Daminion...");

            try
            {
                Console.WriteLine("---- LoginButton_Click: TRY block entered, BEFORE calling _daminionClient.LoginAsync ----"); // <-- ADD THIS
                bool loginSuccess = await _daminionClient.LoginAsync(daminionUrl, username, password);
                Console.WriteLine($"---- LoginButton_Click: AFTER calling _daminionClient.LoginAsync, loginSuccess = {loginSuccess} ----"); // <-- ADD THIS

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
                Console.WriteLine($"---- LoginButton_Click: CATCH ArgumentException: {argEx.Message} ----"); // <-- ADD THIS
            }
            catch (HttpRequestException httpEx)
            {
                UpdateStatus($"Login network error: {httpEx.Message}. Ensure Daminion server is accessible. Check Debug Output.");
                Console.WriteLine($"---- LoginButton_Click: CATCH HttpRequestException: {httpEx.Message} ----"); // <-- ADD THIS
                if (httpEx.InnerException != null) Console.WriteLine($"---- LoginButton_Click: InnerHttpRequestException: {httpEx.InnerException.Message} ----"); // <-- ADD THIS
            }
            catch (Exception ex)
            {
                UpdateStatus($"An unexpected error occurred during login: {ex.Message}. Check Debug Output.");
                Console.WriteLine($"---- LoginButton_Click: CATCH Exception: {ex.Message} ----"); // <-- ADD THIS
                if (ex.InnerException != null) Console.WriteLine($"---- LoginButton_Click: InnerException: {ex.InnerException.Message} ----"); // <-- ADD THIS
                Console.WriteLine($"---- LoginButton_Click: StackTrace: {ex.StackTrace} ----"); // <-- ADD THIS
            }
            finally
            {
                LoginButton.IsEnabled = true;
                Console.WriteLine("---- LoginButton_Click: FINALLY block executed ----"); // <-- ADD THIS
            }
            Console.WriteLine("---- LoginButton_Click: END ----"); // <-- ADD THIS
        }
        private async void FetchTagsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                UpdateStatus("Please login to Daminion first.");
                return;
            }

            FetchTagsButton.IsEnabled = false;
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
                        sb.AppendLine("\nWARNING: One or more target tags (Description, Keywords, Categories) were not found by common names. Please check their GUIDs manually if needed by inspecting Daminion's settings or the full list (if displayed).");
                        StartProcessingButton.IsEnabled = false;
                    }
                    else
                    {
                        sb.AppendLine("\nSuccessfully identified GUIDs for Description, Keywords, and Categories.");
                        // Enable StartProcessingButton only if Item ID is also present
                        StartProcessingButton.IsEnabled = !string.IsNullOrWhiteSpace(DaminionItemIdTextBox.Text);
                    }
                    UpdateStatus(sb.ToString());
                    // You could add a ListBox to display all tags if desired for manual selection.
                }
                else
                {
                    UpdateStatus("No tags returned from Daminion or an error occurred.");
                    StartProcessingButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error fetching tags: {ex.Message}");
                StartProcessingButton.IsEnabled = false;
            }
            finally
            {
                FetchTagsButton.IsEnabled = true;
            }
        }

        private async void TestOllamaButton_Click(object sender, RoutedEventArgs e)
        {
            string ollamaUrl = OllamaUrlTextBox.Text;
            string modelName = OllamaModelTextBox.Text;
            string prompt = OllamaPromptTextBox.Text;

            if (string.IsNullOrWhiteSpace(ollamaUrl) || string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(prompt))
            {
                UpdateStatus("Please enter Ollama URL, Model Name, and Prompt.");
                return;
            }

            _ollamaClient = new OllamaApiClient(ollamaUrl);

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select an Image for Ollama Analysis",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TestOllamaButton.IsEnabled = false;
                UpdateStatus($"Loading image: {openFileDialog.FileName}\nSending to Ollama. This may take some time...", true);

                try
                {
                    byte[] imageBytes = await File.ReadAllBytesAsync(openFileDialog.FileName);
                    OllamaGenerateResponse? ollamaApiResponse = await _ollamaClient.AnalyzeImageAsync(modelName, prompt, imageBytes);

                    var sb = new StringBuilder();
                    if (ollamaApiResponse != null && !string.IsNullOrEmpty(ollamaApiResponse.Response))
                    {
                        sb.AppendLine($"Ollama Model: {ollamaApiResponse.Model}, Done: {ollamaApiResponse.Done}");
                        ParsedOllamaContent parsedContent = OllamaResponseParser.ParseLlavaResponse(ollamaApiResponse.Response);
                        sb.AppendLine("--- PARSED CONTENT ---");
                        sb.AppendLine($"Description: {(string.IsNullOrWhiteSpace(parsedContent.Description) ? "N/A" : parsedContent.Description)}");
                        sb.AppendLine($"Categories: {(parsedContent.Categories.Any() ? string.Join("; ", parsedContent.Categories) : "None")}");
                        sb.AppendLine($"Keywords: {(parsedContent.Keywords.Any() ? string.Join("; ", parsedContent.Keywords) : "None")}");
                        sb.AppendLine($"Successfully Parsed Flag: {parsedContent.SuccessfullyParsed}");
                        sb.AppendLine("--- RAW OLLAMA RESPONSE (first 500 chars) ---");
                        sb.AppendLine(ollamaApiResponse.Response.Substring(0, Math.Min(ollamaApiResponse.Response.Length, 500)));
                    }
                    else if (ollamaApiResponse != null)
                    {
                        sb.AppendLine($"Ollama analysis returned a response object, but the main content was empty or indicated failure.");
                        sb.AppendLine($"Raw Object: Model={ollamaApiResponse.Model}, Done={ollamaApiResponse.Done}, Response='{ollamaApiResponse.Response}'");
                    }
                    else
                    {
                        sb.AppendLine("Ollama analysis returned a null response object. Check console for errors.");
                    }
                    UpdateStatus(sb.ToString(), true);
                }
                catch (ArgumentNullException argEx) { UpdateStatus($"Input error for Ollama: {argEx.Message}", true); }
                catch (HttpRequestException httpEx) { UpdateStatus($"Ollama network error: {httpEx.Message}. Ensure Ollama server ({ollamaUrl}) is running and accessible.", true); }
                catch (JsonException jsonEx) { UpdateStatus($"Ollama response parsing error: {jsonEx.Message}. The response from Ollama was not valid JSON.", true); }
                catch (Exception ex) { UpdateStatus($"An error occurred during Ollama test: {ex.Message}", true); }
                finally { TestOllamaButton.IsEnabled = true; }
            }
            else { UpdateStatus("Ollama test cancelled: No image selected.", true); }
        }

        private async void StartProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_daminionClient == null || !_daminionClient.IsAuthenticated)
            {
                UpdateStatus("Error: Please log in to Daminion first."); return;
            }
            if (string.IsNullOrEmpty(_descriptionTagGuid) || string.IsNullOrEmpty(_keywordsTagGuid) || string.IsNullOrEmpty(_categoriesTagGuid))
            {
                UpdateStatus("Error: Daminion tag GUIDs (Description, Keywords, Categories) are missing. Please fetch tags first."); return;
            }
            if (string.IsNullOrWhiteSpace(DaminionItemIdTextBox.Text) || !long.TryParse(DaminionItemIdTextBox.Text, out long itemId))
            {
                UpdateStatus("Error: Please enter a valid Daminion Media Item ID."); return;
            }

            string ollamaUrl = OllamaUrlTextBox.Text;
            string ollamaModel = OllamaModelTextBox.Text;
            string ollamaPrompt = OllamaPromptTextBox.Text;
            if (string.IsNullOrWhiteSpace(ollamaUrl) || string.IsNullOrWhiteSpace(ollamaModel) || string.IsNullOrWhiteSpace(ollamaPrompt))
            {
                UpdateStatus("Error: Please ensure Ollama URL, Model, and Prompt are set."); return;
            }

            // Disable buttons during processing
            SetUiInteraction(false);
            var processLog = new StringBuilder();
            UpdateStatus($"Starting processing for Daminion Item ID: {itemId}...");

            try
            {
                // 1. Get absolute path for the Daminion item
                processLog.AppendLine("Fetching image path from Daminion...");
                DaminionPathResult pathResult = await _daminionClient.GetAbsolutePathsAsync(new List<long> { itemId });

                if (!pathResult.Success || pathResult.Paths == null || !pathResult.Paths.TryGetValue(itemId.ToString(), out string? imagePath) || string.IsNullOrEmpty(imagePath))
                {
                    processLog.AppendLine($"Failed to get image path for item {itemId}. Error: {pathResult.ErrorMessage ?? "Unknown error."}");
                    UpdateStatus(processLog.ToString(), true); return;
                }
                processLog.AppendLine($"Image path received: {imagePath}");

                // 2. Read image file
                byte[] imageBytes;
                try
                {
                    processLog.AppendLine("Reading image file...");
                    imageBytes = await File.ReadAllBytesAsync(imagePath);
                    processLog.AppendLine($"Image file read successfully ({imageBytes.Length} bytes).");
                }
                catch (Exception ex)
                {
                    processLog.AppendLine($"Failed to read image file at '{imagePath}': {ex.Message}. Ensure the path is accessible.");
                    UpdateStatus(processLog.ToString(), true); return;
                }

                // 3. Send image to Ollama
                processLog.AppendLine("Sending image to Ollama for analysis...");
                _ollamaClient = new OllamaApiClient(ollamaUrl);
                OllamaGenerateResponse? ollamaApiResponse = await _ollamaClient.AnalyzeImageAsync(ollamaModel, ollamaPrompt, imageBytes);

                if (ollamaApiResponse == null || string.IsNullOrEmpty(ollamaApiResponse.Response) || !ollamaApiResponse.Done)
                {
                    processLog.AppendLine($"Ollama analysis failed or returned empty/incomplete response. Response text: '{ollamaApiResponse?.Response ?? "Null response object."}'");
                    // Decide if to proceed with "Ollama Null" or stop
                    // For now, let's try to update with a placeholder if parsing fails but response object exists
                    if (ollamaApiResponse == null) { UpdateStatus(processLog.ToString(), true); return; }
                }
                else { processLog.AppendLine("Ollama analysis successful."); }


                // 4. Parse Ollama's response
                processLog.AppendLine("Parsing Ollama response...");
                ParsedOllamaContent parsedContent = OllamaResponseParser.ParseLlavaResponse(ollamaApiResponse.Response ?? string.Empty);
                if (!parsedContent.SuccessfullyParsed && !string.IsNullOrEmpty(ollamaApiResponse.Response))
                {
                    parsedContent.Description = $"Ollama (unparsed): {ollamaApiResponse.Response.Substring(0, Math.Min(ollamaApiResponse.Response.Length, 200))}";
                }
                else if (!parsedContent.SuccessfullyParsed && string.IsNullOrEmpty(ollamaApiResponse.Response))
                {
                    parsedContent.Description = "Ollama: No content generated.";
                }


                processLog.AppendLine($"Parsed Description: {(string.IsNullOrEmpty(parsedContent.Description) ? "N/A" : parsedContent.Description.Substring(0, Math.Min(parsedContent.Description.Length, 100)) + "...")}");
                processLog.AppendLine($"Parsed Categories: {(parsedContent.Categories.Any() ? string.Join("; ", parsedContent.Categories) : "N/A")}");
                processLog.AppendLine($"Parsed Keywords: {(parsedContent.Keywords.Any() ? string.Join("; ", parsedContent.Keywords) : "N/A")}");

                // 5. Update Daminion with the new metadata
                processLog.AppendLine("Preparing to update Daminion metadata...");
                var operations = new List<DaminionUpdateOperation>();

                // Description (Tag GUID stored in _descriptionTagGuid)
                operations.Add(new DaminionUpdateOperation { Guid = _descriptionTagGuid!, Value = parsedContent.Description, Id = 0, Remove = false });

                // Categories (Tag GUID stored in _categoriesTagGuid)
                // Daminion's batchChange "value" is a string. If categories/keywords are multi-value,
                // Daminion might accept them semi-colon separated or require one operation per value.
                // Assuming semi-colon separated for now.
                if (parsedContent.Categories.Any())
                    operations.Add(new DaminionUpdateOperation { Guid = _categoriesTagGuid!, Value = string.Join("; ", parsedContent.Categories), Id = 0, Remove = false });
                else // If no categories, explicitly set to empty or a placeholder
                    operations.Add(new DaminionUpdateOperation { Guid = _categoriesTagGuid!, Value = (parsedContent.SuccessfullyParsed ? "" : "Ollama: No categories generated"), Id = 0, Remove = false });


                // Keywords (Tag GUID stored in _keywordsTagGuid)
                if (parsedContent.Keywords.Any())
                    operations.Add(new DaminionUpdateOperation { Guid = _keywordsTagGuid!, Value = string.Join("; ", parsedContent.Keywords), Id = 0, Remove = false });
                else // If no keywords, explicitly set to empty or a placeholder
                    operations.Add(new DaminionUpdateOperation { Guid = _keywordsTagGuid!, Value = (parsedContent.SuccessfullyParsed ? "" : "Ollama: No keywords generated"), Id = 0, Remove = false });


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
                processLog.AppendLine($"An critical error occurred during processing: {ex.Message}\n{ex.StackTrace}");
                UpdateStatus(processLog.ToString(), true);
            }
            finally
            {
                SetUiInteraction(true); // Re-enable UI
            }
        }

        private void SetUiInteraction(bool enable)
        {
            LoginButton.IsEnabled = enable;
            FetchTagsButton.IsEnabled = enable && (_daminionClient?.IsAuthenticated ?? false);
            TestOllamaButton.IsEnabled = enable;
            StartProcessingButton.IsEnabled = enable && (_daminionClient?.IsAuthenticated ?? false) &&
                                            !string.IsNullOrEmpty(_descriptionTagGuid) &&
                                            !string.IsNullOrEmpty(DaminionItemIdTextBox.Text);
            // Consider disabling TextBoxes too
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