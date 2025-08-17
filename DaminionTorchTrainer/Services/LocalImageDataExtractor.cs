using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaminionTorchTrainer.Models;
using DaminionOllamaInteractionLib.Services;

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for extracting training data from local image files
    /// </summary>
    public class LocalImageDataExtractor
    {
        private readonly ImageProcessor _imageProcessor;
        private long _nextId = 1;

        public LocalImageDataExtractor(ImageProcessor imageProcessor)
        {
            _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
        }

        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp" };

        public async Task<TrainingDataset> ExtractTrainingDataAsync(
            string folderPath,
            bool includeSubfolders = true,
            int maxItems = 1000,
            Action<int, int, string>? progressCallback = null)
        {
            Console.WriteLine($"[LocalImageDataExtractor] Starting data extraction from folder: '{folderPath}' using ImageProcessor.");

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var imageFiles = Directory.GetFiles(folderPath, "*.*", searchOption)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .Take(maxItems)
                .ToList();

            Console.WriteLine($"[LocalImageDataExtractor] Found {imageFiles.Count} image files.");

            var trainingSamples = new List<TrainingData>();
            var metadataVocabulary = await BuildMetadataVocabularyAsync(imageFiles);

            progressCallback?.Invoke(0, imageFiles.Count, "Extracting features and labels...");

            for (int i = 0; i < imageFiles.Count; i++)
            {
                var imageFile = imageFiles[i];
                try
                {
                    var features = await _imageProcessor.ExtractFeaturesAsync(imageFile);
                    if (features == null)
                    {
                        Console.WriteLine($"[LocalImageDataExtractor] Skipping file due to feature extraction failure: {imageFile}");
                        continue;
                    }

                    var labels = ExtractLabels(imageFile, metadataVocabulary);

                    trainingSamples.Add(new TrainingData
                    {
                        Id = _nextId++,
                        FileName = Path.GetFileName(imageFile),
                        FilePath = imageFile,
                        Features = features,
                        Labels = labels,
                        DataSource = DataSourceType.Local
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LocalImageDataExtractor] Error processing file {imageFile}: {ex.Message}");
                }

                progressCallback?.Invoke(i + 1, imageFiles.Count, $"Processed {i + 1}/{imageFiles.Count} images...");
            }

            var featureDimension = _imageProcessor.FeatureDimension;
            var labelDimension = metadataVocabulary.Count;

            var dataset = new TrainingDataset
            {
                Name = $"Local_Dataset_{DateTime.Now:yyyyMMdd_HHmmss}",
                Description = $"Training dataset from local folder: {folderPath}",
                Samples = trainingSamples,
                FeatureDimension = featureDimension,
                LabelDimension = labelDimension,
                MetadataVocabulary = metadataVocabulary,
                DataSource = DataSourceType.Local,
                SourcePath = folderPath
            };

            progressCallback?.Invoke(imageFiles.Count, imageFiles.Count, "Dataset creation complete.");
            Console.WriteLine($"[LocalImageDataExtractor] Created dataset with {trainingSamples.Count} samples. Feature dimension: {featureDimension}, Label dimension: {labelDimension}");

            return dataset;
        }

        private async Task<Dictionary<string, int>> BuildMetadataVocabularyAsync(List<string> imageFiles)
        {
            var uniqueTerms = new HashSet<string>();
            foreach (var imageFile in imageFiles)
            {
                try
                {
                    using var metadataService = new ImageMetadataService(imageFile);
                    metadataService.Read();
                    foreach (var category in metadataService.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        uniqueTerms.Add(category.Trim().ToLowerInvariant());
                    }
                    foreach (var keyword in metadataService.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                    {
                        uniqueTerms.Add(keyword.Trim().ToLowerInvariant());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LocalImageDataExtractor] Error reading metadata from {imageFile} for vocabulary: {ex.Message}");
                }
            }

            var vocabulary = new Dictionary<string, int>();
            int index = 0;
            foreach (var term in uniqueTerms.OrderBy(t => t))
            {
                vocabulary[term] = index++;
            }
            return vocabulary;
        }

        private List<float> ExtractLabels(string imagePath, Dictionary<string, int> vocabulary)
        {
            var labels = new float[vocabulary.Count];
            try
            {
                using var metadataService = new ImageMetadataService(imagePath);
                metadataService.Read();

                foreach (var category in metadataService.Categories.Where(c => !string.IsNullOrWhiteSpace(c)))
                {
                    if (vocabulary.TryGetValue(category.Trim().ToLowerInvariant(), out int index))
                    {
                        labels[index] = 1.0f;
                    }
                }
                foreach (var keyword in metadataService.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)))
                {
                    if (vocabulary.TryGetValue(keyword.Trim().ToLowerInvariant(), out int index))
                    {
                        labels[index] = 1.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalImageDataExtractor] Error extracting labels from {imagePath}: {ex.Message}");
            }
            return labels.ToList();
        }
    }
}
