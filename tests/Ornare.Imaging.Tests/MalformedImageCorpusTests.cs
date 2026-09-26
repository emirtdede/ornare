using System;
using System.IO;
using System.Text;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;

namespace Ornare.Imaging.Tests;

public class MalformedImageCorpusTests
{
    private readonly ImagePipeline _pipeline = new();

    [Fact]
    public void ProcessImageTruncatedPngReturnsImageCorrupted()
    {
        // Valid PNG header (8 bytes) followed by incomplete chunk data
        byte[] truncatedPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];
        string tempIco = Path.Combine(Path.GetTempPath(), $"test_trunc_{Guid.NewGuid():N}.ico");

        var result = _pipeline.ProcessImageBytes(truncatedPng, tempIco);

        Assert.False(result.IsSuccess);
        Assert.True(result.ErrorCode == ErrorCode.ImageCorrupted || result.ErrorCode == ErrorCode.ImageDecodeFailed);
    }

    [Fact]
    public void ProcessImageTruncatedJpegReturnsImageCorrupted()
    {
        // JPEG SOI (FF D8) followed by truncated APP0 marker
        byte[] truncatedJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];
        string tempIco = Path.Combine(Path.GetTempPath(), $"test_trunc_jpg_{Guid.NewGuid():N}.ico");

        var result = _pipeline.ProcessImageBytes(truncatedJpeg, tempIco);

        Assert.False(result.IsSuccess);
        Assert.True(result.ErrorCode == ErrorCode.ImageCorrupted || result.ErrorCode == ErrorCode.ImageDecodeFailed);
    }

    [Fact]
    public void ProcessImageCorruptWebPReturnsImageCorrupted()
    {
        // RIFF header with invalid WEBP payload
        byte[] corruptWebp = [0x52, 0x49, 0x46, 0x46, 0x20, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20, 0x00, 0x00];
        string tempIco = Path.Combine(Path.GetTempPath(), $"test_corrupt_webp_{Guid.NewGuid():N}.ico");

        var result = _pipeline.ProcessImageBytes(corruptWebp, tempIco);

        Assert.False(result.IsSuccess);
        Assert.True(result.ErrorCode == ErrorCode.ImageCorrupted || result.ErrorCode == ErrorCode.ImageDecodeFailed);
    }

    [Fact]
    public void ProcessImageMaliciousSvgPayloadRejectedSafely()
    {
        // Malicious SVG with XXE/external entity attempt
        string maliciousSvg = @"<?xml version=""1.0"" standalone=""no""?>
<!DOCTYPE svg [
  <!ELEMENT svg ANY >
  <!ENTITY xxe SYSTEM ""file:///C:/Windows/win.ini"" >
]>
<svg width=""100"" height=""100"">&xxe;</svg>";

        byte[] svgBytes = Encoding.UTF8.GetBytes(maliciousSvg);
        string tempIco = Path.Combine(Path.GetTempPath(), $"test_svg_{Guid.NewGuid():N}.ico");

        var result = _pipeline.ProcessImageBytes(svgBytes, tempIco);

        Assert.False(result.IsSuccess);
        // Under F09 SVG support, malicious SVG is rejected with SvgSecurityViolation (or UnsupportedImageFormat)
        Assert.True(result.ErrorCode == ErrorCode.SvgSecurityViolation || result.ErrorCode == ErrorCode.UnsupportedImageFormat);
    }

    [Theory]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.Ico)]
    public void ProcessImageAdditionalSupportedFormatsGenerateValidIco(MagickFormat format)
    {
        byte[] bytes;
        if (format == MagickFormat.Ico)
        {
            using var collection = new MagickImageCollection();
            using var frame = new MagickImage(MagickColors.Crimson, 32, 32);
            frame.Format = MagickFormat.Ico;
            collection.Add(frame);
            bytes = collection.ToByteArray(MagickFormat.Ico);
        }
        else
        {
            using var testImage = new MagickImage(MagickColors.Crimson, 64, 64);
            testImage.Format = format;
            bytes = testImage.ToByteArray();
        }

        string tempIco = Path.Combine(Path.GetTempPath(), $"test_fmt_{format}_{Guid.NewGuid():N}.ico");
        try
        {
            var result = _pipeline.ProcessImageBytes(bytes, tempIco);
            Assert.True(result.IsSuccess, $"Failed for {format}: {result.ErrorCode} - {result.ErrorMessage}");
            Assert.True(File.Exists(tempIco));
            Assert.NotNull(result.Value);
            Assert.Equal(10, result.Value.FrameSizes.Count);
        }
        finally
        {
            if (File.Exists(tempIco))
            {
                File.Delete(tempIco);
            }
        }
    }
}
