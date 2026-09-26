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

public sealed class StatusPresetTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public StatusPresetTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_presets_{Guid.NewGuid():N}");
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
    public void CatalogContainsExactFourStatusPresetsWithVersionOne()
    {
        var status = StatusPresetCatalog.StatusPresets;
        Assert.Equal(4, status.Count);

        var inProgress = StatusPresetCatalog.GetPreset("status.in-progress");
        var done = StatusPresetCatalog.GetPreset("status.done");
        var archive = StatusPresetCatalog.GetPreset("status.archive");
        var important = StatusPresetCatalog.GetPreset("status.important");

        Assert.NotNull(inProgress);
        Assert.NotNull(done);
        Assert.NotNull(archive);
        Assert.NotNull(important);

        Assert.Equal(1, inProgress.Version);
        Assert.Equal(1, done.Version);
        Assert.Equal(1, archive.Version);
        Assert.Equal(1, important.Version);

        foreach (var p in status)
        {
            Assert.True(ColorConversion.TryParseHex(p.BaseHex, out _, out _, out _), $"BaseHex '{p.BaseHex}' must be valid hex.");
            Assert.True(StatusBadgeAssets.TryGetPathData(p.BadgeAssetId, out string? pathData) && !string.IsNullOrEmpty(pathData),
                $"BadgeAssetId '{p.BadgeAssetId}' must have valid path data.");
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayNameResourceKey));
            Assert.False(string.IsNullOrWhiteSpace(p.DescriptionResourceKey));
        }
    }

    [Fact]
    public void StatusBadgeAssetsGeneratesValidSvgForKnownBadges()
    {
        string[] badges =
        [
            "ic_fluent_clock_24_regular",
            "ic_fluent_checkmark_circle_24_regular",
            "ic_fluent_archive_24_regular",
            "ic_fluent_important_24_regular"
        ];

        foreach (var id in badges)
        {
            string svg = StatusBadgeAssets.GenerateBadgeSvg(id, "#FF0000");
            Assert.Contains("viewBox=\"0 0 24 24\"", svg);
            Assert.Contains("fill=\"#FF0000\"", svg);
            Assert.Contains("<path", svg);
        }

        Assert.Throws<ArgumentException>(() => StatusBadgeAssets.GenerateBadgeSvg("unknown_badge"));
    }

    [Theory]
    [InlineData(16, false)]
    [InlineData(20, false)]
    [InlineData(24, false)]
    [InlineData(32, true)]
    [InlineData(48, true)]
    [InlineData(64, true)]
    [InlineData(256, true)]
    public void StatusBadgeCompositorBadgeOmissionRulesEnforced(int frameSize, bool shouldComposite)
    {
        // Create a plain solid test frame
        using var frame = new MagickImage(MagickColors.DodgerBlue, (uint)frameSize, (uint)frameSize);
        using var baseline = new MagickImage(MagickColors.DodgerBlue, (uint)frameSize, (uint)frameSize);

        StatusBadgeCompositor.CompositeBadge(frame, "ic_fluent_clock_24_regular", "#2563EB");

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
    public void StatusBadgeCompositorContrastAdaptationEnsuresHighContrast()
    {
        // Light base color -> White disk or high contrast
        var (diskLight, glyphLight) = StatusBadgeCompositor.DetermineBadgeColors("#F8FAFC");
        Assert.NotNull(diskLight);
        Assert.NotNull(glyphLight);
        Assert.NotEqual(diskLight, glyphLight);

        // Dark base color -> White disk
        var (diskDark, glyphDark) = StatusBadgeCompositor.DetermineBadgeColors("#0F172A");
        Assert.NotNull(diskDark);
        Assert.NotNull(glyphDark);
        Assert.NotEqual(diskDark, glyphDark);

        // For all catalog presets, disk and glyph must have contrast >= 3.0:1
        foreach (var p in StatusPresetCatalog.All)
        {
            var (dHex, gHex) = StatusBadgeCompositor.DetermineBadgeColors(p.BaseHex);
            Assert.True(ColorConversion.TryParseHex(dHex, out byte dr, out byte dg, out byte db));
            Assert.True(ColorConversion.TryParseHex(gHex, out byte gr, out byte gg, out byte gb));

            double ratio = WcagContrast.ComputeContrastRatio(dr, dg, db, gr, gg, gb);
            Assert.True(ratio >= 3.0, $"Preset {p.Id} badge disk and glyph contrast ratio must be >= 3.0:1. Was {ratio:F2}:1");
        }
    }

    [Fact]
    public void CreatePresetRecipeGeneratesDeterministicRecipeWithBadgeDivergence()
    {
        var preset = StatusPresetCatalog.InProgress;
        var presetRecipe = FolderTemplateRenderer.CreatePresetRecipe(preset, "ornare-classic");
        var baseRecipe = FolderTemplateRenderer.CreateRecipe(preset.BaseHex, "ornare-classic");

        Assert.Equal(preset.Id, presetRecipe.PresetId);
        Assert.Equal(preset.BadgeAssetId, presetRecipe.BadgeAssetId);
        Assert.Equal(34, presetRecipe.BadgeScale);

        // Preset recipe and base recipe must have distinct render keys
        string presetKey = presetRecipe.ComputeRenderKey();
        string baseKey = baseRecipe.ComputeRenderKey();
        Assert.NotEqual(presetKey, baseKey);

        // Different presets must have distinct render keys
        var doneRecipe = FolderTemplateRenderer.CreatePresetRecipe(StatusPresetCatalog.Done, "ornare-classic");
        string doneKey = doneRecipe.ComputeRenderKey();
        Assert.NotEqual(presetKey, doneKey);
    }

    [Fact]
    public void RenderPresetIcoGeneratesValid10FrameIcoForAllPresets()
    {
        foreach (var preset in StatusPresetCatalog.All)
        {
            string outIco = Path.Combine(_tempDir, $"{preset.Id}.ico");
            var result = FolderTemplateRenderer.RenderPresetIco(_pipeline, preset, outIco, "ornare-classic");

            Assert.True(result.IsSuccess, $"Rendering preset {preset.Id} ICO failed: {result.ErrorMessage}");
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
    public void RenderPresetPreviewPngProducesValid256x256PngForAllPresets()
    {
        foreach (var preset in StatusPresetCatalog.All)
        {
            var result = FolderTemplateRenderer.RenderPresetPreviewPng(preset, "ornare-soft");
            Assert.True(result.IsSuccess, $"Preview rendering failed for {preset.Id}: {result.ErrorMessage}");
            Assert.NotNull(result.Value);
            Assert.True(result.Value.Length > 0);

            // PNG magic number: 0x89 0x50 0x4E 0x47
            byte[] bytes = result.Value;
            Assert.True(bytes.Length >= 8);
            Assert.Equal(0x89, bytes[0]);
            Assert.Equal(0x50, bytes[1]);
            Assert.Equal(0x4E, bytes[2]);
            Assert.Equal(0x47, bytes[3]);

            using var img = new MagickImage(bytes);
            Assert.Equal(256u, img.Width);
            Assert.Equal(256u, img.Height);
        }
    }

    [Fact]
    public void PerformancePresetCatalogLookupIsSubMillisecond()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            _ = StatusPresetCatalog.All;
            _ = StatusPresetCatalog.GetPreset("status.in-progress");
            _ = StatusPresetCatalog.GetPreset("status.done");
            _ = StatusPresetCatalog.GetPreset("status.archive");
            _ = StatusPresetCatalog.GetPreset("status.important");
        }
        sw.Stop();

        _output.WriteLine($"5,000 preset catalog queries took: {sw.ElapsedMilliseconds} ms");
        Assert.True(sw.ElapsedMilliseconds < 50, $"Catalog lookups must be sub-millisecond; took {sw.ElapsedMilliseconds} ms for 5,000 operations.");
    }
}
