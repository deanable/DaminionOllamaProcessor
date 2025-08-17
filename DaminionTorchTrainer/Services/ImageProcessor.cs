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
using TorchVision;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace DaminionTorchTrainer.Services
{
    public class ImageProcessor : IDisposable
    {
        private readonly Module<Tensor, Tensor> _model;
        private readonly Device _device;
        public int FeatureDimension { get; }

        public ImageProcessor(DeviceType deviceType = DeviceType.CPU)
        {
            _device = torch.device(deviceType);

            var resnet = vision.models.resnet50(weights: vision.models.ResNet50_Weights.IMAGENET1K_V2);

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

        private async Task<Tensor> ImageToTensorAsync(string imagePath)
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(224, 224),
                Mode = ResizeMode.Crop
            }));

            var rgbImage = image.CloneAs<Rgb24>();

            var memory = new Memory<byte>(new byte[rgbImage.Width * rgbImage.Height * 3]);
            rgbImage.CopyPixelDataTo(memory.Span);

            var tensor = torch.tensor(memory.ToArray(), new long[] { rgbImage.Height, rgbImage.Width, 3 });
            tensor = tensor.permute(2, 0, 1);
            tensor = tensor.to(ScalarType.Float32).div(255);

            var mean = new[] { 0.485f, 0.456f, 0.406f };
            var std = new[] { 0.229f, 0.224f, 0.225f };
            tensor = vision.transforms.functional.normalize(tensor, mean, std);

            return tensor.unsqueeze(0);
        }

        public void Dispose()
        {
            _model?.Dispose();
        }
    }
}
