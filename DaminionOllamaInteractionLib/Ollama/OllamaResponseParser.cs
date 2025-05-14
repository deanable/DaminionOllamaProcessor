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

            if (!parsedContent.SuccessfullyParsed && !string.IsNullOrWhiteSpace(parsedContent.Description))
            {
                parsedContent.SuccessfullyParsed = true;
            }
            else if (string.IsNullOrWhiteSpace(parsedContent.Description) && !parsedContent.Categories.Any() && !parsedContent.Keywords.Any())
            {
                if (!string.IsNullOrWhiteSpace(llavaResponseText))
                {
                    parsedContent.Description = llavaResponseText;
                    parsedContent.SuccessfullyParsed = true;
                }
                else
                {
                    parsedContent.Description = "Ollama returned content, but parsing failed to extract structured data.";
                    parsedContent.SuccessfullyParsed = false;
                }
            }
            return parsedContent;
        }
    }
}