// DaminionOllamaInteractionLib/Daminion/DaminionUpdatePayload.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Daminion
{
    /// <summary>
    /// Represents a batch change request for updating multiple items in Daminion.
    /// </summary>
    public class DaminionBatchChangeRequest
    {
        /// <summary>
        /// Gets or sets the list of item IDs to update.
        /// </summary>
        [JsonPropertyName("ids")]
        public List<long> Ids { get; set; } = new List<long>();
        /// <summary>
        /// Gets or sets the list of update operations to apply.
        /// </summary>
        [JsonPropertyName("data")]
        public List<DaminionUpdateOperation> Data { get; set; } = new List<DaminionUpdateOperation>();
    }

    /// <summary>
    /// Represents a single update operation for a Daminion batch change request.
    /// </summary>
    public class DaminionUpdateOperation
    {
        /// <summary>
        /// Gets or sets the GUID of the tag to update.
        /// </summary>
        [JsonPropertyName("guid")]
        public string Guid { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the tag value ID (set to 0 if assigning by text).
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }
        /// <summary>
        /// Gets or sets the value to assign to the tag.
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets a value indicating whether to remove the tag value.
        /// </summary>
        [JsonPropertyName("remove")]
        public bool Remove { get; set; } = false;
        /// <summary>
        /// Gets the tag GUID (alias for Guid).
        /// </summary>
        public string TagGuid => Guid;
        /// <summary>
        /// Gets the operation type ("remove" or "add").
        /// </summary>
        public string Operation => Remove ? "remove" : "add";
    }

    /// <summary>
    /// Represents the response from a Daminion batch change request.
    /// </summary>
    public class DaminionBatchChangeResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the batch change was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }
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
    }
}