// DaminionOllamaApp/Models/AppSettings.cs
using System.ComponentModel;
using System.Security; // Required for SecureString if you choose to use it later

namespace DaminionOllamaApp.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private string _daminionServerUrl = "http://researchserver.juicefilm.local/daminion"; // Example default
        private string _daminionUsername = "admin";
        private string _daminionPassword = "admin"; // For simplicity now, consider SecureString later
        private string _ollamaServerUrl = "http://researchserver.juicefilm.local:11434"; // Example default
        private string _ollamaModelName = "llava:13b"; // Example default
        private string _ollamaPrompt = "Please describe this image in detail. Identify key objects, subjects, and the overall scene. If relevant, suggest suitable categories and keywords.\n\nDescription:\n\nCategories:\n- Category1\n- Category2\n\nKeywords:\n- Keyword1, Keyword2, Keyword3"; // Example default

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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ... existing properties ...

        private string _daminionDescriptionTagGuid = string.Empty;
        private string _daminionKeywordsTagGuid = string.Empty;
        private string _daminionCategoriesTagGuid = string.Empty;
        private string _daminionFlagTagGuid = string.Empty; // For the main "Flag" tag
                                                            // You might also need IDs/GUIDs for specific flag *values* like "Processed"

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
        public string DaminionFlagTagGuid // GUID of the "Flag" tag itself
        {
            get => _daminionFlagTagGuid;
            set { if (_daminionFlagTagGuid != value) { _daminionFlagTagGuid = value; OnPropertyChanged(nameof(DaminionFlagTagGuid)); } }
        }


    }
}