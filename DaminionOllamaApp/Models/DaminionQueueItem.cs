// DaminionOllamaApp/Models/DaminionQueueItem.cs
using System.ComponentModel;
using System.IO;

namespace DaminionOllamaApp.Models
{
    // We can reuse the ProcessingStatus enum from FileQueueItem.cs
    // If it's not accessible due to namespace/file structure, you might need to move it
    // to a more common location or redeclare it here. For now, assume it's accessible.

    public class DaminionQueueItem : INotifyPropertyChanged
    {
        private long _daminionItemId;
        private string _filePath = string.Empty;
        private string _fileName = string.Empty; // Can be Daminion item name or file name
        private ProcessingStatus _status = ProcessingStatus.Unprocessed;
        private string _statusMessage = string.Empty;

        // Alias property for compatibility with existing code
        public long Id => DaminionItemId;

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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public DaminionQueueItem(long daminionId, string initialName = "Loading...")
        {
            DaminionItemId = daminionId;
            FileName = initialName; // Initially set to item ID or a placeholder
            Status = ProcessingStatus.Unprocessed;
            StatusMessage = "Awaiting path information.";
        }
    }
}