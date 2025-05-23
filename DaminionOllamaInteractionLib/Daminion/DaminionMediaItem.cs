// DaminionOllamaInteractionLib/Daminion/DaminionMediaItem.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Daminion
{
    // This class corresponds to the 'Item' structure described on page 16
    // of the "API v4 original by Yuri.pdf" document.
    public class DaminionMediaItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; } // [cite: 84]

        [JsonPropertyName("hashCode")]
        public long? HashCode { get; set; } // [cite: 84]

        [JsonPropertyName("name")]
        public string? Name { get; set; } // [cite: 85]

        [JsonPropertyName("fileName")]
        public string? FileName { get; set; } // [cite: 86]

        [JsonPropertyName("mediaFormat")]
        public string? MediaFormat { get; set; } // [cite: 86]

        [JsonPropertyName("versionControlState")]
        public int? VersionControlState { get; set; } // [cite: 87]

        [JsonPropertyName("colorLabel")]
        public long? ColorLabel { get; set; } // ID of the value, [cite: 89]

        [JsonPropertyName("width")]
        public int? Width { get; set; } // [cite: 90]

        [JsonPropertyName("height")]
        public int? Height { get; set; } // [cite: 91]

        [JsonPropertyName("fileSize")]
        public long? FileSize { get; set; } // [cite: 92]

        [JsonPropertyName("formatType")]
        public string? FormatType { get; set; } // [cite: 92]

        [JsonPropertyName("expirationDate")]
        public string? ExpirationDate { get; set; } // [cite: 92]
    }

    // This class is a wrapper for the response from the GET /api/mediaItems/get endpoint
    // as described on page 15 of the "API v4 original by Yuri.pdf" document.
    public class DaminionSearchMediaItemsResponse
    {
        [JsonPropertyName("mediaItems")]
        public List<DaminionMediaItem>? MediaItems { get; set; } // [cite: 83]

        [JsonPropertyName("error")]
        public string? Error { get; set; } // [cite: 82]

        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; } // [cite: 82]

        [JsonPropertyName("success")]
        public bool Success { get; set; } // [cite: 83]

        // The API documentation for /api/mediaItems/get (page 15) does not explicitly show "totalCount".
        // However, /api/mediaItems/getSort (page 13) does. If you find "totalCount" in the actual
        // response for /api/mediaItems/get, you can add it here.
        // [JsonPropertyName("totalCount")]
        // public int TotalCount { get; set; }
    }
}