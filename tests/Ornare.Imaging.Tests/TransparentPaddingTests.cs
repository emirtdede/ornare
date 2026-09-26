using System;
using System.IO;
using System.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;

namespace Ornare.Imaging.Tests;

public class TransparentPaddingTests
{
    private static readonly int[] IcoFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
    private readonly ImagePipeline _pipeline = new();

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(30)]
    public void ComputePlacementWithPaddingAllFrameSizes(int paddingPercent)
    {
        foreach (int size in IcoFrameSizes)
        {
            var geom = ImagePlacementCalculator.ComputePlacement(
                sourceWidth: 200,
                sourceHeight: 200,
                canvasWidth: size,
                canvasHeight: size,
                mode: ImagePlacementMode.Cover,
                paddingPercent: paddingPercent);

            int expectedPad = (int)Math.Round(size * (paddingPercent / 100.0), MidpointRounding.AwayFromZero);
            int expectedUsable = Math.Max(1, size - (expectedPad * 2));

            Assert.Equal(expectedPad, geom.DestX);
            Assert.Equal(expectedPad, geom.DestY);
            Assert.Equal(expectedUsable, geom.DestWidth);
            Assert.Equal(expectedUsable, geom.DestHeight);
            Assert.Equal(size, geom.CanvasWidth);
            Assert.Equal(size, geom.CanvasHeight);

            // In Cover mode with square source on square canvas, source crop must be full square
            Assert.Equal(0, geom.SourceCropX);
            Assert.Equal(0, geom.SourceCropY);
            Assert.Equal(200, geom.SourceCropWidth);
            Assert.Equal(200, geom.SourceCropHeight);
        }
    }

    [Fact]
    public void PaddingValidationRejectsInvalidNegativeAndOver30()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderRecipe
        {
            PaddingPercent = -1
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderRecipe
        {
            PaddingPercent = 31
        });

        // Valid boundaries [0, 30] must succeed
        var r0 = new RenderRecipe { PaddingPercent = 0 };
        Assert.Equal(0, r0.PaddingPercent);

        var r8 = new RenderRecipe { PaddingPercent = 8 };
        Assert.Equal(8, r8.PaddingPercent);

        var r30 = new RenderRecipe { PaddingPercent = 30 };
        Assert.Equal(30, r30.PaddingPercent);
    }

    [Fact]
    public void DeserializationRejectsCorruptPadding()
    {
        var validRecipe = new RenderRecipe
        {
            SourceAssetHash = "dummyhash",
            PaddingPercent = 8
        };
        byte[] bytes = validRecipe.ToCanonicalBytes();

        // Roundtrip deserialization
        var deserialized = CanonicalRecipeSerializer.Deserialize(bytes);
        Assert.Equal(8, deserialized.PaddingPercent);

        // Tamper with padding value in serialized binary stream (offset of paddingPercent)
        // Find 8 as an int32 in the stream and replace with 99
        byte[] tampered = (byte[])bytes.Clone();
        for (int i = 0; i < tampered.Length - 4; i++)
        {
            if (tampered[i] == 8 && tampered[i + 1] == 0 && tampered[i + 2] == 0 && tampered[i + 3] == 0)
            {
                tampered[i] = 99; // Set invalid padding = 99
                break;
            }
        }

        Assert.Throws<InvalidDataException>(() => CanonicalRecipeSerializer.Deserialize(tampered));
    }

    [Fact]
    public void RenderCacheKeyDiffersWhenPaddingDiffers()
    {
        string dummyHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        var recipe0 = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            PaddingPercent = 0
        };

        var recipe8 = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            PaddingPercent = 8
        };

        var recipe30 = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            PaddingPercent = 30
        };

        string key0 = recipe0.ComputeRenderKey();
        string key8 = recipe8.ComputeRenderKey();
        string key30 = recipe30.ComputeRenderKey();

        Assert.NotEqual(key0, key8);
        Assert.NotEqual(key0, key30);
        Assert.NotEqual(key8, key30);

        // Repeat serialization produces identical key
        Assert.Equal(key8, recipe8.ComputeRenderKey());
    }

    [Fact]
    public void PreviewPngMatchesFinal256FrameWithPadding()
    {
        using var testImage = new MagickImage(MagickColors.Crimson, 200, 200);
        testImage.Format = MagickFormat.Png;
        byte[] imageBytes = testImage.ToByteArray();

        string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_test_pad_{Guid.NewGuid():N}.ico");
        try
        {
            // Process ICO with 8% padding (pad = 20px per side on 256 canvas -> inner 216x216)
            var icoResult = _pipeline.ProcessImageBytes(imageBytes, tempIco, ImagePlacementMode.Cover, paddingPercent: 8);
            Assert.True(icoResult.IsSuccess, icoResult.ErrorMessage);

            // Generate Preview PNG with 8% padding
            var previewResult = ImagePipeline.RenderPlacementPreviewPng(imageBytes, 256, ImagePlacementMode.Cover, paddingPercent: 8);
            Assert.True(previewResult.IsSuccess, previewResult.ErrorMessage);
            byte[] previewBytes = previewResult.Value!;

            using MagickImageCollection icoFrames = new(tempIco);
            var frame256 = icoFrames.FirstOrDefault(f => f.Width == 256 && f.Height == 256);
            Assert.NotNull(frame256);

            using var previewImage = new MagickImage(previewBytes);

            // Inset area (at 5, 5) must be completely transparent in both
            var borderPixelIco = frame256.GetPixels().GetPixel(5, 5);
            var borderPixelPrev = previewImage.GetPixels().GetPixel(5, 5);
            Assert.Equal((ushort)0, borderPixelIco.ToColor()?.A ?? 0);
            Assert.Equal((ushort)0, borderPixelPrev.ToColor()?.A ?? 0);

            // Center area (at 128, 128) must be filled with crimson in both
            var centerPixelIco = frame256.GetPixels().GetPixel(128, 128);
            var centerPixelPrev = previewImage.GetPixels().GetPixel(128, 128);
            Assert.Equal(centerPixelIco.ToColor(), centerPixelPrev.ToColor());
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
    public void ProcessImageBytesRendersAllThreeModesWithPaddingToValidIco()
    {
        using var testImage = new MagickImage(MagickColors.ForestGreen, 300, 150);
        testImage.Format = MagickFormat.Png;
        byte[] imageBytes = testImage.ToByteArray();

        foreach (var mode in new[] { ImagePlacementMode.Contain, ImagePlacementMode.Cover, ImagePlacementMode.Fill })
        {
            string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_test_pad_mode_{mode}_{Guid.NewGuid():N}.ico");
            try
            {
                var result = _pipeline.ProcessImageBytes(imageBytes, tempIco, mode, paddingPercent: 8);
                Assert.True(result.IsSuccess, $"Mode {mode} with padding failed: {result.ErrorMessage}");
                Assert.True(File.Exists(tempIco));

                using MagickImageCollection frames = new(tempIco);
                Assert.Equal(10, frames.Count);

                int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
                var actualSizes = frames.Select(f => (int)f.Width).OrderBy(s => s).ToArray();
                Assert.Equal(expectedSizes, actualSizes);
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
}
