using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using static TorchSharp.torchvision;

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

            // Load pre-trained ResNet50 model using the correct static accessor
            var resnet = models.resnet50(weights: models.ResNet50_Weights.IMAGENET1K_V2);

            // Get all layers except the final fully connected layer (fc)
            var modules = resnet.named_modules().ToList();
            var feature_extractor_modules = modules
                .Where(m => m.name != "fc")
                .Select(m => m.module)
                .ToArray();

            _model = Sequential(feature_extractor_modules);

            _model.to(_device);
            _model.eval();

            // For ResNet50, the output of the layer before 'fc' is 2048
            FeatureDimension = 2048;

            Log.Information("ImageProcessor initialized with ResNet50 on device {Device}. Feature dimension: {FeatureDimension}", _device, FeatureDimension);
        }

        /// <summary>
        /// Extracts high-level visual features from an image using the pre-trained model.
        /// </summary>
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
                var tensor = await ImageToTensorAsync(imagePath);
                tensor = tensor.to(_device);

                var features = _model.forward(tensor);

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
        /// Loads an image, converts it to a tensor, and preprocesses it.
        /// </summary>
        private async Task<Tensor> ImageToTensorAsync(string imagePath)
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(224, 224),
                Mode = ResizeMode.Crop
            }));

            // Ensure image is in RGB format
            var rgbImage = image.CloneAs<Rgb24>();

            var memory = new Memory<byte>(new byte[rgbImage.Width * rgbImage.Height * 3]);
            rgbImage.CopyPixelDataTo(memory.Span);

            // Create tensor from byte array
            var tensor = torch.from_buffer(memory, new long[] { rgbImage.Height, rgbImage.Width, 3 }, ScalarType.Byte);

            // Permute dimensions from HWC to CHW
            tensor = tensor.permute(2, 0, 1);

            // Convert to float and scale to [0, 1]
            tensor = tensor.to(ScalarType.Float32).div(255);

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
