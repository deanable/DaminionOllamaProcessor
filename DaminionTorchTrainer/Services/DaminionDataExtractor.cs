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
            var searchResponse = await _daminionClient.SearchMediaItemsAsync($"5000,{searchQuery}", "5000,all", maxItems);
            if (searchResponse?.Success != true || searchResponse.MediaItems == null || !searchResponse.MediaItems.Any())
            {
                Log.Warning("No media items found for search: '{SearchQuery}'", searchQuery);
                return new TrainingDataset();
            }
            return await ProcessMediaItems(searchResponse.MediaItems, progressCallback);
        }

        public async Task<TrainingDataset> ExtractTrainingDataFromCollectionAsync(
            long collectionId,
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            var searchResponse = await _daminionClient.SearchMediaItemsAsync($"12,{collectionId}", "12,all", maxItems);
            if (searchResponse?.Success != true || searchResponse.MediaItems == null || !searchResponse.MediaItems.Any())
            {
                Log.Warning("No media items found for collection: {CollectionId}", collectionId);
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
                progressCallback?.Invoke(i + 1, mediaItems.Count, $"Processing {mediaItem.FileName}...");
                var sample = await ConvertToTrainingSampleAsync(mediaItem, metadataVocabulary);
                if (sample != null) samples.Add(sample);
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
            
            if (pathsResponse?.Success != true) return new Dictionary<string, int>();
            
            foreach (var mediaItem in mediaItems)
            {
                if (pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out var filePath) && File.Exists(filePath))
                {
                    try
                    {
                        using var metadataService = new DaminionOllamaInteractionLib.Services.ImageMetadataService(filePath);
                        metadataService.Read();
                        foreach (var term in metadataService.Categories.Concat(metadataService.Keywords).Where(t => !string.IsNullOrWhiteSpace(t)))
                        {
                            uniqueTerms.Add(term.ToLowerInvariant().Trim());
                        }
                    }
                    catch (Exception ex) { Log.Warning(ex, "Failed to read metadata from {Path}", filePath); }
                }
            }
            return uniqueTerms.OrderBy(x => x).Select((term, index) => new { term, index }).ToDictionary(p => p.term, p => p.index);
        }

        private async Task<TrainingData?> ConvertToTrainingSampleAsync(DaminionMediaItem mediaItem, Dictionary<string, int> vocab)
        {
            var pathsResponse = await _daminionClient.GetAbsolutePathsAsync(new List<long> { mediaItem.Id });
            if (pathsResponse?.Success != true || !pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out var filePath) || !File.Exists(filePath))
            {
                Log.Warning("File not found for media item {Id}", mediaItem.Id);
                return null;
            }

            var features = await _imageProcessor.ExtractFeaturesAsync(filePath);
            if (features == null) return null;

            var labels = ExtractMetadataLabels(filePath, vocab);
            return new TrainingData { Id = mediaItem.Id, FileName = mediaItem.FileName, FilePath = filePath, Features = features, Labels = labels };
        }

        private List<float> ExtractMetadataLabels(string imagePath, Dictionary<string, int> vocab)
        {
            var labels = new float[vocab.Count];
            try
            {
                using var metadataService = new DaminionOllamaInteractionLib.Services.ImageMetadataService(imagePath);
                metadataService.Read();
                foreach (var term in metadataService.Categories.Concat(metadataService.Keywords).Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    if (vocab.TryGetValue(term.ToLowerInvariant().Trim(), out var index)) labels[index] = 1.0f;
                }
            }
            catch (Exception ex) { Log.Warning(ex, "Failed to extract labels from {Path}", imagePath); }
            return labels.ToList();
        }
    }
}
