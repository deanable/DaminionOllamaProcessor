using System.Collections.Generic;

namespace DaminionTorchTrainer.Models
{
    /// <summary>
    /// Represents the results of a completed training session.
    /// </summary>
    public class TrainingResults
    {
        /// <summary>
        /// The path to the saved model file.
        /// </summary>
        public string ModelPath { get; set; } = string.Empty;

        /// <summary>
        /// A history of progress snapshots taken during training.
        /// </summary>
        public List<TrainingProgress> TrainingHistory { get; set; } = new();

        /// <summary>
        /// The final training loss value.
        /// </summary>
        public float FinalTrainingLoss { get; set; }

        /// <summary>
        /// The final validation loss value.
        /// </summary>
        public float FinalValidationLoss { get; set; }

        /// <summary>
        /// The final training accuracy value.
        /// </summary>
        public float FinalTrainingAccuracy { get; set; }

        /// <summary>
        /// The final validation accuracy value.
        /// </summary>
        public float FinalValidationAccuracy { get; set; }

        /// <summary>
        /// The method or framework used for training (e.g., "TorchSharp", "ML.NET").
        /// </summary>
        public string TrainingMethod { get; set; } = string.Empty;

        /// <summary>
        /// The specific algorithm or architecture used.
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;
    }
}
