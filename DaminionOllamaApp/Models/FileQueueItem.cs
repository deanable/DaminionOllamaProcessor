// DaminionOllamaApp/Models/FileQueueItem.cs
using System.ComponentModel;
using System.IO; // Required for Path
using System.Runtime.CompilerServices; // Required for CallerMemberName
using System.Collections.Generic; // Required for EqualityComparer

namespace DaminionOllamaApp.Models
{
    /// <summary>
    /// Represents the processing status of a file in the queue.
    /// </summary>
    public enum ProcessingStatus
    {
        /// <summary>The file has not been processed yet.</summary>
        Unprocessed,
        /// <summary>The file is queued for processing.</summary>
        Queued,
        /// <summary>The file is currently being processed.</summary>
        Processing,
        /// <summary>The file has been processed but not yet completed.</summary>
        Processed,
        /// <summary>The file processing is complete.</summary>
        Completed,
        /// <summary>An error occurred during processing.</summary>
        Error,
        /// <summary>The processing was cancelled.</summary>
        Cancelled
    }

    /// <summary>
    /// Represents a file item in the processing queue, including its path, status, and related metadata.
    /// Implements INotifyPropertyChanged for data binding.
    /// </summary>
    public class FileQueueItem : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private string _fileName = string.Empty;
        private ProcessingStatus _status = ProcessingStatus.Unprocessed;
        private string _statusMessage = string.Empty;
        private long? _daminionItemId; // <-- NEW PROPERTY
        private string _mimeType = "image/jpeg";

        /// <summary>
        /// Gets or sets the Daminion item ID if this file is associated with a Daminion catalog entry.
        /// </summary>
        public long? DaminionItemId
        {
            get => _daminionItemId;
            set { SetProperty(ref _daminionItemId, value); }
        }

        /// <summary>
        /// Gets or sets the full file path on disk.
        /// Setting this property will also update FileName if it was not explicitly set.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the file name. This may differ from the file path if set explicitly.
        /// </summary>
        public string FileName
        {
            get => _fileName;
            // Allow public set for cases where filename might be different from Path.GetFileName (e.g. Daminion title)
            set { SetProperty(ref _fileName, value); }
        }

        /// <summary>
        /// Gets or sets the current processing status of the file.
        /// </summary>
        public ProcessingStatus Status
        {
            get => _status;
            set { SetProperty(ref _status, value); }
        }

        /// <summary>
        /// Gets or sets a status message describing the current state or error.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { SetProperty(ref _statusMessage, value); }
        }

        /// <summary>
        /// Gets or sets the MIME type of the file (e.g., image/jpeg).
        /// </summary>
        public string MimeType
        {
            get => _mimeType;
            set { SetProperty(ref _mimeType, value); }
        }

        /// <summary>
        /// Gets a display identifier for the file, showing either the Daminion ID or the file path.
        /// </summary>
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

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Sets the property value and raises PropertyChanged if the value changes.
        /// </summary>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Constructor for local files. Initializes with the given file path and default values.
        /// </summary>
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
            MimeType = "image/jpeg";
        }

        /// <summary>
        /// Constructor for Daminion items. Initializes with file path, explicit file name, and Daminion ID.
        /// </summary>
        public FileQueueItem(string filePath, string initialFileName, long daminionId)
        {
            DaminionItemId = daminionId;
            FilePath = filePath; // Sets _filePath and calls OnPropertyChanged for FilePath
            FileName = initialFileName; // Explicitly set FileName
            Status = ProcessingStatus.Unprocessed;
            StatusMessage = string.Empty;
            MimeType = "image/jpeg";
        }

        /// <summary>
        /// Parameterless constructor for XAML design-time support.
        /// </summary>
        public FileQueueItem() { MimeType = "image/jpeg"; } // Parameterless for XAML design-time if needed/used
    }
}