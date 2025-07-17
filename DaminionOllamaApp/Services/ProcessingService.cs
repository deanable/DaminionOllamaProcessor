// DaminionOllamaApp/Services/ProcessingService.cs
using DaminionOllamaApp.Models;
using DaminionOllamaInteractionLib.Ollama;
using DaminionOllamaInteractionLib.OpenRouter;
using DaminionOllamaInteractionLib.Services; // For ImageMetadataService
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DaminionOllamaApp;

namespace DaminionOllamaApp.Services
{
    /// <summary>
    /// Provides methods for processing local files using AI services (Ollama, OpenRouter, or Gemma).
    /// Handles reading file bytes, sending requests to AI providers, and updating processing status.
    /// </summary>
    public class ProcessingService
    {
        /// <summary>
        /// Processes a local file asynchronously using the selected AI provider and updates its status.
        /// </summary>
        /// <param name="item">The file queue item to process.</param>
        /// <param name="settings">The application settings, including AI provider configuration.</param>
        /// <param name="reportProgress">Callback to report progress messages.</param>
        /// <param name="cancellationToken">Token to support cancellation.</param>
        public async Task ProcessLocalFileAsync(
            FileQueueItem item,
            AppSettings settings,
            Action<string> reportProgress,
            CancellationToken cancellationToken)
        {
            // Validate input arguments
            if (item == null || settings == null)
            {
                reportProgress?.Invoke($"Error: File item or settings are null for {item?.FileName ?? "Unknown File"}.");
                if (App.Logger != null) App.Logger.Log($"Error: File item or settings are null for {item?.FileName ?? "Unknown File"}.");
                if (item != null) item.Status = ProcessingStatus.Error;
                return;
            }

            try
            {
                // Log settings and update status
                if (App.Logger != null)
                {
                    App.Logger.Log($"Settings at processing start for {item.FileName}: UseOpenRouter={settings.UseOpenRouter}, OpenRouterModelName={settings.OpenRouterModelName}, OllamaModelName={settings.OllamaModelName}, OllamaServerUrl={settings.OllamaServerUrl}");
                }
                item.Status = ProcessingStatus.Processing;
                item.StatusMessage = "Starting...";
                reportProgress?.Invoke($"Processing: {item.FileName} - Reading file...");
                if (App.Logger != null) App.Logger.Log($"Processing started for {item.FileName}");

                // Check for cancellation before starting
                if (cancellationToken.IsCancellationRequested)
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Cancelled before starting.";
                    reportProgress?.Invoke($"Cancelled: {item.FileName}");
                    if (App.Logger != null) App.Logger.Log($"Processing cancelled before starting for {item.FileName}");
                    return;
                }

                // Read image bytes from file
                if (App.Logger != null) App.Logger.Log($"Reading file bytes for {item.FileName}");
                byte[] imageBytes;
                try
                {
                    imageBytes = await File.ReadAllBytesAsync(item.FilePath, cancellationToken);
                    if (App.Logger != null) App.Logger.Log($"File bytes read for {item.FileName}, size: {imageBytes.Length} bytes");
                }
                catch (Exception ex)
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"Error reading file: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    if (App.Logger != null) App.Logger.Log($"Error reading file {item.FileName}: {ex.Message}");
                    return;
                }

                // Prepare and send request to the selected AI provider
                if (settings.UseOpenRouter)
                {
                    if (App.Logger != null) App.Logger.Log($"Preparing OpenRouter request payload for {item.FileName}");
                }
                else
                {
                    if (App.Logger != null) App.Logger.Log($"Preparing Ollama request payload for {item.FileName}");
                }

                string providerName = settings.SelectedAiProvider switch
                {
                    AiProvider.Gemma => "Google (Gemma)",
                    AiProvider.OpenRouter => "OpenRouter",
                    AiProvider.Ollama => "Ollama",
                    _ => "Unknown Provider"
                };
                item.StatusMessage = $"Processing: {item.FileName} - Sending to {providerName}...";
                reportProgress?.Invoke(item.StatusMessage);
                if (App.Logger != null) App.Logger.Log($"Sending {item.FileName} to AI provider: {providerName}");
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

                // 2. Call AI API (Ollama, OpenRouter, or Gemma)
                string? aiResponse;
                string usedModelName = null;
                int inputTokens = 0; // Placeholder, update with real token count if available
                int outputTokens = 0; // Placeholder, update with real token count if available
                try
                {
                    if (settings.SelectedAiProvider == AiProvider.Gemma)
                    {
                        if (App.Logger != null) App.Logger.Log($"Sending request to Gemma for {item.FileName} (Model: {settings.GemmaModelName}, MimeType: {item.MimeType ?? "image/jpeg"})");
                        var gemmaClient = new GemmaApiClient(settings.GemmaServiceAccountJsonPath, settings.GemmaModelName);
                        var gemmaResult = await gemmaClient.GenerateContentAsync(settings.OllamaPrompt, imageBytes, item.MimeType ?? "image/jpeg");
                        aiResponse = gemmaResult != null ? gemmaResult : string.Empty;
                        usedModelName = settings.GemmaModelName;
                        // TODO: Parse token usage from response if available
                        if (App.Logger != null) App.Logger.Log($"Gemma response for {item.FileName}: {aiResponse.Substring(0, Math.Min(aiResponse.Length, 500))}");
                        if (string.IsNullOrWhiteSpace(aiResponse))
                        {
                            throw new Exception("Gemma returned an empty response");
                        }
                    }
                    else if (settings.UseOpenRouter)
                    {
                        // Log request metadata
                        if (App.Logger != null)
                        {
                            App.Logger.Log($"OpenRouter request metadata for {item.FileName}: Model={settings.OpenRouterModelName}, PromptSnippet={settings.OllamaPrompt.Substring(0, Math.Min(settings.OllamaPrompt.Length, 100))}, ImageSize={imageBytes.Length} bytes, ImageBase64Snippet={Convert.ToBase64String(imageBytes).Substring(0, 40)}...");
                        }
                        var openRouterClient = new OpenRouterApiClient(settings.OpenRouterApiKey, settings.OpenRouterHttpReferer);
                        // Pre-check API key by listing models
                        var modelCheck = await openRouterClient.ListModelsAsync();
                        if (modelCheck == null)
                        {
                            if (App.Logger != null) App.Logger.Log($"OpenRouter API key check failed for {item.FileName}: Unable to list models. Aborting.");
                            item.Status = ProcessingStatus.Error;
                            item.StatusMessage = "OpenRouter API key invalid or unauthorized.";
                            reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                            return;
                        }
                        if (App.Logger != null) App.Logger.Log($"OpenRouter API key check succeeded for {item.FileName}: Models listed.");
                        var openRouterResult = await openRouterClient.AnalyzeImageAsync(
                            settings.OpenRouterModelName, 
                            settings.OllamaPrompt, 
                            imageBytes);
                        usedModelName = settings.OpenRouterModelName;
                        // TODO: Parse token usage from response if available
                        if (App.Logger != null)
                        {
                            App.Logger.Log($"OpenRouter response for {item.FileName}: StatusCode={openRouterResult.StatusCode}, ContentSnippet={openRouterResult.Content?.Substring(0, Math.Min(openRouterResult.Content?.Length ?? 0, 200))}, ErrorMessage={openRouterResult.ErrorMessage}, RawResponseSnippet={openRouterResult.RawResponse?.Substring(0, Math.Min(openRouterResult.RawResponse?.Length ?? 0, 500))}");
                        }
                        aiResponse = openRouterResult.Content ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(aiResponse))
                        {
                            throw new Exception("OpenRouter returned an empty response");
                        }
                    }
                    else
                    {
                        if (App.Logger != null) App.Logger.Log($"Sending request to Ollama for {item.FileName}");
                        OllamaApiClient ollamaClient = new OllamaApiClient(settings.OllamaServerUrl);
                        OllamaGenerateResponse? ollamaResponse = await ollamaClient.AnalyzeImageAsync(settings.OllamaModelName, settings.OllamaPrompt, imageBytes);
                        usedModelName = settings.OllamaModelName;
                        // Ollama is always free, so no spend tracking
                        if (App.Logger != null) App.Logger.Log($"Ollama response received for {item.FileName}: {ollamaResponse?.Response?.Substring(0, Math.Min(ollamaResponse?.Response?.Length ?? 0, 200))}");
                        if (ollamaResponse == null || !ollamaResponse.Done || string.IsNullOrWhiteSpace(ollamaResponse.Response))
                        {
                            throw new Exception($"Ollama returned an empty or invalid response. API Message: {ollamaResponse?.Response?.Substring(0, Math.Min(ollamaResponse.Response?.Length ?? 0, 100)) ?? "N/A"}");
                        }
                        aiResponse = ollamaResponse.Response;
                    }
                }
                catch (HttpRequestException ex)
                {
                    var serviceName = settings.UseOpenRouter ? "OpenRouter" : "Ollama";
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"{serviceName} connection error: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    if (App.Logger != null) App.Logger.Log($"{serviceName} connection error for {item.FileName}: {ex.Message}");
                    return;
                }
                catch (TaskCanceledException ex)
                {
                    var serviceName = settings.UseOpenRouter ? "OpenRouter" : "Ollama";
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"{serviceName} request timed out: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    if (App.Logger != null) App.Logger.Log($"{serviceName} request timed out for {item.FileName}: {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    var serviceName = settings.UseOpenRouter ? "OpenRouter" : "Ollama";
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"{serviceName} API error: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    if (App.Logger != null) App.Logger.Log($"{serviceName} API error for {item.FileName}: {ex.Message}");
                    return;
                }

                // --- Usage/Spend Tracking ---
                if (!string.IsNullOrEmpty(usedModelName))
                {
                    var usage = settings.GetOrCreateModelUsage(usedModelName);
                    // For now, increment by 1 request; update with real token counts if available
                    usage.InputTokensUsed += inputTokens > 0 ? inputTokens : 1000; // Assume 1K tokens/request as a placeholder
                    usage.OutputTokensUsed += outputTokens;
                    // Look up pricing
                    if (DaminionOllamaApp.Services.ModelPricingTable.Pricing.TryGetValue(usedModelName, out var pricing))
                    {
                        int paidInputTokens = Math.Max(0, usage.InputTokensUsed - pricing.FreeInputTokens);
                        int paidOutputTokens = Math.Max(0, usage.OutputTokensUsed - pricing.FreeOutputTokens);
                        usage.EstimatedSpendUSD = (paidInputTokens / 1000.0) * pricing.PricePer1KInputTokens + (paidOutputTokens / 1000.0) * pricing.PricePer1KOutputTokens;
                        usage.FreeTierExceeded = usage.InputTokensUsed > pricing.FreeInputTokens;
                    }
                    else
                    {
                        usage.EstimatedSpendUSD = 0;
                        usage.FreeTierExceeded = false;
                    }
                }

                if (App.Logger != null) App.Logger.Log($"Parsing AI response for {item.FileName}");
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

                // Log the raw model response for debugging parser issues
                if (App.Logger != null)
                {
                    App.Logger.Log($"[AI Raw Response] {item.FileName}: {aiResponse}");
                }
                // 3. Parse AI response
                ParsedOllamaContent parsedContent = OllamaResponseParser.ParseLlavaResponse(aiResponse);
                if (!parsedContent.SuccessfullyParsed)
                {
                    item.Status = ProcessingStatus.Error;
                    // If parsing fails but we have a description, use the raw response as description.
                    // Otherwise, indicate parsing failure.
                    if (!string.IsNullOrWhiteSpace(aiResponse) && string.IsNullOrWhiteSpace(parsedContent.Description) && !parsedContent.Keywords.Any() && !parsedContent.Categories.Any())
                    {
                        parsedContent.Description = aiResponse; // Fallback
                        parsedContent.SuccessfullyParsed = true; // Consider it parsed as a single block
                        reportProgress?.Invoke($"Warning: {item.FileName} - Could not parse structured data, using full response as description.");
                    }
                    else
                    {
                        item.StatusMessage = "Failed to parse structured data from AI response.";
                        reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                        return;
                    }
                }


                reportProgress?.Invoke($"Processing: {item.FileName} - Writing metadata to file...");
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

                // 4. Write metadata to image file
                try
                {
                    // Using ImageMetadataService as it's more comprehensive
                    var metadataService = new ImageMetadataService(item.FilePath);
                    metadataService.Read(); // Read existing metadata first
                    metadataService.PopulateFromOllamaContent(parsedContent);
                    metadataService.Save(); // Save changes

                    item.Status = ProcessingStatus.Processed;
                    item.StatusMessage = "Metadata written successfully.";
                    reportProgress?.Invoke($"Success: {item.FileName} - Metadata written.");
                }
                catch (Exception ex)
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"Error writing metadata: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                item.Status = ProcessingStatus.Cancelled;
                item.StatusMessage = "Processing cancelled by user.";
                reportProgress?.Invoke($"Cancelled: {item.FileName}");
            }
            catch (Exception ex)
            {
                item.Status = ProcessingStatus.Error;
                item.StatusMessage = $"Unexpected error: {ex.Message}";
                reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
            }
        }
    }
}