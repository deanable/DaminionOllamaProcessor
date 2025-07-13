// DaminionOllamaInteractionLib/Ollama/ParsedOllamaContent.cs
using System.Collections.Generic;

namespace DaminionOllamaInteractionLib.Ollama
{
    /// <summary>
    /// Represents the structured metadata extracted from an Ollama AI response.
    /// This class serves as a data container for the parsed content after processing
    /// an AI-generated response through the OllamaResponseParser.
    /// 
    /// The class contains both the extracted structured data and the original raw response
    /// for reference and debugging purposes.
    /// </summary>
    public class ParsedOllamaContent // Must be public
    {
        /// <summary>
        /// Gets or sets the descriptive text extracted from the AI response.
        /// This typically contains the main content describing the analyzed image.
        /// If structured parsing fails, this may contain the entire raw response.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of categories extracted from the AI response.
        /// Categories represent high-level classifications or themes identified in the content.
        /// Examples: "Nature", "Architecture", "Portrait", "Landscape".
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of keywords extracted from the AI response.
        /// Keywords are specific terms and tags that can be used for searching and indexing.
        /// Examples: "sunset", "mountain", "blue sky", "reflection".
        /// </summary>
        public List<string> Keywords { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the original, unprocessed response text from the AI service.
        /// This is stored for reference, debugging, and fallback purposes.
        /// Useful for troubleshooting parsing issues or when structured extraction fails.
        /// </summary>
        public string RawResponse { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the parsing operation successfully
        /// extracted structured content from the AI response.
        /// 
        /// True if at least one structured section (categories, keywords, or description)
        /// was successfully identified and parsed. False if parsing failed or only
        /// the raw response could be used as a fallback description.
        /// </summary>
        public bool SuccessfullyParsed { get; set; } = false;
    }
}