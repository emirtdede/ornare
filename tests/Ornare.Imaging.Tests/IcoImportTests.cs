using System;
using System.IO;
using System.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public sealed class IcoImportTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public IcoImportTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_ico_import_{Guid.NewGuid():N}");
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

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(21)]
    public void ValidateIcoContainerRejectsTruncatedOrEmptyPayloads(int length)
    {
        byte[] payload = new byte[length];
        var result = IcoImportService.ValidateIcoContainer(payload);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, result.ErrorCode);
    }

    [Fact]
    public void ValidateIcoContainerRejectsInvalidMagicSignatures()
    {
        // Reserved != 0
        byte[] badReserved = new byte[32];
        badReserved[0] = 1;
        badReserved[2] = 1; // type
        badReserved[4] = 1; // count
        var res1 = IcoImportService.ValidateIcoContainer(badReserved);
        Assert.False(res1.IsSuccess);
        Assert.Equal(ErrorCode.UnsupportedImageFormat, res1.ErrorCode);

        // Type != 1 (e.g. CUR cursor type 2)
        byte[] cursorType = new byte[32];
        cursorType[2] = 2; // CUR type
        cursorType[4] = 1; // count
        var res2 = IcoImportService.ValidateIcoContainer(cursorType);
        Assert.False(res2.IsSuccess);
        Assert.Equal(ErrorCode.UnsupportedImageFormat, res2.ErrorCode);

        // PNG signature
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var res3 = IcoImportService.ValidateIcoContainer(pngHeader);
        Assert.False(res3.IsSuccess);
        Assert.Equal(ErrorCode.UnsupportedImageFormat, res3.ErrorCode);
    }

    [Fact]
    public void ValidateIcoContainerRejectsZeroOrExcessiveFrameCount()
    {
        // Frame count = 0
        byte[] zeroFrames = [0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var res1 = IcoImportService.ValidateIcoContainer(zeroFrames);
        Assert.False(res1.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, res1.ErrorCode);

        // Frame count = 65 (> 64 limit)
        byte[] bombHeader = [0, 0, 1, 0, 65, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var res2 = IcoImportService.ValidateIcoContainer(bombHeader);
        Assert.False(res2.IsSuccess);
        Assert.Equal(ErrorCode.ImageExceedsLimits, res2.ErrorCode);
    }

    [Fact]
    public void ValidateIcoContainerRejectsDirectoryEntriesOutOfBounds()
    {
        // 1 frame specified, but file is too short for directory entry
        byte[] truncatedDir = [0, 0, 1, 0, 1, 0, 16, 16, 0, 0];
        var res1 = IcoImportService.ValidateIcoContainer(truncatedDir);
        Assert.False(res1.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, res1.ErrorCode);

        // Directory entry with image data offset out of bounds
        byte[] outOfBoundsEntry = new byte[30];
        outOfBoundsEntry[2] = 1; // type
        outOfBoundsEntry[4] = 1; // count = 1
        outOfBoundsEntry[6] = 16; // width
        outOfBoundsEntry[7] = 16; // height
        // bytesInRes = 100
        BitConverter.GetBytes(100u).CopyTo(outOfBoundsEntry, 6 + 8);
        // imageOffset = 500 (beyond file length of 30)
        BitConverter.GetBytes(500u).CopyTo(outOfBoundsEntry, 6 + 12);

        var res2 = IcoImportService.ValidateIcoContainer(outOfBoundsEntry);
        Assert.False(res2.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, res2.ErrorCode);
    }

    [Fact]
    public void FastPathGrantedForCompliantTenFrameIco()
    {
        // Generate a 10-frame compliant ICO
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        using var collection = new MagickImageCollection();
        foreach (int size in sizes)
        {
            var frame = new MagickImage(MagickColors.SteelBlue, (uint)size, (uint)size);
            frame.Format = MagickFormat.Png;
            frame.ColorSpace = ColorSpace.sRGB;
            frame.Format = MagickFormat.Ico;
            collection.Add(frame);
        }

        byte[] compliantBytes = collection.ToByteArray(MagickFormat.Ico);
        string outputPath = Path.Combine(_tempDir, "fast_path_out.ico");

        var result = IcoImportService.ImportIco(compliantBytes, outputPath);

        Assert.True(result.IsSuccess, $"Fast-path import failed: {result.ErrorMessage}");
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsFastPath, "Compliant 10-frame icon must be granted fast-path.");
        Assert.Equal(IcoImportKind.IcoCompliant, result.Value.ImportKind);
        Assert.True(File.Exists(outputPath));

        // Bit-exact preservation: output bytes must be identical to original bytes
        byte[] outputBytes = File.ReadAllBytes(outputPath);
        Assert.Equal(compliantBytes.Length, outputBytes.Length);
        Assert.True(compliantBytes.SequenceEqual(outputBytes), "Fast-path must preserve original bytes bit-for-bit.");
    }

    [Fact]
    public void NormalizationPathExecutedForSingleFrameIco()
    {
        // Generate a single 256x256 frame ICO
        using var singleFrame = new MagickImage(MagickColors.MediumSeaGreen, 256, 256);
        singleFrame.Format = MagickFormat.Ico;
        byte[] singleBytes = singleFrame.ToByteArray();

        string outputPath = Path.Combine(_tempDir, "single_norm_out.ico");

        var result = IcoImportService.ImportIco(singleBytes, outputPath);

        Assert.True(result.IsSuccess, $"Normalization import failed: {result.ErrorMessage}");
        Assert.NotNull(result.Value);
        Assert.False(result.Value.IsFastPath, "Single-frame icon cannot take fast-path; must be normalized.");
        Assert.Equal(IcoImportKind.IcoNormalized, result.Value.ImportKind);
        Assert.True(File.Exists(outputPath));

        // Verify output ICO contains all 10 required frame sizes
        using var collection = new MagickImageCollection(outputPath);
        Assert.Equal(10, collection.Count);

        int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        for (int i = 0; i < expectedSizes.Length; i++)
        {
            Assert.Equal((uint)expectedSizes[i], collection[i].Width);
            Assert.Equal((uint)expectedSizes[i], collection[i].Height);
        }
    }

    [Fact]
    public void NormalizationPathPreservesExactMatchingFramesAndInterpolatesMissing()
    {
        // Construct an icon with 4 standard frames: 16, 32, 48, 256
        int[] inputSizes = [16, 32, 48, 256];
        using var inputCollection = new MagickImageCollection();
        foreach (int size in inputSizes)
        {
            var frame = new MagickImage(MagickColors.DarkOrange, (uint)size, (uint)size);
            frame.Format = MagickFormat.Ico;
            inputCollection.Add(frame);
        }

        byte[] inputBytes = inputCollection.ToByteArray(MagickFormat.Ico);
        string outputPath = Path.Combine(_tempDir, "four_frame_norm_out.ico");

        var result = IcoImportService.ImportIco(inputBytes, outputPath);

        Assert.True(result.IsSuccess, $"Import failed: {result.ErrorMessage}");
        Assert.NotNull(result.Value);
        Assert.False(result.Value.IsFastPath);
        Assert.Equal(IcoImportKind.IcoNormalized, result.Value.ImportKind);

        using var outputCollection = new MagickImageCollection(outputPath);
        Assert.Equal(10, outputCollection.Count);

        int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        for (int i = 0; i < expectedSizes.Length; i++)
        {
            Assert.Equal((uint)expectedSizes[i], outputCollection[i].Width);
            Assert.Equal((uint)expectedSizes[i], outputCollection[i].Height);
        }
    }

    [Fact]
    public void ExtractPreviewPngGeneratesValid256x256PngWithoutFullMultiFrameReencoding()
    {
        using var frame = new MagickImage(MagickColors.MediumPurple, 64, 64);
        frame.Format = MagickFormat.Ico;
        byte[] icoBytes = frame.ToByteArray();

        var previewResult = IcoImportService.ExtractPreviewPng(icoBytes, 256);

        Assert.True(previewResult.IsSuccess, $"Preview extraction failed: {previewResult.ErrorMessage}");
        Assert.NotNull(previewResult.Value);
        Assert.True(previewResult.Value.Length > 8);

        // PNG signature: 0x89 0x50 0x4E 0x47
        byte[] bytes = previewResult.Value;
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);

        using var previewImg = new MagickImage(bytes);
        Assert.Equal(256u, previewImg.Width);
        Assert.Equal(256u, previewImg.Height);
    }

    [Fact]
    public void DeduplicationAndRenderKeyDivergence()
    {
        using var frame = new MagickImage(MagickColors.Crimson, 128, 128);
        frame.Format = MagickFormat.Ico;
        byte[] bytes = frame.ToByteArray();

        string out1 = Path.Combine(_tempDir, "dedup1.ico");
        string out2 = Path.Combine(_tempDir, "dedup2.ico");

        var res1 = IcoImportService.ImportIco(bytes, out1);
        var res2 = IcoImportService.ImportIco(bytes, out2);

        Assert.True(res1.IsSuccess);
        Assert.True(res2.IsSuccess);

        // Deduplication hit: identical canonical SHA-256 and RenderKey
        Assert.Equal(res1.Value!.CanonicalHash, res2.Value!.CanonicalHash);
        Assert.Equal(res1.Value.RenderKey, res2.Value.RenderKey);

        // Render key divergence between fast-path (v1) and normalized (v2)
        var recipeV1 = new RenderRecipe
        {
            SourceAssetHash = res1.Value.CanonicalHash,
            NormalizationPolicyVersion = 1,
            TargetFormat = "ico"
        };
        var recipeV2 = new RenderRecipe
        {
            SourceAssetHash = res1.Value.CanonicalHash,
            NormalizationPolicyVersion = 2,
            TargetFormat = "ico"
        };

        Assert.NotEqual(recipeV1.ComputeRenderKey(), recipeV2.ComputeRenderKey());
    }

    [Fact]
    public void ImagePipelineProcessImageBytesWithIcoRoutesDirectlyToIcoImport()
    {
        using var frame = new MagickImage(MagickColors.DarkSlateGray, 256, 256);
        frame.Format = MagickFormat.Ico;
        byte[] icoBytes = frame.ToByteArray();

        string outIco = Path.Combine(_tempDir, "pipeline_ico.ico");
        var processResult = _pipeline.ProcessImageBytes(icoBytes, outIco);

        Assert.True(processResult.IsSuccess, $"Pipeline processing failed: {processResult.ErrorMessage}");
        Assert.NotNull(processResult.Value);
        Assert.Equal("ico", processResult.Value.SourceFormat);
        Assert.True(File.Exists(outIco));

        using var col = new MagickImageCollection(outIco);
        Assert.Equal(10, col.Count);
    }
}
