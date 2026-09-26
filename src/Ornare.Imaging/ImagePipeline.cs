using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<string> ComputeSourceHashStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            sha256.AppendData(buffer, 0, bytesRead);
        }
        return Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
    }

    public string ComputeSourceHash(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            sha256.AppendData(buffer, 0, bytesRead);
        }
        return Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
    }

    public string ComputeSourceHash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public bool IsValidIco(string path, int? expectedMinFrames = null, long? expectedSize = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            FileInfo fi = new(path);
            if (fi.Length < 22)
            {
                return false;
            }

            if (expectedSize.HasValue && expectedSize.Value > 0 && fi.Length != expectedSize.Value)
            {
                return false;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[6];
            if (fs.Read(header, 0, 6) < 6)
            {
                return false;
            }

            if (header[0] != 0 || header[1] != 0 || header[2] != 1 || header[3] != 0)
            {
                return false;
            }

            int frameCount = header[4] | (header[5] << 8);
            if (frameCount <= 0)
            {
                return false;
            }

            if (expectedMinFrames.HasValue && frameCount < expectedMinFrames.Value)
            {
                return false;
            }

            return true;
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

        string contentHash = ComputeSourceHash(imageBytes);
        RenderRecipe defaultRecipe = new()
        {
            SourceAssetHash = contentHash,
            PlacementMode = 0,
            TargetFormat = "ico",
            TargetFrameSizes = IcoFrameSizes
        };

        return ProcessRecipe(defaultRecipe, imageBytes, outputIcoPath);
    }

    public OperationResult<NormalizedImageResult> ProcessRecipe(
        RenderRecipe recipe,
        byte[]? sourceBytes,
        string outputIcoPath,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null,
        Action<string>? cacheInvalidate = null,
        string? cacheDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        string renderKey = recipe.ComputeRenderKey();

        // 1. Fast cache lookup before decoding
        if (cacheLookup != null)
        {
            var cachedEntry = cacheLookup(renderKey);
            if (cachedEntry != null && !string.IsNullOrEmpty(cachedEntry.CanonicalIcoPath))
            {
                if (IsValidIco(cachedEntry.CanonicalIcoPath, expectedMinFrames: 1, expectedSize: cachedEntry.FileSizeBytes))
                {
                    try
                    {
                        string? outDir = Path.GetDirectoryName(outputIcoPath);
                        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                        {
                            Directory.CreateDirectory(outDir);
                        }

                        if (!string.Equals(Path.GetFullPath(cachedEntry.CanonicalIcoPath), Path.GetFullPath(outputIcoPath), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(cachedEntry.CanonicalIcoPath, outputIcoPath, overwrite: true);
                        }

                        int[] parsedFrames = cachedEntry.FrameSizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(s => int.TryParse(s, out int v) ? v : 0)
                            .Where(v => v > 0)
                            .ToArray();
                        if (parsedFrames.Length == 0)
                        {
                            parsedFrames = IcoFrameSizes;
                        }

                        return OperationResult.Success(new NormalizedImageResult
                        {
                            ContentHash = cachedEntry.SourceAssetHash,
                            SourceWidth = 256,
                            SourceHeight = 256,
                            SourceFormat = "ico",
                            GeneratedIcoPath = Path.GetFullPath(outputIcoPath),
                            FrameSizes = parsedFrames,
                            RenderKey = renderKey,
                            IsFromCache = true
                        });
                    }
                    catch
                    {
                        // Proceed to render on cache read/copy failure
                    }
                }
                else
                {
                    cacheInvalidate?.Invoke(renderKey);
                    try
                    {
                        if (File.Exists(cachedEntry.CanonicalIcoPath))
                        {
                            File.Delete(cachedEntry.CanonicalIcoPath);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Cache miss: require valid source bytes
        if (sourceBytes == null || sourceBytes.Length == 0)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.InvalidTarget, "Cannot render recipe without source bytes on cache miss.");
        }

        var renderResult = RenderImageBytesCore(sourceBytes, outputIcoPath, renderKey);
        if (!renderResult.IsSuccess || renderResult.Value == null)
        {
            return renderResult;
        }

        var result = renderResult.Value;

        // Atomically publish to cache directory if specified
        if (!string.IsNullOrEmpty(cacheDirectory) && File.Exists(outputIcoPath))
        {
            try
            {
                Directory.CreateDirectory(cacheDirectory);
                string canonicalCacheFile = Path.Combine(cacheDirectory, $"{renderKey}.ico");
                string tempFile = Path.Combine(cacheDirectory, $"tmp_{Guid.NewGuid():N}.tmp");

                try
                {
                    File.Copy(outputIcoPath, tempFile, overwrite: true);

                    if (IsValidIco(tempFile, expectedMinFrames: 1))
                    {
                        try
                        {
                            File.Move(tempFile, canonicalCacheFile, overwrite: true);
                        }
                        catch (IOException)
                        {
                            // Race condition: another thread published first. Winner stands.
                        }

                        FileInfo fi = new(canonicalCacheFile);
                        RenderCacheEntry entry = new()
                        {
                            RenderKey = renderKey,
                            RecipeVersion = recipe.RecipeVersion,
                            RecipeJson = string.Empty,
                            SourceAssetHash = result.ContentHash,
                            CanonicalIcoPath = canonicalCacheFile,
                            FrameSizes = string.Join(",", result.FrameSizes),
                            FileSizeBytes = fi.Length,
                            CreatedAt = DateTimeOffset.UtcNow,
                            LastUsedAt = DateTimeOffset.UtcNow
                        };
                        cacheStore?.Invoke(entry);
                    }
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
                // Non-critical cache publishing failure
            }
        }

        return OperationResult.Success(result);
    }

    private OperationResult<NormalizedImageResult> RenderImageBytesCore(byte[] imageBytes, string outputIcoPath, string renderKey)
    {
        bool isIco = imageBytes.Length >= 4 && imageBytes[0] == 0 && imageBytes[1] == 0 && imageBytes[2] == 1 && imageBytes[3] == 0;
        MagickImageInfo info;
        try
        {
            info = isIco
                ? new MagickImageInfo(imageBytes, new MagickReadSettings { Format = MagickFormat.Ico })
                : new MagickImageInfo(imageBytes);
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

        try
        {
            using MagickImage sourceImage = isIco
                ? new(imageBytes, new MagickReadSettings { Format = MagickFormat.Ico })
                : new(imageBytes);

            sourceImage.AutoOrient();
            sourceImage.ColorSpace = ColorSpace.sRGB;

            string contentHash = ComputeSourceHash(imageBytes);

            uint maxDim = Math.Max(sourceImage.Width, sourceImage.Height);
            using MagickImage squareMaster = new(MagickColors.Transparent, maxDim, maxDim);
            squareMaster.Format = MagickFormat.Png;
            squareMaster.ColorSpace = ColorSpace.sRGB;

            int offsetX = (int)(maxDim - sourceImage.Width) / 2;
            int offsetY = (int)(maxDim - sourceImage.Height) / 2;
            squareMaster.Composite(sourceImage, offsetX, offsetY, CompositeOperator.Over);

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
                FrameSizes = IcoFrameSizes,
                RenderKey = renderKey,
                IsFromCache = false
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
