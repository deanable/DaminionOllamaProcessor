using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using TorchSharp;
using TorchSharp.torchvision;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace DaminionTorchTrainer.Services
{
    /// <summary>
    /// Service for processing images and extracting features using a pre-trained model.
    /// </summary>
    public class ImageProcessor : IDisposable
    {
        private readonly Module<Tensor, Tensor> _model;
        private readonly Device _device;
        public int FeatureDimension { get; }

        public ImageProcessor(DeviceType deviceType = DeviceType.CPU)
        {
            _device = torch.device(deviceType);

            // Load pre-trained ResNet50 model
            var resnet = models.resnet50(weights: models.ResNet50_Weights.IMAGENET1K_V2);

            // Remove the final fully connected layer to get feature embeddings
            var layers = resnet.modules().ToList();
            _model = Sequential(layers.Take(layers.Count - 1).ToArray());

            _model.to(_device);
            _model.eval();

            // Determine the feature dimension from the model
            // For ResNet50, the output of the layer before the fc is (batch_size, 2048, 1, 1)
            FeatureDimension = 2048;

            Log.Information("ImageProcessor initialized with ResNet50 on device {Device}. Feature dimension: {FeatureDimension}", _device, FeatureDimension);
        }

        /// <summary>
        /// Extracts high-level visual features from an image using the pre-trained model.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <returns>A list of floats representing the feature vector.</returns>
        public async Task<List<float>?> ExtractFeaturesAsync(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                Log.Warning("Image file not found: {ImagePath}", imagePath);
                return null;
            }

            try
            {
                using var scope = torch.no_grad();
                var tensor = await PreprocessImageAsync(imagePath);
                tensor = tensor.to(_device);

                var features = _model.forward(tensor);

                // Squeeze the spatial dimensions (H, W) and move to CPU
                features = features.squeeze(-1).squeeze(-1).cpu();

                return features.data<float>().ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to extract features from {ImagePath}", imagePath);
                return null;
            }
        }

        /// <summary>
        /// Preprocesses an image to the format expected by the ResNet model.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <returns>A tensor representing the preprocessed image.</returns>
        private async Task<Tensor> PreprocessImageAsync(string imagePath)
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);

            // Resize to 224x224, the standard input size for ResNet
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(224, 224),
                Mode = ResizeMode.Crop
            }));

            // Convert to tensor
            var tensor = image.ToTensor();

            // Normalize with ImageNet mean and std
            var mean = new[] { 0.485f, 0.456f, 0.406f };
            var std = new[] { 0.229f, 0.224f, 0.225f };
            tensor = transforms.functional.normalize(tensor, mean, std);

            // Add batch dimension
            return tensor.unsqueeze(0);
        }

        public void Dispose()
        {
            _model?.Dispose();
        }
    }
}
