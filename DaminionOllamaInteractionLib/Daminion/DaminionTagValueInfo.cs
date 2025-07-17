// DaminionOllamaInteractionLib/Daminion/DaminionTagValueInfo.cs
using System.Collections.Generic;
using System.Text.Json.Serialization; // Ensure this using directive is present

namespace DaminionOllamaInteractionLib.Daminion
{
    /// <summary>
    /// Represents a tag value in Daminion.
    /// </summary>
    public class DaminionTagValue
    {
        /// <summary>
        /// Gets or sets the display text of the tag value.
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the unique ID of the tag value.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this is the default value for the tag.
        /// </summary>
        [JsonPropertyName("isDefaultValue")]
        public bool IsDefaultValue { get; set; }
        /// <summary>
        /// Gets or sets the ID of the tag this value belongs to.
        /// </summary>
        [JsonPropertyName("tagId")]
        public long TagId { get; set; }
        /// <summary>
        /// Gets or sets the raw value (internal representation).
        /// </summary>
        [JsonPropertyName("rawValue")]
        public string RawValue { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the name of the tag this value belongs to.
        /// </summary>
        [JsonPropertyName("tagName")]
        public string TagName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets a value indicating whether this tag value has child values.
        /// </summary>
        [JsonPropertyName("hasChilds")]
        public bool HasChilds { get; set; }
    }

    /// <summary>
    /// Represents the response from the Daminion API for getting tag values.
    /// </summary>
    public class DaminionGetTagValuesResponse
    {
        /// <summary>
        /// Gets or sets the list of tag values returned by the API.
        /// </summary>
        [JsonPropertyName("values")]
        public List<DaminionTagValue>? Values { get; set; }
        /// <summary>
        /// Gets or sets the path of tag values (hierarchical path).
        /// </summary>
        [JsonPropertyName("path")]
        public List<DaminionTagValue>? Path { get; set; }
        /// <summary>
        /// Gets or sets the tag associated with these values.
        /// </summary>
        [JsonPropertyName("tag")]
        public DaminionTag? Tag { get; set; }
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
}