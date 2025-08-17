using System.Text.Json.Serialization;

namespace DaminionTorchTrainer.Models
{
    /// <summary>
    /// Represents ML.NET Model Builder configuration (.mbconfig) file structure
    /// </summary>
    public class MbConfig
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("dataSources")]
        public List<DataSource> DataSources { get; set; } = new List<DataSource>();

        [JsonPropertyName("preprocessing")]
        public PreprocessingConfig Preprocessing { get; set; } = new PreprocessingConfig();

        [JsonPropertyName("training")]
        public MbTrainingConfig Training { get; set; } = new MbTrainingConfig();

        [JsonPropertyName("evaluation")]
        public EvaluationConfig Evaluation { get; set; } = new EvaluationConfig();
    }

    public class DataSource
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("separator")]
        public string Separator { get; set; } = ",";

        [JsonPropertyName("hasHeader")]
        public bool HasHeader { get; set; } = true;
    }

    public class PreprocessingConfig
    {
        [JsonPropertyName("featureColumns")]
        public List<string> FeatureColumns { get; set; } = new List<string>();

        [JsonPropertyName("labelColumn")]
        public string LabelColumn { get; set; } = string.Empty;

        [JsonPropertyName("categoricalColumns")]
        public List<string> CategoricalColumns { get; set; } = new List<string>();

        [JsonPropertyName("numericalColumns")]
        public List<string> NumericalColumns { get; set; } = new List<string>();
    }

    public class MbTrainingConfig
    {
        [JsonPropertyName("algorithm")]
        public string Algorithm { get; set; } = "FastTree";

        [JsonPropertyName("hyperparameters")]
        public Dictionary<string, object> Hyperparameters { get; set; } = new Dictionary<string, object>();

        [JsonPropertyName("crossValidation")]
        public CrossValidationConfig CrossValidation { get; set; } = new CrossValidationConfig();

        [JsonPropertyName("epochs")]
        public int Epochs { get; set; } = 100;

        [JsonPropertyName("batchSize")]
        public int BatchSize { get; set; } = 32;
    }

    public class CrossValidationConfig
    {
        [JsonPropertyName("folds")]
        public int Folds { get; set; } = 5;
    }

    public class EvaluationConfig
    {
        [JsonPropertyName("metrics")]
        public List<string> Metrics { get; set; } = new List<string> { "Accuracy", "F1Score", "Precision", "Recall" };
    }
}
