using System.IO;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;

namespace Ornare.Imaging.Tests;

public class ImagePipelineTests
{
    private readonly ImagePipeline _pipeline = new();

    [Theory]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.WebP)]
    public void ProcessImageSupportedFormatsGeneratesValidMultiResolutionIco(MagickFormat format)
    {
        // 1. Create a synthetic test image with dimensions 120x80 (non-square to test contain)
        using var testImage = new MagickImage(MagickColors.SteelBlue, 120, 80);
        testImage.Format = format;
        byte[] imageBytes = testImage.ToByteArray();

        string tempIco = Path.Combine(Path.GetTempPath(), $"test_ico_{Guid.NewGuid():N}.ico");

        try
        {
            var result = _pipeline.ProcessImageBytes(imageBytes, tempIco);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.True(File.Exists(tempIco));
            Assert.NotEmpty(result.Value.ContentHash);

            // Read the generated ICO back to verify frames
            using var icoCollection = new MagickImageCollection(tempIco);
            Assert.Equal(10, icoCollection.Count);

            int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
            for (int i = 0; i < expectedSizes.Length; i++)
            {
                Assert.Equal((uint)expectedSizes[i], icoCollection[i].Width);
                Assert.Equal((uint)expectedSizes[i], icoCollection[i].Height);
            }
        }
        finally
        {
            if (File.Exists(tempIco))
            {
                File.Delete(tempIco);
            }
        }
    }

    [Fact]
    public void ProcessImageCorruptedBytesReturnsImageCorrupted()
    {
        byte[] garbage = [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03];
        string tempIco = Path.Combine(Path.GetTempPath(), $"test_garbage_{Guid.NewGuid():N}.ico");

        var result = _pipeline.ProcessImageBytes(garbage, tempIco);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, result.ErrorCode);
    }

    [Fact]
    public void ProcessImageEmptyPayloadReturnsInvalidTarget()
    {
        string tempIco = Path.Combine(Path.GetTempPath(), $"test_empty_{Guid.NewGuid():N}.ico");

        var result = _pipeline.ProcessImageBytes([], tempIco);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidTarget, result.ErrorCode);
    }
}
