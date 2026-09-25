using System.IO;
using System.Security.Cryptography;
using ImageMagick;
using Ornare.Core;

namespace Ornare.Imaging;

public sealed class ImagePipeline : IImagePipeline
{
    private static readonly int[] IcoFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    public ImagePipeline()
    {
        ImageSecurityPolicy.Initialize();
    }

    public bool ValidateImageFormat(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            MagickImageInfo info = new(filePath);
            return ImageSecurityPolicy.IsFormatAllowed(info.Format);
        }
        catch
        {
            return false;
        }
    }

    public OperationResult<NormalizedImageResult> ProcessImage(string inputPath, string outputIcoPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.InvalidTarget, "Input image file does not exist.");
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(inputPath);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.FilesystemError, $"Failed to read input image: {ex.Message}");
        }

        return ProcessImageBytes(bytes, outputIcoPath);
    }

    public OperationResult<NormalizedImageResult> ProcessImageBytes(byte[] imageBytes, string outputIcoPath)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.InvalidTarget, "Image payload is empty.");
        }

        // 1. Validate header / metadata without full decoding
        MagickImageInfo info;
        try
        {
            info = new MagickImageInfo(imageBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.ImageCorrupted, $"Malformed image header: {ex.Message}");
        }

        if (!ImageSecurityPolicy.IsFormatAllowed(info.Format))
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.UnsupportedImageFormat, $"Image format '{info.Format}' is not allowed.");
        }

        if (info.Width > 16384 || info.Height > 16384 || ((long)info.Width * info.Height) > 100_000_000)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.ImageExceedsLimits, "Image dimensions exceed configured security boundaries.");
        }

        // 2. Decode and normalize
        try
        {
            using MagickImage sourceImage = new(imageBytes);

            sourceImage.AutoOrient();
            sourceImage.ColorSpace = ColorSpace.sRGB;

            // Compute deterministic content hash from source bytes
            string contentHash = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();

            // Contain layout: square transparent canvas of max dimension
            uint maxDim = Math.Max(sourceImage.Width, sourceImage.Height);
            using MagickImage squareMaster = new(MagickColors.Transparent, maxDim, maxDim);
            squareMaster.Format = MagickFormat.Png;
            squareMaster.ColorSpace = ColorSpace.sRGB;

            int offsetX = (int)(maxDim - sourceImage.Width) / 2;
            int offsetY = (int)(maxDim - sourceImage.Height) / 2;
            squareMaster.Composite(sourceImage, offsetX, offsetY, CompositeOperator.Over);

            // 3. Multi-resolution ICO generation (INV-IMG-003: every frame directly from normalized source)
            using MagickImageCollection icoFrames = new();
            foreach (int size in IcoFrameSizes)
            {
                MagickImage frame = (MagickImage)squareMaster.Clone();
                frame.FilterType = FilterType.Lanczos;
                frame.Resize((uint)size, (uint)size);
                frame.Format = MagickFormat.Ico;
                icoFrames.Add(frame);
            }

            string? dir = Path.GetDirectoryName(outputIcoPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            icoFrames.Write(outputIcoPath, MagickFormat.Ico);

            NormalizedImageResult result = new()
            {
                ContentHash = contentHash,
                SourceWidth = (int)info.Width,
                SourceHeight = (int)info.Height,
                SourceFormat = info.Format.ToString(),
                GeneratedIcoPath = Path.GetFullPath(outputIcoPath),
                FrameSizes = IcoFrameSizes
            };

            return OperationResult.Success(result);
        }
        catch (MagickException mex)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.ImageDecodeFailed, $"Image processing failed: {mex.Message}");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.IcoGenerationFailed, $"Failed to generate ICO: {ex.Message}");
        }
    }
}
