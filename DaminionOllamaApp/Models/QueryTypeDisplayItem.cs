using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaminionOllamaApp.Models
{
    // Add this class definition within the DaminionOllamaApp.ViewModels namespace,
    // or in a Models file and add the appropriate using statement.
    // For simplicity here, placing it in the ViewModel file.
    public class QueryTypeDisplayItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string QueryType { get; set; } = string.Empty; // Added missing property
        public string QueryLine { get; set; } = string.Empty;
        public string Operators { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }
}
