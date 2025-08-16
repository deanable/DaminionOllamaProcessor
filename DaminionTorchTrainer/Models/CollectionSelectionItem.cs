using System;

namespace DaminionTorchTrainer.Models
{
    /// <summary>
    /// Represents a collection selection item for the UI ComboBox
    /// </summary>
    public class CollectionSelectionItem
    {
        /// <summary>
        /// Gets or sets the display text for the collection
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the collection ID value
        /// </summary>
        public long Value { get; set; }

        /// <summary>
        /// Gets or sets the collection GUID
        /// </summary>
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional description or metadata
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the CollectionSelectionItem class
        /// </summary>
        public CollectionSelectionItem() { }

        /// <summary>
        /// Initializes a new instance of the CollectionSelectionItem class with specified values
        /// </summary>
        /// <param name="text">Display text</param>
        /// <param name="value">Collection ID</param>
        /// <param name="guid">Collection GUID</param>
        /// <param name="description">Optional description</param>
        public CollectionSelectionItem(string text, long value, string guid, string description = "")
        {
            Text = text;
            Value = value;
            Guid = guid;
            Description = description;
        }

        /// <summary>
        /// Returns the display text for the ComboBox
        /// </summary>
        /// <returns>The display text</returns>
        public override string ToString()
        {
            return Text;
        }
    }
}
