using System;
using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging.Badges;
using Ornare.Imaging.Emblems;
using Ornare.Imaging.Templates;

namespace Ornare.Imaging;

/// <summary>
/// Authoritative renderer for folder style templates and color variants (F01).
/// Binds OrnareClassicTemplate + FolderPaletteGenerator (F10) + SvgRecolorService (F09) + ImagePipeline (F18) + FolderEmblemCompositor.
/// </summary>
public static class FolderTemplateRenderer
{
    static FolderTemplateRenderer()
    {
        ImageSecurityPolicy.Initialize();
    }
    /// <summary>
    /// Builds the deterministic RenderRecipe for a colored template icon with optional center emblem.
    /// </summary>
    public static RenderRecipe CreateRecipe(
        string baseHex,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
        FolderEmblemRecipe? emblem = null,
        string badgeAssetId = "",
        int badgeVersion = 1,
        int badgeAnchor = 0,
        int badgeScale = 0)
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
            BadgeAssetId = badgeAssetId,
            BadgeVersion = badgeVersion,
            BadgeAnchor = badgeAnchor,
            BadgeScale = badgeScale,
            TargetFormat = "ico",
            TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256],
            EmblemKind = emblem != null && emblem.HasEmblem ? (int)emblem.Kind : 0,
            EmblemContentId = emblem?.ContentId ?? string.Empty,
            EmblemGraphemeSequence = emblem?.GraphemeSequence ?? string.Empty,
            EmblemForegroundColor = emblem?.ForegroundColor ?? "Auto",
            EmblemScale = emblem?.Scale ?? 1.0,
            EmblemOffsetX = emblem?.OffsetX ?? 0.0,
            EmblemOffsetY = emblem?.OffsetY ?? 0.0
        };
    }

    /// <summary>
    /// Renders a full multi-resolution ICO for the specified color and template, utilizing the F18 render cache.
    /// </summary>
    public static OperationResult<NormalizedImageResult> RenderColoredTemplateIco(
        IImagePipeline pipeline,
        string baseHex,
        string outputIcoPath,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
        FolderEmblemRecipe? emblem = null,
        string badgeAssetId = "",
        int badgeVersion = 1,
        int badgeAnchor = 0,
        int badgeScale = 0,
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
            recipe = CreateRecipe(baseHex, templateId, templateVersion, emblem, badgeAssetId, badgeVersion, badgeAnchor, badgeScale);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<NormalizedImageResult>(ErrorCode.InvalidTarget, "The template could not be rendered.", ex.ToString());
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
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
        FolderEmblemRecipe? emblem = null)
    {
        return RenderTemplatePng(baseHex, 256, templateId, templateVersion, emblem);
    }

    /// <summary>
    /// Renders a template directly to PNG at any allowed target size (256, 512, 1024) (F14).
    /// </summary>
    public static OperationResult<byte[]> RenderTemplatePng(
        string baseHex,
        int targetSize = ExportPngConstants.DefaultSize,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
        FolderEmblemRecipe? emblem = null)
    {
        if (!ExportPngConstants.IsAllowedSize(targetSize))
        {
            return OperationResult.Failure<byte[]>(ErrorCode.InvalidTarget, $"Invalid target size {targetSize}.");
        }

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
                Width = (uint)targetSize,
                Height = (uint)targetSize,
                BackgroundColor = MagickColors.Transparent
            };

            byte[] pngBytes;
            lock (SvgRasterizationGate.Lock)
            {
                using var image = new MagickImage(recoloredSvgBytes, readSettings);
                if (image.Width != targetSize || image.Height != targetSize)
                {
                    image.FilterType = FilterType.Lanczos;
                    image.Resize((uint)targetSize, (uint)targetSize);
                }

                if (emblem != null && emblem.HasEmblem)
                {
                    FolderEmblemCompositor.CompositeEmblem(image, emblem, normalizedHex, descriptor.EmblemSafeZone);
                }

                image.Format = MagickFormat.Png;
                image.ColorSpace = ColorSpace.sRGB;
                pngBytes = image.ToByteArray();
            }
            return OperationResult.Success(pngBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.ImageCorrupted, "The preview could not be generated.", ex.ToString());
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
        IImagePipeline pipeline,
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
            return OperationResult.Failure<NormalizedImageResult>(ErrorCode.InvalidTarget, "The template could not be rendered.", ex.ToString());
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
        return RenderPresetPng(preset, 256, templateId, templateVersion);
    }

    /// <summary>
    /// Renders a status preset directly to PNG at any allowed target size (256, 512, 1024) (F14).
    /// </summary>
    public static OperationResult<byte[]> RenderPresetPng(
        StatusPreset preset,
        int targetSize = ExportPngConstants.DefaultSize,
        string templateId = FolderTemplateCatalog.DefaultTemplateId,
        int templateVersion = FolderTemplateCatalog.DefaultTemplateVersion)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var baseResult = RenderTemplatePng(preset.BaseHex, targetSize, templateId, templateVersion);
        if (!baseResult.IsSuccess || baseResult.Value == null)
        {
            return baseResult;
        }

        try
        {
            using var image = new MagickImage(baseResult.Value);
            StatusBadgeCompositor.CompositeBadge(image, preset.BadgeAssetId, preset.BaseHex);
            image.Format = MagickFormat.Png;
            image.ColorSpace = ColorSpace.sRGB;

            byte[] pngBytes = image.ToByteArray();
            return OperationResult.Success(pngBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<byte[]>(ErrorCode.ImageCorrupted, "The preset image could not be generated.", ex.ToString());
        }
    }
}
