// DaminionOllamaInteractionLib/Daminion/DaminionTagValueInfo.cs
using System.Collections.Generic;
using System.Text.Json.Serialization; // Ensure this using directive is present

namespace DaminionOllamaInteractionLib.Daminion
{
    public class DaminionTagValue
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("isDefaultValue")]
        public bool IsDefaultValue { get; set; }

        [JsonPropertyName("tagId")]
        public long TagId { get; set; }

        [JsonPropertyName("rawValue")]
        public string RawValue { get; set; } = string.Empty;

        [JsonPropertyName("tagName")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("hasChilds")]
        public bool HasChilds { get; set; }
    }

    public class DaminionGetTagValuesResponse
    {
        [JsonPropertyName("values")]
        public List<DaminionTagValue>? Values { get; set; }

        [JsonPropertyName("path")]
        public List<DaminionTagValue>? Path { get; set; }

        [JsonPropertyName("tag")]
        public DaminionTag? Tag { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}