using System;
using System.Diagnostics;
using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;
using Ornare.Imaging.Badges;
using Ornare.Imaging.Templates;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public sealed class PriorityPresetTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public PriorityPresetTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_priority_{Guid.NewGuid():N}");
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
    public void CatalogContainsExactThreePriorityPresetsWithVersionOne()
    {
        var priorityPresets = StatusPresetCatalog.PriorityPresets;
        Assert.Equal(3, priorityPresets.Count);

        var high = StatusPresetCatalog.GetPreset("priority.high");
        var medium = StatusPresetCatalog.GetPreset("priority.medium");
        var low = StatusPresetCatalog.GetPreset("priority.low");

        Assert.NotNull(high);
        Assert.NotNull(medium);
        Assert.NotNull(low);

        Assert.Equal(1, high.Version);
        Assert.Equal(1, medium.Version);
        Assert.Equal(1, low.Version);

        Assert.Equal("#DC3545", high.BaseHex);
        Assert.Equal("#E8A317", medium.BaseHex);
        Assert.Equal("#2563EB", low.BaseHex);

        Assert.Equal("ornare_priority_high_24", high.BadgeAssetId);
        Assert.Equal("ornare_priority_medium_24", medium.BadgeAssetId);
        Assert.Equal("ornare_priority_low_24", low.BadgeAssetId);

        foreach (var p in priorityPresets)
        {
            Assert.True(ColorConversion.TryParseHex(p.BaseHex, out _, out _, out _), $"BaseHex '{p.BaseHex}' must be valid hex.");
            Assert.True(StatusBadgeAssets.TryGetPathData(p.BadgeAssetId, out string? pathData) && !string.IsNullOrEmpty(pathData),
                $"BadgeAssetId '{p.BadgeAssetId}' must have valid path data.");
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayNameResourceKey));
            Assert.False(string.IsNullOrWhiteSpace(p.DescriptionResourceKey));
        }
    }

    [Fact]
    public void OrnareGeometricBadgeSvgIsValidAndStandalone()
    {
        var expectedPaths = new[]
        {
            ("ornare_priority_high_24", "M4 16L8 16L12 10L16 16L20 16L12 4Z"),
            ("ornare_priority_medium_24", "M6 10H18A2 2 0 0 1 20 12A2 2 0 0 1 18 14H6A2 2 0 0 1 4 12A2 2 0 0 1 6 10Z"),
            ("ornare_priority_low_24", "M4 8L8 8L12 14L16 8L20 8L12 20Z")
        };

        foreach (var (assetId, expectedPath) in expectedPaths)
        {
            Assert.True(StatusBadgeAssets.TryGetPathData(assetId, out string? pathData));
            Assert.Equal(expectedPath, pathData);

            string svg = StatusBadgeAssets.GenerateBadgeSvg(assetId, "#123456");
            Assert.Contains("viewBox=\"0 0 24 24\"", svg);
            Assert.Contains("width=\"24\"", svg);
            Assert.Contains("height=\"24\"", svg);
            Assert.Contains("fill=\"#123456\"", svg);
            Assert.Contains(expectedPath, svg);

            // Verify standalone geometry: no filters, texts, gradients, images, clip paths
            Assert.DoesNotContain("<filter", svg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<linearGradient", svg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<radialGradient", svg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<image", svg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<clipPath", svg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<mask", svg, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(16, false)]
    [InlineData(20, false)]
    [InlineData(24, false)]
    [InlineData(32, true)]
    [InlineData(48, true)]
    [InlineData(64, true)]
    [InlineData(256, true)]
    public void PriorityBadgeOmissionRulesEnforced(int frameSize, bool shouldComposite)
    {
        using var frame = new MagickImage(MagickColors.Crimson, (uint)frameSize, (uint)frameSize);
        using var baseline = new MagickImage(MagickColors.Crimson, (uint)frameSize, (uint)frameSize);

        StatusBadgeCompositor.CompositeBadge(frame, "ornare_priority_high_24", "#DC3545");

        double diff = frame.Compare(baseline, ErrorMetric.RootMeanSquared);

        if (shouldComposite)
        {
            Assert.True(diff > 0, $"Badge should be composited on {frameSize}x{frameSize} frame.");
        }
        else
        {
            Assert.Equal(0.0, diff);
        }
    }

    [Fact]
    public void ContrastRatioBetweenDiskAndGlyphExceedsThreeToOne()
    {
        foreach (var p in StatusPresetCatalog.PriorityPresets)
        {
            var (dHex, gHex) = StatusBadgeCompositor.DetermineBadgeColors(p.BaseHex);
            Assert.True(ColorConversion.TryParseHex(dHex, out byte dr, out byte dg, out byte db));
            Assert.True(ColorConversion.TryParseHex(gHex, out byte gr, out byte gg, out byte gb));

            double ratio = WcagContrast.ComputeContrastRatio(dr, dg, db, gr, gg, gb);
            Assert.True(ratio >= 3.0, $"Priority preset {p.Id} badge disk and glyph contrast ratio must be >= 3.0:1. Was {ratio:F2}:1");
        }
    }

    [Fact]
    public void DeterministicRenderKeyDivergenceAcrossPriorityPresets()
    {
        var highRecipe = FolderTemplateRenderer.CreatePresetRecipe(StatusPresetCatalog.PriorityHigh, "ornare-classic");
        var medRecipe = FolderTemplateRenderer.CreatePresetRecipe(StatusPresetCatalog.PriorityMedium, "ornare-classic");
        var lowRecipe = FolderTemplateRenderer.CreatePresetRecipe(StatusPresetCatalog.PriorityLow, "ornare-classic");

        var highBase = FolderTemplateRenderer.CreateRecipe(StatusPresetCatalog.PriorityHigh.BaseHex, "ornare-classic");

        string highKey = highRecipe.ComputeRenderKey();
        string medKey = medRecipe.ComputeRenderKey();
        string lowKey = lowRecipe.ComputeRenderKey();
        string baseKey = highBase.ComputeRenderKey();

        Assert.NotEqual(highKey, medKey);
        Assert.NotEqual(highKey, lowKey);
        Assert.NotEqual(medKey, lowKey);

        Assert.NotEqual(highKey, baseKey);
    }

    [Fact]
    public void RenderPriorityPresetIcoGeneratesValid10FrameIco()
    {
        foreach (var preset in StatusPresetCatalog.PriorityPresets)
        {
            string outIco = Path.Combine(_tempDir, $"{preset.Id}.ico");
            var result = FolderTemplateRenderer.RenderPresetIco(_pipeline, preset, outIco, "ornare-classic");

            Assert.True(result.IsSuccess, $"Rendering priority preset {preset.Id} ICO failed: {result.ErrorMessage}");
            Assert.NotNull(result.Value);
            Assert.True(File.Exists(outIco));

            using var collection = new MagickImageCollection(outIco);
            Assert.Equal(10, collection.Count);

            uint[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
            for (int i = 0; i < expectedSizes.Length; i++)
            {
                Assert.Equal(expectedSizes[i], collection[i].Width);
                Assert.Equal(expectedSizes[i], collection[i].Height);
            }
        }
    }

    [Fact]
    public void PerformancePriorityCatalogLookupIsSubMillisecond()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            _ = StatusPresetCatalog.PriorityPresets;
            _ = StatusPresetCatalog.GetPreset("priority.high");
            _ = StatusPresetCatalog.GetPreset("priority.medium");
            _ = StatusPresetCatalog.GetPreset("priority.low");
        }
        sw.Stop();

        _output.WriteLine($"4,000 priority preset catalog queries took: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 50, $"Priority catalog lookups must be sub-millisecond; took {sw.ElapsedMilliseconds} ms for 4,000 operations.");
    }
}
