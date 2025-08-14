# DaminionTorchTrainer - Dual Purpose Guide

## Overview

The DaminionTorchTrainer is now a **dual-purpose application** that can train neural networks on image metadata from two sources:

1. **Daminion API** - Extract metadata from Daminion DAM system
2. **Local Images** - Extract metadata from local image files

## Features

### 🔗 API Mode (Daminion)
- **Connection**: Connect to Daminion server with authentication
- **Search**: Use Daminion search queries and operators
- **Metadata**: Extract Categories and Keywords from Daminion tags
- **Scalability**: Process thousands of media items from catalog

### 📁 Local Mode (Local Images)
- **Folder Selection**: Browse and select local image folders
- **Metadata Extraction**: Read EXIF, IPTC, and XMP metadata
- **Subfolder Support**: Option to include subfolders
- **Format Support**: JPG, PNG, BMP, TIFF, GIF, WebP

### 🤖 Training Pipeline
- **TorchSharp**: Neural network training with GPU support
- **Real-time Monitoring**: Live training progress and metrics
- **ONNX Export**: Export models for ML.NET deployment
- **Configurable**: Learning rate, epochs, batch size, etc.

## Usage Guide

### Step 1: Select Data Source
- Choose between "Daminion API" or "Local Images" radio buttons
- UI will automatically show/hide relevant controls

### Step 2: Configure Data Source

#### For API Mode:
1. Enter Daminion server URL (default: http://localhost:8080)
2. Enter username and password
3. Click "Connect" to authenticate
4. (Optional) Enter search query and operators
5. Set maximum items to extract

#### For Local Mode:
1. Click "Browse" to select image folder
2. Check "Include Subfolders" if needed
3. Set maximum items to extract

### Step 3: Extract Training Data
- Click "Extract Data" button
- Application will process images and build vocabularies
- Status will show number of samples extracted

### Step 4: Configure Training
- Adjust learning rate, epochs, batch size
- Select device (CPU/CUDA)
- Configure model architecture
- Save/load training configurations

### Step 5: Start Training
- Click "Start Training" to begin
- Monitor progress in real-time
- Stop training anytime with "Stop Training"

### Step 6: Export Model
- Click "Export ONNX Model" to save for ML.NET
- Use "Open Export Folder" to locate saved models

## Technical Details

### Metadata Extraction

#### API Mode:
- Extracts Categories and Keywords from Daminion tag values
- Uses Daminion API endpoints for tag discovery
- Builds vocabularies from available tag values

#### Local Mode:
- Reads EXIF, IPTC, and XMP metadata using ImageMagick
- Extracts Categories from XMP `dc:type` field
- Extracts Keywords from IPTC `Keyword` and XMP `dc:subject` fields
- Extracts Description from multiple sources

### Feature Engineering

Both modes create feature vectors including:
- **Basic Features**: Width, height, file size
- **Format Encoding**: One-hot encoding of file formats
- **Category Encoding**: One-hot encoding of categories
- **Keyword Encoding**: One-hot encoding of keywords
- **Text Features**: Description analysis (length, word count, etc.)

### Model Architecture

- **Input**: Variable-length feature vectors
- **Hidden Layers**: Configurable (default: 256, 128, 64)
- **Output**: Multi-label classification
- **Activation**: ReLU with dropout regularization
- **Loss**: Cross-entropy for multi-label classification

## File Structure

```
DaminionTorchTrainer/
├── Models/
│   ├── TrainingData.cs          # Enhanced with data source tracking
│   └── TrainingConfig.cs        # Training configuration
├── Services/
│   ├── DaminionDataExtractor.cs # API data extraction (enhanced)
│   ├── LocalImageDataExtractor.cs # Local data extraction (new)
│   └── TorchSharpTrainer.cs     # Training service
├── ViewModels/
│   └── MainViewModel.cs         # Enhanced with dual-mode support
├── Converters/
│   ├── EnumToBooleanConverter.cs # Radio button binding
│   └── EnumToVisibilityConverter.cs # UI visibility control
└── MainWindow.xaml              # Dual-purpose UI
```

## Dependencies

- **TorchSharp**: Neural network training
- **Microsoft.ML.OnnxConverter**: ONNX model export
- **System.Drawing.Common**: Image dimension extraction
- **DaminionOllamaInteractionLib**: Daminion API integration
- **ImageMagick**: Local image metadata extraction

## Troubleshooting

### API Mode Issues:
- Verify Daminion server is running
- Check authentication credentials
- Ensure network connectivity
- Verify API endpoints are accessible

### Local Mode Issues:
- Ensure image files are readable
- Check folder permissions
- Verify supported image formats
- Monitor console output for metadata extraction errors

### Training Issues:
- Reduce batch size for memory constraints
- Use CPU if CUDA is not available
- Check training data quality and quantity
- Monitor console output for training progress

## Future Enhancements

- **Image Analysis**: Integrate computer vision features
- **Advanced Preprocessing**: More sophisticated text analysis
- **Hyperparameter Optimization**: Automated tuning
- **Model Evaluation**: Comprehensive metrics and validation
- **Batch Processing**: Process multiple datasets
- **Cloud Integration**: Support for cloud storage

## Support

For issues and questions:
1. Check the console output for detailed error messages
2. Verify all dependencies are properly installed
3. Ensure sufficient disk space for model export
4. Check system requirements for TorchSharp and CUDA
