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
    /// <summary>
    /// Service for extracting training data from Daminion metadata
    /// </summary>
    public class DaminionDataExtractor
    {
        private readonly DaminionApiClient _daminionClient;
        private readonly ImageProcessor _imageProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaminionDataExtractor"/> class.
        /// </summary>
        /// <param name="daminionClient">The Daminion API client</param>
        /// <param name="imageProcessor">The image processor for feature extraction</param>
        public DaminionDataExtractor(DaminionApiClient daminionClient, ImageProcessor imageProcessor)
        {
            _daminionClient = daminionClient ?? throw new ArgumentNullException(nameof(daminionClient));
            _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
        }

        /// <summary>
        /// Extracts training data from Daminion catalog using simple text search
        /// </summary>
        /// <param name="searchQuery">Simple text search query (e.g., "city", "interior exterior")</param>
        /// <param name="maxItems">Maximum number of items to extract</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <returns>A training dataset</returns>
        public async Task<TrainingDataset> ExtractTrainingDataAsync(
            string searchQuery = "",
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Log.Information("===== VISUAL CONTENT TRAINING DATA EXTRACTION =====");
            Log.Information("Search Query: '{SearchQuery}', MaxItems: {MaxItems}", searchQuery, maxItems);

            // Step 1: Search for media items
            progressCallback?.Invoke(0, maxItems, "Searching for media items...");
            
            var queryLine = string.IsNullOrWhiteSpace(searchQuery) ? "" : $"5000,{searchQuery}";
            var operators = string.IsNullOrWhiteSpace(searchQuery) ? "" : "5000,all";
            
            Log.Information("Calling SearchMediaItemsAsync with queryLine='{QueryLine}', operators='{Operators}', maxItems={MaxItems}", 
                queryLine, operators, maxItems);
            var searchResponse = await _daminionClient.SearchMediaItemsAsync(queryLine, operators, maxItems);
            
            Log.Information("Search response received - Success: {Success}, Error: {Error}, MediaItemsCount: {MediaItemsCount}", 
                searchResponse?.Success, searchResponse?.Error, searchResponse?.MediaItems?.Count ?? 0);
            
            if (searchResponse?.Success != true || searchResponse.MediaItems == null || searchResponse.MediaItems.Count == 0)
            {
                Log.Warning("No media items found for search query: '{SearchQuery}'", searchQuery);
                return new TrainingDataset { Samples = new List<TrainingData>(), FeatureDimension = 0, LabelDimension = 0 };
            }

            var mediaItems = searchResponse.MediaItems;
            Log.Information("Found {MediaItemsCount} media items for query '{SearchQuery}'", mediaItems.Count, searchQuery);
            
            var metadataVocabulary = await BuildMetadataVocabularyAsync(mediaItems);
            
            Log.Information("Built metadata vocabulary with {VocabularySize} unique terms", metadataVocabulary.Count);

            var samples = new List<TrainingData>();
            var processedCount = 0;

            foreach (var mediaItem in mediaItems)
            {
                try
                {
                    progressCallback?.Invoke(processedCount, mediaItems.Count, $"Processing {mediaItem.FileName}...");
                    
                    var sample = await ConvertToTrainingSampleAsync(mediaItem, metadataVocabulary);
                    if (sample != null)
                    {
                        samples.Add(sample);
                        Log.Information("Created training sample for {FileName}: {ImageFeatures} features, {LabelCount} labels", 
                            mediaItem.FileName, sample.Features.Count, sample.Labels.Count(x => x > 0));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to convert media item {MediaItemId} to training sample", mediaItem.Id);
                }
                
                processedCount++;
            }

            var featureDimension = _imageProcessor.FeatureDimension;
            var labelDimension = metadataVocabulary.Count;

            Log.Information("===== EXTRACTION SUMMARY =====");
            Log.Information("Samples: {SampleCount}", samples.Count);
            Log.Information("Features: {FeatureDimension} (from ResNet50)", featureDimension);
            Log.Information("Labels: {LabelDimension} (metadata terms: {MetadataTerms})", labelDimension, string.Join(", ", metadataVocabulary.Keys));

            return new TrainingDataset
            {
                Samples = samples,
                FeatureDimension = featureDimension,
                LabelDimension = labelDimension,
                MetadataVocabulary = metadataVocabulary
            };
        }

        /// <summary>
        /// Extracts training data from Daminion catalog using a specific collection
        /// </summary>
        public async Task<TrainingDataset> ExtractTrainingDataFromCollectionAsync(
            long collectionId,
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Log.Information("===== COLLECTION-BASED VISUAL CONTENT TRAINING DATA EXTRACTION =====");
            Log.Information("Collection ID: {CollectionId}, MaxItems: {MaxItems}", collectionId, maxItems);

            progressCallback?.Invoke(0, maxItems, "Searching for media items in collection...");
            
            var queryLine = $"12,{collectionId}";
            var operators = "12,all";
            
            Log.Information("Searching with query: {QueryLine}, operators: {Operators}", queryLine, operators);
            var searchResponse = await _daminionClient.SearchMediaItemsAsync(queryLine, operators, maxItems);
            
            Log.Information("Collection search response received - Success: {Success}, Error: {Error}, MediaItemsCount: {MediaItemsCount}", 
                searchResponse?.Success, searchResponse?.Error, searchResponse?.MediaItems?.Count ?? 0);
            
            if (searchResponse?.Success != true || searchResponse.MediaItems == null || searchResponse.MediaItems.Count == 0)
            {
                Log.Warning("No media items found for collection ID: {CollectionId}", collectionId);
                return new TrainingDataset { Samples = new List<TrainingData>(), FeatureDimension = 0, LabelDimension = 0 };
            }

            var mediaItems = searchResponse.MediaItems;
            Log.Information("Found {MediaItemsCount} media items in collection {CollectionId}", mediaItems.Count, collectionId);
            
            var metadataVocabulary = await BuildMetadataVocabularyAsync(mediaItems);
            
            Log.Information("Built metadata vocabulary with {VocabularySize} unique terms", metadataVocabulary.Count);

            var samples = new List<TrainingData>();
            var processedCount = 0;

            foreach (var mediaItem in mediaItems)
            {
                try
                {
                    progressCallback?.Invoke(processedCount, mediaItems.Count, $"Processing {mediaItem.FileName}...");
                    
                    var sample = await ConvertToTrainingSampleAsync(mediaItem, metadataVocabulary);
                    if (sample != null)
                    {
                        samples.Add(sample);
                        Log.Information("Created training sample for {FileName}: {ImageFeatures} features, {LabelCount} labels", 
                            mediaItem.FileName, sample.Features.Count, sample.Labels.Count(x => x > 0));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to convert media item {MediaItemId} to training sample", mediaItem.Id);
                }
                
                processedCount++;
            }

            var featureDimension = _imageProcessor.FeatureDimension;
            var labelDimension = metadataVocabulary.Count;

            Log.Information("===== COLLECTION EXTRACTION SUMMARY =====");
            Log.Information("Collection ID: {CollectionId}", collectionId);
            Log.Information("Samples: {SampleCount}", samples.Count);
            Log.Information("Features: {FeatureDimension} (from ResNet50)", featureDimension);
            Log.Information("Labels: {LabelDimension} (metadata terms: {MetadataTerms})", labelDimension, string.Join(", ", metadataVocabulary.Keys));

            return new TrainingDataset
            {
                Samples = samples,
                FeatureDimension = featureDimension,
                LabelDimension = labelDimension,
                MetadataVocabulary = metadataVocabulary
            };
        }

        /// <summary>
        /// Builds metadata vocabulary from EXIF/IPTC data in the media items
        /// </summary>
        private async Task<Dictionary<string, int>> BuildMetadataVocabularyAsync(List<DaminionMediaItem> mediaItems)
        {
            var metadataVocabulary = new Dictionary<string, int>();
            var uniqueTerms = new HashSet<string>();
            
            Log.Information("Building metadata vocabulary from {MediaItemsCount} media items", mediaItems.Count);
            
            var itemIds = mediaItems.Select(item => item.Id).ToList();
            var pathsResponse = await _daminionClient.GetAbsolutePathsAsync(itemIds);
            
            if (pathsResponse?.Success != true || pathsResponse.Paths == null)
            {
                Log.Warning("Failed to get file paths, cannot extract metadata");
                return metadataVocabulary;
            }
            
            foreach (var mediaItem in mediaItems)
            {
                if (pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out var filePath) && File.Exists(filePath))
                {
                    try
                    {
                        using var metadataService = new DaminionOllamaInteractionLib.Services.ImageMetadataService(filePath);
                        metadataService.Read();
                        
                        foreach (var category in metadataService.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                        {
                            uniqueTerms.Add(category.ToLowerInvariant().Trim());
                        }
                        
                        foreach (var keyword in metadataService.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                        {
                            uniqueTerms.Add(keyword.ToLowerInvariant().Trim());
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to extract metadata from {FilePath}", filePath);
                    }
                }
            }
            
            var sortedTerms = uniqueTerms.OrderBy(x => x).ToList();
            for (int i = 0; i < sortedTerms.Count; i++)
            {
                metadataVocabulary[sortedTerms[i]] = i;
            }
            
            Log.Information("Metadata vocabulary built with {VocabularySize} terms", metadataVocabulary.Count);
            
            return metadataVocabulary;
        }

        /// <summary>
        /// Converts a media item to a training sample using the ImageProcessor for feature extraction.
        /// </summary>
        private async Task<TrainingData?> ConvertToTrainingSampleAsync(DaminionMediaItem mediaItem, Dictionary<string, int> metadataVocabulary)
        {
            try
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
                    Log.Warning("Failed to extract visual features from {FilePath} using ImageProcessor", filePath);
                    return null;
                }

                var labels = await ExtractMetadataLabelsAsync(filePath, metadataVocabulary);

                Log.Information("Converted {FileName} - Features: {FeatureCount} (ResNet50), Labels: {LabelCount}",
                    mediaItem.FileName, visualFeatures.Count, labels.Count(x => x > 0));

                return new TrainingData
                {
                    Id = mediaItem.Id,
                    FileName = mediaItem.FileName,
                    FilePath = filePath,
                    Features = visualFeatures,
                    Labels = labels,
                    DataSource = DataSourceType.API
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to convert media item {MediaItemId} to training sample", mediaItem.Id);
                return null;
            }
        }

        /// <summary>
        /// Extracts metadata labels from EXIF/IPTC data
        /// </summary>
        private async Task<List<float>> ExtractMetadataLabelsAsync(string imagePath, Dictionary<string, int> metadataVocabulary)
        {
            var labels = new float[metadataVocabulary.Count];
            
            try
            {
                using var metadataService = new DaminionOllamaInteractionLib.Services.ImageMetadataService(imagePath);
                metadataService.Read();
                
                foreach (var category in metadataService.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                {
                    if (metadataVocabulary.TryGetValue(category.ToLowerInvariant().Trim(), out var index))
                    {
                        labels[index] = 1.0f;
                    }
                }
                
                foreach (var keyword in metadataService.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                {
                    if (metadataVocabulary.TryGetValue(keyword.ToLowerInvariant().Trim(), out var index))
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
