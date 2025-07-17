// DaminionOllamaInteractionLib/Daminion/DaminionMediaItem.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DaminionOllamaInteractionLib.Daminion
{
    /// <summary>
    /// Represents a media item in the Daminion system, as described in the Daminion API documentation.
    /// </summary>
    public class DaminionMediaItem
    {
        /// <summary>
        /// Gets or sets the unique ID of the media item.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }
        /// <summary>
        /// Gets or sets the hash code of the media item (optional).
        /// </summary>
        [JsonPropertyName("hashCode")]
        public long? HashCode { get; set; }
        /// <summary>
        /// Gets or sets the name of the media item.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        /// <summary>
        /// Gets or sets the file name of the media item.
        /// </summary>
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }
        /// <summary>
        /// Gets or sets the media format (e.g., image, video).
        /// </summary>
        [JsonPropertyName("mediaFormat")]
        public string? MediaFormat { get; set; }
        /// <summary>
        /// Gets or sets the version control state of the media item.
        /// </summary>
        [JsonPropertyName("versionControlState")]
        public int? VersionControlState { get; set; }
        /// <summary>
        /// Gets or sets the color label ID for the media item.
        /// </summary>
        [JsonPropertyName("colorLabel")]
        public long? ColorLabel { get; set; }
        /// <summary>
        /// Gets or sets the width of the media item (pixels).
        /// </summary>
        [JsonPropertyName("width")]
        public int? Width { get; set; }
        /// <summary>
        /// Gets or sets the height of the media item (pixels).
        /// </summary>
        [JsonPropertyName("height")]
        public int? Height { get; set; }
        /// <summary>
        /// Gets or sets the file size of the media item (bytes).
        /// </summary>
        [JsonPropertyName("fileSize")]
        public long? FileSize { get; set; }
        /// <summary>
        /// Gets or sets the format type of the media item.
        /// </summary>
        [JsonPropertyName("formatType")]
        public string? FormatType { get; set; }
        /// <summary>
        /// Gets or sets the expiration date of the media item (if any).
        /// </summary>
        [JsonPropertyName("expirationDate")]
        public string? ExpirationDate { get; set; }
    }

    /// <summary>
    /// Represents the response from the Daminion API for searching media items.
    /// </summary>
    public class DaminionSearchMediaItemsResponse
    {
        /// <summary>
        /// Gets or sets the list of media items returned by the search.
        /// </summary>
        [JsonPropertyName("mediaItems")]
        public List<DaminionMediaItem>? MediaItems { get; set; }
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
        /// Gets or sets a value indicating whether the search was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        // The API documentation for /api/mediaItems/get (page 15) does not explicitly show "totalCount".
        // However, /api/mediaItems/getSort (page 13) does. If you find "totalCount" in the actual
        // response for /api/mediaItems/get, you can add it here.
        // [JsonPropertyName("totalCount")]
        // public int TotalCount { get; set; }
    }
}