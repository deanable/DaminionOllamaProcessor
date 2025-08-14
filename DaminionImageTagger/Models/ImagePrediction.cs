using System;
using System.Collections.Generic;

namespace DaminionImageTagger.Models
{
    /// <summary>
    /// Represents a prediction result for an image
    /// </summary>
    public class ImagePrediction
    {
        /// <summary>
        /// Gets or sets the image file path
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image file name
        /// </summary>
        public string ImageName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the predicted categories with confidence scores
        /// </summary>
        public List<PredictionResult> Categories { get; set; } = new();

        /// <summary>
        /// Gets or sets the predicted keywords with confidence scores
        /// </summary>
        public List<PredictionResult> Keywords { get; set; } = new();

        /// <summary>
        /// Gets or sets the prediction timestamp
        /// </summary>
        public DateTime PredictionTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the model used for prediction
        /// </summary>
        public string ModelPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a single prediction result with confidence score
    /// </summary>
    public class PredictionResult
    {
        /// <summary>
        /// Gets or sets the predicted label
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the confidence score (0.0 to 1.0)
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Gets or sets the index in the vocabulary
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets a formatted confidence percentage
        /// </summary>
        public string ConfidencePercentage => $"{Confidence * 100:F1}%";

        /// <summary>
        /// Gets whether this prediction meets the confidence threshold
        /// </summary>
        public bool IsConfident => Confidence >= 0.5f;
    }
}
