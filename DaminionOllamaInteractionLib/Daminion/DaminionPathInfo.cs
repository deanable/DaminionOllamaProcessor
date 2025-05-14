// DaminionOllamaInteractionLib/Daminion/DaminionPathInfo.cs
using System.Collections.Generic;
// No System.Text.Json.Serialization needed here directly unless you add JsonPropertyName for some reason

namespace DaminionOllamaInteractionLib.Daminion
{
    public class DaminionPathResult // Must be public
    {
        public Dictionary<string, string>? Paths { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}