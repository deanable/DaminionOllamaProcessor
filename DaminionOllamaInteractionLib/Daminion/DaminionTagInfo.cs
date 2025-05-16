// DaminionOllamaInteractionLib/Daminion/DaminionTagInfo.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Daminion // <--- Check this namespace
{
    /// <summary>
    /// Represents the response from the Daminion API for getting tags.
    /// </summary>
    public class DaminionGetTagsResponse // <--- Must be public
    {
        [JsonPropertyName("data")]
        public List<DaminionTag>? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    /// <summary>
    /// Represents a tag in Daminion.
    /// </summary>
    public class DaminionTag // <--- Must be public
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("indexed")]
        public bool Indexed { get; set; }

        [JsonPropertyName("guid")]
        public string Guid { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("originName")]
        public string? OriginName { get; set; }

        [JsonPropertyName("readOnly")]
        public bool ReadOnly { get; set; }

        [JsonPropertyName("dataType")]
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Returns a string representation of the DaminionTag object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Name} (ID: {Id}, GUID: {Guid}, Type: {DataType}, Indexed: {Indexed})";
        }
    }
}