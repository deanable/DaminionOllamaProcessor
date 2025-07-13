// DaminionOllamaInteractionLib/Daminion/DaminionUpdatePayload.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Daminion
{
    /// <summary>
    /// This class is used to create a batch change request for Daminion.
    /// </summary>
    public class DaminionBatchChangeRequest // Must be public
    {
        [JsonPropertyName("ids")]
        public List<long> Ids { get; set; } = new List<long>();

        [JsonPropertyName("data")]
        public List<DaminionUpdateOperation> Data { get; set; } = new List<DaminionUpdateOperation>();
    }

    /// <summary>
    /// This class is used to create a batch change request for Daminion.
    /// </summary>
    public class DaminionUpdateOperation // Must be public
    {
        [JsonPropertyName("guid")]
        public string Guid { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public long Id { get; set; } // Tag value ID. Set to 0 if assigning by text.

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("remove")]
        public bool Remove { get; set; } = false;

        // Alias properties for compatibility with existing code
        public string TagGuid => Guid;
        public string Operation => Remove ? "remove" : "add";
    }

    /// <summary>
    /// This class is used to create a batch change response for Daminion.
    /// </summary>
    public class DaminionBatchChangeResponse // Must be public
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; } // Must be public

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }
    }
}