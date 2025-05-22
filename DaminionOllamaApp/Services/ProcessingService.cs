// DaminionOllamaApp/Services/ProcessingService.cs
using DaminionOllamaApp.Models;
using DaminionOllamaInteractionLib.Ollama;
using DaminionOllamaInteractionLib.Services; // For ImageMetadataService
using System;
using System.IO;
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

                // 2. Call Ollama API
                OllamaApiClient ollamaClient = new OllamaApiClient(settings.OllamaServerUrl);
                OllamaGenerateResponse? ollamaResponse = null;
                try
                {
                    ollamaResponse = await ollamaClient.AnalyzeImageAsync(settings.OllamaModelName, settings.OllamaPrompt, imageBytes);
                }
                catch (Exception ex) // Catch specific exceptions if OllamaApiClient throws them, or general Exception
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"Ollama API error: {ex.Message}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    return;
                }


                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);

                if (ollamaResponse == null || !ollamaResponse.Done || string.IsNullOrWhiteSpace(ollamaResponse.Response))
                {
                    item.Status = ProcessingStatus.Error;
                    item.StatusMessage = $"Ollama returned an empty or invalid response. API Message: {ollamaResponse?.Response?.Substring(0, Math.Min(ollamaResponse.Response.Length, 100)) ?? "N/A"}";
                    reportProgress?.Invoke($"Error: {item.FileName} - {item.StatusMessage}");
                    return;
                }

                reportProgress?.Invoke($"Processing: {item.FileName} - Parsing Ollama response...");

                // 3. Parse Ollama response
                ParsedOllamaContent parsedContent = OllamaResponseParser.ParseLlavaResponse(ollamaResponse.Response);
                if (!parsedContent.SuccessfullyParsed)
                {
                    item.Status = ProcessingStatus.Error;
                    // If parsing fails but we have a description, use the raw response as description.
                    // Otherwise, indicate parsing failure.
                    if (!string.IsNullOrWhiteSpace(ollamaResponse.Response) && string.IsNullOrWhiteSpace(parsedContent.Description) && !parsedContent.Keywords.Any() && !parsedContent.Categories.Any())
                    {
                        parsedContent.Description = ollamaResponse.Response; // Fallback
                        parsedContent.SuccessfullyParsed = true; // Consider it parsed as a single block
                        reportProgress?.Invoke($"Warning: {item.FileName} - Could not parse structured data, using full response as description.");
                    }
                    else
                    {
                        item.StatusMessage = "Failed to parse structured data from Ollama response.";
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