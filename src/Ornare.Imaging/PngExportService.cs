using System;
using System.IO;
using System.Linq;
using System.Text;
using ImageMagick;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging.Badges;
using Ornare.Imaging.Templates;

namespace Ornare.Imaging;

/// <summary>
/// Authoritative derived PNG renderer and cache manager (F14).
/// Supports high-resolution vector and raster rendering for exactly 256, 512, and 1024 sizes (default 1024).
/// Bounded memory lifecycle with immediate bitmap disposal.
/// </summary>
public static class PngExportService
{
    private static readonly byte[] PngHeaderMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    static PngExportService()
    {
        ImageSecurityPolicy.Initialize();
    }

    /// <summary>
    /// Validates basic PNG header, minimum size (at least 33 bytes), container signature, and chunk structure (F14).
    /// </summary>
    public static bool ValidatePngBasic(string filePath, int expectedSize = 0)
    {
        return TryGetPngInfo(filePath, expectedSize, out _, out _, out _);
    }

    /// <summary>
    /// Validates basic PNG bytes without reading from disk.
    /// </summary>
    public static bool ValidatePngBytes(byte[] pngBytes, int expectedSize = 0)
    {
        if (pngBytes == null || pngBytes.Length < 33) return false;
        return TryGetPngInfoFromBytes(pngBytes, expectedSize, out _, out _, out _);
    }

    /// <summary>
    /// Extracts width, height, and alpha presence directly from PNG IHDR chunk without decoding (F14).
    /// </summary>
    public static bool TryGetPngInfo(string filePath, int expectedSize, out int width, out int height, out bool hasAlpha)
    {
        width = 0;
        height = 0;
        hasAlpha = false;

        try
        {
            FileInfo fi = new(filePath);
            if (fi.Length < 33) return false;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[26];
            if (fs.Read(header, 0, 26) < 26) return false;

            return TryGetPngInfoFromBytes(header, expectedSize, out width, out height, out hasAlpha);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPngInfoFromBytes(ReadOnlySpan<byte> header, int expectedSize, out int width, out int height, out bool hasAlpha)
    {
        width = 0;
        height = 0;
        hasAlpha = false;

        if (header.Length < 26) return false;

        for (int i = 0; i < 8; i++)
        {
            if (header[i] != PngHeaderMagic[i]) return false;
        }

        if (header[8] != 0 || header[9] != 0 || header[10] != 0 || header[11] != 13) return false;
        if (header[12] != 0x49 || header[13] != 0x48 || header[14] != 0x44 || header[15] != 0x52) return false;

        width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];

        if (width <= 0 || height <= 0) return false;

        if (expectedSize > 0 && (width != expectedSize || height != expectedSize))
        {
            return false;
        }

        byte colorType = header[25];
        hasAlpha = colorType is 4 or 6;

        return true;
    }

    /// <summary>
    /// Renders a recipe directly to PNG bytes at the specified target resolution (F14).
    /// Always renders vectors directly at targetSize without upscaling small raster frames.
    /// </summary>
    public static OperationResult<byte[]> RenderPng(
        RenderRecipe recipe,
        byte[]? sourceBytes,
        int targetSize = ExportPngConstants.DefaultSize)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        if (!ExportPngConstants.IsAllowedSize(targetSize))
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.InvalidTarget,
                $"PNG export target size must be one of {string.Join(", ", ExportPngConstants.AllowedSizes)}. Received: {targetSize}.");
        }

        // 1. Template / Preset vector rendering
        if (!string.IsNullOrEmpty(recipe.TemplateId))
        {
            return RenderTemplateVectorPng(recipe, targetSize);
        }

        if (sourceBytes == null || sourceBytes.Length == 0)
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.InvalidTarget, "Cannot render PNG without source bytes when no template is specified.");
        }

        bool isSvg = IsPotentialSvg(sourceBytes);
        if (isSvg || recipe.SvgRecolorMode != 0)
        {
            return RenderSvgVectorPng(recipe, sourceBytes, targetSize);
        }

        return RenderRasterSourcePng(recipe, sourceBytes, targetSize);
    }

    /// <summary>
    /// Renders PNG with render cache reuse (F14).
    /// Checks cache for exact hit (targetFormat = "png", frameSize = targetSize), otherwise renders on demand and updates cache.
    /// </summary>
    public static OperationResult<RenderPngResult> RenderPngWithCache(
        RenderRecipe recipe,
        byte[]? sourceBytes,
        int targetSize = ExportPngConstants.DefaultSize,
        string? cacheDirectory = null,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        if (!ExportPngConstants.IsAllowedSize(targetSize))
        {
            return OperationResult.Failure<RenderPngResult>(
                ErrorCode.InvalidTarget,
                $"PNG export target size must be one of {string.Join(", ", ExportPngConstants.AllowedSizes)}. Received: {targetSize}.");
        }

        string sourceHash = !string.IsNullOrEmpty(recipe.SourceAssetHash)
            ? recipe.SourceAssetHash
            : sourceBytes != null
                ? ImagePipeline.ComputeSourceHashStatic(sourceBytes)
                : string.Empty;

        RenderRecipe normalizedRecipe = recipe with
        {
            SourceAssetHash = sourceHash,
            TargetFormat = "png",
            TargetFrameSizes = [targetSize]
        };

        string renderKey = normalizedRecipe.ComputeRenderKey();

        // Check cache hit
        if (cacheLookup != null)
        {
            var cachedEntry = cacheLookup(renderKey);
            if (cachedEntry != null && !string.IsNullOrEmpty(cachedEntry.CanonicalIcoPath) && File.Exists(cachedEntry.CanonicalIcoPath))
            {
                if (TryGetPngInfo(cachedEntry.CanonicalIcoPath, targetSize, out int w, out int h, out bool hasAlpha))
                {
                    try
                    {
                        byte[] cachedBytes = File.ReadAllBytes(cachedEntry.CanonicalIcoPath);
                        return OperationResult.Success(new RenderPngResult
                        {
                            PngBytes = cachedBytes,
                            Width = w,
                            Height = h,
                            HasAlpha = hasAlpha,
                            RenderKey = renderKey,
                            IsFromCache = true,
                            CachedFilePath = cachedEntry.CanonicalIcoPath
                        });
                    }
                    catch
                    {
                        // Proceed to render on read failure
                    }
                }
            }
        }

        // Cache miss: render on demand
        var renderResult = RenderPng(normalizedRecipe, sourceBytes, targetSize);
        if (!renderResult.IsSuccess || renderResult.Value == null)
        {
            return OperationResult.Failure<RenderPngResult>(
                renderResult.ErrorCode, renderResult.ErrorMessage ?? "PNG rendering failed.");
        }

        byte[] pngBytes = renderResult.Value;
        string? publishedCacheFile = null;

        // Atomically publish to cache directory if specified
        if (!string.IsNullOrEmpty(cacheDirectory))
        {
            try
            {
                Directory.CreateDirectory(cacheDirectory);
                string canonicalCacheFile = Path.Combine(cacheDirectory, $"{renderKey}.png");
                string tempCacheFile = Path.Combine(cacheDirectory, $"tmp_{Guid.NewGuid():N}.tmp");

                try
                {
                    File.WriteAllBytes(tempCacheFile, pngBytes);
                    if (ValidatePngBasic(tempCacheFile, targetSize))
                    {
                        try
                        {
                            File.Move(tempCacheFile, canonicalCacheFile, overwrite: true);
                        }
                        catch (IOException)
                        {
                            // Race condition handled: existing published artifact stands
                        }

                        publishedCacheFile = canonicalCacheFile;
                        FileInfo fi = new(canonicalCacheFile);
                        RenderCacheEntry entry = new()
                        {
                            RenderKey = renderKey,
                            RecipeVersion = normalizedRecipe.RecipeVersion,
                            RecipeJson = string.Empty,
                            SourceAssetHash = sourceHash,
                            CanonicalIcoPath = canonicalCacheFile,
                            FrameSizes = targetSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                        if (File.Exists(tempCacheFile)) File.Delete(tempCacheFile);
                    }
                    catch { }
                }
            }
            catch
            {
                // Non-critical cache publishing failure
            }
        }

        TryGetPngInfoFromBytes(pngBytes, targetSize, out int finalWidth, out int finalHeight, out bool finalHasAlpha);

        return OperationResult.Success(new RenderPngResult
        {
            PngBytes = pngBytes,
            Width = finalWidth > 0 ? finalWidth : targetSize,
            Height = finalHeight > 0 ? finalHeight : targetSize,
            HasAlpha = finalHasAlpha,
            RenderKey = renderKey,
            IsFromCache = false,
            CachedFilePath = publishedCacheFile
        });
    }

    private static OperationResult<byte[]> RenderTemplateVectorPng(RenderRecipe recipe, int targetSize)
    {
        var descriptor = FolderTemplateCatalog.GetTemplate(recipe.TemplateId, recipe.TemplateVersion);
        string normalizedHex = !string.IsNullOrEmpty(recipe.SvgRecolorValue) ? recipe.SvgRecolorValue : "#0078D4";
        if (ColorConversion.TryParseHex(normalizedHex, out byte r, out byte g, out byte b))
        {
            normalizedHex = ColorConversion.ToHex(r, g, b);
        }

        var folderPalette = FolderPaletteGenerator.GenerateFromHex(normalizedHex);
        var semanticPalette = folderPalette.ToSemanticColorPalette();

        string templateSvg = descriptor.SvgContent;
        var recolorResult = SvgRecolorService.RecolorSemanticTemplate(templateSvg, semanticPalette);
        if (!recolorResult.IsSuccess || string.IsNullOrEmpty(recolorResult.Value))
        {
            return OperationResult.Failure<byte[]>(
                recolorResult.ErrorCode, recolorResult.ErrorMessage ?? "SVG template recoloring failed.");
        }

        string adjustedSvg = AdjustSvgDimensions(recolorResult.Value, targetSize);
        byte[] recoloredSvgBytes = Encoding.UTF8.GetBytes(adjustedSvg);

        try
        {
            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Width = (uint)targetSize,
                Height = (uint)targetSize,
                Density = new Density(96.0 * targetSize / 256.0),
                BackgroundColor = MagickColors.Transparent
            };

            lock (SvgRasterizationGate.Lock)
            {
                using var image = new MagickImage(recoloredSvgBytes, readSettings);
                if (image.Width != targetSize || image.Height != targetSize)
                {
                    image.FilterType = FilterType.Lanczos;
                    image.Resize((uint)targetSize, (uint)targetSize);
                }
                image.ColorSpace = ColorSpace.sRGB;

                if (!string.IsNullOrEmpty(recipe.BadgeAssetId))
                {
                    StatusBadgeCompositor.CompositeBadge(image, recipe.BadgeAssetId, normalizedHex);
                }

                if (recipe.Background == BackgroundMode.SolidColor && !string.IsNullOrWhiteSpace(recipe.BackgroundColor))
                {
                    using var canvas = new MagickImage(
                        new MagickColor(BackgroundSpec.NormalizeHex(recipe.BackgroundColor)),
                        (uint)targetSize,
                        (uint)targetSize);
                    canvas.ColorSpace = ColorSpace.sRGB;
                    canvas.Composite(image, 0, 0, CompositeOperator.Over);
                    canvas.Format = MagickFormat.Png;
                    return OperationResult.Success(canvas.ToByteArray());
                }

                image.Format = MagickFormat.Png;
                return OperationResult.Success(image.ToByteArray());
            }
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.ImageCorrupted, $"Template vector rasterization failed: {ex.Message}");
        }
    }

    private static OperationResult<byte[]> RenderSvgVectorPng(RenderRecipe recipe, byte[] sourceBytes, int targetSize)
    {
        var secCheck = SvgSecurityValidator.Validate(sourceBytes);
        if (!secCheck.IsSuccess)
        {
            return OperationResult.Failure<byte[]>(secCheck.ErrorCode, secCheck.ErrorMessage ?? "SVG validation failed.");
        }

        byte[] svgBytesToRender = sourceBytes;
        if (recipe.SvgRecolorMode == 1) // SemanticTemplate
        {
            string svgStr = Encoding.UTF8.GetString(sourceBytes);
            var folderPalette = FolderPaletteGenerator.GenerateFromHex(recipe.SvgRecolorValue);
            var palette = folderPalette.ToSemanticColorPalette();
            var recolorResult = SvgRecolorService.RecolorSemanticTemplate(svgStr, palette);
            if (!recolorResult.IsSuccess || string.IsNullOrEmpty(recolorResult.Value))
            {
                return OperationResult.Failure<byte[]>(recolorResult.ErrorCode, recolorResult.ErrorMessage ?? "Failed to recolor semantic SVG template.");
            }
            svgBytesToRender = Encoding.UTF8.GetBytes(recolorResult.Value);
        }
        else if (recipe.SvgRecolorMode == 2) // SingleColor
        {
            string svgStr = Encoding.UTF8.GetString(sourceBytes);
            var recolorResult = SvgRecolorService.RecolorUserSingleColorSvg(svgStr, recipe.SvgRecolorValue);
            if (!recolorResult.IsSuccess || string.IsNullOrEmpty(recolorResult.Value))
            {
                return OperationResult.Failure<byte[]>(recolorResult.ErrorCode, recolorResult.ErrorMessage ?? "Failed to recolor single-color SVG.");
            }
            svgBytesToRender = Encoding.UTF8.GetBytes(recolorResult.Value);
        }

        string adjustedUserSvg = AdjustSvgDimensions(Encoding.UTF8.GetString(svgBytesToRender), targetSize);
        byte[] finalSvgBytes = Encoding.UTF8.GetBytes(adjustedUserSvg);

        try
        {
            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Width = (uint)targetSize,
                Height = (uint)targetSize,
                Density = new Density(96.0 * targetSize / 256.0),
                BackgroundColor = MagickColors.Transparent
            };

            lock (SvgRasterizationGate.Lock)
            {
                using var image = new MagickImage(finalSvgBytes, readSettings);
                if (image.Width != targetSize || image.Height != targetSize)
                {
                    image.FilterType = FilterType.Lanczos;
                    image.Resize((uint)targetSize, (uint)targetSize);
                }
                image.ColorSpace = ColorSpace.sRGB;

                if (!string.IsNullOrEmpty(recipe.BadgeAssetId))
                {
                    StatusBadgeCompositor.CompositeBadge(image, recipe.BadgeAssetId, recipe.SvgRecolorValue);
                }

                if (recipe.Background == BackgroundMode.SolidColor && !string.IsNullOrWhiteSpace(recipe.BackgroundColor))
                {
                    using var canvas = new MagickImage(
                        new MagickColor(BackgroundSpec.NormalizeHex(recipe.BackgroundColor)),
                        (uint)targetSize,
                        (uint)targetSize);
                    canvas.ColorSpace = ColorSpace.sRGB;
                    canvas.Composite(image, 0, 0, CompositeOperator.Over);
                    canvas.Format = MagickFormat.Png;
                    return OperationResult.Success(canvas.ToByteArray());
                }

                image.Format = MagickFormat.Png;
                return OperationResult.Success(image.ToByteArray());
            }
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.ImageCorrupted, $"SVG vector rasterization failed: {ex.Message}");
        }
    }

    private static OperationResult<byte[]> RenderRasterSourcePng(RenderRecipe recipe, byte[] sourceBytes, int targetSize)
    {
        bool isIco = sourceBytes.Length >= 4 && sourceBytes[0] == 0 && sourceBytes[1] == 0 && sourceBytes[2] == 1 && sourceBytes[3] == 0;

        MagickImageInfo info;
        try
        {
            info = isIco
                ? new MagickImageInfo(sourceBytes, new MagickReadSettings { Format = MagickFormat.Ico })
                : new MagickImageInfo(sourceBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.ImageCorrupted, $"Malformed image header: {ex.Message}");
        }

        if (!isIco && !ImageSecurityPolicy.IsFormatAllowed(info.Format))
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.UnsupportedImageFormat, $"Image format '{info.Format}' is not allowed.");
        }

        if (info.Width > 16384 || info.Height > 16384 || ((long)info.Width * info.Height) > 100_000_000)
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.ImageExceedsLimits, "Image dimensions exceed configured security boundaries.");
        }

        try
        {
            using MagickImage sourceImage = isIco
                ? ExtractLargestIcoFrame(sourceBytes)
                : new MagickImage(sourceBytes);

            sourceImage.AutoOrient();
            sourceImage.ColorSpace = ColorSpace.sRGB;

            MagickColor canvasBg = (recipe.Background == BackgroundMode.SolidColor && !string.IsNullOrWhiteSpace(recipe.BackgroundColor))
                ? new MagickColor(BackgroundSpec.NormalizeHex(recipe.BackgroundColor))
                : MagickColors.Transparent;

            var geom = ImagePlacementCalculator.ComputePlacement(
                (int)sourceImage.Width,
                (int)sourceImage.Height,
                targetSize,
                targetSize,
                recipe.ImagePlacement,
                recipe.PaddingPercent);

            using var subImage = (geom.SourceCropX == 0 && geom.SourceCropY == 0 && geom.SourceCropWidth == sourceImage.Width && geom.SourceCropHeight == sourceImage.Height)
                ? (MagickImage)sourceImage.Clone()
                : (MagickImage)sourceImage.CloneArea(new MagickGeometry(geom.SourceCropX, geom.SourceCropY, (uint)geom.SourceCropWidth, (uint)geom.SourceCropHeight));

            subImage.FilterType = FilterType.Lanczos;
            subImage.Resize(new MagickGeometry((uint)geom.DestWidth, (uint)geom.DestHeight) { IgnoreAspectRatio = true });

            using MagickImage canvas = new(canvasBg, (uint)targetSize, (uint)targetSize);
            canvas.ColorSpace = ColorSpace.sRGB;
            canvas.Composite(subImage, geom.DestX, geom.DestY, CompositeOperator.Over);

            if (!string.IsNullOrEmpty(recipe.BadgeAssetId))
            {
                StatusBadgeCompositor.CompositeBadge(canvas, recipe.BadgeAssetId, recipe.SvgRecolorValue);
            }

            canvas.Format = MagickFormat.Png;
            return OperationResult.Success(canvas.ToByteArray());
        }
        catch (MagickException mex)
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.ImageDecodeFailed, $"Image processing failed: {mex.Message}");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(
                ErrorCode.ImageCorrupted, $"Failed to render PNG: {ex.Message}");
        }
    }

    private static MagickImage ExtractLargestIcoFrame(byte[] icoBytes)
    {
        using var collection = new MagickImageCollection(icoBytes, new MagickReadSettings { Format = MagickFormat.Ico });
        if (collection.Count == 0)
        {
            throw new InvalidDataException("ICO file contains no frames.");
        }

        var largest = collection.OrderByDescending(f => f.Width * f.Height).First();
        return (MagickImage)largest.Clone();
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

    private static string AdjustSvgDimensions(string svgContent, int targetSize)
    {
        if (string.IsNullOrWhiteSpace(svgContent)) return svgContent;

        int svgCloseIdx = svgContent.IndexOf('>');
        if (svgCloseIdx < 0) return svgContent;

        string openTag = svgContent.Substring(0, svgCloseIdx + 1);
        string rest = svgContent.Substring(svgCloseIdx + 1);

        string adjustedOpenTag = System.Text.RegularExpressions.Regex.Replace(
            openTag,
            @"\bwidth\s*=\s*[""'][^""']*[""']",
            $"width=\"{targetSize}\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        adjustedOpenTag = System.Text.RegularExpressions.Regex.Replace(
            adjustedOpenTag,
            @"\bheight\s*=\s*[""'][^""']*[""']",
            $"height=\"{targetSize}\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return adjustedOpenTag + rest;
    }
}
