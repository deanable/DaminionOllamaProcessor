// DaminionOllamaInteractionLib/Ollama/OllamaGenerateResponse.cs
using System.Text.Json.Serialization; // Required for JsonPropertyName
using System.Collections.Generic;     // Required for List

namespace DaminionOllamaInteractionLib.Ollama
{
    public class OllamaGenerateResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty; // ISO 8601 date string

        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty; // This is the main content from Llava

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        // Optional fields that might be present in the response
        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; set; }

        [JsonPropertyName("load_duration")]
        public long? LoadDuration { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }

        [JsonPropertyName("eval_duration")]
        public long? EvalDuration { get; set; }

        // Context is usually a large array of numbers, you might not need to deserialize it fully
        // unless you plan to use it for follow-up requests.
        // For now, we can ignore it or deserialize as JsonElement if needed later.
        // [JsonPropertyName("context")]
        // public List<int>? Context { get; set; }
    }
}