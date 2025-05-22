// DaminionOllamaApp/Models/FileQueueItem.cs
using System.ComponentModel;
using System.IO; // Required for Path

namespace DaminionOllamaApp.Models
{
    public enum ProcessingStatus
    {
        Unprocessed,
        Queued,
        Processing,
        Processed,
        Error,
        Cancelled
    }

    public class FileQueueItem : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private string _fileName = string.Empty;
        private ProcessingStatus _status = ProcessingStatus.Unprocessed;
        private string _statusMessage = string.Empty;

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    FileName = Path.GetFileName(_filePath); // Automatically update FileName
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        public string FileName // Read-only from the perspective of a direct set, updated by FilePath
        {
            get => _fileName;
            private set // Private setter so it's only changed when FilePath changes
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

        public string StatusMessage // Used for more details, especially for errors
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

        // Constructor
        public FileQueueItem(string filePath)
        {
            FilePath = filePath; // This will also set FileName
            Status = ProcessingStatus.Unprocessed;
            StatusMessage = string.Empty; // Initialize status message
        }
        // Parameterless constructor for XAML design-time instantiation if needed (though typically not for item models)
        public FileQueueItem() { }
    }
}