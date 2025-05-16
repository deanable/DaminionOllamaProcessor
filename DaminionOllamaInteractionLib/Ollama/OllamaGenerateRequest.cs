// DaminionOllamaInteractionLib/Ollama/OllamaGenerateRequest.cs
using System.Text.Json.Serialization; // Required for JsonPropertyName
using System.Collections.Generic;     // Required for List

namespace DaminionOllamaInteractionLib.Ollama
{
    /// <summary>
    /// Represents a request to generate text using the Ollama API.
    /// </summary>
    public class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("images")]
        public List<string>? Images { get; set; } // List of Base64 encoded images

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false; // We want the full response, not a stream
    }
}