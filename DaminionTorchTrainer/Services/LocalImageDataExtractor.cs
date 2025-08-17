using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaminionTorchTrainer.Models;
using DaminionOllamaInteractionLib.Services;
using Serilog;

namespace DaminionTorchTrainer.Services
{
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
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            var imageFiles = Directory.GetFiles(folderPath, "*.*", includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .Take(maxItems)
                .ToList();

            var metadataVocabulary = await BuildMetadataVocabularyAsync(imageFiles);
            var samples = new List<TrainingData>();

            for (int i = 0; i < imageFiles.Count; i++)
            {
                var imageFile = imageFiles[i];
                progressCallback?.Invoke(i + 1, imageFiles.Count, $"Processing {Path.GetFileName(imageFile)}...");

                var features = await _imageProcessor.ExtractFeaturesAsync(imageFile);
                if (features == null) continue;

                var labels = ExtractLabels(imageFile, metadataVocabulary);

                samples.Add(new TrainingData
                {
                    Id = _nextId++,
                    FileName = Path.GetFileName(imageFile),
                    FilePath = imageFile,
                    Features = features,
                    Labels = labels,
                });
            }

            return new TrainingDataset
            {
                Samples = samples,
                FeatureDimension = _imageProcessor.FeatureDimension,
                LabelDimension = metadataVocabulary.Count,
                MetadataVocabulary = metadataVocabulary,
            };
        }

        private Task<Dictionary<string, int>> BuildMetadataVocabularyAsync(List<string> imageFiles)
        {
            var uniqueTerms = new HashSet<string>();
            foreach (var imageFile in imageFiles)
            {
                try
                {
                    using var metadataService = new ImageMetadataService(imageFile);
                    metadataService.Read();
                    foreach (var term in metadataService.Categories.Concat(metadataService.Keywords))
                    {
                        if (!string.IsNullOrWhiteSpace(term))
                        {
                            uniqueTerms.Add(term.Trim().ToLowerInvariant());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not read metadata for {File}", imageFile);
                }
            }
            return Task.FromResult(uniqueTerms.OrderBy(t => t).Select((term, index) => new { term, index }).ToDictionary(p => p.term, p => p.index));
        }

        private List<float> ExtractLabels(string imagePath, Dictionary<string, int> vocabulary)
        {
            var labels = new float[vocabulary.Count];
            try
            {
                using var metadataService = new ImageMetadataService(imagePath);
                metadataService.Read();

                foreach (var term in metadataService.Categories.Concat(metadataService.Keywords))
                {
                    if (!string.IsNullOrWhiteSpace(term) && vocabulary.TryGetValue(term.Trim().ToLowerInvariant(), out int index))
                    {
                        labels[index] = 1.0f;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not extract labels from {File}", imagePath);
            }
            return labels.ToList();
        }
    }
}
