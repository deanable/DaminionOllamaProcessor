// DaminionOllamaInteractionLib/Ollama/OllamaResponseParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaminionOllamaInteractionLib.Ollama
{
    public static class OllamaResponseParser // Must be public and static
    {
        public static ParsedOllamaContent ParseLlavaResponse(string llavaResponseText)
        {
            var parsedContent = new ParsedOllamaContent { RawResponse = llavaResponseText };
            if (string.IsNullOrWhiteSpace(llavaResponseText))
            {
                parsedContent.Description = "Ollama returned an empty response.";
                return parsedContent;
            }

            string description = llavaResponseText;

            // Attempt to extract categories
            var categoriesMatch = Regex.Match(llavaResponseText, @"Categories:(.*?)(\n\n|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (categoriesMatch.Success)
            {
                string categoriesBlock = categoriesMatch.Groups[1].Value.Trim();
                parsedContent.Categories = categoriesBlock.Split(new[] { '\n', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                                      .Select(c => c.Trim())
                                                      .Where(c => !string.IsNullOrWhiteSpace(c))
                                                      .ToList();
                description = description.Replace(categoriesMatch.Value, "").Trim();
                parsedContent.SuccessfullyParsed = true;
            }

            // Attempt to extract keywords
            var keywordsMatch = Regex.Match(llavaResponseText, @"Keywords:(.*?)(\n\n|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (keywordsMatch.Success)
            {
                string keywordsBlock = keywordsMatch.Groups[1].Value.Trim();
                parsedContent.Keywords = keywordsBlock.Split(new[] { '\n', '-', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(k => k.Trim())
                                                    .Where(k => !string.IsNullOrWhiteSpace(k))
                                                    .ToList();
                description = description.Replace(keywordsMatch.Value, "").Trim();
                parsedContent.SuccessfullyParsed = true;
            }

            parsedContent.Description = description.Trim();

            // If no specific sections were found, the whole text is the description.
            if (!parsedContent.SuccessfullyParsed && !string.IsNullOrWhiteSpace(parsedContent.Description))
            {
                parsedContent.SuccessfullyParsed = true;
            }
            else if (string.IsNullOrWhiteSpace(parsedContent.Description) && !parsedContent.Categories.Any() && !parsedContent.Keywords.Any())
            {
                // This case handles if parsing resulted in empty fields but raw response wasn't empty.
                if (!string.IsNullOrWhiteSpace(llavaResponseText))
                {
                    parsedContent.Description = llavaResponseText; // Fallback to raw response as description
                    parsedContent.SuccessfullyParsed = true; // Consider it "parsed" as a description block
                }
                else
                {
                    // This case should be rare if initial null/whitespace check passed.
                    parsedContent.Description = "Ollama returned content, but parsing failed to extract structured data.";
                    parsedContent.SuccessfullyParsed = false;
                }
            }
            return parsedContent;
        }
    }
}