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
            if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

            var imageFiles = Directory.GetFiles(folderPath, "*.*", includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .Take(maxItems)
                .ToList();

            var vocab = await BuildMetadataVocabularyAsync(imageFiles);
            var samples = new List<TrainingData>();

            for (int i = 0; i < imageFiles.Count; i++)
            {
                var file = imageFiles[i];
                progressCallback?.Invoke(i + 1, imageFiles.Count, $"Processing {Path.GetFileName(file)}...");

                var features = await _imageProcessor.ExtractFeaturesAsync(file);
                if (features == null) continue;

                samples.Add(new TrainingData {
                    Id = _nextId++,
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Features = features,
                    Labels = ExtractLabels(file, vocab),
                });
            }

            return new TrainingDataset {
                Samples = samples,
                FeatureDimension = _imageProcessor.FeatureDimension,
                LabelDimension = vocab.Count,
                MetadataVocabulary = vocab,
            };
        }

        private async Task<Dictionary<string, int>> BuildMetadataVocabularyAsync(List<string> imageFiles)
        {
            var uniqueTerms = new HashSet<string>();
            foreach (var file in imageFiles)
            {
                try
                {
                    using var metadata = new ImageMetadataService(file);
                    metadata.Read();
                    foreach (var term in metadata.Categories.Concat(metadata.Keywords).Where(t => !string.IsNullOrWhiteSpace(t)))
                    {
                        uniqueTerms.Add(term.Trim().ToLowerInvariant());
                    }
                }
                catch (Exception ex) { Log.Warning(ex, "Could not read metadata for {File}", file); }
            }
            return await Task.FromResult(uniqueTerms.OrderBy(t => t).Select((t, i) => new { t, i }).ToDictionary(p => p.t, p => p.i));
        }

        private List<float> ExtractLabels(string imagePath, Dictionary<string, int> vocab)
        {
            var labels = new float[vocab.Count];
            try
            {
                using var metadata = new ImageMetadataService(imagePath);
                metadata.Read();
                foreach (var term in metadata.Categories.Concat(metadata.Keywords).Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    if (vocab.TryGetValue(term.Trim().ToLowerInvariant(), out int index)) labels[index] = 1.0f;
                }
            }
            catch (Exception ex) { Log.Warning(ex, "Could not extract labels from {File}", imagePath); }
            return labels.ToList();
        }
    }
}
