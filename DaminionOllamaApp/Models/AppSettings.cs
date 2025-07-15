// DaminionOllamaApp/Models/AppSettings.cs
using System.ComponentModel;
using System.Security; // Required for SecureString if you choose to use it later

namespace DaminionOllamaApp.Models
{
    // NEW: Enum to define the available AI providers.
    public enum AiProvider
    {
        Ollama,
        OpenRouter
    }

    public class AppSettings : INotifyPropertyChanged
    {
        // -- AI Provider Selection --
        private AiProvider _selectedAiProvider = AiProvider.Ollama; // Default to Ollama

        // -- Existing Daminion Properties --
        private string _daminionServerUrl = "http://researchserver.juicefilm.local/daminion"; // Example default
        private string _daminionUsername = "admin";
        private string _daminionPassword = "admin"; // For simplicity now, consider SecureString later

        // -- Existing Ollama Properties --
        private string _ollamaServerUrl = "http://researchserver.juicefilm.local:11434"; // Example default
        private string _ollamaModelName = "llava:13b"; // Example default
        private string _ollamaPrompt = "Please describe this image in detail. Identify key objects, subjects, and the overall scene. If relevant, suggest suitable categories and keywords.\n\nDescription:\n\nCategories:\n- Category1\n- Category2\n\nKeywords:\n- Keyword1, Keyword2, Keyword3"; // Example default

        // -- OpenRouter Properties --
        private string _openRouterApiKey = string.Empty;
        private string _openRouterHttpReferer = "http://localhost"; // Replace with your actual app name or URL
        private string _openRouterModelName = "google/gemini-pro-vision"; // A sensible default

        // NEW: Public property for the selected AI provider
        public AiProvider SelectedAiProvider
        {
            get => _selectedAiProvider;
            set
            {
                if (_selectedAiProvider != value)
                {
                    _selectedAiProvider = value;
                    // Synchronize UseOpenRouter
                    UseOpenRouter = (value == AiProvider.OpenRouter);
                    OnPropertyChanged(nameof(SelectedAiProvider));
                }
            }
        }

        public string DaminionServerUrl
        {
            get => _daminionServerUrl;
            set
            {
                if (_daminionServerUrl != value)
                {
                    _daminionServerUrl = value;
                    OnPropertyChanged(nameof(DaminionServerUrl));
                }
            }
        }

        public string DaminionUsername
        {
            get => _daminionUsername;
            set
            {
                if (_daminionUsername != value)
                {
                    _daminionUsername = value;
                    OnPropertyChanged(nameof(DaminionUsername));
                }
            }
        }

        public string DaminionPassword // Consider changing to SecureString for better security
        {
            get => _daminionPassword;
            set
            {
                if (_daminionPassword != value)
                {
                    _daminionPassword = value;
                    OnPropertyChanged(nameof(DaminionPassword));
                }
            }
        }

        public string OllamaServerUrl
        {
            get => _ollamaServerUrl;
            set
            {
                if (_ollamaServerUrl != value)
                {
                    _ollamaServerUrl = value;
                    OnPropertyChanged(nameof(OllamaServerUrl));
                }
            }
        }

        public string OllamaModelName
        {
            get => _ollamaModelName;
            set
            {
                if (_ollamaModelName != value)
                {
                    _ollamaModelName = value;
                    OnPropertyChanged(nameof(OllamaModelName));
                }
            }
        }

        public string OllamaPrompt
        {
            get => _ollamaPrompt;
            set
            {
                if (_ollamaPrompt != value)
                {
                    _ollamaPrompt = value;
                    OnPropertyChanged(nameof(OllamaPrompt));
                }
            }
        }

        public string OpenRouterApiKey
        {
            get => _openRouterApiKey;
            set
            {
                if (_openRouterApiKey != value)
                {
                    _openRouterApiKey = value;
                    OnPropertyChanged(nameof(OpenRouterApiKey));
                }
            }
        }

        public string OpenRouterHttpReferer
        {
            get => _openRouterHttpReferer;
            set
            {
                if (_openRouterHttpReferer != value)
                {
                    _openRouterHttpReferer = value;
                    OnPropertyChanged(nameof(OpenRouterHttpReferer));
                }
            }
        }

        public string OpenRouterModelName
        {
            get => _openRouterModelName;
            set
            {
                if (_openRouterModelName != value)
                {
                    _openRouterModelName = value;
                    OnPropertyChanged(nameof(OpenRouterModelName));
                }
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ... existing properties for Daminion Tag GUIDs and Flags ...
        private string _daminionDescriptionTagGuid = string.Empty;
        private string _daminionKeywordsTagGuid = string.Empty;
        private string _daminionCategoriesTagGuid = string.Empty;
        private string _daminionFlagTagGuid = string.Empty;

        public string DaminionDescriptionTagGuid
        {
            get => _daminionDescriptionTagGuid;
            set { if (_daminionDescriptionTagGuid != value) { _daminionDescriptionTagGuid = value; OnPropertyChanged(nameof(DaminionDescriptionTagGuid)); } }
        }
        public string DaminionKeywordsTagGuid
        {
            get => _daminionKeywordsTagGuid;
            set { if (_daminionKeywordsTagGuid != value) { _daminionKeywordsTagGuid = value; OnPropertyChanged(nameof(DaminionKeywordsTagGuid)); } }
        }
        public string DaminionCategoriesTagGuid
        {
            get => _daminionCategoriesTagGuid;
            set { if (_daminionCategoriesTagGuid != value) { _daminionCategoriesTagGuid = value; OnPropertyChanged(nameof(DaminionCategoriesTagGuid)); } }
        }
        public string DaminionFlagTagGuid
        {
            get => _daminionFlagTagGuid;
            set { if (_daminionFlagTagGuid != value) { _daminionFlagTagGuid = value; OnPropertyChanged(nameof(DaminionFlagTagGuid)); } }
        }

        private bool _automaticallyUpdateFlagAfterOllama = false;
        private string _flagValueIdToClearAfterOllama = string.Empty;
        private string _flagValueIdToSetAfterOllama = string.Empty;

        public bool AutomaticallyUpdateFlagAfterOllama
        {
            get => _automaticallyUpdateFlagAfterOllama;
            set
            {
                if (_automaticallyUpdateFlagAfterOllama != value)
                {
                    _automaticallyUpdateFlagAfterOllama = value;
                    OnPropertyChanged(nameof(AutomaticallyUpdateFlagAfterOllama));
                }
            }
        }

        public string FlagValueIdToClearAfterOllama
        {
            get => _flagValueIdToClearAfterOllama;
            set
            {
                if (_flagValueIdToClearAfterOllama != value)
                {
                    _flagValueIdToClearAfterOllama = value;
                    OnPropertyChanged(nameof(FlagValueIdToClearAfterOllama));
                }
            }
        }

        public string FlagValueIdToSetAfterOllama
        {
            get => _flagValueIdToSetAfterOllama;
            set
            {
                if (_flagValueIdToSetAfterOllama != value)
                {
                    _flagValueIdToSetAfterOllama = value;
                    OnPropertyChanged(nameof(FlagValueIdToSetAfterOllama));
                }
            }
        }

        // --- Daminion Query Properties ---
        private string _daminionQueryType = string.Empty;
        private string _daminionQueryLine = string.Empty;
        private string _daminionProcessingPrompt = "Please describe this image in detail. Identify key objects, subjects, and the overall scene. If relevant, suggest suitable categories and keywords.\n\nDescription:\n\nCategories:\n- Category1\n- Category2\n\nKeywords:\n- Keyword1, Keyword2, Keyword3";

        public string DaminionQueryType
        {
            get => _daminionQueryType;
            set
            {
                if (_daminionQueryType != value)
                {
                    _daminionQueryType = value;
                    OnPropertyChanged(nameof(DaminionQueryType));
                }
            }
        }

        public string DaminionQueryLine
        {
            get => _daminionQueryLine;
            set
            {
                if (_daminionQueryLine != value)
                {
                    _daminionQueryLine = value;
                    OnPropertyChanged(nameof(DaminionQueryLine));
                }
            }
        }

        public string DaminionProcessingPrompt
        {
            get => _daminionProcessingPrompt;
            set
            {
                if (_daminionProcessingPrompt != value)
                {
                    _daminionProcessingPrompt = value;
                    OnPropertyChanged(nameof(DaminionProcessingPrompt));
                }
            }
        }

        // --- AI Provider Selection Properties ---
        private bool _useOpenRouter = false;

        public bool UseOpenRouter
        {
            get => _useOpenRouter;
            set
            {
                if (_useOpenRouter != value)
                {
                    _useOpenRouter = value;
                    // Synchronize SelectedAiProvider
                    SelectedAiProvider = value ? AiProvider.OpenRouter : AiProvider.Ollama;
                    OnPropertyChanged(nameof(UseOpenRouter));
                }
            }
        }

        // --- Alias Properties for Compatibility ---
        public string OllamaModel => OllamaModelName;
        public string OpenRouterModel => OpenRouterModelName;
    }
}