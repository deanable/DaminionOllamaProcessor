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
                if (item != null) item.Status = ProcessingStatus.Error;
                return;
            }

            try
            {
                item.Status = ProcessingStatus.Processing;
                item.StatusMessage = "Starting...";
                reportProgress?.Invoke($"Processing: {item.FileName} - Reading file...");

                if (cancellationToken.IsCancellationRequested)
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.StatusMessage = "Cancelled before starting.";
                    reportProgress?.Invoke($"Cancelled: {item.FileName}");
                    return;
                }

                // 1. Read image bytes
                byte[] imageBytes;
                try
                {
                    imageBytes = await File.ReadAllBytesAsync(item.FilePath, cancellationToken);
                }
                catch (Exception ex)
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"Error reading file: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    return;
                }

                reportProgress?.Invoke($"Processing: {item.FileName} - Sending to Ollama...");
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

                // 2. Call AI API (Ollama or OpenRouter)
                string aiResponse;
                try
                {
                    if (settings.UseOpenRouter)
                    {
                        // Use OpenRouter service
                        var openRouterClient = new OpenRouterApiClient(settings.OpenRouterApiKey, settings.OpenRouterHttpReferer);
                        aiResponse = await openRouterClient.AnalyzeImageAsync(
                            settings.OpenRouterModelName, 
                            settings.OllamaPrompt, 
                            imageBytes);
                        
                        if (string.IsNullOrWhiteSpace(aiResponse))
                        {
                            throw new Exception("OpenRouter returned an empty response");
                        }
                    }
                    else
                    {
                        // Use Ollama service
                        OllamaApiClient ollamaClient = new OllamaApiClient(settings.OllamaServerUrl);
                        OllamaGenerateResponse? ollamaResponse = await ollamaClient.AnalyzeImageAsync(settings.OllamaModelName, settings.OllamaPrompt, imageBytes);
                        
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
                    return;
                }
                catch (TaskCanceledException ex)
                {
                    var serviceName = settings.UseOpenRouter ? "OpenRouter" : "Ollama";
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"{serviceName} request timed out: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    return;
                }
                catch (Exception ex)
                {
                    var serviceName = settings.UseOpenRouter ? "OpenRouter" : "Ollama";
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"{serviceName} API error: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    return;
                }


                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

                reportProgress?.Invoke($"Processing: {item.FileName} - Parsing AI response...");

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