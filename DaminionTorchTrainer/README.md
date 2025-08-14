# Daminion TorchSharp Trainer

A WPF application for training neural networks on Daminion metadata using TorchSharp and exporting to ONNX format.

## Overview

This application allows you to:
- Connect to a Daminion DAM system
- Extract metadata from media items in the catalog
- Train neural networks on the extracted metadata
- Export trained models in various formats (PyTorch, ONNX)
- Monitor training progress in real-time

## Features

### Data Extraction
- Connect to Daminion API
- Search and filter media items
- Extract metadata including:
  - File properties (size, dimensions, format)
  - Tags and categories
  - Color labels and version control state
  - File paths and descriptions

### Model Training
- Configurable neural network architecture
- Support for different optimizers (Adam, SGD, AdamW)
- Multiple loss functions (CrossEntropy, MSE, BCE)
- Early stopping and validation
- Real-time training progress monitoring
- GPU acceleration support (CUDA)

### Model Export
- PyTorch model format (.pt)
- ONNX format for cross-platform deployment
- Training configuration and dataset information
- Timestamped model versions

## Requirements

- .NET 8.0
- Windows 10/11
- Daminion DAM system with API access
- Optional: CUDA-capable GPU for accelerated training

## Installation

1. Build the solution:
   ```bash
   dotnet build
   ```

2. Run the application:
   ```bash
   dotnet run --project DaminionTorchTrainer
   ```

## Usage

### 1. Connect to Daminion
- Enter the Daminion server URL (default: http://localhost:8080)
- Click "Connect" to establish connection

### 2. Extract Training Data
- Enter search query and operators (optional)
- Set maximum number of items to extract
- Click "Extract Data" to fetch metadata from Daminion

### 3. Configure Training
- Set learning rate, epochs, and batch size
- Choose device (CPU/CUDA)
- Configure model architecture
- Save/load training configurations

### 4. Start Training
- Click "Start Training" to begin model training
- Monitor progress in real-time
- Stop training at any time with "Stop Training"

### 5. Export Models
- Trained models are automatically saved
- Models include configuration and dataset information
- Export formats: PyTorch (.pt) and ONNX (.onnx)

## Configuration

### Training Parameters
- **Learning Rate**: Step size for gradient descent (default: 0.001)
- **Epochs**: Number of training iterations (default: 100)
- **Batch Size**: Samples per training batch (default: 32)
- **Validation Split**: Percentage for validation data (default: 0.2)

### Model Architecture
- **Feature Dimension**: Input feature size (default: 128)
- **Hidden Dimensions**: Layer sizes [256, 128, 64]
- **Output Dimension**: Number of output classes (default: 10)
- **Dropout Rate**: Regularization strength (default: 0.2)

### Advanced Options
- **Early Stopping**: Stop training when validation loss plateaus
- **Weight Decay**: L2 regularization strength
- **Device**: Training device (CPU/CUDA)

## File Structure

```
DaminionTorchTrainer/
├── Models/
│   ├── TrainingData.cs          # Training data models
│   └── TrainingConfig.cs        # Configuration models
├── Services/
│   ├── DaminionDataExtractor.cs # Data extraction service
│   └── TorchSharpTrainer.cs     # Training service
├── ViewModels/
│   └── MainViewModel.cs         # Main application logic
├── Views/
│   └── MainWindow.xaml          # User interface
└── README.md                    # This file
```

## Dependencies

- **TorchSharp**: PyTorch for .NET
- **Microsoft.ML.OnnxConverter**: ONNX model conversion
- **Microsoft.ML.OnnxRuntime**: ONNX model inference
- **System.Text.Json**: JSON serialization
- **DaminionOllamaInteractionLib**: Daminion API integration

## Architecture

The application follows the MVVM (Model-View-ViewModel) pattern:

- **Models**: Data structures for training data and configuration
- **Services**: Business logic for data extraction and training
- **ViewModels**: Application state and command handling
- **Views**: User interface components

## Integration with Daminion

The application integrates with Daminion through the existing `DaminionOllamaInteractionLib`:

- Uses the same API client for consistency
- Extracts metadata from media items
- Supports search queries and filtering
- Maintains authentication and session management

## Training Data Format

Training data is extracted from Daminion metadata and includes:

```json
{
  "id": 12345,
  "fileName": "image.jpg",
  "mediaFormat": "image",
  "width": 1920,
  "height": 1080,
  "fileSize": 2048576,
  "formatType": "JPEG",
  "tags": ["nature", "landscape"],
  "categories": ["photography"],
  "features": [0.5, -0.2, 1.0, ...],
  "labels": [0, 1, 0, 0, ...]
}
```

## Model Output

Trained models are saved with:

- PyTorch model file (.pt)
- Training configuration (JSON)
- Dataset information (JSON)
- Timestamped directory structure

## Troubleshooting

### Connection Issues
- Verify Daminion server is running
- Check API URL and port
- Ensure network connectivity

### Training Issues
- Reduce batch size for memory constraints
- Use CPU if CUDA is not available
- Check training data quality and quantity

### Build Issues
- Ensure .NET 8.0 SDK is installed
- Restore NuGet packages: `dotnet restore`
- Clean and rebuild: `dotnet clean && dotnet build`

## Future Enhancements

- Support for more model architectures
- Hyperparameter optimization
- Model evaluation and metrics
- Batch processing capabilities
- Integration with other ML frameworks

## License

This project is part of the DaminionOllamaProcessor solution and follows the same licensing terms.

## Support

For issues and questions:
1. Check the troubleshooting section
2. Review the Daminion API documentation
3. Examine the console output for error messages
4. Verify TorchSharp installation and compatibility
