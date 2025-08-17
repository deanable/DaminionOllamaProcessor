using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using DaminionImageTagger.Models;
using DaminionImageTagger.Services;

namespace DaminionImageTagger.ViewModels
{
    /// <summary>
    /// Main ViewModel for the Daminion Image Tagger application.
    /// This class manages the application's state and logic, including file selection,
    /// ONNX model loading, and image prediction.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly OnnxPredictionService _predictionService;
        private string _selectedImagePath = string.Empty;
        private string _selectedModelPath = string.Empty;
        private ImagePrediction? _currentPrediction;
        private string _status = "Ready";
        private bool _isModelLoaded = false;
        private bool _isPredicting = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel()
        {
            _predictionService = new OnnxPredictionService();

            // Initialize commands
            BrowseImageCommand = new RelayCommand(BrowseImage);
            BrowseModelCommand = new RelayCommand(BrowseModel);
            PredictCommand = new RelayCommand(PredictImage, CanPredict);
            LoadModelCommand = new RelayCommand(LoadModel, CanLoadModel);
        }

        #region Properties

        /// <summary>
        /// Gets or sets the path to the selected image file.
        /// </summary>
        public string SelectedImagePath
        {
            get => _selectedImagePath;
            set => SetProperty(ref _selectedImagePath, value);
        }

        /// <summary>
        /// Gets or sets the path to the selected ONNX model file.
        /// </summary>
        public string SelectedModelPath
        {
            get => _selectedModelPath;
            set => SetProperty(ref _selectedModelPath, value);
        }

        /// <summary>
        /// Gets or sets the current image prediction result.
        /// </summary>
        public ImagePrediction? CurrentPrediction
        {
            get => _currentPrediction;
            set => SetProperty(ref _currentPrediction, value);
        }

        /// <summary>
        /// Gets or sets the current status message to be displayed to the user.
        /// </summary>
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether an ONNX model is loaded.
        /// </summary>
        public bool IsModelLoaded
        {
            get => _isModelLoaded;
            set => SetProperty(ref _isModelLoaded, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether a prediction is currently in progress.
        /// </summary>
        public bool IsPredicting
        {
            get => _isPredicting;
            set => SetProperty(ref _isPredicting, value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to browse for an image file.
        /// </summary>
        public ICommand BrowseImageCommand { get; }
        /// <summary>
        /// Command to browse for an ONNX model file.
        /// </summary>
        public ICommand BrowseModelCommand { get; }
        /// <summary>
        /// Command to run a prediction on the selected image.
        /// </summary>
        public ICommand PredictCommand { get; }
        /// <summary>
        /// Command to load the selected ONNX model.
        /// </summary>
        public ICommand LoadModelCommand { get; }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Opens a file dialog to allow the user to select an image file.
        /// </summary>
        private void BrowseImage()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Image File",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif;*.gif;*.webp|All Files|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SelectedImagePath = openFileDialog.FileName;
                Status = $"Selected image: {Path.GetFileName(SelectedImagePath)}";
            }
        }

        /// <summary>
        /// Opens a file dialog to allow the user to select an ONNX model file.
        /// </summary>
        private void BrowseModel()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select ONNX Model File",
                Filter = "ONNX Files|*.onnx|All Files|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SelectedModelPath = openFileDialog.FileName;
                Status = $"Selected model: {Path.GetFileName(SelectedModelPath)}";
                IsModelLoaded = false; // Reset model loaded state
            }
        }

        /// <summary>
        /// Loads the ONNX model from the specified path.
        /// </summary>
        private void LoadModel()
        {
            try
            {
                Status = "Loading model...";
                IsModelLoaded = false;

                if (string.IsNullOrEmpty(SelectedModelPath))
                {
                    Status = "Please select a model file first.";
                    return;
                }

                if (!File.Exists(SelectedModelPath))
                {
                    Status = "Model file not found.";
                    return;
                }

                var success = _predictionService.LoadModel(SelectedModelPath);
                if (success)
                {
                    IsModelLoaded = true;
                    Status = $"Model loaded successfully. Categories: {_predictionService.CategoryCount}, Keywords: {_predictionService.KeywordCount}";
                }
                else
                {
                    Status = "Failed to load model.";
                }
            }
            catch (Exception ex)
            {
                Status = $"Error loading model: {ex.Message}";
            }
        }

        /// <summary>
        /// Runs a prediction on the selected image using the loaded ONNX model.
        /// </summary>
        private void PredictImage()
        {
            try
            {
                if (!IsModelLoaded)
                {
                    Status = "Please load a model first.";
                    return;
                }

                if (string.IsNullOrEmpty(SelectedImagePath))
                {
                    Status = "Please select an image file first.";
                    return;
                }

                if (!File.Exists(SelectedImagePath))
                {
                    Status = "Image file not found.";
                    return;
                }

                IsPredicting = true;
                Status = "Making prediction...";

                // Run prediction on background thread
                var prediction = _predictionService.PredictImage(SelectedImagePath);
                
                CurrentPrediction = prediction;
                Status = $"Prediction completed. Found {prediction.Categories.Count} categories and {prediction.Keywords.Count} keywords.";
            }
            catch (Exception ex)
            {
                Status = $"Error making prediction: {ex.Message}";
            }
            finally
            {
                IsPredicting = false;
            }
        }

        /// <summary>
        /// Determines whether the prediction command can be executed.
        /// </summary>
        /// <returns>True if a model is loaded, an image is selected, and no prediction is in progress; otherwise, false.</returns>
        private bool CanPredict()
        {
            return IsModelLoaded && !string.IsNullOrEmpty(SelectedImagePath) && File.Exists(SelectedImagePath) && !IsPredicting;
        }

        /// <summary>
        /// Determines whether the load model command can be executed.
        /// </summary>
        /// <returns>True if a model path is selected; otherwise, false.</returns>
        private bool CanLoadModel()
        {
            return !string.IsNullOrEmpty(SelectedModelPath) && File.Exists(SelectedModelPath);
        }

        #endregion

        #region INotifyPropertyChanged

        /// <summary>
        /// Event raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets the property value and raises the PropertyChanged event if the value has changed.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="field">The backing field of the property.</param>
        /// <param name="value">The new value of the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if the value was changed; otherwise, false.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// A simple command that relays its execution to delegates.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>true if this command can be executed; otherwise, false.</returns>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        public void Execute(object? parameter) => _execute();
    }
}
