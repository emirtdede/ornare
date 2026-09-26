using System;
using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging.Badges;
using Ornare.Imaging.Templates;

namespace Ornare.Imaging;

/// <summary>
/// Authoritative renderer for folder style templates and color variants (F01).
/// Binds OrnareClassicTemplate + FolderPaletteGenerator (F10) + SvgRecolorService (F09) + ImagePipeline (F18).
/// </summary>
public static class FolderTemplateRenderer
{
    static FolderTemplateRenderer()
    {
        ImageSecurityPolicy.Initialize();
    }
    /// <summary>
    /// Builds the deterministic RenderRecipe for a colored template icon.
    /// </summary>
    public static RenderRecipe CreateRecipe(
        string baseHex,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion)
    {
        if (!ColorConversion.TryParseHex(baseHex, out byte r, out byte g, out byte b))
        {
            throw new ArgumentException($"Invalid 6-character hex color '{baseHex}'.", nameof(baseHex));
        }

        var descriptor = FolderTemplateCatalog.GetTemplate(templateId, templateVersion);
        string normalizedHex = ColorConversion.ToHex(r, g, b);
        byte[] templateBytes = descriptor.GetBytes();
        string sourceAssetHash = ImagePipeline.ComputeSourceHashStatic(templateBytes);

        return new RenderRecipe
        {
            SourceAssetHash = sourceAssetHash,
            TemplateId = descriptor.Id,
            TemplateVersion = descriptor.Version,
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
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
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
            recipe = CreateRecipe(baseHex, templateId, templateVersion);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(ErrorCode.InvalidTarget, ex.Message);
        }

        var descriptor = FolderTemplateCatalog.GetTemplate(recipe.TemplateId, recipe.TemplateVersion);
        byte[] templateBytes = descriptor.GetBytes();
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
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion)
    {
        if (!ColorConversion.TryParseHex(baseHex, out byte r, out byte g, out byte b))
        {
            return OperationResult.Failure<byte[]>(ErrorCode.InvalidTarget, $"Invalid 6-character hex color '{baseHex}'.");
        }

        var descriptor = FolderTemplateCatalog.GetTemplate(templateId, templateVersion);
        string normalizedHex = ColorConversion.ToHex(r, g, b);
        var folderPalette = FolderPaletteGenerator.GenerateFromRgb(r, g, b);
        var semanticPalette = folderPalette.ToSemanticColorPalette();

        string templateSvg = descriptor.SvgContent;
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
                Height = 256,
                BackgroundColor = MagickColors.Transparent
            };

            byte[] pngBytes;
            lock (SvgRasterizationGate.Lock)
            {
                using var image = new MagickImage(recoloredSvgBytes, readSettings);
                image.Format = MagickFormat.Png;
                image.ColorSpace = ColorSpace.sRGB;
                pngBytes = image.ToByteArray();
            }
            return OperationResult.Success(pngBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.ImageCorrupted, $"Preview rasterization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the deterministic RenderRecipe for a status preset icon (F04).
    /// </summary>
    public static RenderRecipe CreatePresetRecipe(
        StatusPreset preset,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var baseRecipe = CreateRecipe(preset.BaseHex, templateId, templateVersion);
        return baseRecipe with
        {
            PresetId = preset.Id,
            PresetVersion = preset.Version,
            BadgeAssetId = preset.BadgeAssetId,
            BadgeVersion = preset.BadgeVersion,
            BadgeAnchor = 0,
            BadgeScale = 34
        };
    }

    /// <summary>
    /// Renders a full multi-resolution ICO for the specified status preset and template (F04), utilizing the F18 render cache.
    /// Badges are omitted on 16, 20, and 24 px frames and composited on >= 32 px frames.
    /// </summary>
    public static OperationResult<NormalizedImageResult> RenderPresetIco(
        ImagePipeline pipeline,
        StatusPreset preset,
        string outputIcoPath,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
        string? cacheDirectory = null,
        Func<string, RenderCacheEntry?>? cacheLookup = null,
        Action<RenderCacheEntry>? cacheStore = null,
        Action<string>? cacheInvalidate = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputIcoPath);

        RenderRecipe recipe;
        try
        {
            recipe = CreatePresetRecipe(preset, templateId, templateVersion);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(ErrorCode.InvalidTarget, ex.Message);
        }

        var descriptor = FolderTemplateCatalog.GetTemplate(recipe.TemplateId, recipe.TemplateVersion);
        byte[] templateBytes = descriptor.GetBytes();
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
    /// Renders a fast, bounded 256x256 PNG preview for a status preset with the badge composited.
    /// </summary>
    public static OperationResult<byte[]> RenderPresetPreviewPng(
        StatusPreset preset,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var basePreviewResult = RenderPreviewPng(preset.BaseHex, templateId, templateVersion);
        if (!basePreviewResult.IsSuccess || basePreviewResult.Value == null)
        {
            return basePreviewResult;
        }

        try
        {
            using var image = new MagickImage(basePreviewResult.Value);
            StatusBadgeCompositor.CompositeBadge(image, preset.BadgeAssetId, preset.BaseHex);
            image.Format = MagickFormat.Png;
            image.ColorSpace = ColorSpace.sRGB;

            byte[] pngBytes = image.ToByteArray();
            return OperationResult.Success(pngBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.ImageCorrupted, $"Preset preview rasterization failed: {ex.Message}");
        }
    }
}
