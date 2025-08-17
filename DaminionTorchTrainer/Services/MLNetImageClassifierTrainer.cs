using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaminionTorchTrainer.Models;
using Microsoft.ML;
using Microsoft.ML.Vision;
using Serilog;

namespace DaminionTorchTrainer.Services
{
    // Helper class for ML.NET data loading
    public class ImageData
    {
        public string ImagePath { get; set; }
        public string Label { get; set; }
    }

    public class MLNetImageClassifierTrainer
    {
        private readonly MLContext _mlContext;

        public MLNetImageClassifierTrainer()
        {
            _mlContext = new MLContext(seed: 1);
        }

        public async Task<string> TrainModelAsync(TrainingDataset dataset, Action<string> logCallback)
        {
            if (dataset == null || !dataset.Samples.Any())
            {
                logCallback("Dataset is empty. Cannot train model.");
                throw new ArgumentException("Dataset is empty.");
            }

            // 1. Prepare data for ML.NET
            logCallback("Preparing data for ML.NET...");
            var imageData = new List<ImageData>();
            var allLabels = new HashSet<string>();

            foreach (var sample in dataset.Samples.Where(s => !string.IsNullOrEmpty(s.FilePath)))
            {
                var labels = sample.Labels.Select((v, i) => v > 0.5f ? dataset.MetadataVocabulary.FirstOrDefault(kvp => kvp.Value == i).Key : null)
                                      .Where(k => k != null)
                                      .ToList();

                foreach (var label in labels)
                {
                    imageData.Add(new ImageData { ImagePath = sample.FilePath, Label = label });
                    allLabels.Add(label);
                }
            }

            if (!imageData.Any())
            {
                logCallback("No valid labels found in the dataset. Cannot train a classifier.");
                throw new InvalidOperationException("No labels found in the dataset.");
            }

            IDataView trainingDataView = _mlContext.Data.LoadFromEnumerable(imageData);
            trainingDataView = _mlContext.Data.ShuffleRows(trainingDataView);

            // 2. Define the training pipeline using the Options object
            logCallback("Defining the ML.NET training pipeline...");
            var options = new ImageClassificationTrainer.Options()
            {
                LabelColumnName = "LabelAsKey",
                FeatureColumnName = "Features",
                Arch = ImageClassificationTrainer.Architecture.ResnetV250, // Specify architecture here
                Epoch = 50, // Example: specify other parameters
                BatchSize = 10,
                LearningRate = 0.01f,
                ValidationSet = null // No validation set for simplicity, but can be added
            };

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "LabelAsKey", inputColumnName: "Label")
                .Append(_mlContext.Transforms.LoadRawImageBytes(outputColumnName: "Image", imageFolder: null, inputColumnName: "ImagePath"))
                .Append(_mlContext.Transforms.CopyColumns(outputColumnName: "Features", inputColumnName: "Image"))
                .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(options))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // 3. Train the model
            logCallback("Training the ML.NET model... This may take a while.");
            ITransformer trainedModel = pipeline.Fit(trainingDataView);
            logCallback("Model training complete.");

            // 4. Save the model
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", $"mlnet_{timestamp}");
            Directory.CreateDirectory(modelDir);
            var modelPath = Path.Combine(modelDir, "model.zip");

            _mlContext.Model.Save(trainedModel, trainingDataView.Schema, modelPath);
            logCallback($"Model saved to: {modelPath}");

            var vocabPath = Path.Combine(modelDir, "vocabulary.json");
            var vocabJson = System.Text.Json.JsonSerializer.Serialize(allLabels.ToList());
            await File.WriteAllTextAsync(vocabPath, vocabJson);
            logCallback($"Vocabulary saved to: {vocabPath}");

            return modelPath;
        }
    }
}
