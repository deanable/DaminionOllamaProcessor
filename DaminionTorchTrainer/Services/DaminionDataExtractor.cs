using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Daminion;
using DaminionTorchTrainer.Models;
using Serilog;
using System.IO; // Added for Path.GetFileNameWithoutExtension
using System.Drawing; // Added for Image and Bitmap

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for extracting training data from Daminion metadata
    /// </summary>
    public class DaminionDataExtractor
    {
        private readonly DaminionApiClient _daminionClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaminionDataExtractor"/> class.
        /// </summary>
        /// <param name="daminionClient">The Daminion API client</param>
        public DaminionDataExtractor(DaminionApiClient daminionClient)
        {
            _daminionClient = daminionClient ?? throw new ArgumentNullException(nameof(daminionClient));
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
            
            // Based on Daminion web interface analysis, text search uses:
            // - ID 5000 for "Everywhere" search (includes Filename, Folder, Title, Description, etc.)
            // - Format: "5000,{searchTerm}" 
            // - Operators: "5000,all" (match all terms)
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
            
            // Log first few items to verify they're different
            var sampleItems = mediaItems.Take(5).ToList();
            Log.Information("Sample items for query '{SearchQuery}':", searchQuery);
            for (int i = 0; i < sampleItems.Count; i++)
            {
                var item = sampleItems[i];
                Log.Information("  [{Index}] ID: {Id}, FileName: '{FileName}'", i, item.Id, item.FileName);
            }

            // Step 2: Extract metadata and build vocabulary
            progressCallback?.Invoke(0, mediaItems.Count, "Extracting metadata and building vocabulary...");
            var metadataVocabulary = await BuildMetadataVocabularyAsync(mediaItems);
            
            Log.Information("Built metadata vocabulary with {VocabularySize} unique terms", metadataVocabulary.Count);

            // Step 3: Convert media items to training samples with actual image data
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

            // Step 4: Create dataset
            var featureDimension = CalculateFeatureDimension();
            var labelDimension = metadataVocabulary.Count;

            Log.Information("===== EXTRACTION SUMMARY =====");
            Log.Information("Samples: {SampleCount}", samples.Count);
            Log.Information("Features: {FeatureDimension} (visual features from image pixels)", featureDimension);
            Log.Information("Labels: {LabelDimension} (metadata terms: {MetadataTerms})", labelDimension, string.Join(", ", metadataVocabulary.Keys));

            return new TrainingDataset
            {
                Samples = samples,
                FeatureDimension = featureDimension,
                LabelDimension = labelDimension,
                MetadataVocabulary = metadataVocabulary // Store the vocabulary for logging
            };
        }

        /// <summary>
        /// Extracts training data from Daminion catalog using a specific collection
        /// </summary>
        /// <param name="collectionId">ID of the collection to extract from</param>
        /// <param name="maxItems">Maximum number of items to extract</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <returns>A training dataset</returns>
        public async Task<TrainingDataset> ExtractTrainingDataFromCollectionAsync(
            long collectionId,
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Log.Information("===== COLLECTION-BASED VISUAL CONTENT TRAINING DATA EXTRACTION =====");
            Log.Information("Collection ID: {CollectionId}, MaxItems: {MaxItems}", collectionId, maxItems);

            // Step 1: Search for media items in the collection
            progressCallback?.Invoke(0, maxItems, "Searching for media items in collection...");
            
            // Based on Daminion web interface analysis, the query format is:
            // queryLine=12,{keywordId}&operators=12,all
            // Where 12 is the search type ID for keywords, and collectionId is the keyword ID
            var queryLine = $"12,{collectionId}";
            var operators = "12,all"; // "all" means all items matching the keyword
            
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
            
            // Log first few items to verify they're different
            var sampleItems = mediaItems.Take(5).ToList();
            Log.Information("Sample items in collection {CollectionId}:", collectionId);
            for (int i = 0; i < sampleItems.Count; i++)
            {
                var item = sampleItems[i];
                Log.Information("  [{Index}] ID: {Id}, FileName: '{FileName}'", i, item.Id, item.FileName);
            }

            // Step 2: Extract metadata and build vocabulary (same as search-based method)
            progressCallback?.Invoke(0, mediaItems.Count, "Extracting metadata and building vocabulary...");
            var metadataVocabulary = await BuildMetadataVocabularyAsync(mediaItems);
            
            Log.Information("Built metadata vocabulary with {VocabularySize} unique terms", metadataVocabulary.Count);

            // Step 3: Convert media items to training samples with actual image data
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

            // Step 4: Create dataset
            var featureDimension = CalculateFeatureDimension();
            var labelDimension = metadataVocabulary.Count;

            Log.Information("===== COLLECTION EXTRACTION SUMMARY =====");
            Log.Information("Collection ID: {CollectionId}", collectionId);
            Log.Information("Samples: {SampleCount}", samples.Count);
            Log.Information("Features: {FeatureDimension} (visual features from image pixels)", featureDimension);
            Log.Information("Labels: {LabelDimension} (metadata terms: {MetadataTerms})", labelDimension, string.Join(", ", metadataVocabulary.Keys));

            return new TrainingDataset
            {
                Samples = samples,
                FeatureDimension = featureDimension,
                LabelDimension = labelDimension,
                MetadataVocabulary = metadataVocabulary // Store the vocabulary for logging
            };
        }

        /// <summary>
        /// Builds content vocabulary from the search results by extracting unique keywords/tags
        /// </summary>
        private async Task<Dictionary<string, int>> BuildContentVocabularyAsync(List<DaminionMediaItem> mediaItems)
        {
            var contentVocabulary = new Dictionary<string, int>();
            var uniqueTerms = new HashSet<string>();
            
            Log.Information("Building content vocabulary from {MediaItemsCount} media items", mediaItems.Count);
            
            // Extract content terms from file names and any available metadata
            foreach (var mediaItem in mediaItems)
            {
                // Extract terms from filename (remove extension and split by common separators)
                if (!string.IsNullOrEmpty(mediaItem.FileName))
                {
                    var fileName = Path.GetFileNameWithoutExtension(mediaItem.FileName);
                    var terms = ExtractTermsFromText(fileName);
                    foreach (var term in terms)
                    {
                        uniqueTerms.Add(term);
                    }
                }
                
                // Extract terms from format type
                if (!string.IsNullOrEmpty(mediaItem.FormatType))
                {
                    var terms = ExtractTermsFromText(mediaItem.FormatType);
                    foreach (var term in terms)
                    {
                        uniqueTerms.Add(term);
                    }
                }
                
                // Extract terms from media format
                if (!string.IsNullOrEmpty(mediaItem.MediaFormat))
                {
                    var terms = ExtractTermsFromText(mediaItem.MediaFormat);
                    foreach (var term in terms)
                    {
                        uniqueTerms.Add(term);
                    }
                }
            }
            
            // Build vocabulary with indices
            int index = 0;
            foreach (var term in uniqueTerms.OrderBy(t => t))
            {
                contentVocabulary[term] = index++;
                Log.Information("Content term {Index}: '{Term}'", index-1, term);
            }
            
            Log.Information("Built content vocabulary with {VocabularySize} unique terms", contentVocabulary.Count);
            return contentVocabulary;
        }
        
        /// <summary>
        /// Extracts meaningful terms from text by splitting on common separators and filtering
        /// </summary>
        private List<string> ExtractTermsFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();
                
            // Split on common separators: spaces, underscores, hyphens, dots, etc.
            var separators = new[] { ' ', '_', '-', '.', ',', ';', ':', '(', ')', '[', ']', '{', '}' };
            var terms = text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                           .Select(t => t.Trim().ToLowerInvariant())
                           .Where(t => t.Length > 1 && !t.All(char.IsDigit)) // Filter out single chars and pure numbers
                           .Where(t => !IsCommonStopWord(t)) // Filter out common stop words
                           .ToList();
                           
            return terms;
        }
        
        /// <summary>
        /// Checks if a term is a common stop word that should be filtered out
        /// </summary>
        private bool IsCommonStopWord(string term)
        {
            var stopWords = new HashSet<string> { 
                "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by",
                "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did",
                "will", "would", "could", "should", "may", "might", "can", "this", "that", "these", "those"
            };
            return stopWords.Contains(term);
        }

        /// <summary>
        /// Builds metadata vocabulary from EXIF/IPTC data in the media items
        /// </summary>
        private async Task<Dictionary<string, int>> BuildMetadataVocabularyAsync(List<DaminionMediaItem> mediaItems)
        {
            var metadataVocabulary = new Dictionary<string, int>();
            var uniqueTerms = new HashSet<string>();
            
            Log.Information("Building metadata vocabulary from {MediaItemsCount} media items", mediaItems.Count);
            
            // Get file paths first
            var itemIds = mediaItems.Select(item => item.Id).ToList();
            var pathsResponse = await _daminionClient.GetAbsolutePathsAsync(itemIds);
            
            if (pathsResponse?.Success != true || pathsResponse.Paths == null)
            {
                Log.Warning("Failed to get file paths, cannot extract metadata");
                return metadataVocabulary;
            }
            
            // Extract metadata from each image file
            foreach (var mediaItem in mediaItems)
            {
                if (pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out var filePath) && File.Exists(filePath))
                {
                    try
                    {
                        // Extract metadata using ImageMetadataService
                        using var metadataService = new DaminionOllamaInteractionLib.Services.ImageMetadataService(filePath);
                        metadataService.Read();
                        
                        // Add categories and keywords to vocabulary
                        foreach (var category in metadataService.Categories)
                        {
                            if (!string.IsNullOrWhiteSpace(category))
                            {
                                uniqueTerms.Add(category.ToLowerInvariant().Trim());
                            }
                        }
                        
                        foreach (var keyword in metadataService.Keywords)
                        {
                            if (!string.IsNullOrWhiteSpace(keyword))
                            {
                                uniqueTerms.Add(keyword.ToLowerInvariant().Trim());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to extract metadata from {FilePath}", filePath);
                    }
                }
            }
            
            // Build vocabulary dictionary
            var sortedTerms = uniqueTerms.OrderBy(x => x).ToList();
            for (int i = 0; i < sortedTerms.Count; i++)
            {
                metadataVocabulary[sortedTerms[i]] = i;
            }
            
            Log.Information("Metadata vocabulary built with {VocabularySize} terms: {Terms}", 
                metadataVocabulary.Count, string.Join(", ", metadataVocabulary.Keys));
            
            return metadataVocabulary;
        }

        /// <summary>
        /// Converts a media item to a training sample with actual image pixels
        /// </summary>
        private async Task<TrainingData?> ConvertToTrainingSampleAsync(DaminionMediaItem mediaItem, Dictionary<string, int> metadataVocabulary)
        {
            try
            {
                // Get file path
                var pathsResponse = await _daminionClient.GetAbsolutePathsAsync(new List<long> { mediaItem.Id });
                if (pathsResponse?.Success != true || !pathsResponse.Paths.TryGetValue(mediaItem.Id.ToString(), out var filePath) || !File.Exists(filePath))
                {
                    Log.Warning("File not found for media item {MediaItemId}: {FileName}", mediaItem.Id, mediaItem.FileName);
                    return null;
                }
                
                // Extract visual features from image pixels
                var visualFeatures = await ExtractVisualFeaturesAsync(filePath);
                if (visualFeatures == null)
                {
                    Log.Warning("Failed to extract visual features from {FilePath}", filePath);
                    return null;
                }
                
                // Extract metadata labels
                var labels = await ExtractMetadataLabelsAsync(filePath, metadataVocabulary);
                
                Log.Information("Converted {FileName} - Visual features: {FeatureCount}, Labels: {LabelCount}", 
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
        /// Extracts visual features from image pixels using a higher resolution approach
        /// </summary>
        private async Task<List<float>?> ExtractVisualFeaturesAsync(string imagePath)
        {
            try
            {
                // Use higher resolution for better accuracy: 512x512 instead of 224x224
                using var image = System.Drawing.Image.FromFile(imagePath);
                using var bitmap = new System.Drawing.Bitmap(image);
                
                // Resize to higher resolution (512x512) for better feature extraction
                using var resizedBitmap = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(512, 512));
                
                var features = new List<float>();
                
                // Extract RGB values from pixels with finer sampling (every 4th pixel instead of 8th)
                // This gives us 128x128 = 16,384 pixels, each with 3 RGB values = 49,152 features
                for (int y = 0; y < resizedBitmap.Height; y += 4) // Sample every 4th pixel for higher detail
                {
                    for (int x = 0; x < resizedBitmap.Width; x += 4)
                    {
                        var pixel = resizedBitmap.GetPixel(x, y);
                        features.Add(pixel.R / 255.0f); // Normalize to 0-1
                        features.Add(pixel.G / 255.0f);
                        features.Add(pixel.B / 255.0f);
                    }
                }
                
                Log.Information("Extracted {FeatureCount} visual features from {ImagePath} (512x512 resolution, 4-pixel sampling)", 
                    features.Count, Path.GetFileName(imagePath));
                return features;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extract visual features from {ImagePath}", imagePath);
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
                
                // Set labels for categories
                foreach (var category in metadataService.Categories)
                {
                    if (!string.IsNullOrWhiteSpace(category) && 
                        metadataVocabulary.TryGetValue(category.ToLowerInvariant().Trim(), out var index))
                    {
                        labels[index] = 1.0f;
                    }
                }
                
                // Set labels for keywords
                foreach (var keyword in metadataService.Keywords)
                {
                    if (!string.IsNullOrWhiteSpace(keyword) && 
                        metadataVocabulary.TryGetValue(keyword.ToLowerInvariant().Trim(), out var index))
                    {
                        labels[index] = 1.0f;
                    }
                }
                
                var activeLabels = labels.Count(x => x > 0);
                Log.Information("Extracted {ActiveLabels} metadata labels from {ImagePath}", activeLabels, Path.GetFileName(imagePath));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to extract metadata labels from {ImagePath}", imagePath);
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
        /// Calculates the feature dimension based on visual features
        /// </summary>
        private int CalculateFeatureDimension()
        {
            // Visual features: 512x512 image, sampled every 4th pixel, 3 RGB channels
            // (512/4) * (512/4) * 3 = 128 * 128 * 3 = 49,152 features
            return 49152;
        }
    }
}


