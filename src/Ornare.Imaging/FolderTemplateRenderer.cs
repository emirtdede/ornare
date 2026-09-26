using System;
using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging.Templates;

namespace Ornare.Imaging;

/// <summary>
/// Authoritative renderer for folder style templates and color variants (F01).
/// Binds OrnareClassicTemplate + FolderPaletteGenerator (F10) + SvgRecolorService (F09) + ImagePipeline (F18).
/// </summary>
public static class FolderTemplateRenderer
{
    /// <summary>
    /// Builds the deterministic RenderRecipe for a colored template icon.
    /// </summary>
    public static RenderRecipe CreateRecipe(
        string baseHex,
        string templateId = OrnareClassicTemplate.TemplateId,
        int templateVersion = OrnareClassicTemplate.TemplateVersion)
    {
        if (!ColorConversion.TryParseHex(baseHex, out byte r, out byte g, out byte b))
        {
            throw new ArgumentException($"Invalid 6-character hex color '{baseHex}'.", nameof(baseHex));
        }

        string normalizedHex = ColorConversion.ToHex(r, g, b);
        byte[] templateBytes = OrnareClassicTemplate.GetTemplateBytes();
        string sourceAssetHash = ImagePipeline.ComputeSourceHashStatic(templateBytes);

        return new RenderRecipe
        {
            SourceAssetHash = sourceAssetHash,
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            PaletteAlgorithmVersion = FolderPaletteGenerator.PaletteAlgorithmVersion,
            PaletteParameters = normalizedHex,
            SvgRecolorMode = 1, // SemanticTemplate
            SvgRecolorValue = normalizedHex,
            SvgRecolorVersion = 1,
            TargetFormat = "ico",
            TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
        };
    }

    /// <summary>
    /// Renders a full multi-resolution ICO for the specified color and template, utilizing the F18 render cache.
    /// </summary>
    public static OperationResult<NormalizedImageResult> RenderColoredTemplateIco(
        ImagePipeline pipeline,
        string baseHex,
        string outputIcoPath,
        string? cacheDirectory = null,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null,
        Action<string>? cacheInvalidate = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputIcoPath);

        RenderRecipe recipe;
        try
        {
            recipe = CreateRecipe(baseHex);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(ErrorCode.InvalidTarget, ex.Message);
        }

        byte[] templateBytes = OrnareClassicTemplate.GetTemplateBytes();
        return pipeline.ProcessRecipe(
            recipe,
            templateBytes,
            outputIcoPath,
            cacheLookup: cacheLookup,
            cacheStore: cacheStore,
            cacheInvalidate: cacheInvalidate,
            cacheDirectory: cacheDirectory);
    }

    /// <summary>
    /// Renders a fast, bounded 256x256 PNG preview for UI display without encoding a multi-frame ICO file.
    /// </summary>
    public static OperationResult<byte[]> RenderPreviewPng(
        string baseHex,
        string templateId = OrnareClassicTemplate.TemplateId)
    {
        if (!ColorConversion.TryParseHex(baseHex, out byte r, out byte g, out byte b))
        {
            return OperationResult.Failure<byte[]>(ErrorCode.InvalidTarget, $"Invalid 6-character hex color '{baseHex}'.");
        }

        string normalizedHex = ColorConversion.ToHex(r, g, b);
        var folderPalette = FolderPaletteGenerator.GenerateFromRgb(r, g, b);
        var semanticPalette = folderPalette.ToSemanticColorPalette();

        string templateSvg = OrnareClassicTemplate.SvgContent;
        var recolorResult = SvgRecolorService.RecolorSemanticTemplate(templateSvg, semanticPalette);
        if (!recolorResult.IsSuccess || string.IsNullOrEmpty(recolorResult.Value))
        {
            return OperationResult.Failure<byte[]>(recolorResult.ErrorCode, recolorResult.ErrorMessage ?? "SVG recoloring failed.");
        }

        byte[] recoloredSvgBytes = System.Text.Encoding.UTF8.GetBytes(recolorResult.Value);

        try
        {
            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Width = 256,
                Height = 256
            };

            using var image = new MagickImage(recoloredSvgBytes, readSettings);
            image.Format = MagickFormat.Png;
            image.ColorSpace = ColorSpace.sRGB;

            byte[] pngBytes = image.ToByteArray();
            return OperationResult.Success(pngBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.ImageCorrupted, $"Preview rasterization failed: {ex.Message}");
        }
    }
}
