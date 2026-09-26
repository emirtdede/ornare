using System;
using System.IO;
using System.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Ornare.Windows;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

public sealed class AtomicExportIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public AtomicExportIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_atomic_export_{Guid.NewGuid():N}");
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
    public void ExportIcoRoundTripVerifiesAllTenFrames()
    {
        string sourceIco = Path.Combine(_tempDir, "source_10frames.ico");
        var renderResult = FolderTemplateRenderer.RenderPresetIco(_pipeline, StatusPresetCatalog.Done, sourceIco, "ornare-classic");
        Assert.True(renderResult.IsSuccess, $"Render failed: {renderResult.ErrorMessage}");

        string exportDestination = Path.Combine(_tempDir, "exported_done.ico");

        var exportResult = AtomicExportWriter.ExportAtomically(
            sourceIco,
            exportDestination,
            customValidator: path => _pipeline.IsValidIco(path, expectedMinFrames: 10),
            isFromCache: true);

        Assert.True(exportResult.IsSuccess, $"Export failed: {exportResult.ErrorMessage}");
        Assert.NotNull(exportResult.Value);
        Assert.True(exportResult.Value.IsFromCache);
        Assert.Equal(10, exportResult.Value.FrameCount);
        Assert.True(File.Exists(exportDestination));

        // Re-import and verify all 10 frames
        using var collection = new MagickImageCollection(exportDestination);
        Assert.Equal(10, collection.Count);

        uint[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        for (int i = 0; i < expectedSizes.Length; i++)
        {
            Assert.Equal(expectedSizes[i], collection[i].Width);
            Assert.Equal(expectedSizes[i], collection[i].Height);
        }
    }

    [Fact]
    public void ExportIcoExistingDestinationReplacedAtomically()
    {
        string sourceIco = Path.Combine(_tempDir, "source_replace.ico");
        var renderResult = FolderTemplateRenderer.RenderPresetIco(_pipeline, StatusPresetCatalog.InProgress, sourceIco, "ornare-soft");
        Assert.True(renderResult.IsSuccess);

        string exportDestination = Path.Combine(_tempDir, "existing_target.ico");
        byte[] originalDummyBytes = "PRE_EXISTING_DUMMY_CONTENT_DO_NOT_CORRUPT"u8.ToArray();
        File.WriteAllBytes(exportDestination, originalDummyBytes);

        var exportResult = AtomicExportWriter.ExportAtomically(
            sourceIco,
            exportDestination,
            customValidator: path => _pipeline.IsValidIco(path, expectedMinFrames: 10));

        Assert.True(exportResult.IsSuccess);
        Assert.True(File.Exists(exportDestination));

        // Verify content was replaced with valid 10-frame ICO, not dummy text
        byte[] newBytes = File.ReadAllBytes(exportDestination);
        Assert.False(newBytes.SequenceEqual(originalDummyBytes));

        using var collection = new MagickImageCollection(exportDestination);
        Assert.Equal(10, collection.Count);
    }

    [Fact]
    public void ExportIcoInjectedFailurePreservesOriginalDestination()
    {
        string exportDestination = Path.Combine(_tempDir, "protected_target.ico");
        byte[] protectedBytes = "CRITICAL_ORIGINAL_FILE_CONTENT_MUST_STAY_INTACT"u8.ToArray();
        File.WriteAllBytes(exportDestination, protectedBytes);

        // Intentionally create a corrupted 0-byte or invalid source
        string invalidSource = Path.Combine(_tempDir, "corrupted_source.ico");
        File.WriteAllBytes(invalidSource, [0, 0, 1, 0, 0, 0]);

        var exportResult = AtomicExportWriter.ExportAtomically(
            invalidSource,
            exportDestination,
            customValidator: path => _pipeline.IsValidIco(path, expectedMinFrames: 10));

        Assert.False(exportResult.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, exportResult.ErrorCode);

        // Verify protected original destination was NOT overwritten or truncated
        Assert.True(File.Exists(exportDestination));
        byte[] currentBytes = File.ReadAllBytes(exportDestination);
        Assert.True(protectedBytes.SequenceEqual(currentBytes), "Original destination file must remain byte-for-byte intact after failed export.");

        // Verify no orphan .ornare-export-*.tmp files leaked
        var orphanedTemps = Directory.GetFiles(_tempDir, ".ornare-export-*.tmp");
        Assert.Empty(orphanedTemps);
    }

    [Fact]
    public void ExportIcoCleanupStaleSiblingTemps()
    {
        string staleTemp = Path.Combine(_tempDir, ".ornare-export-stale.tmp");
        File.WriteAllBytes(staleTemp, [1, 2, 3]);
        File.SetLastWriteTimeUtc(staleTemp, DateTime.UtcNow.AddHours(-25)); // 25 hours old

        string freshTemp = Path.Combine(_tempDir, ".ornare-export-fresh.tmp");
        File.WriteAllBytes(freshTemp, [4, 5, 6]);
        File.SetLastWriteTimeUtc(freshTemp, DateTime.UtcNow.AddMinutes(-10)); // 10 minutes old

        AtomicExportWriter.CleanupStaleSiblingTemps(_tempDir, TimeSpan.FromHours(24));

        Assert.False(File.Exists(staleTemp), "Stale temp older than 24h must be deleted.");
        Assert.True(File.Exists(freshTemp), "Fresh temp younger than 24h must be preserved.");
    }

    [Fact]
    public void ExportIcoCacheHitMatchesCacheArtifactByteForByte()
    {
        string sourceIco = Path.Combine(_tempDir, "cache_artifact.ico");
        var renderResult = FolderTemplateRenderer.RenderPresetIco(_pipeline, StatusPresetCatalog.Important, sourceIco, "ornare-flat");
        Assert.True(renderResult.IsSuccess);

        byte[] sourceBytes = File.ReadAllBytes(sourceIco);
        string exportDestination = Path.Combine(_tempDir, "cache_hit_export.ico");

        var exportResult = AtomicExportWriter.ExportAtomically(
            sourceIco,
            exportDestination,
            customValidator: path => _pipeline.IsValidIco(path, expectedMinFrames: 10),
            isFromCache: true);

        Assert.True(exportResult.IsSuccess);
        Assert.True(exportResult.Value!.IsFromCache);

        byte[] exportedBytes = File.ReadAllBytes(exportDestination);
        Assert.True(sourceBytes.SequenceEqual(exportedBytes), "Cache-hit export must be byte-for-byte identical to the cache artifact.");
    }
}
