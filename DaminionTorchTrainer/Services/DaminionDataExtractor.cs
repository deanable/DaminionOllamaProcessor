using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion;
using DaminionTorchTrainer.Models;
using Serilog;
using System.IO;

namespace DaminionTorchTrainer.Services
{
    public class DaminionDataExtractor
    {
        private readonly DaminionApiClient _daminionClient;
        private readonly ImageProcessor _imageProcessor;

        public DaminionDataExtractor(DaminionApiClient daminionClient, ImageProcessor imageProcessor)
        {
            _daminionClient = daminionClient ?? throw new ArgumentNullException(nameof(daminionClient));
            _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
        }

        public async Task<TrainingDataset> ExtractTrainingDataAsync(
            string searchQuery = "",
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Log.Information("===== VISUAL CONTENT TRAINING DATA EXTRACTION =====");
            var searchResponse = await _daminionClient.SearchMediaItemsAsync($"5000,{searchQuery}", "5000,all", maxItems);
            
            if (searchResponse?.Success != true || searchResponse.MediaItems == null || searchResponse.MediaItems.Count == 0)
            {
                Log.Warning("No media items found for search query: '{SearchQuery}'", searchQuery);
                return new TrainingDataset();
            }

            return await ProcessMediaItems(searchResponse.MediaItems, progressCallback);
        }

        public async Task<TrainingDataset> ExtractTrainingDataFromCollectionAsync(
            long collectionId,
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Log.Information("===== COLLECTION-BASED VISUAL CONTENT TRAINING DATA EXTRACTION =====");
            var searchResponse = await _daminionClient.SearchMediaItemsAsync($"12,{collectionId}", "12,all", maxItems);
            
            if (searchResponse?.Success != true || searchResponse.MediaItems == null || searchResponse.MediaItems.Count == 0)
            {
                Log.Warning("No media items found for collection ID: {CollectionId}", collectionId);
                return new TrainingDataset();
            }

            return await ProcessMediaItems(searchResponse.MediaItems, progressCallback);
        }

        private async Task<TrainingDataset> ProcessMediaItems(List<DaminionMediaItem> mediaItems, Action<int, int, string>? progressCallback)
        {
            var metadataVocabulary = await BuildMetadataVocabularyAsync(mediaItems);
            var samples = new List<TrainingData>();

            for(int i = 0; i < mediaItems.Count; i++)
            {
                var mediaItem = mediaItems[i];
                progressCallback?.Invoke(i, mediaItems.Count, $"Processing {mediaItem.FileName}...");
                var sample = await ConvertToTrainingSampleAsync(mediaItem, metadataVocabulary);
                if (sample != null)
                {
                    samples.Add(sample);
                }
            }

            return new TrainingDataset
            {
                Samples = samples,
                FeatureDimension = _imageProcessor.FeatureDimension,
                LabelDimension = metadataVocabulary.Count,
                MetadataVocabulary = metadataVocabulary
            };
        }

        private async Task<Dictionary<string, int>> BuildMetadataVocabularyAsync(List<DaminionMediaItem> mediaItems)
        {
            var uniqueTerms = new HashSet<string>();
            var itemIds = mediaItems.Select(item => item.Id).ToList();
            var pathsResponse = await _daminionClient.GetAbsolutePathsAsync(itemIds);
            
            if (pathsResponse?.Success != true || pathsResponse.Paths == null)
            {
                Log.Warning("Failed to get file paths, cannot extract metadata for vocabulary.");
                return new Dictionary<string, int>();
            }
            
            foreach (var mediaItem in mediaItems)
            {
                if (pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out var filePath) && File.Exists(filePath))
                {
                    try
                    {
                        using var metadataService = new DaminionOllamaInteractionLib.Services.ImageMetadataService(filePath);
                        metadataService.Read();
                        foreach (var term in metadataService.Categories.Concat(metadataService.Keywords))
                        {
                            if (!string.IsNullOrWhiteSpace(term))
                            {
                                uniqueTerms.Add(term.ToLowerInvariant().Trim());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to extract metadata from {FilePath}", filePath);
                    }
                }
            }
            
            return uniqueTerms.OrderBy(x => x).Select((term, index) => new { term, index })
                              .ToDictionary(pair => pair.term, pair => pair.index);
        }

        private async Task<TrainingData?> ConvertToTrainingSampleAsync(DaminionMediaItem mediaItem, Dictionary<string, int> metadataVocabulary)
        {
            var pathsResponse = await _daminionClient.GetAbsolutePathsAsync(new List<long> { mediaItem.Id });
            if (pathsResponse?.Success != true || !pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out var filePath) || !File.Exists(filePath))
            {
                Log.Warning("File not found for media item {MediaItemId}: {FileName}", mediaItem.Id, mediaItem.FileName);
                return null;
            }

            var visualFeatures = await _imageProcessor.ExtractFeaturesAsync(filePath);
            if (visualFeatures == null)
            {
                Log.Warning("Failed to extract visual features from {FilePath}", filePath);
                return null;
            }

            var labels = ExtractMetadataLabels(filePath, metadataVocabulary);

            return new TrainingData
            {
                Id = mediaItem.Id,
                FileName = mediaItem.FileName,
                FilePath = filePath,
                Features = visualFeatures,
                Labels = labels
            };
        }

        private List<float> ExtractMetadataLabels(string imagePath, Dictionary<string, int> metadataVocabulary)
        {
            var labels = new float[metadataVocabulary.Count];
            try
            {
                using var metadataService = new DaminionOllamaInteractionLib.Services.ImageMetadataService(imagePath);
                metadataService.Read();
                
                foreach (var term in metadataService.Categories.Concat(metadataService.Keywords))
                {
                    if (!string.IsNullOrWhiteSpace(term) && metadataVocabulary.TryGetValue(term.ToLowerInvariant().Trim(), out var index))
                    {
                        labels[index] = 1.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to extract metadata labels from {ImagePath}", imagePath);
            }
            return labels.ToList();
        }
    }
}
