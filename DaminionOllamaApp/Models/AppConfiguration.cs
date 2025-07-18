using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace DaminionOllamaApp.Models
{
    /// <summary>
    /// Strongly-typed configuration model with validation.
    /// </summary>
    public class AppConfiguration
    {
        public DaminionConfig Daminion { get; set; } = new();
        public OllamaConfig Ollama { get; set; } = new();
        public OpenRouterConfig OpenRouter { get; set; } = new();
        public GemmaConfig Gemma { get; set; } = new();
        public BillingConfig Billing { get; set; } = new();
    }

    public class DaminionConfig
    {
        [Required]
        [Url]
        public string ServerUrl { get; set; } = string.Empty;
        
        [Required]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        public string Password { get; set; } = string.Empty;
        
        public string DescriptionTagGuid { get; set; } = string.Empty;
        public string KeywordsTagGuid { get; set; } = string.Empty;
        public string CategoriesTagGuid { get; set; } = string.Empty;
    }

    public class OllamaConfig
    {
        [Required]
        [Url]
        public string ServerUrl { get; set; } = "http://localhost:11434";
        
        [Required]
        public string ModelName { get; set; } = "llava:13b";
        
        public string DefaultPrompt { get; set; } = "Please describe this image in detail...";
    }

    public class OpenRouterConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string HttpReferer { get; set; } = "http://localhost";
        public string ModelName { get; set; } = "google/gemini-pro-vision";
    }

    public class GemmaConfig
    {
        public string ServiceAccountJsonPath { get; set; } = string.Empty;
        public string ModelName { get; set; } = "gemini-1.5-pro";
    }

    public class BillingConfig
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Dataset { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
    }
}