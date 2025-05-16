// DaminionOllamaInteractionLib/Daminion/DaminionPathInfo.cs
using System.Collections.Generic;

namespace DaminionOllamaInteractionLib.Daminion
{
    /// <summary>
    /// Represents the result of a Daminion path operation. 
    /// </summary>
    public class DaminionPathResult // Must be public
    {
        /// <summary>
        /// Gets or sets the paths returned by the Daminion operation.
        /// </summary>
        public Dictionary<string, string>? Paths { get; set; }
        /// <summary>
        /// Gets or sets the success status of the Daminion operation.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the error message if the operation was not successful.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}