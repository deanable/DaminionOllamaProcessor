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
    /// Main ViewModel for the Daminion Image Tagger application
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

        public MainViewModel()
        {
            _predictionService = new OnnxPredictionService();

            // Initialize commands
            BrowseImageCommand = new RelayCommand(BrowseImage);
            BrowseModelCommand = new RelayCommand(BrowseModel);
            PredictCommand = new RelayCommand(PredictImage, () => CanPredict());
            LoadModelCommand = new RelayCommand(LoadModel, () => CanLoadModel());
        }

        #region Properties

        public string SelectedImagePath
        {
            get => _selectedImagePath;
            set => SetProperty(ref _selectedImagePath, value);
        }

        public string SelectedModelPath
        {
            get => _selectedModelPath;
            set => SetProperty(ref _selectedModelPath, value);
        }

        public ImagePrediction? CurrentPrediction
        {
            get => _currentPrediction;
            set => SetProperty(ref _currentPrediction, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsModelLoaded
        {
            get => _isModelLoaded;
            set => SetProperty(ref _isModelLoaded, value);
        }

        public bool IsPredicting
        {
            get => _isPredicting;
            set => SetProperty(ref _isPredicting, value);
        }

        #endregion

        #region Commands

        public ICommand BrowseImageCommand { get; }
        public ICommand BrowseModelCommand { get; }
        public ICommand PredictCommand { get; }
        public ICommand LoadModelCommand { get; }

        #endregion

        #region Command Implementations

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

        private bool CanPredict()
        {
            return IsModelLoaded && !string.IsNullOrEmpty(SelectedImagePath) && File.Exists(SelectedImagePath) && !IsPredicting;
        }

        private bool CanLoadModel()
        {
            return !string.IsNullOrEmpty(SelectedModelPath) && File.Exists(SelectedModelPath);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
    /// Simple relay command implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();
    }
}
