using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion;
using DaminionTorchTrainer.Models;
using Serilog;

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for extracting training data from Daminion metadata
    /// </summary>
    public class DaminionDataExtractor
    {
        private readonly DaminionApiClient _daminionClient;
        private readonly Dictionary<string, int> _tagToIndex = new();
        private readonly Dictionary<string, int> _categoryToIndex = new();
        private readonly Dictionary<string, int> _keywordToIndex = new();
        private readonly Dictionary<string, int> _formatToIndex = new();
        private readonly Dictionary<long, string> _tagIdToGuid = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DaminionDataExtractor"/> class.
        /// </summary>
        /// <param name="daminionClient">The Daminion API client</param>
        public DaminionDataExtractor(DaminionApiClient daminionClient)
        {
            _daminionClient = daminionClient ?? throw new ArgumentNullException(nameof(daminionClient));
        }

        /// <summary>
        /// Extracts training data from Daminion catalog
        /// </summary>
        /// <param name="queryLine">Search query for media items</param>
        /// <param name="fOperators">Search operators</param>
        /// <param name="maxItems">Maximum number of items to extract</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <returns>A training dataset</returns>
        public async Task<TrainingDataset> ExtractTrainingDataAsync(
            string queryLine = "",
            string fOperators = "",
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Console.WriteLine($"[DaminionDataExtractor] Starting data extraction with query: '{queryLine}', maxItems: {maxItems}");
            Log.Information("Starting Daminion data extraction - Query: {Query}, MaxItems: {MaxItems}", queryLine, maxItems);

            // First, get all available tags to build the vocabulary
            progressCallback?.Invoke(0, 100, "Building vocabularies...");
            await BuildVocabulariesAsync();
            progressCallback?.Invoke(10, 100, "Vocabularies built successfully");

            // Search for media items
            progressCallback?.Invoke(15, 100, "Searching for media items...");
            Console.WriteLine($"[DaminionDataExtractor] Searching for media items...");
            var searchResponse = await _daminionClient.SearchMediaItemsAsync(queryLine, fOperators);
            
            // Log search response details
            Console.WriteLine($"[DaminionDataExtractor] Search response - Success: {searchResponse?.Success}, Error: {searchResponse?.Error}");
            if (searchResponse?.Success == true && searchResponse.MediaItems != null)
            {
                Console.WriteLine($"[DaminionDataExtractor] Search response contains {searchResponse.MediaItems.Count} media items");
                
                // Log first few media items for inspection
                var sampleItems = searchResponse.MediaItems.Take(3).ToList();
                foreach (var item in sampleItems)
                {
                    Console.WriteLine($"[DaminionDataExtractor] Sample media item - ID: {item.Id}, FileName: {item.FileName}, " +
                                    $"Format: {item.FormatType}, MediaFormat: {item.MediaFormat}, " +
                                    $"Width: {item.Width}, Height: {item.Height}, FileSize: {item.FileSize}");
                }
            }
            
            if (searchResponse?.Success != true || searchResponse.MediaItems == null)
            {
                throw new InvalidOperationException($"Failed to search media items: {searchResponse?.Error ?? "Unknown error"}");
            }

            var mediaItems = searchResponse.MediaItems.Take(maxItems).ToList();
            Console.WriteLine($"[DaminionDataExtractor] Processing {mediaItems.Count} media items (requested max: {maxItems}, available: {searchResponse.MediaItems.Count})");

            // Get file paths for the items
            progressCallback?.Invoke(25, 100, $"Getting file paths for {mediaItems.Count} items...");
            var itemIds = mediaItems.Select(item => item.Id).ToList();
            Console.WriteLine($"[DaminionDataExtractor] Getting file paths for {itemIds.Count} items...");
            var pathsResponse = await _daminionClient.GetAbsolutePathsAsync(itemIds);
            
            // Log paths response details
            Console.WriteLine($"[DaminionDataExtractor] Paths response - Success: {pathsResponse?.Success}, Error: {pathsResponse?.ErrorMessage}");
            if (pathsResponse?.Success == true && pathsResponse.Paths != null)
            {
                Console.WriteLine($"[DaminionDataExtractor] Paths response contains {pathsResponse.Paths.Count} file paths");
                
                // Log first few paths for inspection
                var samplePaths = pathsResponse.Paths.Take(3).ToList();
                foreach (var path in samplePaths)
                {
                    Console.WriteLine($"[DaminionDataExtractor] Sample path - ID: {path.Key}, Path: {path.Value}");
                }
            }
            
            var trainingSamples = new List<TrainingData>();

            progressCallback?.Invoke(35, 100, "Converting media items to training data...");
            
            for (int i = 0; i < mediaItems.Count; i++)
            {
                var mediaItem = mediaItems[i];
                var trainingData = await ConvertToTrainingDataAsync(mediaItem, pathsResponse);
                if (trainingData != null)
                {
                    trainingSamples.Add(trainingData);
                }
                
                // Report progress every 10 items or at the end
                if (i % 10 == 0 || i == mediaItems.Count - 1)
                {
                    var progress = 35 + (int)((i + 1) * 60.0 / mediaItems.Count);
                    progressCallback?.Invoke(progress, 100, $"Processed {i + 1}/{mediaItems.Count} items...");
                }
            }

            // Calculate dimensions based on vocabularies
            var featureDimension = CalculateFeatureDimension();
            var labelDimension = _categoryToIndex.Count + _keywordToIndex.Count; // Use categories + keywords for multi-label

            var dataset = new TrainingDataset
            {
                Name = $"Daminion_Dataset_{DateTime.Now:yyyyMMdd_HHmmss}",
                Description = $"Training dataset extracted from Daminion catalog with query: {queryLine}",
                Samples = trainingSamples,
                FeatureDimension = featureDimension,
                LabelDimension = labelDimension
            };

            progressCallback?.Invoke(100, 100, "Dataset creation completed");
            
            Console.WriteLine($"[DaminionDataExtractor] Created dataset with {trainingSamples.Count} samples, " +
                            $"feature dimension: {featureDimension}, label dimension: {labelDimension}");
            
            Log.Information("Dataset created successfully - Samples: {SampleCount}, Features: {FeatureDim}, Labels: {LabelDim}", 
                trainingSamples.Count, featureDimension, labelDimension);

            return dataset;
        }

        /// <summary>
        /// Builds vocabularies from available tags and categories
        /// </summary>
        private async Task BuildVocabulariesAsync()
        {
            // Get all tags
            Console.WriteLine($"[DaminionDataExtractor] Getting all tags...");
            var tagsResponse = await _daminionClient.GetTagsAsync();
            
            // Log tags response details
            Console.WriteLine($"[DaminionDataExtractor] Tags response - Success: {tagsResponse?.Success}, Error: {tagsResponse?.Error}");
            if (tagsResponse?.Success == true && tagsResponse.Data != null)
            {
                int index = 0;
                Console.WriteLine($"[DaminionDataExtractor] Processing {tagsResponse.Data.Count} tags...");
                foreach (var tag in tagsResponse.Data)
                {
                    _tagToIndex[tag.Name] = index++;
                    _tagIdToGuid[tag.Id] = tag.Guid;
                    
                    Console.WriteLine($"[DaminionDataExtractor] Tag {index-1}: ID={tag.Id}, Name='{tag.Name}', GUID={tag.Guid}");
                    
                    // Identify Categories and Keywords tags
                    if (tag.Name.Equals("Categories", StringComparison.OrdinalIgnoreCase) ||
                        tag.Name.Equals("Category", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[DaminionDataExtractor] Found Categories tag: ID={tag.Id}, Name='{tag.Name}'");
                        await BuildCategoryVocabularyAsync(tag.Id);
                    }
                    else if (tag.Name.Equals("Keywords", StringComparison.OrdinalIgnoreCase) ||
                             tag.Name.Equals("Keyword", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[DaminionDataExtractor] Found Keywords tag: ID={tag.Id}, Name='{tag.Name}'");
                        await BuildKeywordVocabularyAsync(tag.Id);
                    }
                }
                Console.WriteLine($"[DaminionDataExtractor] Built tag vocabulary with {_tagToIndex.Count} tags");
            }

            // Build format vocabulary
            var formats = new[] { "image", "video", "audio", "document" };
            int formatIndex = 0;
            foreach (var format in formats)
            {
                _formatToIndex[format] = formatIndex++;
            }
        }

        /// <summary>
        /// Builds category vocabulary from Daminion Categories tag
        /// </summary>
        private async Task BuildCategoryVocabularyAsync(long categoryTagId)
        {
            try
            {
                Console.WriteLine($"[DaminionDataExtractor] Getting category values for tag ID: {categoryTagId}");
                var categoryValuesResponse = await _daminionClient.GetTagValuesAsync(categoryTagId, 1000, 0);
                
                // Log category values response details
                Console.WriteLine($"[DaminionDataExtractor] Category values response - Success: {categoryValuesResponse?.Success}, Error: {categoryValuesResponse?.Error}");
                if (categoryValuesResponse?.Success == true && categoryValuesResponse.Values != null)
                {
                    Console.WriteLine($"[DaminionDataExtractor] Processing {categoryValuesResponse.Values.Count} category values...");
                    int index = 0;
                    foreach (var categoryValue in categoryValuesResponse.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(categoryValue.Text))
                        {
                            var trimmedText = categoryValue.Text.Trim();
                            _categoryToIndex[trimmedText] = index++;
                            Console.WriteLine($"[DaminionDataExtractor] Category {index-1}: ID={categoryValue.Id}, Text='{trimmedText}'");
                        }
                    }
                    Console.WriteLine($"[DaminionDataExtractor] Built category vocabulary with {_categoryToIndex.Count} categories");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DaminionDataExtractor] Error building category vocabulary: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds keyword vocabulary from Daminion Keywords tag
        /// </summary>
        private async Task BuildKeywordVocabularyAsync(long keywordTagId)
        {
            try
            {
                Console.WriteLine($"[DaminionDataExtractor] Getting keyword values for tag ID: {keywordTagId}");
                var keywordValuesResponse = await _daminionClient.GetTagValuesAsync(keywordTagId, 1000, 0);
                
                // Log keyword values response details
                Console.WriteLine($"[DaminionDataExtractor] Keyword values response - Success: {keywordValuesResponse?.Success}, Error: {keywordValuesResponse?.Error}");
                if (keywordValuesResponse?.Success == true && keywordValuesResponse.Values != null)
                {
                    Console.WriteLine($"[DaminionDataExtractor] Processing {keywordValuesResponse.Values.Count} keyword values...");
                    int index = 0;
                    foreach (var keywordValue in keywordValuesResponse.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(keywordValue.Text))
                        {
                            var trimmedText = keywordValue.Text.Trim();
                            _keywordToIndex[trimmedText] = index++;
                            Console.WriteLine($"[DaminionDataExtractor] Keyword {index-1}: ID={keywordValue.Id}, Text='{trimmedText}'");
                        }
                    }
                    Console.WriteLine($"[DaminionDataExtractor] Built keyword vocabulary with {_keywordToIndex.Count} keywords");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DaminionDataExtractor] Error building keyword vocabulary: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts a Daminion media item to training data
        /// </summary>
        private async Task<TrainingData?> ConvertToTrainingDataAsync(
            DaminionMediaItem mediaItem,
            DaminionPathResult? pathsResponse)
        {
            try
            {
                Console.WriteLine($"[DaminionDataExtractor] Converting media item {mediaItem.Id} - {mediaItem.FileName}");
                
                var trainingData = new TrainingData
                {
                    Id = mediaItem.Id,
                    FileName = mediaItem.FileName,
                    MediaFormat = mediaItem.MediaFormat,
                    Width = mediaItem.Width,
                    Height = mediaItem.Height,
                    FileSize = mediaItem.FileSize,
                    FormatType = mediaItem.FormatType,
                    ColorLabel = mediaItem.ColorLabel,
                    VersionControlState = mediaItem.VersionControlState,
                    ExpirationDate = mediaItem.ExpirationDate
                };

                // Get file path if available
                if (pathsResponse?.Paths != null && 
                    pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out string? filePath))
                {
                    trainingData.FilePath = filePath;
                }

                // Extract features from metadata
                trainingData.Features = ExtractFeatures(mediaItem);
                Console.WriteLine($"[DaminionDataExtractor] Extracted {trainingData.Features.Count} features for item {mediaItem.Id}");

                // Extract labels from metadata
                trainingData.Labels = ExtractLabels(mediaItem);
                Console.WriteLine($"[DaminionDataExtractor] Extracted {trainingData.Labels.Count} labels for item {mediaItem.Id}");

                return trainingData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DaminionDataExtractor] Error converting media item {mediaItem.Id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts numerical features from media item metadata
        /// </summary>
        private List<float> ExtractFeatures(DaminionMediaItem mediaItem)
        {
            var features = new List<float>();

            // Basic numerical features
            var width = mediaItem.Width ?? 0;
            var height = mediaItem.Height ?? 0;
            var fileSize = mediaItem.FileSize ?? 0;
            var colorLabel = mediaItem.ColorLabel ?? 0;
            var versionControlState = mediaItem.VersionControlState ?? 0;
            
            features.Add(width);
            features.Add(height);
            features.Add(fileSize);
            features.Add(colorLabel);
            features.Add(versionControlState);
            
            Console.WriteLine($"[DaminionDataExtractor] Basic features for item {mediaItem.Id}: " +
                            $"Width={width}, Height={height}, FileSize={fileSize}, " +
                            $"ColorLabel={colorLabel}, VersionControlState={versionControlState}");

            // Format type encoding
            var formatEncoding = new float[_formatToIndex.Count];
            if (!string.IsNullOrEmpty(mediaItem.FormatType) && _formatToIndex.TryGetValue(mediaItem.FormatType.ToLower(), out int formatIndex))
            {
                formatEncoding[formatIndex] = 1.0f;
                Console.WriteLine($"[DaminionDataExtractor] Format encoding for item {mediaItem.Id}: FormatType='{mediaItem.FormatType}' -> index {formatIndex}");
            }
            else
            {
                Console.WriteLine($"[DaminionDataExtractor] Format encoding for item {mediaItem.Id}: FormatType='{mediaItem.FormatType}' -> not found in vocabulary");
            }
            features.AddRange(formatEncoding);

            // Media format encoding
            var mediaFormatEncoding = new float[_formatToIndex.Count];
            if (!string.IsNullOrEmpty(mediaItem.MediaFormat) && _formatToIndex.TryGetValue(mediaItem.MediaFormat.ToLower(), out int mediaFormatIndex))
            {
                mediaFormatEncoding[mediaFormatIndex] = 1.0f;
                Console.WriteLine($"[DaminionDataExtractor] Media format encoding for item {mediaItem.Id}: MediaFormat='{mediaItem.MediaFormat}' -> index {mediaFormatIndex}");
            }
            else
            {
                Console.WriteLine($"[DaminionDataExtractor] Media format encoding for item {mediaItem.Id}: MediaFormat='{mediaItem.MediaFormat}' -> not found in vocabulary");
            }
            features.AddRange(mediaFormatEncoding);

            // Category encoding (placeholder - will be populated when we get actual tag values)
            var categoryEncoding = new float[_categoryToIndex.Count];
            features.AddRange(categoryEncoding);

            // Keyword encoding (placeholder - will be populated when we get actual tag values)
            var keywordEncoding = new float[_keywordToIndex.Count];
            features.AddRange(keywordEncoding);

            // Normalize features
            return NormalizeFeatures(features);
        }

        /// <summary>
        /// Extracts labels from media item metadata
        /// </summary>
        private List<float> ExtractLabels(DaminionMediaItem mediaItem)
        {
            var totalLabels = _categoryToIndex.Count + _keywordToIndex.Count;
            var labels = new float[totalLabels];

            // For now, we'll use format type as a simple classification target
            // TODO: Implement actual tag value extraction for each media item
            if (!string.IsNullOrEmpty(mediaItem.FormatType) && _formatToIndex.TryGetValue(mediaItem.FormatType.ToLower(), out int formatIndex))
            {
                // Map format index to a position in the combined label space
                var mappedIndex = formatIndex % totalLabels; // Simple mapping to avoid out of bounds
                if (mappedIndex < labels.Length)
                {
                    labels[mappedIndex] = 1.0f;
                }
            }

            return labels.ToList();
        }

        /// <summary>
        /// Normalizes features to have zero mean and unit variance
        /// </summary>
        private List<float> NormalizeFeatures(List<float> features)
        {
            if (features.Count == 0) return features;

            var mean = features.Average();
            var variance = features.Select(f => (f - mean) * (f - mean)).Average();
            var stdDev = (float)Math.Sqrt(variance);

            if (stdDev == 0) return features;

            return features.Select(f => (f - mean) / stdDev).ToList();
        }

        /// <summary>
        /// Calculates the feature dimension based on vocabularies
        /// </summary>
        private int CalculateFeatureDimension()
        {
            // Basic features: width, height, fileSize, colorLabel, versionControlState
            int basicFeatures = 5;
            
            // Format encodings
            int formatEncodings = _formatToIndex.Count * 2; // formatType + mediaFormat
            
            // Category encodings
            int categoryEncodings = _categoryToIndex.Count;
            
            // Keyword encodings
            int keywordEncodings = _keywordToIndex.Count;
            
            return basicFeatures + formatEncodings + categoryEncodings + keywordEncodings;
        }

        /// <summary>
        /// Gets the tag vocabulary
        /// </summary>
        public Dictionary<string, int> GetTagVocabulary() => new(_tagToIndex);

        /// <summary>
        /// Gets the category vocabulary
        /// </summary>
        public Dictionary<string, int> GetCategoryVocabulary() => new(_categoryToIndex);

        /// <summary>
        /// Gets the keyword vocabulary
        /// </summary>
        public Dictionary<string, int> GetKeywordVocabulary() => new(_keywordToIndex);

        /// <summary>
        /// Gets the format vocabulary
        /// </summary>
        public Dictionary<string, int> GetFormatVocabulary() => new(_formatToIndex);
    }
}
