// DaminionOllamaApp/Models/DaminionQueueItem.cs
using System.ComponentModel;
using System.IO;

namespace DaminionOllamaApp.Models
{
    // We can reuse the ProcessingStatus enum from FileQueueItem.cs
    // If it's not accessible due to namespace/file structure, you might need to move it
    // to a more common location or redeclare it here. For now, assume it's accessible.

    /// <summary>
    /// Represents an item in the Daminion processing queue, including its Daminion ID, file path, name, status, and status message.
    /// Implements INotifyPropertyChanged for data binding.
    /// </summary>
    public class DaminionQueueItem : INotifyPropertyChanged
    {
        private long _daminionItemId;
        private string _filePath = string.Empty;
        private string _fileName = string.Empty; // Can be Daminion item name or file name
        private ProcessingStatus _status = ProcessingStatus.Unprocessed;
        private string _statusMessage = string.Empty;

        /// <summary>
        /// Gets the Daminion item ID (alias for DaminionItemId).
        /// </summary>
        public long Id => DaminionItemId;

        /// <summary>
        /// Gets or sets the Daminion item ID associated with this queue item.
        /// </summary>
        public long DaminionItemId
        {
            get => _daminionItemId;
            set
            {
                if (_daminionItemId != value)
                {
                    _daminionItemId = value;
                    OnPropertyChanged(nameof(DaminionItemId));
                }
            }
        }

        /// <summary>
        /// Gets or sets the file path for this item. Setting this will also update FileName if not set.
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    if (string.IsNullOrEmpty(_fileName) && !string.IsNullOrEmpty(_filePath))
                    {
                        FileName = Path.GetFileName(_filePath);
                    }
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        /// <summary>
        /// Gets or sets the file name (could be Daminion item's title or filename).
        /// </summary>
        public string FileName // Could be Daminion item's title or filename
        {
            get => _fileName;
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        /// <summary>
        /// Gets or sets the current processing status of the item.
        /// </summary>
        public ProcessingStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        /// <summary>
        /// Gets or sets a status message describing the current state or error.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Constructor for a Daminion queue item. Initializes with the given Daminion ID and optional initial name.
        /// </summary>
        /// <param name="daminionId">The Daminion item ID.</param>
        /// <param name="initialName">The initial name or placeholder for the item.</param>
        public DaminionQueueItem(long daminionId, string initialName = "Loading...")
        {
            DaminionItemId = daminionId;
            FileName = initialName; // Initially set to item ID or a placeholder
            Status = ProcessingStatus.Unprocessed;
            StatusMessage = "Awaiting path information.";
        }
    }
}