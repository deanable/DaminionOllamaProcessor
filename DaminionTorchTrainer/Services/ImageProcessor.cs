using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TorchSharp;
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

            // Load pre-trained ResNet50 model using the fully qualified name
            var resnet = TorchSharp.torchvision.models.resnet50(weights: TorchSharp.torchvision.models.ResNet50_Weights.IMAGENET1K_V2);

            var modules = resnet.named_modules().ToList();
            var feature_extractor_modules = modules
                .Where(m => m.name != "fc")
                .Select(m => m.module)
                .ToArray();

            _model = Sequential(feature_extractor_modules);

            _model.to(_device);
            _model.eval();

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
        private Task<Tensor> ImageToTensorAsync(string imagePath)
        {
            return Task.Run(() =>
            {
                using var image = new Bitmap(imagePath);
                // Resize to 224x224
                using var resized = new Bitmap(image, new Size(224, 224));

                var rect = new Rectangle(0, 0, resized.Width, resized.Height);
                BitmapData bmpData = resized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                try
                {
                    IntPtr ptr = bmpData.Scan0;
                    int bytes = Math.Abs(bmpData.Stride) * resized.Height;
                    byte[] rgbValues = new byte[bytes];

                    Marshal.Copy(ptr, rgbValues, 0, bytes);

                    // The byte array is in BGR order. We need to convert it to RGB for the model.
                    byte[] rgbCorrected = new byte[bytes];
                    for (int i = 0; i < rgbValues.Length; i += 3)
                    {
                        rgbCorrected[i] = rgbValues[i + 2];     // R
                        rgbCorrected[i + 1] = rgbValues[i + 1]; // G
                        rgbCorrected[i + 2] = rgbValues[i];     // B
                    }

                    var tensor = torch.from_buffer(rgbCorrected, new long[] { resized.Height, resized.Width, 3 }, ScalarType.Byte);
                    tensor = tensor.permute(2, 0, 1);
                    tensor = tensor.to(ScalarType.Float32).div(255);

                    var mean = new[] { 0.485f, 0.456f, 0.406f };
                    var std = new[] { 0.229f, 0.224f, 0.225f };
                    tensor = TorchSharp.torchvision.transforms.functional.normalize(tensor, mean, std);

                    return tensor.unsqueeze(0);
                }
                finally
                {
                    resized.UnlockBits(bmpData);
                }
            });
        }

        public void Dispose()
        {
            _model?.Dispose();
        }
    }
}
