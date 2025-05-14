// DaminionOllamaInteractionLib/Ollama/ParsedOllamaContent.cs
using System.Collections.Generic;

namespace DaminionOllamaInteractionLib.Ollama
{
    public class ParsedOllamaContent // Must be public
    {
        public string Description { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Keywords { get; set; } = new List<string>();
        public string RawResponse { get; set; } = string.Empty;
        public bool SuccessfullyParsed { get; set; } = false;
    }
}