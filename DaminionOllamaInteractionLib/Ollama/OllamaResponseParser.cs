// DaminionOllamaInteractionLib/Ollama/OllamaResponseParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace DaminionOllamaInteractionLib.Ollama
{
    /// <summary>
    /// Static utility class responsible for parsing responses from the Ollama AI service.
    /// This parser extracts structured metadata (categories, keywords, descriptions) 
    /// from free-form AI text responses using regex patterns.
    /// 
    /// The parser expects AI responses to follow a specific format with sections like:
    /// - Categories: [list of categories]
    /// - Keywords: [list of keywords]
    /// - Description: [descriptive text]
    /// 
    /// If the expected format is not found, the parser falls back to treating
    /// the entire response as a description.
    /// </summary>
    public static class OllamaResponseParser // Must be public and static
    {
        #region Constants
        /// <summary>
        /// Regular expression pattern for extracting categories from AI responses.
        /// Matches "Categories:" followed by content until double newline or end of string.
        /// </summary>
        private static readonly Regex CategoriesRegex = new Regex(
            @"Categories:(.*?)(\n\n|\z)", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Regular expression pattern for extracting keywords from AI responses.
        /// Matches "Keywords:" followed by content until double newline or end of string.
        /// </summary>
        private static readonly Regex KeywordsRegex = new Regex(
            @"Keywords:(.*?)(\n\n|\z)", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Regular expression pattern for extracting description from AI responses.
        /// Matches "Description:" followed by content until double newline or end of string.
        /// </summary>
        private static readonly Regex DescriptionRegex = new Regex(
            @"Description:(.*?)(\n\n|\z)", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Characters used to split list items in categories and keywords sections.
        /// </summary>
        private static readonly char[] ListSeparators = { '\n', '-', ',' };
        #endregion

        #region Public Methods
        /// <summary>
        /// Parses the response from the Ollama AI service and extracts structured metadata.
        /// 
        /// The method attempts to extract:
        /// 1. Categories - organized classification tags
        /// 2. Keywords - searchable terms and tags
        /// 3. Description - detailed textual description
        /// 
        /// If structured sections are not found, the entire response is treated as a description.
        /// </summary>
        /// <param name="llavaResponseText">The raw text response from the Ollama AI service.</param>
        /// <returns>A ParsedOllamaContent object containing the extracted metadata.</returns>
        public static ParsedOllamaContent ParseLlavaResponse(string llavaResponseText)
        {
            var parsedContent = new ParsedOllamaContent { RawResponse = llavaResponseText };
            if (string.IsNullOrWhiteSpace(llavaResponseText))
            {
                parsedContent.Description = "Ollama returned an empty response.";
                return parsedContent;
            }

            // Try to parse as JSON and extract the text field
            string? textBlock = null;
            try
            {
                using var doc = JsonDocument.Parse(llavaResponseText);
                var root = doc.RootElement;
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    if (candidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var part = parts[0];
                        if (part.TryGetProperty("text", out var textProp))
                        {
                            textBlock = textProp.GetString();
                        }
                    }
                }
            }
            catch { /* Not JSON, fallback to old logic */ }

            if (!string.IsNullOrWhiteSpace(textBlock))
            {
                // Use the extracted text for section splitting
                llavaResponseText = textBlock;
            }

            // --- Extract clean Description (first paragraph before Categories/Keywords) ---
            string description = llavaResponseText;
            int catIdx = llavaResponseText.IndexOf("Categories:", StringComparison.OrdinalIgnoreCase);
            int keyIdx = llavaResponseText.IndexOf("Keywords:", StringComparison.OrdinalIgnoreCase);
            int firstSectionIdx = -1;
            if (catIdx >= 0 && keyIdx >= 0) firstSectionIdx = Math.Min(catIdx, keyIdx);
            else if (catIdx >= 0) firstSectionIdx = catIdx;
            else if (keyIdx >= 0) firstSectionIdx = keyIdx;
            if (firstSectionIdx > 0)
                description = llavaResponseText.Substring(0, firstSectionIdx).Trim();
            var descParagraph = description.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            parsedContent.Description = string.IsNullOrWhiteSpace(descParagraph) ? description.Trim() : descParagraph;

            // --- Extract Categories ---
            if (TryExtractCategories(llavaResponseText, out var categories))
                parsedContent.Categories = categories;

            // --- Extract Keywords ---
            if (TryExtractKeywords(llavaResponseText, out var keywords))
                parsedContent.Keywords = keywords;

            parsedContent.SuccessfullyParsed =
                !string.IsNullOrWhiteSpace(parsedContent.Description) ||
                (parsedContent.Categories?.Count > 0) ||
                (parsedContent.Keywords?.Count > 0);

            return parsedContent;
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// Attempts to extract categories from the AI response text.
        /// </summary>
        /// <param name="responseText">The full AI response text.</param>
        /// <param name="categories">Output parameter containing the extracted categories.</param>
        /// <returns>True if categories were successfully extracted, false otherwise.</returns>
        private static bool TryExtractCategories(string responseText, out List<string> categories)
        {
            categories = new List<string>();
            
            var match = CategoriesRegex.Match(responseText);
            if (!match.Success)
                return false;

            var categoriesBlock = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(categoriesBlock))
                return false;

            // Parse the categories block into individual items
            categories = ParseListItems(categoriesBlock);
            return categories.Any();
        }

        /// <summary>
        /// Attempts to extract keywords from the AI response text.
        /// </summary>
        /// <param name="responseText">The full AI response text.</param>
        /// <param name="keywords">Output parameter containing the extracted keywords.</param>
        /// <returns>True if keywords were successfully extracted, false otherwise.</returns>
        private static bool TryExtractKeywords(string responseText, out List<string> keywords)
        {
            keywords = new List<string>();
            
            var match = KeywordsRegex.Match(responseText);
            if (!match.Success)
                return false;

            var keywordsBlock = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(keywordsBlock))
                return false;

            // Parse the keywords block into individual items
            keywords = ParseListItems(keywordsBlock);
            return keywords.Any();
        }

        /// <summary>
        /// Attempts to extract a description from the AI response text.
        /// </summary>
        /// <param name="responseText">The full AI response text.</param>
        /// <param name="description">Output parameter containing the extracted description.</param>
        /// <returns>True if a description was successfully extracted, false otherwise.</returns>
        private static bool TryExtractDescription(string responseText, out string description)
        {
            description = string.Empty;
            
            var match = DescriptionRegex.Match(responseText);
            if (!match.Success)
                return false;

            description = match.Groups[1].Value.Trim();
            return !string.IsNullOrWhiteSpace(description);
        }

        /// <summary>
        /// Parses a block of text into individual list items.
        /// Handles various separators like newlines, dashes, and commas.
        /// </summary>
        /// <param name="listBlock">The text block containing list items.</param>
        /// <returns>A list of cleaned, non-empty items.</returns>
        private static List<string> ParseListItems(string listBlock)
        {
            return listBlock
                .Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        /// <summary>
        /// Removes a matched section from the text using the specified regex pattern.
        /// Used to clean up the description after extracting structured sections.
        /// </summary>
        /// <param name="text">The text to process.</param>
        /// <param name="pattern">The regex pattern to match and remove.</param>
        /// <returns>The text with the matched section removed.</returns>
        private static string RemoveSection(string text, Regex pattern)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                return text.Replace(match.Value, "").Trim();
            }
            return text;
        }
        #endregion
    }
}