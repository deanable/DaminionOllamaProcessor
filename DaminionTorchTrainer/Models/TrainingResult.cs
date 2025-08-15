using System.Text.Json.Serialization;

namespace DaminionTorchTrainer.Models
{
    /// <summary>
    /// Represents a single training result with confidence score
    /// </summary>
    public class TrainingResult
    {
        /// <summary>
        /// Gets or sets the epoch number
        /// </summary>
        [JsonPropertyName("epoch")]
        public int Epoch { get; set; }

        /// <summary>
        /// Gets or sets the training loss
        /// </summary>
        [JsonPropertyName("trainingLoss")]
        public float TrainingLoss { get; set; }

        /// <summary>
        /// Gets or sets the validation loss
        /// </summary>
        [JsonPropertyName("validationLoss")]
        public float ValidationLoss { get; set; }

        /// <summary>
        /// Gets or sets the training accuracy
        /// </summary>
        [JsonPropertyName("trainingAccuracy")]
        public float TrainingAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the validation accuracy
        /// </summary>
        [JsonPropertyName("validationAccuracy")]
        public float ValidationAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the confidence score (0.0 to 1.0)
        /// </summary>
        [JsonPropertyName("confidenceScore")]
        public float ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this result was recorded
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets additional details about this result
        /// </summary>
        [JsonPropertyName("details")]
        public string Details { get; set; } = "";

        /// <summary>
        /// Gets whether this result meets the threshold criteria
        /// </summary>
        public bool MeetsThreshold(double threshold)
        {
            return ConfidenceScore >= threshold;
        }
    }
}
