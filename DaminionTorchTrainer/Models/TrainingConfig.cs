using System.Text.Json.Serialization;

namespace DaminionTorchTrainer.Models
{
    /// <summary>
    /// Represents configuration settings for model training
    /// </summary>
    public class TrainingConfig
    {
        /// <summary>
        /// Gets or sets the learning rate for training
        /// </summary>
        [JsonPropertyName("learningRate")]
        public float LearningRate { get; set; } = 0.001f;

        /// <summary>
        /// Gets or sets the number of training epochs
        /// </summary>
        [JsonPropertyName("epochs")]
        public int Epochs { get; set; } = 100;

        /// <summary>
        /// Gets or sets the batch size for training
        /// </summary>
        [JsonPropertyName("batchSize")]
        public int BatchSize { get; set; } = 32;

        /// <summary>
        /// Gets or sets the validation split percentage (0.0 to 1.0)
        /// </summary>
        [JsonPropertyName("validationSplit")]
        public float ValidationSplit { get; set; } = 0.2f;

        /// <summary>
        /// Gets or sets the feature dimension for the model
        /// </summary>
        [JsonPropertyName("featureDimension")]
        public int FeatureDimension { get; set; } = 128;

        /// <summary>
        /// Gets or sets the hidden layer dimensions
        /// </summary>
        [JsonPropertyName("hiddenDimensions")]
        public int[] HiddenDimensions { get; set; } = { 256, 128, 64 };

        /// <summary>
        /// Gets or sets the output dimension for the model
        /// </summary>
        [JsonPropertyName("outputDimension")]
        public int OutputDimension { get; set; } = 10;

        /// <summary>
        /// Gets or sets the dropout rate for regularization
        /// </summary>
        [JsonPropertyName("dropoutRate")]
        public float DropoutRate { get; set; } = 0.2f;

        /// <summary>
        /// Gets or sets the weight decay for L2 regularization
        /// </summary>
        [JsonPropertyName("weightDecay")]
        public float WeightDecay { get; set; } = 0.0001f;

        /// <summary>
        /// Gets or sets whether to use early stopping
        /// </summary>
        [JsonPropertyName("useEarlyStopping")]
        public bool UseEarlyStopping { get; set; } = true;

        /// <summary>
        /// Gets or sets the patience for early stopping
        /// </summary>
        [JsonPropertyName("earlyStoppingPatience")]
        public int EarlyStoppingPatience { get; set; } = 10;

        /// <summary>
        /// Gets or sets the minimum improvement for early stopping
        /// </summary>
        [JsonPropertyName("earlyStoppingMinDelta")]
        public float EarlyStoppingMinDelta { get; set; } = 0.001f;

        /// <summary>
        /// Gets or sets the model architecture type
        /// </summary>
        [JsonPropertyName("modelArchitecture")]
        public string ModelArchitecture { get; set; } = "FeedForward";

        /// <summary>
        /// Gets or sets the optimizer type
        /// </summary>
        [JsonPropertyName("optimizer")]
        public string Optimizer { get; set; } = "Adam";

        /// <summary>
        /// Gets or sets the loss function type
        /// </summary>
        [JsonPropertyName("lossFunction")]
        public string LossFunction { get; set; } = "BCE"; // Use BCE for multi-label classification

        /// <summary>
        /// Gets or sets the output path for saving the trained model
        /// </summary>
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; } = "models/";

        /// <summary>
        /// Gets or sets the model name
        /// </summary>
        [JsonPropertyName("modelName")]
        public string ModelName { get; set; } = "daminion_classifier";

        /// <summary>
        /// Gets or sets whether to save the model in ONNX format
        /// </summary>
        [JsonPropertyName("saveOnnx")]
        public bool SaveOnnx { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to save the model in TorchScript format
        /// </summary>
        [JsonPropertyName("saveTorchScript")]
        public bool SaveTorchScript { get; set; } = false;

        /// <summary>
        /// Gets or sets the device to use for training (CPU, CUDA, MPS)
        /// </summary>
        [JsonPropertyName("device")]
        public string Device { get; set; } = "CPU";
    }

    /// <summary>
    /// Represents training progress information
    /// </summary>
    public class TrainingProgress
    {
        /// <summary>
        /// Gets or sets the current epoch
        /// </summary>
        [JsonPropertyName("currentEpoch")]
        public int CurrentEpoch { get; set; }

        /// <summary>
        /// Gets or sets the total epochs
        /// </summary>
        [JsonPropertyName("totalEpochs")]
        public int TotalEpochs { get; set; }

        /// <summary>
        /// Gets or sets the current training loss
        /// </summary>
        [JsonPropertyName("trainingLoss")]
        public float TrainingLoss { get; set; }

        /// <summary>
        /// Gets or sets the current validation loss
        /// </summary>
        [JsonPropertyName("validationLoss")]
        public float ValidationLoss { get; set; }

        /// <summary>
        /// Gets or sets the current training accuracy
        /// </summary>
        [JsonPropertyName("trainingAccuracy")]
        public float TrainingAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the current validation accuracy
        /// </summary>
        [JsonPropertyName("validationAccuracy")]
        public float ValidationAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the current learning rate
        /// </summary>
        [JsonPropertyName("learningRate")]
        public float LearningRate { get; set; }

        /// <summary>
        /// Gets or sets the training status
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "Idle";

        /// <summary>
        /// Gets or sets the progress percentage (0.0 to 1.0)
        /// </summary>
        [JsonPropertyName("progress")]
        public float Progress => TotalEpochs > 0 ? (float)CurrentEpoch / TotalEpochs : 0.0f;
    }
}
