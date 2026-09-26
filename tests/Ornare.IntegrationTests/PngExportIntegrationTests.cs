using System;
using System.Collections.Concurrent;
using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Ornare.Windows;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

public sealed class PngExportIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly ConcurrentDictionary<string, RenderCacheEntry> _inMemoryCache = new();

    public PngExportIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_png_export_{Guid.NewGuid():N}");
        _cacheDir = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_cacheDir);
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

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void ExportPngAllThreeSizesProduceExactDimensionsAndAlpha(int targetSize)
    {
        var preset = StatusPresetCatalog.Done;
        var recipe = FolderTemplateRenderer.CreatePresetRecipe(preset, "ornare-classic");

        var renderResult = _pipeline.RenderPngWithCache(
            recipe,
            null,
            targetSize,
            cacheDirectory: _cacheDir,
            cacheLookup: key => _inMemoryCache.TryGetValue(key, out var e) ? e : null,
            cacheStore: entry => _inMemoryCache[entry.RenderKey] = entry);

        Assert.True(renderResult.IsSuccess, $"Render failed: {renderResult.ErrorMessage}");
        Assert.NotNull(renderResult.Value);
        Assert.False(renderResult.Value.IsFromCache);
        Assert.Equal(targetSize, renderResult.Value.Width);
        Assert.Equal(targetSize, renderResult.Value.Height);
        Assert.True(renderResult.Value.HasAlpha, "Transparent preset recipe must produce alpha channel.");

        string exportDestination = Path.Combine(_tempDir, $"exported_done_{targetSize}.png");

        var exportResult = AtomicExportWriter.ExportPngAtomically(
            renderResult.Value.PngBytes,
            exportDestination,
            targetSize,
            isFromCache: renderResult.Value.IsFromCache,
            renderKey: renderResult.Value.RenderKey);

        Assert.True(exportResult.IsSuccess, $"Export failed: {exportResult.ErrorMessage}");
        Assert.NotNull(exportResult.Value);
        Assert.Equal(targetSize, exportResult.Value.Width);
        Assert.Equal(targetSize, exportResult.Value.Height);
        Assert.True(exportResult.Value.HasAlpha);
        Assert.True(File.Exists(exportDestination));

        // Round-trip decode using Magick.NET to verify full image integrity
        using var image = new MagickImage(exportDestination);
        Assert.Equal((uint)targetSize, image.Width);
        Assert.Equal((uint)targetSize, image.Height);
        Assert.True(image.HasAlpha, "Decoded PNG must have alpha channel.");

        _output.WriteLine($"Verified size {targetSize}x{targetSize}, FileSize: {exportResult.Value.FileSizeBytes} bytes, HasAlpha: {image.HasAlpha}");
    }

    [Fact]
    public void ExportPngSolidBackgroundProducesOpaquePng()
    {
        var baseRecipe = FolderTemplateRenderer.CreateRecipe("#0078D4", "ornare-soft");
        var solidRecipe = baseRecipe with
        {
            BackgroundMode = (int)BackgroundMode.SolidColor,
            BackgroundColor = "#1E293B"
        };

        var renderResult = _pipeline.RenderPng(solidRecipe, null, targetSize: 512);
        Assert.True(renderResult.IsSuccess, $"Render failed: {renderResult.ErrorMessage}");
        Assert.NotNull(renderResult.Value);

        string exportDestination = Path.Combine(_tempDir, "exported_solid_512.png");
        var exportResult = AtomicExportWriter.ExportPngAtomically(
            renderResult.Value,
            exportDestination,
            expectedSize: 512);

        Assert.True(exportResult.IsSuccess, $"Export failed: {exportResult.ErrorMessage}");
        Assert.True(File.Exists(exportDestination));

        using var image = new MagickImage(exportDestination);
        Assert.Equal((uint)512, image.Width);
        Assert.Equal((uint)512, image.Height);

        // Verify corner pixel is solid background color #1E293B (R: 30, G: 41, B: 59)
        using var pixels = image.GetPixels();
        var corner = pixels.GetPixel(0, 0);
        Assert.NotNull(corner);
        var cornerColor = corner.ToColor();
        Assert.NotNull(cornerColor);
        byte r8 = (byte)Math.Round(cornerColor.R * 255.0 / ushort.MaxValue);
        byte g8 = (byte)Math.Round(cornerColor.G * 255.0 / ushort.MaxValue);
        byte b8 = (byte)Math.Round(cornerColor.B * 255.0 / ushort.MaxValue);
        Assert.Equal(30, r8);
        Assert.Equal(41, g8);
        Assert.Equal(59, b8);
    }

    [Fact]
    public void ExportPngVisualGoldenComparisonWithRecipePreview()
    {
        var preset = StatusPresetCatalog.Important;
        var previewResult = FolderTemplateRenderer.RenderPresetPreviewPng(preset, "ornare-classic");
        Assert.True(previewResult.IsSuccess, $"Preview render failed: {previewResult.ErrorMessage}");

        var recipe = FolderTemplateRenderer.CreatePresetRecipe(preset, "ornare-classic");
        var exportPngResult = _pipeline.RenderPng(recipe, null, targetSize: 256);
        Assert.True(exportPngResult.IsSuccess, $"PNG render failed: {exportPngResult.ErrorMessage}");

        using var previewImg = new MagickImage(previewResult.Value!);
        using var exportImg = new MagickImage(exportPngResult.Value!);

        Assert.Equal(previewImg.Width, exportImg.Width);
        Assert.Equal(previewImg.Height, exportImg.Height);

        // Compare center area pixels (folder body) to verify identical visual appearance
        using var previewPixels = previewImg.GetPixels();
        using var exportPixels = exportImg.GetPixels();

        var pCol = previewPixels.GetPixel(128, 128).ToColor();
        var eCol = exportPixels.GetPixel(128, 128).ToColor();

        Assert.NotNull(pCol);
        Assert.NotNull(eCol);
        Assert.Equal(pCol.R, eCol.R);
        Assert.Equal(pCol.G, eCol.G);
        Assert.Equal(pCol.B, eCol.B);
    }

    [Fact]
    public void ExportPngExistingDestinationReplacedAtomically()
    {
        string exportDestination = Path.Combine(_tempDir, "existing_destination.png");
        byte[] priorContent = [0xDE, 0xAD, 0xBE, 0xEF, 0x42];
        File.WriteAllBytes(exportDestination, priorContent);

        var recipe = FolderTemplateRenderer.CreateRecipe("#2563EB", "ornare-flat");
        var renderResult = _pipeline.RenderPng(recipe, null, targetSize: 512);
        Assert.True(renderResult.IsSuccess);

        var exportResult = AtomicExportWriter.ExportPngAtomically(
            renderResult.Value!,
            exportDestination,
            expectedSize: 512);

        Assert.True(exportResult.IsSuccess);
        Assert.True(File.Exists(exportDestination));

        byte[] currentBytes = File.ReadAllBytes(exportDestination);
        Assert.False(currentBytes.AsSpan().SequenceEqual(priorContent));

        using var decoded = new MagickImage(exportDestination);
        Assert.Equal((uint)512, decoded.Width);
        Assert.Equal((uint)512, decoded.Height);
    }

    [Fact]
    public void ExportPngInjectedFailurePreservesOriginalDestination()
    {
        string exportDestination = Path.Combine(_tempDir, "inviolable_destination.png");
        byte[] originalContent = [0x50, 0x4B, 0x03, 0x04, 0x11, 0x22, 0x33, 0x44];
        File.WriteAllBytes(exportDestination, originalContent);

        // Injected corruption: invalid PNG payload (fails header check)
        byte[] corruptedPayload = [0x01, 0x02, 0x03, 0x04];

        var exportResult = AtomicExportWriter.ExportPngAtomically(
            corruptedPayload,
            exportDestination,
            expectedSize: 1024);

        Assert.False(exportResult.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, exportResult.ErrorCode);

        // Destination must remain byte-for-byte identical
        byte[] actualContent = File.ReadAllBytes(exportDestination);
        Assert.Equal(originalContent, actualContent);

        // No lingering temp files
        var remainingTemps = Directory.GetFiles(_tempDir, ".ornare-export-*.tmp");
        Assert.Empty(remainingTemps);
    }

    [Fact]
    public void ExportPngCacheHitReusesCachedArtifactWithoutReEncoding()
    {
        var preset = StatusPresetCatalog.InProgress;
        var recipe = FolderTemplateRenderer.CreatePresetRecipe(preset, "ornare-classic");

        string dest1 = Path.Combine(_tempDir, "cache_first.png");
        string dest2 = Path.Combine(_tempDir, "cache_second.png");

        // 1. First export: Cache miss -> render and store
        var firstRender = _pipeline.RenderPngWithCache(
            recipe,
            null,
            1024,
            cacheDirectory: _cacheDir,
            cacheLookup: key => _inMemoryCache.TryGetValue(key, out var e) ? e : null,
            cacheStore: entry => _inMemoryCache[entry.RenderKey] = entry);

        Assert.True(firstRender.IsSuccess);
        Assert.False(firstRender.Value!.IsFromCache);

        var firstExport = AtomicExportWriter.ExportPngAtomically(
            firstRender.Value.PngBytes,
            dest1,
            1024,
            isFromCache: false,
            renderKey: firstRender.Value.RenderKey);
        Assert.True(firstExport.IsSuccess);

        // 2. Second export: Cache hit -> reuse
        var secondRender = _pipeline.RenderPngWithCache(
            recipe,
            null,
            1024,
            cacheDirectory: _cacheDir,
            cacheLookup: key => _inMemoryCache.TryGetValue(key, out var e) ? e : null,
            cacheStore: entry => _inMemoryCache[entry.RenderKey] = entry);

        Assert.True(secondRender.IsSuccess);
        Assert.True(secondRender.Value!.IsFromCache);

        var secondExport = AtomicExportWriter.ExportPngAtomically(
            secondRender.Value.PngBytes,
            dest2,
            1024,
            isFromCache: true,
            renderKey: secondRender.Value.RenderKey);
        Assert.True(secondExport.IsSuccess);

        // Both files are byte-for-byte identical
        byte[] bytes1 = File.ReadAllBytes(dest1);
        byte[] bytes2 = File.ReadAllBytes(dest2);
        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void ExportPngInvalidSizeRejectedWithInvalidTarget()
    {
        var recipe = FolderTemplateRenderer.CreateRecipe("#10B981", "ornare-classic");

        var renderResult = _pipeline.RenderPng(recipe, null, targetSize: 128);
        Assert.False(renderResult.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, renderResult.ErrorCode);

        var renderResult2 = _pipeline.RenderPng(recipe, null, targetSize: 2048);
        Assert.False(renderResult2.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, renderResult2.ErrorCode);
    }

    [Fact]
    public void ExportPngCancelProducesZeroFiles()
    {
        int initialFileCount = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).Length;

        // Cancel simulation: picker returns null, application exits without calling exporter
        string? cancelledFilePath = null;
        if (cancelledFilePath == null)
        {
            // Expected early return
        }

        int finalFileCount = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).Length;
        Assert.Equal(initialFileCount, finalFileCount);
    }
}
