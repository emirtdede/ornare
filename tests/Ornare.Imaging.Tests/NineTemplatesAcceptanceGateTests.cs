using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

/// <summary>
/// Section 48 Template Acceptance Gate Tests for all 9 original Ornare folder templates.
/// Validates security, geometry, semantic roles, multi-resolution render smoke (16..256),
/// manifest compliance, and provenance integrity.
/// </summary>
public sealed class NineTemplatesAcceptanceGateTests : IDisposable
{
    private static readonly int[] RenderSmokeResolutions = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public NineTemplatesAcceptanceGateTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_phase03_gate_{Guid.NewGuid():N}");
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
    public void AllNineTemplatesHaveUniqueSilhouettesAndIds()
    {
        var templates = FolderTemplateCatalog.All;
        Assert.Equal(9, templates.Count);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var svgHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in templates)
        {
            Assert.True(ids.Add(t.Id), $"Duplicate template ID: {t.Id}");

            string hash = Convert.ToHexString(SHA256.HashData(t.GetBytes()));
            Assert.True(svgHashes.Add(hash), $"Template '{t.Id}' has duplicate SVG content.");
        }
    }

    [Fact]
    public void AllNineTemplatesPassSecurityAndGeometryValidation()
    {
        foreach (var t in FolderTemplateCatalog.All)
        {
            // 1. SvgSecurityValidator preflight
            var secResult = SvgSecurityValidator.Validate(t.SvgContent);
            Assert.True(secResult.IsSuccess, $"Template '{t.Id}' failed SVG security preflight: {secResult.ErrorMessage}");

            // 2. FolderTemplateValidator (viewBox, safe margins, semantic roles)
            var valResult = FolderTemplateValidator.Validate(t.SvgContent);
            Assert.True(valResult.IsSuccess, $"Template '{t.Id}' failed folder template validation: {valResult.ErrorMessage}");
        }
    }

    [Fact]
    public void AllNineTemplatesDeclareValidSafeZonesAndBadgeZones()
    {
        foreach (var t in FolderTemplateCatalog.All)
        {
            Assert.NotNull(t.EmblemSafeZone);
            Assert.True(t.EmblemSafeZone.Width > 0.0 && t.EmblemSafeZone.Width <= 1.0);
            Assert.True(t.EmblemSafeZone.Height > 0.0 && t.EmblemSafeZone.Height <= 1.0);
            Assert.True(t.EmblemSafeZone.X >= 0.0 && t.EmblemSafeZone.Right <= 1.0);
            Assert.True(t.EmblemSafeZone.Y >= 0.0 && t.EmblemSafeZone.Bottom <= 1.0);

            Assert.NotNull(t.ReservedBadgeZones);
            Assert.NotEmpty(t.ReservedBadgeZones);
            foreach (var badgeZone in t.ReservedBadgeZones)
            {
                Assert.True(badgeZone.Width > 0.0 && badgeZone.Width <= 1.0);
                Assert.True(badgeZone.Height > 0.0 && badgeZone.Height <= 1.0);
                Assert.True(badgeZone.X >= 0.0 && badgeZone.Right <= 1.0);
                Assert.True(badgeZone.Y >= 0.0 && badgeZone.Bottom <= 1.0);

                // Section 17: Emblem must not intersect badge zones
                Assert.False(t.EmblemSafeZone.IntersectsWith(badgeZone),
                    $"Template '{t.Id}' emblem safe zone intersects with reserved badge zone.");
            }
        }
    }

    [Theory]
    [InlineData("ornare-classic")]
    [InlineData("ornare-soft")]
    [InlineData("ornare-flat")]
    [InlineData("ornare-glass")]
    [InlineData("ornare-aqua")]
    [InlineData("ornare-grove")]
    [InlineData("ornare-air")]
    [InlineData("ornare-flow")]
    [InlineData("ornare-geo")]
    public void TemplateRendersAcrossAllTenStandardResolutions(string templateId)
    {
        var descriptor = FolderTemplateCatalog.GetTemplate(templateId);
        Assert.Equal(templateId, descriptor.Id);

        // Recolor template with Windows Blue
        var palette = FolderPaletteGenerator.GenerateFromRgb(37, 99, 235).ToSemanticColorPalette();
        var recolorResult = SvgRecolorService.RecolorSemanticTemplate(descriptor.SvgContent, palette);
        Assert.True(recolorResult.IsSuccess, recolorResult.ErrorMessage);
        byte[] recoloredSvgBytes = System.Text.Encoding.UTF8.GetBytes(recolorResult.Value!);

        // Render at each required resolution: 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
        foreach (int res in RenderSmokeResolutions)
        {
            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Width = (uint)res,
                Height = (uint)res,
                BackgroundColor = MagickColors.Transparent
            };

            using var image = new MagickImage(recoloredSvgBytes, readSettings);
            if (image.Width != (uint)res || image.Height != (uint)res)
            {
                image.FilterType = FilterType.Lanczos;
                image.Resize((uint)res, (uint)res);
            }
            Assert.Equal((uint)res, image.Width);
            Assert.Equal((uint)res, image.Height);

            // Verify non-empty raster output (alpha channel has visible content)
            var stats = image.Statistics();
            Assert.NotNull(stats);
            var alphaChannel = stats.GetChannel(PixelChannel.Alpha);
            Assert.NotNull(alphaChannel);
            Assert.True(alphaChannel.Mean > 0.0, $"Rendered frame {res}x{res} for '{templateId}' is completely transparent.");
        }
    }

    [Theory]
    [InlineData("ornare-classic")]
    [InlineData("ornare-soft")]
    [InlineData("ornare-flat")]
    [InlineData("ornare-glass")]
    [InlineData("ornare-aqua")]
    [InlineData("ornare-grove")]
    [InlineData("ornare-air")]
    [InlineData("ornare-flow")]
    [InlineData("ornare-geo")]
    public void TemplateProducesValidTenFrameIco(string templateId)
    {
        var descriptor = FolderTemplateCatalog.GetTemplate(templateId);
        string outIco = Path.Combine(_tempDir, $"{templateId}.ico");

        var result = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, "#0078D4", outIco, templateId, descriptor.Version);
        Assert.True(result.IsSuccess, $"ICO generation failed for '{templateId}': {result.ErrorMessage}");
        Assert.True(File.Exists(outIco));
        Assert.True(_pipeline.IsValidIco(outIco, expectedMinFrames: 10));

        using var collection = new MagickImageCollection(outIco);
        Assert.Equal(10, collection.Count);
        for (int i = 0; i < RenderSmokeResolutions.Length; i++)
        {
            Assert.Equal((uint)RenderSmokeResolutions[i], collection[i].Width);
            Assert.Equal((uint)RenderSmokeResolutions[i], collection[i].Height);
        }
    }

    [Fact]
    public void FolderTemplateManifestDeserializesAndMatchesCatalog()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string manifestPath = Path.Combine(repoRoot, "assets", "folders", "manifest.json");

        Assert.True(File.Exists(manifestPath), $"Manifest not found at {manifestPath}");
        string json = File.ReadAllText(manifestPath);

        var manifest = JsonSerializer.Deserialize(json, FolderJsonContext.Default.FolderTemplateManifest);
        Assert.NotNull(manifest);
        Assert.Equal(9, manifest.Templates.Count);

        for (int i = 0; i < 9; i++)
        {
            var manifestEntry = manifest.Templates[i];
            var catalogEntry = FolderTemplateCatalog.All[i];

            Assert.Equal(catalogEntry.Id, manifestEntry.Id);
            Assert.Equal(catalogEntry.Version, manifestEntry.Version);
            Assert.Equal(catalogEntry.DisplayNameResourceKey, manifestEntry.DisplayNameResourceKey);
            Assert.Equal(catalogEntry.SortOrder, manifestEntry.SortOrder);
            Assert.Equal(catalogEntry.ViewBox, manifestEntry.ViewBox);
        }
    }

    [Fact]
    public void ProvenanceFilesExistForAllNineTemplatesWithMatchingHashes()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string provDir = Path.Combine(repoRoot, "assets", "folders", "provenance");
        string svgDir = Path.Combine(repoRoot, "assets", "folders");

        Assert.True(Directory.Exists(provDir), $"Provenance directory not found at {provDir}");

        foreach (var t in FolderTemplateCatalog.All)
        {
            string provFile = Path.Combine(provDir, $"{t.Id}.md");
            Assert.True(File.Exists(provFile), $"Provenance file missing for {t.Id}: {provFile}");

            string provContent = File.ReadAllText(provFile);
            Assert.Contains(t.Id, provContent, StringComparison.Ordinal);
            Assert.Contains("Ornare / Vellium", provContent, StringComparison.Ordinal);
            Assert.Contains("Original from blank canvas", provContent, StringComparison.Ordinal);
            Assert.Contains($"Template Version**: {t.Version}", provContent, StringComparison.Ordinal);

            // Compute actual SVG file hash
            string svgPath = Path.Combine(svgDir, $"{t.Id}.svg");
            Assert.True(File.Exists(svgPath), $"SVG file missing: {svgPath}");

            byte[] fileBytes = File.ReadAllBytes(svgPath);
            string expectedHash = Convert.ToHexString(SHA256.HashData(fileBytes));

            Assert.Contains(expectedHash, provContent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
