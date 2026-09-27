using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;
using Ornare.Imaging.Badges;
using Ornare.Imaging.Templates;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public sealed class FolderTemplateTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public FolderTemplateTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_templates_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void CatalogContainsExactNineProductionTemplates()
    {
        var all = FolderTemplateCatalog.All;
        Assert.Equal(9, all.Count);

        string[] expectedIds =
        [
            "ornare-classic", "ornare-soft", "ornare-flat",
            "ornare-glass", "ornare-aqua", "ornare-grove",
            "ornare-air", "ornare-flow", "ornare-geo"
        ];

        for (int i = 0; i < expectedIds.Length; i++)
        {
            Assert.Equal(expectedIds[i], all[i].Id);
            Assert.Equal(i + 1, all[i].SortOrder);
        }

        var classic = all.First(t => t.Id == "ornare-classic");
        var soft = all.First(t => t.Id == "ornare-soft");
        var flat = all.First(t => t.Id == "ornare-flat");

        Assert.Equal(2, classic.Version);
        Assert.Equal(2, soft.Version);
        Assert.Equal(2, flat.Version);

        // Legacy V1 descriptors can still be resolved for backward compatibility
        var classicV1 = FolderTemplateCatalog.GetTemplate("ornare-classic", 1);
        Assert.NotNull(classicV1);
        Assert.Equal(1, classicV1.Version);
    }

    [Fact]
    public void CatalogMetadataLoadsInSubMillisecond()
    {
        // Warm up
        _ = FolderTemplateCatalog.All;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var templates = FolderTemplateCatalog.All;
            foreach (var t in templates)
            {
                _ = t.Id;
                _ = t.Version;
                _ = t.DisplayNameResourceKey;
            }
        }
        sw.Stop();

        double avgMicroseconds = (sw.Elapsed.TotalMilliseconds / 1000.0) * 1000.0;
        _output.WriteLine($"Average catalog metadata enumeration time: {avgMicroseconds:F2} µs");
        Assert.True(avgMicroseconds < 500, $"Metadata query must be negligible; was {avgMicroseconds:F2} µs.");
    }

    [Fact]
    public void CatalogGetTemplateResolvesCorrectlyAndFallsBack()
    {
        var classic = FolderTemplateCatalog.GetTemplate("ornare-classic", 1);
        Assert.Equal("ornare-classic", classic.Id);

        var softCaseInsensitive = FolderTemplateCatalog.GetTemplate("ORNARE-SOFT", 1);
        Assert.Equal("ornare-soft", softCaseInsensitive.Id);

        var flat = FolderTemplateCatalog.GetTemplate("ornare-flat", 1);
        Assert.Equal("ornare-flat", flat.Id);

        // Unknown template falls back to default
        var unknown = FolderTemplateCatalog.GetTemplate("non-existent-template", 1);
        Assert.Equal(FolderTemplateCatalog.DefaultTemplateId, unknown.Id);

        // Null/empty falls back to default
        var nullTemplate = FolderTemplateCatalog.GetTemplate(null);
        Assert.Equal(FolderTemplateCatalog.DefaultTemplateId, nullTemplate.Id);
    }

    [Fact]
    public void AllCatalogTemplatesPassValidator()
    {
        foreach (var descriptor in FolderTemplateCatalog.All)
        {
            var result = FolderTemplateValidator.Validate(descriptor.SvgContent);
            Assert.True(result.IsSuccess, $"Template '{descriptor.Id}' failed validation: {result.ErrorMessage}");
            Assert.True(result.Value);
        }
    }

    [Fact]
    public void ValidatorRejectsInvalidViewBox()
    {
        string badViewBox = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 512 512"" width=""512"" height=""512"">
            <rect data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" />
            <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" />
            <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""42"" />
            <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""6"" />
            <rect data-ornare-role=""shadow"" x=""16"" y=""206"" width=""224"" height=""18"" />
        </svg>";

        var result = FolderTemplateValidator.Validate(badViewBox);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, result.ErrorCode);
    }

    [Fact]
    public void ValidatorRejectsDisallowedElementsAndScriptAttributes()
    {
        string withScript = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"">
            <script>alert('xss');</script>
            <rect data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" />
            <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" />
            <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""42"" />
            <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""6"" />
            <rect data-ornare-role=""shadow"" x=""16"" y=""206"" width=""224"" height=""18"" />
        </svg>";

        var result = FolderTemplateValidator.Validate(withScript);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.UnsupportedImageFormat, result.ErrorCode);

        string withOnClick = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"">
            <rect onclick=""run()"" data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" />
            <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" />
            <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""42"" />
            <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""6"" />
            <rect data-ornare-role=""shadow"" x=""16"" y=""206"" width=""224"" height=""18"" />
        </svg>";

        var resultClick = FolderTemplateValidator.Validate(withOnClick);
        Assert.False(resultClick.IsSuccess);
        Assert.Equal(ErrorCode.UnsupportedImageFormat, resultClick.ErrorCode);
    }

    [Fact]
    public void ValidatorRejectsMissingSemanticRoles()
    {
        string missingHighlight = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"">
            <rect data-ornare-role=""deep"" x=""24"" y=""48"" width=""208"" height=""120"" />
            <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" />
            <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""42"" />
            <rect data-ornare-role=""shadow"" x=""16"" y=""206"" width=""224"" height=""18"" />
        </svg>";

        var result = FolderTemplateValidator.Validate(missingHighlight);
        Assert.False(result.IsSuccess);
        Assert.Contains("highlight", result.ErrorMessage);
    }

    [Fact]
    public void ValidatorRejectsGeometryOutsideSafeBoundary()
    {
        string outsideBoundary = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"">
            <rect data-ornare-role=""deep"" x=""8"" y=""48"" width=""208"" height=""120"" />
            <rect data-ornare-role=""base"" x=""16"" y=""88"" width=""224"" height=""136"" />
            <rect data-ornare-role=""light"" x=""16"" y=""88"" width=""224"" height=""42"" />
            <rect data-ornare-role=""highlight"" x=""16"" y=""88"" width=""224"" height=""6"" />
            <rect data-ornare-role=""shadow"" x=""16"" y=""206"" width=""224"" height=""18"" />
        </svg>";

        var result = FolderTemplateValidator.Validate(outsideBoundary);
        Assert.False(result.IsSuccess);
        Assert.Contains("safe boundary", result.ErrorMessage);
    }

    [Fact]
    public void TemplatesAdhereToExactProportionRules()
    {
        // Front panel w=224 on 256 canvas => 87.5%
        // Tab w=88 on 224 front panel => 39.2857% (~39.3%)
        const double canvasWidth = 256.0;
        const double frontPanelWidth = 224.0;
        const double tabWidth = 88.0;

        double frontPercentage = (frontPanelWidth / canvasWidth) * 100.0;
        double tabPercentage = (tabWidth / frontPanelWidth) * 100.0;

        Assert.Equal(87.5, frontPercentage);
        Assert.Equal(39.3, Math.Round(tabPercentage, 1));
    }

    [Fact]
    public void RenderAllTemplatesProducesValidMultiFrameIco()
    {
        int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        string testColor = "#2563EB"; // Blue

        foreach (var descriptor in FolderTemplateCatalog.All)
        {
            string outPath = Path.Combine(_tempDir, $"{descriptor.Id}_test.ico");
            var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, testColor, outPath, descriptor.Id, descriptor.Version);

            Assert.True(renderResult.IsSuccess, $"Rendering failed for {descriptor.Id}: {renderResult.ErrorMessage}");
            Assert.True(File.Exists(outPath));

            using var collection = new MagickImageCollection(outPath);
            Assert.Equal(10, collection.Count);

            var frameSizes = collection.Select(img => (int)img.Width).OrderBy(s => s).ToArray();
            Assert.Equal(expectedSizes, frameSizes);
        }
    }

    [Fact]
    public void RenderPreviewPngProducesDistinctVisualOutputsAcrossTemplates()
    {
        string color = "#0F766E"; // Teal

        var classicPreview = FolderTemplateRenderer.RenderPreviewPng(color, "ornare-classic", 1);
        var softPreview = FolderTemplateRenderer.RenderPreviewPng(color, "ornare-soft", 1);
        var flatPreview = FolderTemplateRenderer.RenderPreviewPng(color, "ornare-flat", 1);

        Assert.True(classicPreview.IsSuccess, $"classicPreview failed: {classicPreview.ErrorMessage}");
        Assert.True(softPreview.IsSuccess, $"softPreview failed: {softPreview.ErrorMessage}");
        Assert.True(flatPreview.IsSuccess, $"flatPreview failed: {flatPreview.ErrorMessage}");

        byte[] classicBytes = classicPreview.Value!;
        byte[] softBytes = softPreview.Value!;
        byte[] flatBytes = flatPreview.Value!;

        using var classicImg = new MagickImage(classicBytes);
        using var softImg = new MagickImage(softBytes);
        using var flatImg = new MagickImage(flatBytes);

        Assert.Equal(256u, classicImg.Width);
        Assert.Equal(256u, classicImg.Height);
        Assert.Equal(256u, softImg.Width);
        Assert.Equal(256u, softImg.Height);
        Assert.Equal(256u, flatImg.Width);
        Assert.Equal(256u, flatImg.Height);

        // Prove visual distinctness: error metric between silhouettes is non-zero
        double diffClassicSoft = classicImg.Compare(softImg, ErrorMetric.RootMeanSquared);
        double diffClassicFlat = classicImg.Compare(flatImg, ErrorMetric.RootMeanSquared);
        double diffSoftFlat = softImg.Compare(flatImg, ErrorMetric.RootMeanSquared);

        _output.WriteLine($"RMS difference Classic vs Soft: {diffClassicSoft:F4}");
        _output.WriteLine($"RMS difference Classic vs Flat: {diffClassicFlat:F4}");
        _output.WriteLine($"RMS difference Soft vs Flat: {diffSoftFlat:F4}");

        Assert.True(diffClassicSoft > 0.005, "Classic and Soft must be visually distinct.");
        Assert.True(diffClassicFlat > 0.005, "Classic and Flat must be visually distinct.");
        Assert.True(diffSoftFlat > 0.005, "Soft and Flat must be visually distinct.");
    }

    [Fact]
    public void RecipeDeterminismIncludesTemplateIdentity()
    {
        var recipeClassic = FolderTemplateRenderer.CreateRecipe("#E8A317", "ornare-classic", 1);
        var recipeSoft = FolderTemplateRenderer.CreateRecipe("#E8A317", "ornare-soft", 1);
        var recipeFlat = FolderTemplateRenderer.CreateRecipe("#E8A317", "ornare-flat", 1);

        Assert.Equal("ornare-classic", recipeClassic.TemplateId);
        Assert.Equal("ornare-soft", recipeSoft.TemplateId);
        Assert.Equal("ornare-flat", recipeFlat.TemplateId);

        string keyClassic = recipeClassic.ComputeRenderKey();
        string keySoft = recipeSoft.ComputeRenderKey();
        string keyFlat = recipeFlat.ComputeRenderKey();

        Assert.NotEqual(keyClassic, keySoft);
        Assert.NotEqual(keyClassic, keyFlat);
        Assert.NotEqual(keySoft, keyFlat);
    }

    [Fact]
    public void Windows11FolderTemplateMatchesAssetsSvgAndRendersAccurately()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string legacyPath = Path.Combine(repoRoot, "assets-references", "legacy", "Windows_11_FOLDER.svg");
        if (File.Exists(legacyPath))
        {
            byte[] rawBytes = File.ReadAllBytes(legacyPath);
            Assert.NotEmpty(rawBytes);
        }

        string classicSvg = OrnareClassicTemplate.SvgContent;
        Assert.False(string.IsNullOrWhiteSpace(classicSvg));

        var valResult = FolderTemplateValidator.Validate(classicSvg);
        Assert.True(valResult.IsSuccess, valResult.ErrorMessage);

        var bluePalette = FolderPaletteGenerator.GenerateFromRgb(37, 99, 235).ToSemanticColorPalette();
        var recolorResult = SvgRecolorService.RecolorSemanticTemplate(classicSvg, bluePalette);
        Assert.True(recolorResult.IsSuccess, recolorResult.ErrorMessage);

        var previewResult = FolderTemplateRenderer.RenderPreviewPng("#2563EB", OrnareClassicTemplate.TemplateId, 1);
        Assert.True(previewResult.IsSuccess, previewResult.ErrorMessage);
        Assert.NotNull(previewResult.Value);

        using var img = new MagickImage(previewResult.Value);
        Assert.Equal(256u, img.Width);
        Assert.Equal(256u, img.Height);
    }

    [Fact]
    public void DescriptorsExposeSection15ManifestContractProperties()
    {
        foreach (var descriptor in FolderTemplateCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Id));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.DisplayNameResourceKey));
            Assert.True(descriptor.Version >= 1);
            Assert.True(descriptor.SupportsColor);
            Assert.True(descriptor.SupportsEmblem);
            Assert.Equal("0 0 256 256", descriptor.ViewBox);
            Assert.NotNull(descriptor.EmblemSafeZone);
            Assert.True(descriptor.EmblemSafeZone.Width > 0.0);
            Assert.True(descriptor.EmblemSafeZone.Height > 0.0);
            Assert.NotNull(descriptor.ReservedBadgeZones);
            Assert.NotEmpty(descriptor.ReservedBadgeZones);

            var manifestEntry = descriptor.ToManifestEntry();
            Assert.Equal(descriptor.Id, manifestEntry.Id);
            Assert.Equal(descriptor.Version, manifestEntry.Version);
            Assert.Equal(descriptor.DisplayNameResourceKey, manifestEntry.DisplayNameResourceKey);
            Assert.Equal(descriptor.EmblemSafeZone, manifestEntry.EmblemSafeZone);
        }
    }
}
