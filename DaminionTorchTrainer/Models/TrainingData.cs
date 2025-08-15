using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionTorchTrainer.Models
{
    /// <summary>
    /// Represents training data extracted from Daminion metadata or local images
    /// </summary>
    public class TrainingData
    {
        /// <summary>
        /// Gets or sets the unique identifier for this training sample
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the file name of the media item
        /// </summary>
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        /// <summary>
        /// Gets or sets the media format (e.g., image, video)
        /// </summary>
        [JsonPropertyName("mediaFormat")]
        public string? MediaFormat { get; set; }

        /// <summary>
        /// Gets or sets the width of the media item (pixels)
        /// </summary>
        [JsonPropertyName("width")]
        public int? Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the media item (pixels)
        /// </summary>
        [JsonPropertyName("height")]
        public int? Height { get; set; }

        /// <summary>
        /// Gets or sets the file size of the media item (bytes)
        /// </summary>
        [JsonPropertyName("fileSize")]
        public long? FileSize { get; set; }

        /// <summary>
        /// Gets or sets the format type of the media item
        /// </summary>
        [JsonPropertyName("formatType")]
        public string? FormatType { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with this media item
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the categories associated with this media item
        /// </summary>
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the keywords associated with this media item
        /// </summary>
        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the description of the media item
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the color label ID for the media item
        /// </summary>
        [JsonPropertyName("colorLabel")]
        public long? ColorLabel { get; set; }

        /// <summary>
        /// Gets or sets the version control state of the media item
        /// </summary>
        [JsonPropertyName("versionControlState")]
        public int? VersionControlState { get; set; }

        /// <summary>
        /// Gets or sets the expiration date of the media item (if any)
        /// </summary>
        [JsonPropertyName("expirationDate")]
        public string? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the file path of the media item
        /// </summary>
        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the data source type (API or Local)
        /// </summary>
        [JsonPropertyName("dataSource")]
        public DataSourceType DataSource { get; set; } = DataSourceType.API;

        /// <summary>
        /// Gets or sets the training features extracted from metadata
        /// </summary>
        [JsonPropertyName("features")]
        public List<float> Features { get; set; } = new List<float>();

        /// <summary>
        /// Gets or sets the target labels for training
        /// </summary>
        [JsonPropertyName("labels")]
        public List<float> Labels { get; set; } = new List<float>();
    }

    /// <summary>
    /// Represents the data source type for training data
    /// </summary>
    public enum DataSourceType
    {
        /// <summary>
        /// Data from Daminion API
        /// </summary>
        API,
        
        /// <summary>
        /// Data from local image files
        /// </summary>
        Local
    }

    /// <summary>
    /// Represents a dataset for training
    /// </summary>
    public class TrainingDataset
    {
        /// <summary>
        /// Gets or sets the name of the dataset
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the dataset
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the training samples
        /// </summary>
        [JsonPropertyName("samples")]
        public List<TrainingData> Samples { get; set; } = new List<TrainingData>();

        /// <summary>
        /// Gets or sets the feature dimension
        /// </summary>
        [JsonPropertyName("featureDimension")]
        public int FeatureDimension { get; set; }

        /// <summary>
        /// Gets or sets the label dimension
        /// </summary>
        [JsonPropertyName("labelDimension")]
        public int LabelDimension { get; set; }

        /// <summary>
        /// Gets or sets the total number of samples
        /// </summary>
        [JsonPropertyName("totalSamples")]
        public int TotalSamples => Samples.Count;

        /// <summary>
        /// Gets or sets the data source type for this dataset
        /// </summary>
        [JsonPropertyName("dataSource")]
        public DataSourceType DataSource { get; set; } = DataSourceType.API;

        /// <summary>
        /// Gets or sets the source path (API URL or local folder)
        /// </summary>
        [JsonPropertyName("sourcePath")]
        public string? SourcePath { get; set; }

        /// <summary>
        /// Gets or sets the metadata vocabulary (term to index mapping) for logging purposes
        /// </summary>
        [JsonPropertyName("metadataVocabulary")]
        public Dictionary<string, int>? MetadataVocabulary { get; set; }
    }
}
