// DaminionOllamaApp/Models/FileQueueItem.cs
using System.ComponentModel;
using System.IO; // Required for Path
using System.Runtime.CompilerServices; // Required for CallerMemberName

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
        private long? _daminionItemId; // <-- NEW PROPERTY

        public long? DaminionItemId // <-- NEW PROPERTY
        {
            get => _daminionItemId;
            set { SetProperty(ref _daminionItemId, value); }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value))
                {
                    // Only update FileName from FilePath if FileName wasn't explicitly set by a constructor that takes fileName
                    if (string.IsNullOrEmpty(_fileName) && !string.IsNullOrEmpty(_filePath))
                    {
                        FileName = Path.GetFileName(_filePath); // FileName setter will call OnPropertyChanged
                    }
                }
            }
        }

        public string FileName
        {
            get => _fileName;
            // Allow public set for cases where filename might be different from Path.GetFileName (e.g. Daminion title)
            set { SetProperty(ref _fileName, value); }
        }

        public ProcessingStatus Status
        {
            get => _status;
            set { SetProperty(ref _status, value); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { SetProperty(ref _statusMessage, value); }
        }

        // --- NEW READ-ONLY PROPERTY ---
        public string DisplayIdentifier
        {
            get
            {
                if (DaminionItemId.HasValue)
                {
                    return $"Daminion ID: {DaminionItemId.Value}";
                }
                return FilePath; // Or Path.GetFileName(FilePath) if you prefer just the name as fallback
            }
        }
        // --- END NEW READ-ONLY PROPERTY ---

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Constructor for local files
        public FileQueueItem(string filePath)
        {
            FilePath = filePath; // Sets _filePath and calls OnPropertyChanged for FilePath
                                 // FileName is set by FilePath setter if _fileName is empty
            if (string.IsNullOrEmpty(_fileName) && !string.IsNullOrEmpty(filePath)) // Explicitly ensure FileName is set if not already
            {
                FileName = Path.GetFileName(filePath);
            }
            Status = ProcessingStatus.Unprocessed;
            StatusMessage = string.Empty;
        }

        // Constructor for Daminion items (includes Daminion ID and allows specific initial name)
        public FileQueueItem(string filePath, string initialFileName, long daminionId)
        {
            DaminionItemId = daminionId;
            FilePath = filePath; // Sets _filePath and calls OnPropertyChanged for FilePath
            FileName = initialFileName; // Explicitly set FileName
            Status = ProcessingStatus.Unprocessed;
            StatusMessage = string.Empty;
        }

        public FileQueueItem() { } // Parameterless for XAML design-time if needed/used
    }
}