using System;
using System.IO;
using System.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;

namespace Ornare.Imaging.Tests;

public class ImagePlacementTests
{
    private readonly ImagePipeline _pipeline = new();

    [Theory]
    [InlineData(800, 400, 256, 256)] // Landscape 2:1
    [InlineData(400, 800, 256, 256)] // Portrait 1:2
    [InlineData(500, 500, 256, 256)] // Square 1:1
    public void ComputePlacementContainPreservesAspectRatioAndCenters(int srcW, int srcH, int canvasW, int canvasH)
    {
        var geom = ImagePlacementCalculator.ComputePlacement(srcW, srcH, canvasW, canvasH, ImagePlacementMode.Contain);

        // In Contain: full source is used (no crop)
        Assert.Equal(0, geom.SourceCropX);
        Assert.Equal(0, geom.SourceCropY);
        Assert.Equal(srcW, geom.SourceCropWidth);
        Assert.Equal(srcH, geom.SourceCropHeight);

        // Aspect ratio is preserved
        double srcAspect = (double)srcW / srcH;
        double destAspect = (double)geom.DestWidth / geom.DestHeight;
        Assert.True(Math.Abs(srcAspect - destAspect) < 0.02, $"Aspect ratios should match: src={srcAspect}, dest={destAspect}");

        // Fits inside canvas
        Assert.True(geom.DestWidth <= canvasW);
        Assert.True(geom.DestHeight <= canvasH);

        // Centered
        int expectedDestX = (canvasW - geom.DestWidth) / 2;
        int expectedDestY = (canvasH - geom.DestHeight) / 2;
        Assert.Equal(expectedDestX, geom.DestX);
        Assert.Equal(expectedDestY, geom.DestY);
    }

    [Theory]
    [InlineData(800, 400, 256, 256)] // Landscape 2:1 -> crops width
    [InlineData(400, 800, 256, 256)] // Portrait 1:2 -> crops height
    [InlineData(500, 500, 256, 256)] // Square 1:1 -> exact match
    public void ComputePlacementCoverPreservesAspectRatioAndCenterCrops(int srcW, int srcH, int canvasW, int canvasH)
    {
        var geom = ImagePlacementCalculator.ComputePlacement(srcW, srcH, canvasW, canvasH, ImagePlacementMode.Cover);

        // In Cover: fills the entire destination canvas
        Assert.Equal(0, geom.DestX);
        Assert.Equal(0, geom.DestY);
        Assert.Equal(canvasW, geom.DestWidth);
        Assert.Equal(canvasH, geom.DestHeight);

        // Crop aspect ratio equals target canvas aspect ratio
        double targetAspect = (double)canvasW / canvasH;
        double cropAspect = (double)geom.SourceCropWidth / geom.SourceCropHeight;
        Assert.True(Math.Abs(targetAspect - cropAspect) < 0.02, $"Crop aspect {cropAspect} should match target aspect {targetAspect}");

        // Crop is centered within source bounds
        if (srcW > srcH)
        {
            // Landscape: height kept, width cropped and centered
            Assert.Equal(srcH, geom.SourceCropHeight);
            Assert.Equal(0, geom.SourceCropY);
            int expectedCropX = (srcW - geom.SourceCropWidth) / 2;
            Assert.Equal(expectedCropX, geom.SourceCropX);
        }
        else if (srcH > srcW)
        {
            // Portrait: width kept, height cropped and centered
            Assert.Equal(srcW, geom.SourceCropWidth);
            Assert.Equal(0, geom.SourceCropX);
            int expectedCropY = (srcH - geom.SourceCropHeight) / 2;
            Assert.Equal(expectedCropY, geom.SourceCropY);
        }
        else
        {
            Assert.Equal(0, geom.SourceCropX);
            Assert.Equal(0, geom.SourceCropY);
            Assert.Equal(srcW, geom.SourceCropWidth);
            Assert.Equal(srcH, geom.SourceCropHeight);
        }
    }

    [Theory]
    [InlineData(800, 400, 256, 256)] // Landscape 2:1 -> stretched to 1:1
    [InlineData(400, 800, 256, 256)] // Portrait 1:2 -> stretched to 1:1
    [InlineData(500, 500, 256, 256)] // Square 1:1 -> 1:1
    public void ComputePlacementFillStretchesToCanvasWithoutCropping(int srcW, int srcH, int canvasW, int canvasH)
    {
        var geom = ImagePlacementCalculator.ComputePlacement(srcW, srcH, canvasW, canvasH, ImagePlacementMode.Fill);

        // In Fill: full source is preserved without crop
        Assert.Equal(0, geom.SourceCropX);
        Assert.Equal(0, geom.SourceCropY);
        Assert.Equal(srcW, geom.SourceCropWidth);
        Assert.Equal(srcH, geom.SourceCropHeight);

        // Destination covers entire canvas
        Assert.Equal(0, geom.DestX);
        Assert.Equal(0, geom.DestY);
        Assert.Equal(canvasW, geom.DestWidth);
        Assert.Equal(canvasH, geom.DestHeight);

        // If source is not square and canvas is square, aspect ratio is deliberately distorted
        if (srcW != srcH)
        {
            double srcAspect = (double)srcW / srcH;
            double destAspect = (double)geom.DestWidth / geom.DestHeight;
            Assert.NotEqual(srcAspect, destAspect);
        }
    }

    [Fact]
    public void ComputePlacementBoundaryConditionsHandles1pxAndExtremeDimensions()
    {
        // 1x1 source
        var geom1 = ImagePlacementCalculator.ComputePlacement(1, 1, 256, 256, ImagePlacementMode.Contain);
        Assert.Equal(256, geom1.DestWidth);
        Assert.Equal(256, geom1.DestHeight);

        // 10000x1 extreme landscape
        var geomWide = ImagePlacementCalculator.ComputePlacement(10000, 1, 256, 256, ImagePlacementMode.Contain);
        Assert.Equal(256, geomWide.DestWidth);
        Assert.True(geomWide.DestHeight >= 1);

        // 1x10000 extreme portrait
        var geomTall = ImagePlacementCalculator.ComputePlacement(1, 10000, 256, 256, ImagePlacementMode.Contain);
        Assert.True(geomTall.DestWidth >= 1);
        Assert.Equal(256, geomTall.DestHeight);

        // 0 / negative dimensions clamp safely to minimum 1
        var geomZero = ImagePlacementCalculator.ComputePlacement(0, 0, 0, 0, ImagePlacementMode.Contain);
        Assert.True(geomZero.CanvasWidth >= 1);
        Assert.True(geomZero.CanvasHeight >= 1);
    }

    [Fact]
    public void CacheKeyDiffersByPlacementMode()
    {
        string dummyHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var recipeContain = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            PlacementMode = (int)ImagePlacementMode.Contain
        };

        var recipeCover = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            PlacementMode = (int)ImagePlacementMode.Cover
        };

        var recipeFill = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            PlacementMode = (int)ImagePlacementMode.Fill
        };

        string keyContain = recipeContain.ComputeRenderKey();
        string keyCover = recipeCover.ComputeRenderKey();
        string keyFill = recipeFill.ComputeRenderKey();

        Assert.NotEqual(keyContain, keyCover);
        Assert.NotEqual(keyContain, keyFill);
        Assert.NotEqual(keyCover, keyFill);

        // Repeat calculation is completely deterministic
        Assert.Equal(keyContain, recipeContain.ComputeRenderKey());
    }

    [Fact]
    public void PreviewPngMatchesFinal256FramePlacement()
    {
        // Create an asymmetric 200x100 test image (blue top half, red bottom half)
        using var testImage = new MagickImage(MagickColors.Blue, 200, 100);
        using var bottomHalf = new MagickImage(MagickColors.Red, 200, 50);
        testImage.Composite(bottomHalf, 0, 50, CompositeOperator.Over);
        testImage.Format = MagickFormat.Png;
        byte[] imageBytes = testImage.ToByteArray();

        string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_test_placement_{Guid.NewGuid():N}.ico");
        try
        {
            // Process full ICO with Cover mode
            var icoResult = _pipeline.ProcessImageBytes(imageBytes, tempIco, ImagePlacementMode.Cover);
            Assert.True(icoResult.IsSuccess, icoResult.ErrorMessage);

            // Generate Preview PNG with Cover mode
            var previewResult = ImagePipeline.RenderPlacementPreviewPng(imageBytes, 256, ImagePlacementMode.Cover);
            Assert.True(previewResult.IsSuccess, previewResult.ErrorMessage);
            byte[] previewBytes = previewResult.Value!;

            // Read the 256 frame from the ICO
            using MagickImageCollection icoFrames = new(tempIco);
            var frame256 = icoFrames.FirstOrDefault(f => f.Width == 256 && f.Height == 256);
            Assert.NotNull(frame256);

            using var previewImage = new MagickImage(previewBytes);
            Assert.Equal((uint)256, previewImage.Width);
            Assert.Equal((uint)256, previewImage.Height);

            // Compare pixels of 256 frame and preview image away from the interpolated seam
            var topFrame = frame256.GetPixels().GetPixel(128, 64);
            var topPreview = previewImage.GetPixels().GetPixel(128, 64);
            Assert.Equal(topFrame.ToColor(), topPreview.ToColor());

            var bottomFrame = frame256.GetPixels().GetPixel(128, 192);
            var bottomPreview = previewImage.GetPixels().GetPixel(128, 192);
            Assert.Equal(bottomFrame.ToColor(), bottomPreview.ToColor());
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
    public void ProcessImageBytesRendersAllThreeModesToValidIco()
    {
        using var testImage = new MagickImage(MagickColors.DarkCyan, 300, 150);
        testImage.Format = MagickFormat.Png;
        byte[] imageBytes = testImage.ToByteArray();

        foreach (var mode in new[] { ImagePlacementMode.Contain, ImagePlacementMode.Cover, ImagePlacementMode.Fill })
        {
            string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_test_mode_{mode}_{Guid.NewGuid():N}.ico");
            try
            {
                var result = _pipeline.ProcessImageBytes(imageBytes, tempIco, mode);
                Assert.True(result.IsSuccess, $"Mode {mode} failed: {result.ErrorMessage}");
                Assert.True(File.Exists(tempIco));

                // Verify valid ICO with 10 frames
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
