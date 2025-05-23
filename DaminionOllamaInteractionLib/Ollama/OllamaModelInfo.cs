// DaminionOllamaInteractionLib/Ollama/OllamaModelInfo.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Ollama
{
    /// <summary>
    /// Represents the details of an Ollama model.
    /// </summary>
    public class OllamaModelDetails
    {
        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("families")]
        public List<string>? Families { get; set; }

        [JsonPropertyName("parameter_size")]
        public string? ParameterSize { get; set; }

        [JsonPropertyName("quantization_level")]
        public string? QuantizationLevel { get; set; }
    }

    /// <summary>
    /// Represents the information of an Ollama model.
    /// </summary>
    public class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("modified_at")]
        public string ModifiedAt { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        public OllamaModelDetails? Details { get; set; }
    }

    /// <summary>
    /// Represents the response from the Ollama API for listing tags.
    /// </summary>
    public class OllamaListTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo>? Models { get; set; }
    }
}