// DaminionOllamaInteractionLib/Daminion/DaminionTagInfo.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Daminion // <--- Check this namespace
{
    /// <summary>
    /// Represents the response from the Daminion API for getting tags.
    /// </summary>
    public class DaminionGetTagsResponse
    {
        /// <summary>
        /// Gets or sets the list of tags returned by the API.
        /// </summary>
        [JsonPropertyName("data")]
        public List<DaminionTag>? Data { get; set; }
        /// <summary>
        /// Gets or sets the error message, if any.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
        /// <summary>
        /// Gets or sets the error code, if any.
        /// </summary>
        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the request was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    /// <summary>
    /// Represents a tag in the Daminion system.
    /// </summary>
    public class DaminionTag
    {
        /// <summary>
        /// Gets or sets the unique ID of the tag.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the tag is indexed.
        /// </summary>
        [JsonPropertyName("indexed")]
        public bool Indexed { get; set; }
        /// <summary>
        /// Gets or sets the GUID of the tag.
        /// </summary>
        [JsonPropertyName("guid")]
        public string Guid { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the display name of the tag.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the original name of the tag, if any.
        /// </summary>
        [JsonPropertyName("originName")]
        public string? OriginName { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the tag is read-only.
        /// </summary>
        [JsonPropertyName("readOnly")]
        public bool ReadOnly { get; set; }
        /// <summary>
        /// Gets or sets the data type of the tag.
        /// </summary>
        [JsonPropertyName("dataType")]
        public string DataType { get; set; } = string.Empty;
        /// <summary>
        /// Returns a string representation of the DaminionTag object.
        /// </summary>
        /// <returns>A string describing the tag.</returns>
        public override string ToString()
        {
            return $"{Name} (ID: {Id}, GUID: {Guid}, Type: {DataType}, Indexed: {Indexed})";
        }
    }
}