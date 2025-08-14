using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaminionTorchTrainer.Models;
using DaminionOllamaInteractionLib.Services;
using System.Text.RegularExpressions;

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for extracting training data from local image files
    /// </summary>
    public class LocalImageDataExtractor
    {
        private readonly Dictionary<string, int> _categoryToIndex = new();
        private readonly Dictionary<string, int> _keywordToIndex = new();
        private readonly Dictionary<string, int> _formatToIndex = new();
        private long _nextId = 1;

        /// <summary>
        /// Supported image file extensions
        /// </summary>
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp" };

        /// <summary>
        /// Extracts training data from local image files
        /// </summary>
        /// <param name="folderPath">Path to the folder containing images</param>
        /// <param name="includeSubfolders">Whether to include subfolders</param>
        /// <param name="maxItems">Maximum number of items to extract</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <returns>A training dataset</returns>
        public async Task<TrainingDataset> ExtractTrainingDataAsync(
            string folderPath,
            bool includeSubfolders = true,
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Console.WriteLine($"[LocalImageDataExtractor] Starting data extraction from folder: '{folderPath}'");

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            // Get all image files
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var imageFiles = Directory.GetFiles(folderPath, "*.*", searchOption)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .Take(maxItems)
                .ToList();

            Console.WriteLine($"[LocalImageDataExtractor] Found {imageFiles.Count} image files");

            // First pass: build vocabularies from all images
            progressCallback?.Invoke(0, 100, "Building vocabularies from image metadata...");
            await BuildVocabulariesAsync(imageFiles);
            progressCallback?.Invoke(20, 100, "Vocabularies built successfully");

            // Second pass: extract training data
            var trainingSamples = new List<TrainingData>();

            progressCallback?.Invoke(25, 100, "Converting images to training data...");
            
            for (int i = 0; i < imageFiles.Count; i++)
            {
                var imageFile = imageFiles[i];
                var trainingData = await ConvertToTrainingDataAsync(imageFile);
                if (trainingData != null)
                {
                    trainingSamples.Add(trainingData);
                }
                
                // Report progress every 10 items or at the end
                if (i % 10 == 0 || i == imageFiles.Count - 1)
                {
                    var progress = 25 + (int)((i + 1) * 70.0 / imageFiles.Count);
                    progressCallback?.Invoke(progress, 100, $"Processed {i + 1}/{imageFiles.Count} images...");
                }
            }

            // Calculate dimensions based on vocabularies
            var featureDimension = CalculateFeatureDimension();
            var labelDimension = _categoryToIndex.Count + _keywordToIndex.Count; // Use categories + keywords for multi-label

            var dataset = new TrainingDataset
            {
                Name = $"Local_Dataset_{DateTime.Now:yyyyMMdd_HHmmss}",
                Description = $"Training dataset extracted from local folder: {folderPath}",
                Samples = trainingSamples,
                FeatureDimension = featureDimension,
                LabelDimension = labelDimension,
                DataSource = DataSourceType.Local,
                SourcePath = folderPath
            };

            progressCallback?.Invoke(100, 100, "Dataset creation completed");
            
            Console.WriteLine($"[LocalImageDataExtractor] Created dataset with {trainingSamples.Count} samples, " +
                            $"feature dimension: {featureDimension}, label dimension: {labelDimension}");

            return dataset;
        }

        /// <summary>
        /// Builds vocabularies from all images in the collection
        /// </summary>
        private Task BuildVocabulariesAsync(List<string> imageFiles)
        {
            var allCategories = new HashSet<string>();
            var allKeywords = new HashSet<string>();
            var allFormats = new HashSet<string>();

            foreach (var imageFile in imageFiles)
            {
                try
                {
                    using var metadataService = new ImageMetadataService(imageFile);
                    metadataService.Read();

                    // Collect categories
                    foreach (var category in metadataService.Categories)
                    {
                        if (!string.IsNullOrWhiteSpace(category))
                        {
                            allCategories.Add(category.Trim());
                        }
                    }

                    // Collect keywords
                    foreach (var keyword in metadataService.Keywords)
                    {
                        if (!string.IsNullOrWhiteSpace(keyword))
                        {
                            allKeywords.Add(keyword.Trim());
                        }
                    }

                    // Collect format information
                    var extension = Path.GetExtension(imageFile).ToLowerInvariant();
                    allFormats.Add(extension);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LocalImageDataExtractor] Error reading metadata from {imageFile}: {ex.Message}");
                }
            }

            // Build category vocabulary
            int categoryIndex = 0;
            foreach (var category in allCategories.OrderBy(c => c))
            {
                _categoryToIndex[category] = categoryIndex++;
            }

            // Build keyword vocabulary
            int keywordIndex = 0;
            foreach (var keyword in allKeywords.OrderBy(k => k))
            {
                _keywordToIndex[keyword] = keywordIndex++;
            }

            // Build format vocabulary
            int formatIndex = 0;
            foreach (var format in allFormats.OrderBy(f => f))
            {
                _formatToIndex[format] = formatIndex++;
            }

            Console.WriteLine($"[LocalImageDataExtractor] Built vocabularies: {_categoryToIndex.Count} categories, " +
                            $"{_keywordToIndex.Count} keywords, {_formatToIndex.Count} formats");
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Converts a local image file to training data
        /// </summary>
        private Task<TrainingData?> ConvertToTrainingDataAsync(string imageFile)
        {
            try
            {
                var fileInfo = new FileInfo(imageFile);
                
                var trainingData = new TrainingData
                {
                    Id = _nextId++,
                    FileName = Path.GetFileName(imageFile),
                    FilePath = imageFile,
                    FileSize = fileInfo.Length,
                    FormatType = Path.GetExtension(imageFile).ToLowerInvariant(),
                    MediaFormat = "image",
                    DataSource = DataSourceType.Local
                };

                // Extract metadata using ImageMetadataService
                using var metadataService = new ImageMetadataService(imageFile);
                metadataService.Read();

                trainingData.Description = metadataService.Description;
                trainingData.Categories = metadataService.Categories.ToList();
                trainingData.Keywords = metadataService.Keywords.ToList();

                // Try to get image dimensions
                try
                {
                    using var image = System.Drawing.Image.FromFile(imageFile);
                    trainingData.Width = image.Width;
                    trainingData.Height = image.Height;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LocalImageDataExtractor] Could not get dimensions for {imageFile}: {ex.Message}");
                }

                // Extract features from metadata
                trainingData.Features = ExtractFeatures(trainingData);

                // Extract labels from metadata
                trainingData.Labels = ExtractLabels(trainingData);

                return Task.FromResult<TrainingData?>(trainingData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalImageDataExtractor] Error converting image {imageFile}: {ex.Message}");
                return Task.FromResult<TrainingData?>(null);
            }
        }

        /// <summary>
        /// Extracts numerical features from image metadata
        /// </summary>
        private List<float> ExtractFeatures(TrainingData trainingData)
        {
            var features = new List<float>();

            // Basic numerical features
            features.Add(trainingData.Width ?? 0);
            features.Add(trainingData.Height ?? 0);
            features.Add(trainingData.FileSize ?? 0);

            // Format type encoding
            var formatEncoding = new float[_formatToIndex.Count];
            if (!string.IsNullOrEmpty(trainingData.FormatType) && _formatToIndex.TryGetValue(trainingData.FormatType, out int formatIndex))
            {
                formatEncoding[formatIndex] = 1.0f;
            }
            features.AddRange(formatEncoding);

            // Category encoding (one-hot encoding)
            var categoryEncoding = new float[_categoryToIndex.Count];
            foreach (var category in trainingData.Categories)
            {
                if (_categoryToIndex.TryGetValue(category, out int categoryIndex))
                {
                    categoryEncoding[categoryIndex] = 1.0f;
                }
            }
            features.AddRange(categoryEncoding);

            // Keyword encoding (one-hot encoding)
            var keywordEncoding = new float[_keywordToIndex.Count];
            foreach (var keyword in trainingData.Keywords)
            {
                if (_keywordToIndex.TryGetValue(keyword, out int keywordIndex))
                {
                    keywordEncoding[keywordIndex] = 1.0f;
                }
            }
            features.AddRange(keywordEncoding);

            // Description features (simple text features)
            var descriptionFeatures = ExtractTextFeatures(trainingData.Description);
            features.AddRange(descriptionFeatures);

            // Normalize features
            return NormalizeFeatures(features);
        }

        /// <summary>
        /// Extracts labels from image metadata
        /// </summary>
        private List<float> ExtractLabels(TrainingData trainingData)
        {
            var totalLabels = _categoryToIndex.Count + _keywordToIndex.Count;
            var labels = new float[totalLabels];

            // Use categories as primary labels (first part of the label vector)
            foreach (var category in trainingData.Categories)
            {
                if (_categoryToIndex.TryGetValue(category, out int categoryIndex))
                {
                    if (categoryIndex < _categoryToIndex.Count)
                    {
                        labels[categoryIndex] = 1.0f;
                    }
                }
            }

            // Use keywords as secondary labels (second part of the label vector)
            foreach (var keyword in trainingData.Keywords)
            {
                if (_keywordToIndex.TryGetValue(keyword, out int keywordIndex))
                {
                    var keywordLabelIndex = _categoryToIndex.Count + keywordIndex; // Offset by category count
                    if (keywordLabelIndex < totalLabels)
                    {
                        labels[keywordLabelIndex] = 1.0f;
                    }
                }
            }

            return labels.ToList();
        }

        /// <summary>
        /// Extracts simple text features from description
        /// </summary>
        private List<float> ExtractTextFeatures(string? description)
        {
            var features = new List<float>();

            if (string.IsNullOrWhiteSpace(description))
            {
                // Return zeros for text features
                features.AddRange(new float[10]); // 10 text features
                return features;
            }

            var text = description.ToLowerInvariant();

            // Simple text features
            features.Add(text.Length); // Text length
            features.Add(text.Split(' ').Length); // Word count
            features.Add(text.Count(char.IsUpper)); // Uppercase count
            features.Add(text.Count(char.IsDigit)); // Digit count
            features.Add(text.Count(c => c == '.')); // Sentence count (approximate)
            features.Add(text.Count(c => c == ',')); // Comma count
            features.Add(text.Count(c => c == '!')); // Exclamation count
            features.Add(text.Count(c => c == '?')); // Question count
            features.Add(text.Count(c => c == '#')); // Hash count
            features.Add(text.Count(c => c == '@')); // At symbol count

            return features;
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
            // Basic features: width, height, fileSize
            int basicFeatures = 3;
            
            // Format encodings
            int formatEncodings = _formatToIndex.Count;
            
            // Category encodings
            int categoryEncodings = _categoryToIndex.Count;
            
            // Keyword encodings
            int keywordEncodings = _keywordToIndex.Count;
            
            // Text features
            int textFeatures = 10;
            
            return basicFeatures + formatEncodings + categoryEncodings + keywordEncodings + textFeatures;
        }

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
