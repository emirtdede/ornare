using Ornare.Core;

namespace Ornare.Imaging;

public interface IImagePipeline
{
    bool ValidateImageFormat(string filePath);
    OperationResult<NormalizedImageResult> ProcessImage(string inputPath, string outputIcoPath);
    OperationResult<NormalizedImageResult> ProcessImageBytes(byte[] imageBytes, string outputIcoPath);
}
