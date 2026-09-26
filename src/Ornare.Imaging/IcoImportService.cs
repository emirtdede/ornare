using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ImageMagick;
using Ornare.Core;

namespace Ornare.Imaging;

/// <summary>
/// Authoritative ICO import validator and normalizer (F12).
/// Enforces strict container and security boundaries, fast-path pass-through for compliant ICOs,
/// and deterministic frame-selection normalization for partial or non-standard ICOs.
/// </summary>
public static class IcoImportService
{
    public const int MaxIcoFileSizeBytes = 20 * 1024 * 1024; // 20 MB
    public const int MaxFrameCount = 64;
    public const long MaxDecodedMemoryBytes = 50 * 1024 * 1024; // 50 MB
    public static readonly int[] RequiredSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    /// <summary>
    /// Validates raw ICO header and directory container bounds without full image decode.
    /// Rejects malformed headers, invalid magic, truncated tables, and frame bombs.
    /// </summary>
    public static OperationResult ValidateIcoContainer(byte[] icoBytes)
    {
        if (icoBytes == null || icoBytes.Length < 22)
        {
            return OperationResult.Failure(ErrorCode.ImageCorrupted, "ICO file payload is empty or too short (< 22 bytes).");
        }

        if (icoBytes.Length > MaxIcoFileSizeBytes)
        {
            return OperationResult.Failure(ErrorCode.ImageExceedsLimits, $"ICO file size ({icoBytes.Length} bytes) exceeds maximum limit of {MaxIcoFileSizeBytes} bytes.");
        }

        // Header: 2 bytes reserved (0), 2 bytes type (1 for ICO, 2 for CUR), 2 bytes count
        if (icoBytes[0] != 0 || icoBytes[1] != 0 || icoBytes[2] != 1 || icoBytes[3] != 0)
        {
            return OperationResult.Failure(ErrorCode.UnsupportedImageFormat, "Invalid ICO signature. File is not a valid Windows icon.");
        }

        int frameCount = icoBytes[4] | (icoBytes[5] << 8);
        if (frameCount <= 0)
        {
            return OperationResult.Failure(ErrorCode.ImageCorrupted, "ICO header specifies zero image frames.");
        }

        if (frameCount > MaxFrameCount)
        {
            return OperationResult.Failure(ErrorCode.ImageExceedsLimits, $"ICO frame count ({frameCount}) exceeds maximum safety limit ({MaxFrameCount}).");
        }

        long minExpectedHeaderLength = 6 + (long)frameCount * 16;
        if (icoBytes.Length < minExpectedHeaderLength)
        {
            return OperationResult.Failure(ErrorCode.ImageCorrupted, "ICO file is truncated; directory entries extend beyond file size.");
        }

        // Validate each 16-byte directory entry
        for (int i = 0; i < frameCount; i++)
        {
            int offset = 6 + i * 16;
            uint bytesInRes = BitConverter.ToUInt32(icoBytes, offset + 8);
            uint imageOffset = BitConverter.ToUInt32(icoBytes, offset + 12);

            if (imageOffset < minExpectedHeaderLength)
            {
                return OperationResult.Failure(ErrorCode.ImageCorrupted, $"Directory entry {i} offset ({imageOffset}) overlaps directory header.");
            }

            if (bytesInRes == 0)
            {
                return OperationResult.Failure(ErrorCode.ImageCorrupted, $"Directory entry {i} specifies zero image data bytes.");
            }

            if ((long)imageOffset + bytesInRes > icoBytes.Length)
            {
                return OperationResult.Failure(ErrorCode.ImageCorrupted, $"Directory entry {i} frame data ({imageOffset} + {bytesInRes}) extends beyond file boundary.");
            }
        }

        return OperationResult.Success();
    }

    /// <summary>
    /// Computes deterministic SHA-256 hash of raw input bytes.
    /// </summary>
    public static string ComputeSourceHash(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Imports an ICO file. Executes fast-path preservation for compliant multi-frame icons
    /// and deterministic frame-selection normalization when required.
    /// </summary>
    public static OperationResult<IcoImportResult> ImportIco(
        byte[] icoBytes,
        string outputIcoPath,
        string? cacheDirectory = null,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null)
    {
        var containerCheck = ValidateIcoContainer(icoBytes);
        if (!containerCheck.IsSuccess)
        {
            return OperationResult.Failure<IcoImportResult>(containerCheck.ErrorCode, containerCheck.ErrorMessage ?? "Malformed ICO container.");
        }

        string sha256 = ComputeSourceHash(icoBytes);

        // Decode all frames with Magick.NET to inspect geometry and content
        MagickImageCollection collection;
        try
        {
            var readSettings = new MagickReadSettings { Format = MagickFormat.Ico };
            collection = new MagickImageCollection(icoBytes, readSettings);
        }
        catch (MagickException mex)
        {
            return OperationResult.Failure<IcoImportResult>(ErrorCode.ImageDecodeFailed, $"Failed to decode ICO image frames: {mex.Message}");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<IcoImportResult>(ErrorCode.ImageCorrupted, $"Corrupted ICO data: {ex.Message}");
        }

        using (collection)
        {
            if (collection.Count == 0)
            {
                return OperationResult.Failure<IcoImportResult>(ErrorCode.ImageCorrupted, "No decodable frames found in ICO.");
            }

            // Memory limit check
            long totalMemory = 0;
            foreach (var frame in collection)
            {
                totalMemory += (long)frame.Width * frame.Height * 4;
            }

            if (totalMemory > MaxDecodedMemoryBytes)
            {
                return OperationResult.Failure<IcoImportResult>(ErrorCode.ImageExceedsLimits, $"Decoded ICO memory ({totalMemory} bytes) exceeds limit ({MaxDecodedMemoryBytes} bytes).");
            }

            // Check fast-path criteria:
            // 1. All frames square
            // 2. All dimensions 1..256
            // 3. Contains at least one valid image for EVERY required Ornare size (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
            bool allSquare = true;
            bool allDimensionsValid = true;
            HashSet<int> squareSizes = new();

            foreach (var frame in collection)
            {
                if (frame.Width != frame.Height)
                {
                    allSquare = false;
                }

                if (frame.Width < 1 || frame.Width > 256 || frame.Height < 1 || frame.Height > 256)
                {
                    allDimensionsValid = false;
                }

                if (frame.Width == frame.Height && frame.Width <= 256)
                {
                    squareSizes.Add((int)frame.Width);
                }
            }

            bool hasAllRequiredSizes = RequiredSizes.All(s => squareSizes.Contains(s));

            if (allSquare && allDimensionsValid && hasAllRequiredSizes)
            {
                // FAST PATH: compliant ICO preserved bit-for-bit
                return ExecuteFastPath(icoBytes, sha256, outputIcoPath, squareSizes.OrderBy(s => s).ToArray(), cacheDirectory, cacheLookup, cacheStore);
            }
            else
            {
                // NORMALIZATION PATH: deterministic frame selection
                return ExecuteNormalization(collection, icoBytes, sha256, outputIcoPath, cacheDirectory, cacheLookup, cacheStore);
            }
        }
    }

    private static OperationResult<IcoImportResult> ExecuteFastPath(
        byte[] originalIcoBytes,
        string sha256,
        string outputIcoPath,
        int[] availableSizes,
        string? cacheDirectory,
        Func<string, RenderCacheEntry?>? cacheLookup,
        Action<RenderCacheEntry>? cacheStore)
    {
        var recipe = new RenderRecipe
        {
            SourceAssetHash = sha256,
            NormalizationPolicyVersion = 1,
            TargetFormat = "ico",
            TargetFrameSizes = RequiredSizes
        };
        string renderKey = recipe.ComputeRenderKey();

        // Check cache
        if (cacheLookup != null)
        {
            var cached = cacheLookup(renderKey);
            if (cached != null && !string.IsNullOrEmpty(cached.CanonicalIcoPath) && File.Exists(cached.CanonicalIcoPath))
            {
                EnsureDirectory(outputIcoPath);
                if (!string.Equals(Path.GetFullPath(cached.CanonicalIcoPath), Path.GetFullPath(outputIcoPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(cached.CanonicalIcoPath, outputIcoPath, overwrite: true);
                }

                return OperationResult.Success(new IcoImportResult
                {
                    CanonicalHash = sha256,
                    CanonicalPath = Path.GetFullPath(outputIcoPath),
                    ImportKind = IcoImportKind.IcoCompliant,
                    AvailableSizes = availableSizes,
                    RenderKey = renderKey,
                    IsFastPath = true
                });
            }
        }

        // Write directly to output
        EnsureDirectory(outputIcoPath);
        File.WriteAllBytes(outputIcoPath, originalIcoBytes);

        // Publish to cache
        PublishToCache(outputIcoPath, sha256, renderKey, recipe.RecipeVersion, availableSizes, cacheDirectory, cacheStore);

        return OperationResult.Success(new IcoImportResult
        {
            CanonicalHash = sha256,
            CanonicalPath = Path.GetFullPath(outputIcoPath),
            ImportKind = IcoImportKind.IcoCompliant,
            AvailableSizes = availableSizes,
            RenderKey = renderKey,
            IsFastPath = true
        });
    }

    private static OperationResult<IcoImportResult> ExecuteNormalization(
        MagickImageCollection collection,
        byte[] originalIcoBytes,
        string sha256,
        string outputIcoPath,
        string? cacheDirectory,
        Func<string, RenderCacheEntry?>? cacheLookup,
        Action<RenderCacheEntry>? cacheStore)
    {
        var recipe = new RenderRecipe
        {
            SourceAssetHash = sha256,
            NormalizationPolicyVersion = 2, // Explicitly separate render key for derived normalized ICO
            TargetFormat = "ico",
            TargetFrameSizes = RequiredSizes
        };
        string renderKey = recipe.ComputeRenderKey();

        // Check cache
        if (cacheLookup != null)
        {
            var cached = cacheLookup(renderKey);
            if (cached != null && !string.IsNullOrEmpty(cached.CanonicalIcoPath) && File.Exists(cached.CanonicalIcoPath))
            {
                EnsureDirectory(outputIcoPath);
                if (!string.Equals(Path.GetFullPath(cached.CanonicalIcoPath), Path.GetFullPath(outputIcoPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(cached.CanonicalIcoPath, outputIcoPath, overwrite: true);
                }

                return OperationResult.Success(new IcoImportResult
                {
                    CanonicalHash = sha256,
                    CanonicalPath = Path.GetFullPath(outputIcoPath),
                    ImportKind = IcoImportKind.IcoNormalized,
                    AvailableSizes = RequiredSizes,
                    RenderKey = renderKey,
                    IsFastPath = false
                });
            }
        }

        // Filter valid candidate frames
        var candidates = collection
            .Where(f => f.Width >= 1 && f.Width <= 256 && f.Height >= 1 && f.Height <= 256)
            .ToList();

        if (candidates.Count == 0)
        {
            return OperationResult.Failure<IcoImportResult>(ErrorCode.ImageCorrupted, "No valid candidate frames found in ICO for normalization.");
        }

        using var normalizedCollection = new MagickImageCollection();

        foreach (int targetSize in RequiredSizes)
        {
            // Deterministic frame selection per Fixed Decision 3:
            // 1. Exact-size valid frame first
            // 2. Smallest valid frame whose dimension is greater than the target
            // 3. Largest valid frame
            IMagickImage<ushort> chosenSource;

            var exactMatch = candidates.FirstOrDefault(f => f.Width == targetSize && f.Height == targetSize);
            if (exactMatch != null)
            {
                chosenSource = exactMatch;
            }
            else
            {
                var largerCandidates = candidates.Where(f => Math.Max(f.Width, f.Height) > targetSize).ToList();
                if (largerCandidates.Count > 0)
                {
                    chosenSource = largerCandidates
                        .OrderBy(f => Math.Max(f.Width, f.Height))
                        .ThenByDescending(f => f.Depth)
                        .First();
                }
                else
                {
                    var smallerCandidates = candidates.Where(f => Math.Max(f.Width, f.Height) < targetSize).ToList();
                    chosenSource = smallerCandidates
                        .OrderByDescending(f => Math.Max(f.Width, f.Height))
                        .ThenByDescending(f => f.Depth)
                        .First();
                }
            }

            // Render independently from chosenSource; never resize from a previously generated frame
            using var subImage = chosenSource.Clone();
            subImage.ColorSpace = ColorSpace.sRGB;

            if (chosenSource.Width == targetSize && chosenSource.Height == targetSize)
            {
                // Bit-exact preservation of matching frame
                var nativeFrame = subImage.Clone();
                nativeFrame.ColorSpace = ColorSpace.sRGB;
                nativeFrame.Format = MagickFormat.Ico;
                normalizedCollection.Add(nativeFrame);
            }
            else
            {
                subImage.FilterType = FilterType.Lanczos;
                var geom = ImagePlacementCalculator.ComputePlacement(
                    (int)subImage.Width,
                    (int)subImage.Height,
                    targetSize,
                    targetSize,
                    ImagePlacementMode.Contain,
                    0);

                var resizeGeo = new MagickGeometry((uint)geom.DestWidth, (uint)geom.DestHeight) { IgnoreAspectRatio = true };
                subImage.Resize(resizeGeo);

                MagickImage targetFrame = new(MagickColors.Transparent, (uint)targetSize, (uint)targetSize);
                targetFrame.Format = MagickFormat.Png;
                targetFrame.ColorSpace = ColorSpace.sRGB;
                targetFrame.Composite(subImage, geom.DestX, geom.DestY, CompositeOperator.Over);
                targetFrame.Format = MagickFormat.Ico;
                normalizedCollection.Add(targetFrame);
            }
        }

        EnsureDirectory(outputIcoPath);
        normalizedCollection.Write(outputIcoPath, MagickFormat.Ico);

        // Publish to cache
        PublishToCache(outputIcoPath, sha256, renderKey, recipe.RecipeVersion, RequiredSizes, cacheDirectory, cacheStore);

        return OperationResult.Success(new IcoImportResult
        {
            CanonicalHash = sha256,
            CanonicalPath = Path.GetFullPath(outputIcoPath),
            ImportKind = IcoImportKind.IcoNormalized,
            AvailableSizes = RequiredSizes,
            RenderKey = renderKey,
            IsFastPath = false
        });
    }

    /// <summary>
    /// Extracts a high-quality 256x256 (or requested size) PNG preview from an ICO payload
    /// using bounded decode without full multi-frame transcoding.
    /// </summary>
    public static OperationResult<byte[]> ExtractPreviewPng(byte[] icoBytes, int targetSize = 256)
    {
        var containerCheck = ValidateIcoContainer(icoBytes);
        if (!containerCheck.IsSuccess)
        {
            return OperationResult.Failure<byte[]>(containerCheck.ErrorCode, containerCheck.ErrorMessage ?? "Malformed ICO.");
        }

        try
        {
            var readSettings = new MagickReadSettings { Format = MagickFormat.Ico };
            using var collection = new MagickImageCollection(icoBytes, readSettings);

            if (collection.Count == 0)
            {
                return OperationResult.Failure<byte[]>(ErrorCode.ImageCorrupted, "No frames in ICO.");
            }

            // Find best frame for targetSize: exact first, else smallest larger, else largest
            var candidates = collection.Where(f => f.Width >= 1 && f.Height >= 1).ToList();
            if (candidates.Count == 0)
            {
                return OperationResult.Failure<byte[]>(ErrorCode.ImageCorrupted, "No valid frames.");
            }

            var exact = candidates.FirstOrDefault(f => f.Width == targetSize && f.Height == targetSize);
            IMagickImage<ushort> chosen = exact ?? (candidates.FirstOrDefault(f => Math.Max(f.Width, f.Height) >= targetSize) ?? candidates.OrderByDescending(f => Math.Max(f.Width, f.Height)).First());

            using var previewImage = chosen.Clone();
            previewImage.ColorSpace = ColorSpace.sRGB;

            if (previewImage.Width != targetSize || previewImage.Height != targetSize)
            {
                previewImage.FilterType = FilterType.Lanczos;
                var geom = ImagePlacementCalculator.ComputePlacement((int)previewImage.Width, (int)previewImage.Height, targetSize, targetSize, ImagePlacementMode.Contain, 0);
                var resizeGeo = new MagickGeometry((uint)geom.DestWidth, (uint)geom.DestHeight) { IgnoreAspectRatio = true };
                previewImage.Resize(resizeGeo);

                using var canvas = new MagickImage(MagickColors.Transparent, (uint)targetSize, (uint)targetSize);
                canvas.ColorSpace = ColorSpace.sRGB;
                canvas.Composite(previewImage, geom.DestX, geom.DestY, CompositeOperator.Over);
                canvas.Format = MagickFormat.Png;
                return OperationResult.Success(canvas.ToByteArray());
            }

            previewImage.Format = MagickFormat.Png;
            return OperationResult.Success(previewImage.ToByteArray());
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.ImageDecodeFailed, $"Failed to extract preview: {ex.Message}");
        }
    }

    private static void EnsureDirectory(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static void PublishToCache(
        string sourceIcoPath,
        string sourceAssetHash,
        string renderKey,
        int recipeVersion,
        int[] frameSizes,
        string? cacheDirectory,
        Action<RenderCacheEntry>? cacheStore)
    {
        if (string.IsNullOrEmpty(cacheDirectory) || !File.Exists(sourceIcoPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(cacheDirectory);
            string canonicalCacheFile = Path.Combine(cacheDirectory, $"{renderKey}.ico");
            string tempFile = Path.Combine(cacheDirectory, $"tmp_{Guid.NewGuid():N}.tmp");

            try
            {
                File.Copy(sourceIcoPath, tempFile, overwrite: true);
                try
                {
                    File.Move(tempFile, canonicalCacheFile, overwrite: true);
                }
                catch (IOException)
                {
                    // Winner stands
                }

                FileInfo fi = new(canonicalCacheFile);
                RenderCacheEntry entry = new()
                {
                    RenderKey = renderKey,
                    RecipeVersion = recipeVersion,
                    RecipeJson = string.Empty,
                    SourceAssetHash = sourceAssetHash,
                    CanonicalIcoPath = canonicalCacheFile,
                    FrameSizes = string.Join(",", frameSizes),
                    FileSizeBytes = fi.Length,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastUsedAt = DateTimeOffset.UtcNow
                };
                cacheStore?.Invoke(entry);
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
}
