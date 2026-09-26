using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public sealed class FolderColorsTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public FolderColorsTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_colors_{Guid.NewGuid():N}");
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
    public void CatalogContainsExactTwelveCuratedColorsWithoutDuplicates()
    {
        var expected = new (string id, string hex)[]
        {
            ("color.amber", "#E8A317"),
            ("color.orange", "#F97316"),
            ("color.red", "#DC3545"),
            ("color.rose", "#E11D48"),
            ("color.magenta", "#C026D3"),
            ("color.purple", "#7C3AED"),
            ("color.indigo", "#4F46E5"),
            ("color.blue", "#2563EB"),
            ("color.cyan", "#0891B2"),
            ("color.teal", "#0F766E"),
            ("color.green", "#16A34A"),
            ("color.slate", "#64748B")
        };

        Assert.Equal(expected.Length, BuiltInColors.Entries.Count);

        for (int i = 0; i < expected.Length; i++)
        {
            var entry = BuiltInColors.Entries[i];
            Assert.Equal(expected[i].id, entry.Id);
            Assert.Equal(expected[i].hex, entry.HexValue);
            Assert.False(string.IsNullOrWhiteSpace(entry.ResourceKey));
        }

        // Method should pass without throwing
        BuiltInColors.ValidateCatalog();
    }

    [Fact]
    public void OrnareClassicTemplateGeometryAndRolesAreValid()
    {
        string svg = OrnareClassicTemplate.SvgContent;
        Assert.False(string.IsNullOrWhiteSpace(svg));

        // SVG security preflight must pass
        var secCheck = SvgSecurityValidator.Validate(svg);
        Assert.True(secCheck.IsSuccess, secCheck.ErrorMessage);

        // Verify required geometric elements and semantic roles
        Assert.Contains("data-ornare-role=\"deep\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-ornare-role=\"base\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-ornare-role=\"light\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-ornare-role=\"highlight\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-ornare-role=\"shadow\"", svg, StringComparison.Ordinal);

        // Verify dimensions: viewBox 0 0 256 256
        Assert.Contains("viewBox=\"0 0 256 256\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void AllTwelveColorsRenderValidIcoWithTenFrames()
    {
        int[] expectedFrames = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

        foreach (var entry in BuiltInColors.Entries)
        {
            string outIco = Path.Combine(_tempDir, $"{entry.Id}.ico");
            var result = FolderTemplateRenderer.RenderColoredTemplateIco(_pipeline, entry.HexValue, outIco);

            Assert.True(result.IsSuccess, $"Failed rendering {entry.Id} ({entry.HexValue}): {result.ErrorMessage}");
            Assert.True(File.Exists(outIco));
            Assert.True(_pipeline.IsValidIco(outIco, expectedMinFrames: 10));

            using var collection = new MagickImageCollection(outIco);
            Assert.Equal(10, collection.Count);
            for (int f = 0; f < expectedFrames.Length; f++)
            {
                Assert.Equal((uint)expectedFrames[f], collection[f].Width);
                Assert.Equal((uint)expectedFrames[f], collection[f].Height);
            }
        }
    }

    [Fact]
    public void RenderPreviewPngGeneratesValidPngQuickly()
    {
        string testHex = "#2563EB"; // Blue
        var result = FolderTemplateRenderer.RenderPreviewPng(testHex);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Length > 8);

        // PNG signature: 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A
        ReadOnlySpan<byte> pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(result.Value.AsSpan(0, 8).SequenceEqual(pngHeader));

        using var img = new MagickImage(result.Value);
        Assert.Equal(256u, img.Width);
        Assert.Equal(256u, img.Height);
        Assert.Equal(MagickFormat.Png, img.Format);
    }

    [Fact]
    public void RepeatedSameColorUsesRenderCacheWithZeroDecodeZeroEncode()
    {
        string cacheDir = Path.Combine(_tempDir, "render_cache");
        Directory.CreateDirectory(cacheDir);

        var cacheStore = new Dictionary<string, RenderCacheEntry>(StringComparer.OrdinalIgnoreCase);

        string testHex = "#16A34A"; // Green
        string outIco1 = Path.Combine(_tempDir, "first_render.ico");
        string outIco2 = Path.Combine(_tempDir, "cached_render.ico");

        // 1. Initial render (Cache Miss)
        var result1 = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            testHex,
            outIco1,
            cacheDirectory: cacheDir,
            cacheLookup: key => cacheStore.TryGetValue(key, out var e) ? e : null,
            cacheStore: e => cacheStore[e.RenderKey] = e);

        Assert.True(result1.IsSuccess);
        Assert.False(result1.Value!.IsFromCache);
        Assert.NotEmpty(cacheStore);

        // 2. Second render (Cache Hit)
        var result2 = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            testHex,
            outIco2,
            cacheDirectory: cacheDir,
            cacheLookup: key => cacheStore.TryGetValue(key, out var e) ? e : null,
            cacheStore: e => cacheStore[e.RenderKey] = e);

        Assert.True(result2.IsSuccess);
        Assert.True(result2.Value!.IsFromCache);
        Assert.Equal(result1.Value.RenderKey, result2.Value.RenderKey);
        Assert.True(File.Exists(outIco2));

        // 3. Benchmark cache hit latency (100 sequential cache hits)
        List<double> latencies = [];
        for (int i = 0; i < 100; i++)
        {
            string outIcoIter = Path.Combine(_tempDir, $"bench_{i}.ico");
            var sw = Stopwatch.StartNew();
            var benchResult = FolderTemplateRenderer.RenderColoredTemplateIco(
                _pipeline,
                testHex,
                outIcoIter,
                cacheDirectory: cacheDir,
                cacheLookup: key => cacheStore.TryGetValue(key, out var e) ? e : null,
                cacheStore: e => cacheStore[e.RenderKey] = e);
            sw.Stop();

            Assert.True(benchResult.IsSuccess && benchResult.Value!.IsFromCache);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
            File.Delete(outIcoIter);
        }

        latencies.Sort();
        double p50 = latencies[(int)(latencies.Count * 0.50)];
        double p95 = latencies[(int)(latencies.Count * 0.95)];

        _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "100 Render Cache Hits for Colored Folder: P50 = {0:F3} ms | P95 = {1:F3} ms",
            p50, p95));

        // Requirement: P95 < 10 ms
        Assert.True(p95 < 10.0, $"Render cache lookup/copy P95 ({p95:F3} ms) must be < 10.0 ms");
    }
}
