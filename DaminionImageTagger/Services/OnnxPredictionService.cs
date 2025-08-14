using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using DaminionImageTagger.Models;
using System.Text.Json;

namespace DaminionImageTagger.Services
{
    /// <summary>
    /// Service for making predictions using ONNX models
    /// </summary>
    public class OnnxPredictionService : IDisposable
    {
        private InferenceSession? _session;
        private Dictionary<string, int> _categoryVocabulary = new();
        private Dictionary<string, int> _keywordVocabulary = new();
        private Dictionary<string, int> _formatVocabulary = new();
        private string _modelPath = string.Empty;
        private bool _isLoaded = false;

        /// <summary>
        /// Gets whether the model is loaded and ready for prediction
        /// </summary>
        public bool IsLoaded => _isLoaded && _session != null;

        /// <summary>
        /// Gets the number of categories in the vocabulary
        /// </summary>
        public int CategoryCount => _categoryVocabulary.Count;

        /// <summary>
        /// Gets the number of keywords in the vocabulary
        /// </summary>
        public int KeywordCount => _keywordVocabulary.Count;

        /// <summary>
        /// Loads an ONNX model and its associated metadata
        /// </summary>
        /// <param name="modelPath">Path to the ONNX model file</param>
        /// <returns>True if loaded successfully</returns>
        public bool LoadModel(string modelPath)
        {
            try
            {
                if (!File.Exists(modelPath))
                {
                    Console.WriteLine($"[OnnxPredictionService] Model file not found: {modelPath}");
                    return false;
                }

                // Load the ONNX model
                _session = new InferenceSession(modelPath);
                _modelPath = modelPath;

                // Try to load vocabularies from metadata file
                var metadataPath = Path.ChangeExtension(modelPath, ".json");
                if (File.Exists(metadataPath))
                {
                    LoadVocabulariesFromMetadata(metadataPath);
                }
                else
                {
                    // Try to load from training config
                    var configPath = Path.Combine(Path.GetDirectoryName(modelPath)!, "training_config.json");
                    if (File.Exists(configPath))
                    {
                        LoadVocabulariesFromConfig(configPath);
                    }
                }

                _isLoaded = true;
                Console.WriteLine($"[OnnxPredictionService] Model loaded successfully: {modelPath}");
                Console.WriteLine($"[OnnxPredictionService] Categories: {_categoryVocabulary.Count}, Keywords: {_keywordVocabulary.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnnxPredictionService] Error loading model: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Makes predictions on an image
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Prediction results</returns>
        public ImagePrediction PredictImage(string imagePath)
        {
            if (!IsLoaded)
            {
                throw new InvalidOperationException("Model not loaded. Call LoadModel() first.");
            }

            try
            {
                Console.WriteLine($"[OnnxPredictionService] Predicting image: {imagePath}");

                // Extract features from the image
                var features = ExtractImageFeatures(imagePath);

                // Create input tensor
                var inputTensor = new DenseTensor<float>(features.ToArray(), new int[] { 1, features.Count });
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", inputTensor)
                };

                // Run inference
                using var results = _session!.Run(inputs);
                var output = results.First().Value as DenseTensor<float>;

                if (output == null)
                {
                    throw new InvalidOperationException("Model output is null");
                }

                // Parse predictions
                var prediction = ParsePredictions(output, imagePath);
                Console.WriteLine($"[OnnxPredictionService] Prediction completed for {imagePath}");
                
                return prediction;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnnxPredictionService] Error predicting image {imagePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Extracts features from an image file
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Feature vector</returns>
        private List<float> ExtractImageFeatures(string imagePath)
        {
            var features = new List<float>();

            try
            {
                using var image = Image.FromFile(imagePath);
                var fileInfo = new FileInfo(imagePath);

                // Basic numerical features (same as training)
                features.Add(image.Width);
                features.Add(image.Height);
                features.Add(fileInfo.Length);
                features.Add(0); // ColorLabel (not available for prediction)
                features.Add(0); // VersionControlState (not available for prediction)

                // Format type encoding
                var formatType = Path.GetExtension(imagePath).ToLowerInvariant();
                var formatEncoding = new float[_formatVocabulary.Count];
                if (_formatVocabulary.TryGetValue(formatType, out int formatIndex))
                {
                    formatEncoding[formatIndex] = 1.0f;
                }
                features.AddRange(formatEncoding);

                // Media format encoding
                var mediaFormatEncoding = new float[_formatVocabulary.Count];
                if (_formatVocabulary.TryGetValue("image", out int mediaFormatIndex))
                {
                    mediaFormatEncoding[mediaFormatIndex] = 1.0f;
                }
                features.AddRange(mediaFormatEncoding);

                // Category encoding (zeros for prediction - we're predicting these)
                var categoryEncoding = new float[_categoryVocabulary.Count];
                features.AddRange(categoryEncoding);

                // Keyword encoding (zeros for prediction - we're predicting these)
                var keywordEncoding = new float[_keywordVocabulary.Count];
                features.AddRange(keywordEncoding);

                // Normalize features (same normalization as training)
                return NormalizeFeatures(features);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnnxPredictionService] Error extracting features from {imagePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Parses model output into prediction results
        /// </summary>
        /// <param name="output">Model output tensor</param>
        /// <param name="imagePath">Image file path</param>
        /// <returns>Parsed prediction results</returns>
        private ImagePrediction ParsePredictions(DenseTensor<float> output, string imagePath)
        {
            var prediction = new ImagePrediction
            {
                ImagePath = imagePath,
                ImageName = Path.GetFileName(imagePath),
                ModelPath = _modelPath
            };

            var outputArray = output.ToArray();
            var totalLabels = _categoryVocabulary.Count + _keywordVocabulary.Count;

            if (outputArray.Length != totalLabels)
            {
                Console.WriteLine($"[OnnxPredictionService] Warning: Output dimension mismatch. Expected {totalLabels}, got {outputArray.Length}");
            }

            // Parse categories (first part of output)
            for (int i = 0; i < _categoryVocabulary.Count && i < outputArray.Length; i++)
            {
                var confidence = outputArray[i];
                if (confidence > 0.1f) // Only include predictions with >10% confidence
                {
                    var categoryName = _categoryVocabulary.FirstOrDefault(kvp => kvp.Value == i).Key;
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        prediction.Categories.Add(new PredictionResult
                        {
                            Label = categoryName,
                            Confidence = confidence,
                            Index = i
                        });
                    }
                }
            }

            // Parse keywords (second part of output)
            var keywordStartIndex = _categoryVocabulary.Count;
            for (int i = 0; i < _keywordVocabulary.Count && (keywordStartIndex + i) < outputArray.Length; i++)
            {
                var confidence = outputArray[keywordStartIndex + i];
                if (confidence > 0.1f) // Only include predictions with >10% confidence
                {
                    var keywordName = _keywordVocabulary.FirstOrDefault(kvp => kvp.Value == i).Key;
                    if (!string.IsNullOrEmpty(keywordName))
                    {
                        prediction.Keywords.Add(new PredictionResult
                        {
                            Label = keywordName,
                            Confidence = confidence,
                            Index = i
                        });
                    }
                }
            }

            // Sort by confidence (highest first)
            prediction.Categories = prediction.Categories.OrderByDescending(c => c.Confidence).ToList();
            prediction.Keywords = prediction.Keywords.OrderByDescending(k => k.Confidence).ToList();

            return prediction;
        }

        /// <summary>
        /// Loads vocabularies from metadata file
        /// </summary>
        /// <param name="metadataPath">Path to metadata file</param>
        private void LoadVocabulariesFromMetadata(string metadataPath)
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                
                // This is a simplified approach - in a real implementation, you'd need to
                // store and load the actual vocabularies from the training process
                Console.WriteLine($"[OnnxPredictionService] Loaded metadata from: {metadataPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnnxPredictionService] Error loading metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads vocabularies from training config
        /// </summary>
        /// <param name="configPath">Path to training config file</param>
        private void LoadVocabulariesFromConfig(string configPath)
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                
                // This is a simplified approach - in a real implementation, you'd need to
                // store and load the actual vocabularies from the training process
                Console.WriteLine($"[OnnxPredictionService] Loaded config from: {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnnxPredictionService] Error loading config: {ex.Message}");
            }
        }

        /// <summary>
        /// Normalizes features to have zero mean and unit variance
        /// </summary>
        /// <param name="features">Feature vector</param>
        /// <returns>Normalized features</returns>
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
        /// Sets the vocabularies manually (for testing or when metadata is not available)
        /// </summary>
        /// <param name="categories">Category vocabulary</param>
        /// <param name="keywords">Keyword vocabulary</param>
        /// <param name="formats">Format vocabulary</param>
        public void SetVocabularies(Dictionary<string, int> categories, Dictionary<string, int> keywords, Dictionary<string, int> formats)
        {
            _categoryVocabulary = new Dictionary<string, int>(categories);
            _keywordVocabulary = new Dictionary<string, int>(keywords);
            _formatVocabulary = new Dictionary<string, int>(formats);
            
            Console.WriteLine($"[OnnxPredictionService] Vocabularies set: {categories.Count} categories, {keywords.Count} keywords, {formats.Count} formats");
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
