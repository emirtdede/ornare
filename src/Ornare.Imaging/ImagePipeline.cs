using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging.Badges;

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

    public string ComputeSourceHash(byte[] bytes) => ComputeSourceHashStatic(bytes);

    public static string ComputeSourceHashStatic(byte[] bytes)
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

    public OperationResult<NormalizedImageResult> ProcessImage(
        string inputPath,
        string outputIcoPath,
        ImagePlacementMode placementMode = ImagePlacementMode.Contain,
        int paddingPercent = 0,
        BackgroundMode backgroundMode = BackgroundMode.Transparent,
        string? backgroundColorHex = null)
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

        return ProcessImageBytes(bytes, outputIcoPath, placementMode, paddingPercent, backgroundMode, backgroundColorHex);
    }

    public OperationResult<NormalizedImageResult> ProcessImageBytes(
        byte[] imageBytes,
        string outputIcoPath,
        ImagePlacementMode placementMode = ImagePlacementMode.Contain,
        int paddingPercent = 0,
        BackgroundMode backgroundMode = BackgroundMode.Transparent,
        string? backgroundColorHex = null)
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
            PlacementMode = (int)placementMode,
            PaddingPercent = Math.Clamp(paddingPercent, 0, 30),
            BackgroundMode = (int)backgroundMode,
            BackgroundColor = (backgroundMode == BackgroundMode.SolidColor && !string.IsNullOrWhiteSpace(backgroundColorHex))
                ? BackgroundSpec.NormalizeHex(backgroundColorHex)
                : string.Empty,
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

        byte[] bytesToRender = sourceBytes;
        string originalHash = !string.IsNullOrEmpty(recipe.SourceAssetHash)
            ? recipe.SourceAssetHash
            : ComputeSourceHash(sourceBytes);

        // F12: If input is an ICO and recipe specifies default placement/padding/background/recolor,
        // route directly through IcoImportService for fast-path preservation or deterministic multi-frame normalization.
        bool isRawIco = sourceBytes.Length >= 4 && sourceBytes[0] == 0 && sourceBytes[1] == 0 && sourceBytes[2] == 1 && sourceBytes[3] == 0;
        if (isRawIco &&
            recipe.SvgRecolorMode == 0 &&
            recipe.PlacementMode == (int)ImagePlacementMode.Contain &&
            recipe.PaddingPercent == 0 &&
            recipe.BackgroundMode == (int)BackgroundMode.Transparent &&
            string.IsNullOrEmpty(recipe.BadgeAssetId))
        {
            var importResult = IcoImportService.ImportIco(sourceBytes, outputIcoPath, cacheDirectory, cacheLookup, cacheStore);
            if (importResult.IsSuccess && importResult.Value != null)
            {
                var val = importResult.Value;
                return OperationResult.Success(new NormalizedImageResult
                {
                    ContentHash = val.CanonicalHash,
                    SourceWidth = 256,
                    SourceHeight = 256,
                    SourceFormat = "ico",
                    GeneratedIcoPath = val.CanonicalPath,
                    FrameSizes = val.AvailableSizes,
                    RenderKey = val.RenderKey,
                    IsFromCache = false
                });
            }
            else if (!importResult.IsSuccess)
            {
                return OperationResult.Failure<NormalizedImageResult>(importResult.ErrorCode, importResult.ErrorMessage ?? "ICO import failed.");
            }
        }

        if (recipe.SvgRecolorMode != 0)
        {
            var secCheck = SvgSecurityValidator.Validate(sourceBytes);
            if (!secCheck.IsSuccess)
            {
                return OperationResult.Failure<NormalizedImageResult>(
                    secCheck.ErrorCode, secCheck.ErrorMessage ?? "SVG security check failed.");
            }

            string svgStr = System.Text.Encoding.UTF8.GetString(sourceBytes);
            if (recipe.SvgRecolorMode == 1) // SemanticTemplate
            {
                var folderPalette = FolderPaletteGenerator.GenerateFromHex(recipe.SvgRecolorValue);
                var palette = folderPalette.ToSemanticColorPalette();
                var recolorResult = SvgRecolorService.RecolorSemanticTemplate(svgStr, palette);
                if (!recolorResult.IsSuccess)
                {
                    return OperationResult.Failure<NormalizedImageResult>(
                        recolorResult.ErrorCode, recolorResult.ErrorMessage ?? "Failed to recolor semantic SVG template.");
                }
                bytesToRender = System.Text.Encoding.UTF8.GetBytes(recolorResult.Value!);
            }
            else if (recipe.SvgRecolorMode == 2) // SingleColor
            {
                var recolorResult = SvgRecolorService.RecolorUserSingleColorSvg(svgStr, recipe.SvgRecolorValue);
                if (!recolorResult.IsSuccess)
                {
                    return OperationResult.Failure<NormalizedImageResult>(
                        recolorResult.ErrorCode, recolorResult.ErrorMessage ?? "Failed to recolor single-color SVG.");
                }
                bytesToRender = System.Text.Encoding.UTF8.GetBytes(recolorResult.Value!);
            }
        }

        var renderResult = RenderImageBytesCore(
            bytesToRender,
            outputIcoPath,
            renderKey,
            originalHash,
            recipe.ImagePlacement,
            recipe.PaddingPercent,
            recipe.Background,
            recipe.BackgroundColor,
            recipe.BadgeAssetId,
            recipe.PaletteParameters);
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
                            RecipeJson = recipe.ToJson(),
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

    private static bool IsPotentialSvg(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) return false;
        int i = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            i = 3;
        }

        while (i < bytes.Length && (bytes[i] == ' ' || bytes[i] == '\t' || bytes[i] == '\r' || bytes[i] == '\n'))
        {
            i++;
        }

        if (i >= bytes.Length || bytes[i] != '<')
        {
            return false;
        }

        ReadOnlySpan<byte> span = bytes.AsSpan(i);
        return span.StartsWith("<?xml"u8) || span.StartsWith("<svg"u8) || span.StartsWith("<!DOCTYPE svg"u8);
    }

    private OperationResult<NormalizedImageResult> RenderImageBytesCore(
        byte[] imageBytes,
        string outputIcoPath,
        string renderKey,
        string? overrideContentHash = null,
        ImagePlacementMode placementMode = ImagePlacementMode.Contain,
        double paddingPercent = 0.0,
        BackgroundMode backgroundMode = BackgroundMode.Transparent,
        string? backgroundColorHex = null,
        string? badgeAssetId = null,
        string? badgeFolderBaseHex = null)
    {
        bool isIco = imageBytes.Length >= 4 && imageBytes[0] == 0 && imageBytes[1] == 0 && imageBytes[2] == 1 && imageBytes[3] == 0;
        bool isSvg = IsPotentialSvg(imageBytes);

        if (isSvg)
        {
            var secCheck = SvgSecurityValidator.Validate(imageBytes);
            if (!secCheck.IsSuccess)
            {
                return OperationResult.Failure<NormalizedImageResult>(
                    secCheck.ErrorCode, secCheck.ErrorMessage ?? "SVG security check failed.");
            }
        }

        MagickImageInfo info;
        try
        {
            if (isIco)
            {
                info = new MagickImageInfo(imageBytes, new MagickReadSettings { Format = MagickFormat.Ico });
            }
            else if (isSvg)
            {
                info = ReadSvgInfoGated(imageBytes, new MagickReadSettings { Format = MagickFormat.Svg, BackgroundColor = MagickColors.Transparent });
            }
            else
            {
                info = new MagickImageInfo(imageBytes);
            }
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(
                ErrorCode.ImageCorrupted, $"Malformed image header: {ex.Message}");
        }

        if (!isSvg && !ImageSecurityPolicy.IsFormatAllowed(info.Format))
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
            MagickReadSettings? readSettings = isIco
                ? new MagickReadSettings { Format = MagickFormat.Ico }
                : info.Format == MagickFormat.Svg
                    ? new MagickReadSettings { Format = MagickFormat.Svg, Width = 256, Height = 256, BackgroundColor = MagickColors.Transparent }
                    : null;

            using MagickImage sourceImage = isSvg
                ? ReadSvgGated(imageBytes, readSettings!)
                : (readSettings != null ? new(imageBytes, readSettings) : new(imageBytes));

            sourceImage.AutoOrient();
            sourceImage.ColorSpace = ColorSpace.sRGB;

            string contentHash = overrideContentHash ?? ComputeSourceHash(imageBytes);

            MagickColor canvasBg = (backgroundMode == BackgroundMode.SolidColor && !string.IsNullOrWhiteSpace(backgroundColorHex))
                ? new MagickColor(BackgroundSpec.NormalizeHex(backgroundColorHex))
                : MagickColors.Transparent;

            using MagickImageCollection icoFrames = new();
            foreach (int size in IcoFrameSizes)
            {
                var geom = ImagePlacementCalculator.ComputePlacement(
                    (int)sourceImage.Width,
                    (int)sourceImage.Height,
                    size,
                    size,
                    placementMode,
                    paddingPercent);

                using var subImage = (geom.SourceCropX == 0 && geom.SourceCropY == 0 && geom.SourceCropWidth == sourceImage.Width && geom.SourceCropHeight == sourceImage.Height)
                    ? (MagickImage)sourceImage.Clone()
                    : (MagickImage)sourceImage.CloneArea(new MagickGeometry(geom.SourceCropX, geom.SourceCropY, (uint)geom.SourceCropWidth, (uint)geom.SourceCropHeight));

                subImage.FilterType = FilterType.Lanczos;
                var resizeGeo = new MagickGeometry((uint)geom.DestWidth, (uint)geom.DestHeight) { IgnoreAspectRatio = true };
                subImage.Resize(resizeGeo);

                MagickImage frame = new(canvasBg, (uint)size, (uint)size);
                frame.Format = MagickFormat.Png;
                frame.ColorSpace = ColorSpace.sRGB;
                frame.Composite(subImage, geom.DestX, geom.DestY, CompositeOperator.Over);
                if (!string.IsNullOrEmpty(badgeAssetId))
                {
                    StatusBadgeCompositor.CompositeBadge(frame, badgeAssetId, badgeFolderBaseHex ?? string.Empty);
                }
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

    /// <summary>
    /// Renders a fast, bounded 256x256 PNG preview honoring placement mode, padding, and background compositing (F06, F07, F08).
    /// </summary>
    public static OperationResult<byte[]> RenderPlacementPreviewPng(
        byte[] imageBytes,
        int previewSize = 256,
        ImagePlacementMode placementMode = ImagePlacementMode.Contain,
        double paddingPercent = 0.0,
        BackgroundMode backgroundMode = BackgroundMode.Transparent,
        string? backgroundColorHex = null)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.InvalidTarget, "Image payload is empty.");
        }

        try
        {
            bool isSvg = IsPotentialSvg(imageBytes);
            if (isSvg)
            {
                var sec = SvgSecurityValidator.Validate(imageBytes);
                if (!sec.IsSuccess)
                {
                    return OperationResult.Failure<byte[]>(sec.ErrorCode, sec.ErrorMessage ?? "SVG validation failed.");
                }
            }

            var readSettings = isSvg
                ? new MagickReadSettings { Format = MagickFormat.Svg, Width = (uint)previewSize, Height = (uint)previewSize, BackgroundColor = MagickColors.Transparent }
                : null;

            using var source = isSvg
                ? ReadSvgGated(imageBytes, readSettings!)
                : (readSettings != null ? new MagickImage(imageBytes, readSettings) : new MagickImage(imageBytes));

            source.AutoOrient();
            source.ColorSpace = ColorSpace.sRGB;

            var geom = ImagePlacementCalculator.ComputePlacement(
                (int)source.Width,
                (int)source.Height,
                previewSize,
                previewSize,
                placementMode,
                paddingPercent);

            using var sub = (geom.SourceCropX == 0 && geom.SourceCropY == 0 && geom.SourceCropWidth == source.Width && geom.SourceCropHeight == source.Height)
                ? (MagickImage)source.Clone()
                : (MagickImage)source.CloneArea(new MagickGeometry(geom.SourceCropX, geom.SourceCropY, (uint)geom.SourceCropWidth, (uint)geom.SourceCropHeight));

            sub.FilterType = FilterType.Lanczos;
            var resizeGeo = new MagickGeometry((uint)geom.DestWidth, (uint)geom.DestHeight) { IgnoreAspectRatio = true };
            sub.Resize(resizeGeo);

            MagickColor canvasBg = (backgroundMode == BackgroundMode.SolidColor && !string.IsNullOrWhiteSpace(backgroundColorHex))
                ? new MagickColor(BackgroundSpec.NormalizeHex(backgroundColorHex))
                : MagickColors.Transparent;

            using MagickImage previewCanvas = new(canvasBg, (uint)previewSize, (uint)previewSize);
            previewCanvas.Format = MagickFormat.Png;
            previewCanvas.ColorSpace = ColorSpace.sRGB;
            previewCanvas.Composite(sub, geom.DestX, geom.DestY, CompositeOperator.Over);

            return OperationResult.Success(previewCanvas.ToByteArray());
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.ImageDecodeFailed, $"Preview generation failed: {ex.Message}");
        }
    }

    public OperationResult<IcoImportResult> ImportIco(
        byte[] icoBytes,
        string outputIcoPath,
        string? cacheDirectory = null,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null)
    {
        return IcoImportService.ImportIco(icoBytes, outputIcoPath, cacheDirectory, cacheLookup, cacheStore);
    }

    public OperationResult<byte[]> ExtractIcoPreviewPng(byte[] icoBytes, int targetSize = 256)
    {
        return IcoImportService.ExtractPreviewPng(icoBytes, targetSize);
    }

    public OperationResult<byte[]> RenderPng(
        RenderRecipe recipe,
        byte[]? sourceBytes,
        int targetSize = ExportPngConstants.DefaultSize)
    {
        return PngExportService.RenderPng(recipe, sourceBytes, targetSize);
    }

    public OperationResult<RenderPngResult> RenderPngWithCache(
        RenderRecipe recipe,
        byte[]? sourceBytes,
        int targetSize = ExportPngConstants.DefaultSize,
        string? cacheDirectory = null,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null)
    {
        return PngExportService.RenderPngWithCache(recipe, sourceBytes, targetSize, cacheDirectory, cacheLookup, cacheStore);
    }

    private static MagickImage ReadSvgGated(byte[] bytes, MagickReadSettings settings)
    {
        lock (SvgRasterizationGate.Lock)
        {
            return new MagickImage(bytes, settings);
        }
    }

    private static MagickImageInfo ReadSvgInfoGated(byte[] bytes, MagickReadSettings settings)
    {
        lock (SvgRasterizationGate.Lock)
        {
            return new MagickImageInfo(bytes, settings);
        }
    }
}
