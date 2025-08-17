using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionTorchTrainer.Models
{
    /// <summary>
    /// Represents the structure of an ML.NET Model Builder configuration (.mbconfig) file.
    /// This class defines the schema for data sources, preprocessing, training, and evaluation settings.
    /// </summary>
    public class MbConfig
    {
        /// <summary>
        /// Gets or sets the version of the .mbconfig file format.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the list of data sources for training.
        /// </summary>
        [JsonPropertyName("dataSources")]
        public List<DataSource> DataSources { get; set; } = new List<DataSource>();

        /// <summary>
        /// Gets or sets the data preprocessing configuration.
        /// </summary>
        [JsonPropertyName("preprocessing")]
        public PreprocessingConfig Preprocessing { get; set; } = new PreprocessingConfig();

        /// <summary>
        /// Gets or sets the model training configuration.
        /// </summary>
        [JsonPropertyName("training")]
        public MbTrainingConfig Training { get; set; } = new MbTrainingConfig();

        /// <summary>
        /// Gets or sets the model evaluation configuration.
        /// </summary>
        [JsonPropertyName("evaluation")]
        public EvaluationConfig Evaluation { get; set; } = new EvaluationConfig();
    }

    /// <summary>
    /// Represents a data source for model training.
    /// </summary>
    public class DataSource
    {
        /// <summary>
        /// Gets or sets the type of the data source (e.g., "file", "sql").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the data source.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the separator for delimited files.
        /// </summary>
        [JsonPropertyName("separator")]
        public string Separator { get; set; } = ",";

        /// <summary>
        /// Gets or sets a value indicating whether the data source has a header row.
        /// </summary>
        [JsonPropertyName("hasHeader")]
        public bool HasHeader { get; set; } = true;
    }

    /// <summary>
    /// Represents the data preprocessing configuration.
    /// </summary>
    public class PreprocessingConfig
    {
        /// <summary>
        /// Gets or sets the list of feature columns.
        /// </summary>
        [JsonPropertyName("featureColumns")]
        public List<string> FeatureColumns { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the name of the label column.
        /// </summary>
        [JsonPropertyName("labelColumn")]
        public string LabelColumn { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of categorical columns.
        /// </summary>
        [JsonPropertyName("categoricalColumns")]
        public List<string> CategoricalColumns { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of numerical columns.
        /// </summary>
        [JsonPropertyName("numericalColumns")]
        public List<string> NumericalColumns { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents the model training configuration.
    /// </summary>
    public class MbTrainingConfig
    {
        /// <summary>
        /// Gets or sets the training algorithm.
        /// </summary>
        [JsonPropertyName("algorithm")]
        public string Algorithm { get; set; } = "FastTree";

        /// <summary>
        /// Gets or sets the hyperparameters for the algorithm.
        /// </summary>
        [JsonPropertyName("hyperparameters")]
        public Dictionary<string, object> Hyperparameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the cross-validation configuration.
        /// </summary>
        [JsonPropertyName("crossValidation")]
        public CrossValidationConfig CrossValidation { get; set; } = new CrossValidationConfig();

        /// <summary>
        /// Gets or sets the number of training epochs.
        /// </summary>
        [JsonPropertyName("epochs")]
        public int Epochs { get; set; } = 100;

        /// <summary>
        /// Gets or sets the batch size for training.
        /// </summary>
        [JsonPropertyName("batchSize")]
        public int BatchSize { get; set; } = 32;
    }

    /// <summary>
    /// Represents the cross-validation configuration.
    /// </summary>
    public class CrossValidationConfig
    {
        /// <summary>
        /// Gets or sets the number of folds for cross-validation.
        /// </summary>
        [JsonPropertyName("folds")]
        public int Folds { get; set; } = 5;
    }

    /// <summary>
    /// Represents the model evaluation configuration.
    /// </summary>
    public class EvaluationConfig
    {
        /// <summary>
        /// Gets or sets the list of evaluation metrics.
        /// </summary>
        [JsonPropertyName("metrics")]
        public List<string> Metrics { get; set; } = new List<string> { "Accuracy", "F1Score", "Precision", "Recall" };
    }
}
