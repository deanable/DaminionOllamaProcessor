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
    /// <summary>
    /// Represents a display item for a query type, including its display name, query type, query line, and supported operators.
    /// </summary>
    public class QueryTypeDisplayItem
    {
        /// <summary>
        /// Gets or sets the display name for the query type (shown in UI dropdowns, etc.).
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the internal query type identifier.
        /// </summary>
        public string QueryType { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the query line or example for this query type.
        /// </summary>
        public string QueryLine { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the supported operators for this query type.
        /// </summary>
        public string Operators { get; set; } = string.Empty;
        /// <summary>
        /// Returns the display name for this item.
        /// </summary>
        public override string ToString() => DisplayName;
    }
}
