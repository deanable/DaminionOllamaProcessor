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
    public class ProcessingService
    {
        public async Task ProcessLocalFileAsync(
            FileQueueItem item,
            AppSettings settings,
            Action<string> reportProgress,
            CancellationToken cancellationToken)
        {
            if (item == null || settings == null)
            {
                reportProgress?.Invoke($"Error: File item or settings are null for {item?.FileName ?? "Unknown File"}.");
                if (App.Logger != null) App.Logger.Log($"Error: File item or settings are null for {item?.FileName ?? "Unknown File"}.");
                if (item != null) item.Status = ProcessingStatus.Error;
                return;
            }

            try
            {
                if (App.Logger != null)
                {
                    App.Logger.Log($"Settings at processing start for {item.FileName}: UseOpenRouter={settings.UseOpenRouter}, OpenRouterModelName={settings.OpenRouterModelName}, OllamaModelName={settings.OllamaModelName}, OllamaServerUrl={settings.OllamaServerUrl}");
                }
                item.Status = ProcessingStatus.Processing;
                item.StatusMessage = "Starting...";
                reportProgress?.Invoke($"Processing: {item.FileName} - Reading file...");
                if (App.Logger != null) App.Logger.Log($"Processing started for {item.FileName}");

                if (cancellationToken.IsCancellationRequested)
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Cancelled before starting.";
                    reportProgress?.Invoke($"Cancelled: {item.FileName}");
                    if (App.Logger != null) App.Logger.Log($"Processing cancelled before starting for {item.FileName}");
                    return;
                }

                // 1. Read image bytes
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

                if (settings.UseOpenRouter)
                {
                    if (App.Logger != null) App.Logger.Log($"Preparing OpenRouter request payload for {item.FileName}");
                }
                else
                {
                    if (App.Logger != null) App.Logger.Log($"Preparing Ollama request payload for {item.FileName}");
                }

                reportProgress?.Invoke($"Processing: {item.FileName} - Sending to Ollama...");
                if (App.Logger != null) App.Logger.Log($"Sending {item.FileName} to AI provider: {(settings.UseOpenRouter ? "OpenRouter" : "Ollama")}");
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

                // 2. Call AI API (Ollama or OpenRouter)
                string aiResponse;
                try
                {
                    if (settings.UseOpenRouter)
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
                        if (App.Logger != null)
                        {
                            App.Logger.Log($"OpenRouter response for {item.FileName}: StatusCode={openRouterResult.StatusCode}, ContentSnippet={openRouterResult.Content?.Substring(0, Math.Min(openRouterResult.Content?.Length ?? 0, 200))}, ErrorMessage={openRouterResult.ErrorMessage}, RawResponseSnippet={openRouterResult.RawResponse?.Substring(0, Math.Min(openRouterResult.RawResponse?.Length ?? 0, 500))}");
                        }
                        aiResponse = openRouterResult.Content;
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

                if (App.Logger != null) App.Logger.Log($"Parsing AI response for {item.FileName}");
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

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