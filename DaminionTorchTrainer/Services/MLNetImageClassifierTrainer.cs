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

            // ML.NET's ImageClassificationTrainer works best with single-label data.
            // We will treat each tag as a separate data point.
            // This creates a multi-label scenario by duplicating images with different labels.
            foreach (var sample in dataset.Samples)
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
                logCallback("No labels found in the dataset. Cannot train a classifier.");
                throw new InvalidOperationException("No labels found in the dataset.");
            }

            IDataView trainingDataView = _mlContext.Data.LoadFromEnumerable(imageData);
            trainingDataView = _mlContext.Data.ShuffleRows(trainingDataView);

            // 2. Define the training pipeline
            logCallback("Defining the training pipeline...");
            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(inputColumnName: "Label", outputColumnName: "LabelAsKey")
                .Append(_mlContext.Transforms.LoadImages(outputColumnName: "input", imageFolder: "", inputColumnName: "ImagePath"))
                .Append(_mlContext.Transforms.ResizeImages(outputColumnName: "input", imageWidth: 224, imageHeight: 224, inputColumnName: "input"))
                .Append(_mlContext.Transforms.ExtractPixels(outputColumnName: "input"))
                .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(
                    labelColumnName: "LabelAsKey",
                    featureColumnName: "input",
                    validationSet: null)) // We can add a validation set here for better metrics
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

            // 3. Train the model
            logCallback("Training the model... This may take a while.");
            ITransformer trainedModel = pipeline.Fit(trainingDataView);
            logCallback("Model training complete.");

            // 4. Save the model
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", $"mlnet_{timestamp}");
            Directory.CreateDirectory(modelDir);
            var modelPath = Path.Combine(modelDir, "model.zip");

            _mlContext.Model.Save(trainedModel, trainingDataView.Schema, modelPath);
            logCallback($"Model saved to: {modelPath}");

            // Also save the vocabulary
            var vocabPath = Path.Combine(modelDir, "vocabulary.json");
            var vocabJson = System.Text.Json.JsonSerializer.Serialize(allLabels.ToList());
            await File.WriteAllTextAsync(vocabPath, vocabJson);
            logCallback($"Vocabulary saved to: {vocabPath}");

            return modelPath;
        }
    }
}
