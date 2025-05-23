// DaminionOllamaInteractionLib/Ollama/OllamaModelInfo.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Ollama
{
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

    public class OllamaListTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo>? Models { get; set; }
    }
}