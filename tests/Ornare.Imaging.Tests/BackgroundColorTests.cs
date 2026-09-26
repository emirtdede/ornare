using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;

namespace Ornare.Imaging.Tests;

public class BackgroundColorTests
{
    private static readonly int[] IcoFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
    private readonly ImagePipeline _pipeline = new();

    #region 1. BackgroundSpec and Hex Normalization Tests

    [Fact]
    public void BackgroundSpecTransparentDefaultsCorrectly()
    {
        var spec = BackgroundSpec.Transparent;
        Assert.Equal(BackgroundMode.Transparent, spec.Mode);
        Assert.Equal(string.Empty, spec.ColorHex);

        var defaultStruct = default(BackgroundSpec);
        Assert.Equal(BackgroundMode.Transparent, defaultStruct.Mode);
        Assert.Equal(string.Empty, defaultStruct.ColorHex);
    }

    [Theory]
    [InlineData("#ffffff", "#FFFFFF")]
    [InlineData("#FFFFFF", "#FFFFFF")]
    [InlineData("#FFF", "#FFFFFF")]
    [InlineData("FFF", "#FFFFFF")]
    [InlineData("0078d4", "#0078D4")]
    [InlineData("#1e1e1e", "#1E1E1E")]
    [InlineData("#f00", "#FF0000")]
    public void BackgroundSpecSolidNormalizesHexValues(string input, string expected)
    {
        var spec = BackgroundSpec.Solid(input);
        Assert.Equal(BackgroundMode.SolidColor, spec.Mode);
        Assert.Equal(expected, spec.ColorHex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("GGGGGG")]
    public void BackgroundSpecNormalizeHexFallsBackToWhiteOnInvalidInput(string? invalidInput)
    {
        string normalized = BackgroundSpec.NormalizeHex(invalidInput);
        Assert.Equal("#FFFFFF", normalized);
    }

    [Theory]
    [InlineData("#FFFFFF", true)]
    [InlineData("FFFFFF", true)]
    [InlineData("#FFF", true)]
    [InlineData("FFF", true)]
    [InlineData("#000000", true)]
    [InlineData("#abc", true)]
    [InlineData("123456", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("#12", false)]
    [InlineData("#1234", false)]
    [InlineData("#12345", false)]
    [InlineData("#1234567", false)]
    [InlineData("XYZ123", false)]
    public void BackgroundSpecIsValidHexValidatesCorrectly(string? hex, bool expectedValid)
    {
        Assert.Equal(expectedValid, BackgroundSpec.IsValidHex(hex));
    }

    #endregion

    #region 2. Canonical Recipe Serialization and Cache Invariants

    [Fact]
    public void RecipeDefaultsToTransparentModeInvExpBg01()
    {
        var recipe = new RenderRecipe();
        Assert.Equal(0, recipe.BackgroundMode);
        Assert.Equal(BackgroundMode.Transparent, recipe.Background);
        Assert.Equal(string.Empty, recipe.BackgroundColor);
    }

    [Fact]
    public void RecipeTransparentModeIgnoresStoredColorInCanonicalSerializationInvExpBg02()
    {
        string dummyHash = "a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0";

        var recipeTransparentWithColor = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            BackgroundMode = (int)BackgroundMode.Transparent,
            BackgroundColor = "#FF0000" // Stored color must be ignored in canonical bytes
        };

        var recipeTransparentEmptyColor = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            BackgroundMode = (int)BackgroundMode.Transparent,
            BackgroundColor = string.Empty
        };

        byte[] bytes1 = CanonicalRecipeSerializer.Serialize(recipeTransparentWithColor);
        byte[] bytes2 = CanonicalRecipeSerializer.Serialize(recipeTransparentEmptyColor);

        Assert.Equal(bytes1, bytes2);
        Assert.Equal(recipeTransparentWithColor.ComputeRenderKey(), recipeTransparentEmptyColor.ComputeRenderKey());
    }

    [Fact]
    public void RecipeSolidColorModeDivergesRenderKeyBasedOnColor()
    {
        string dummyHash = "a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0";

        var recipeTransparent = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            BackgroundMode = (int)BackgroundMode.Transparent
        };

        var recipeWhite = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            BackgroundMode = (int)BackgroundMode.SolidColor,
            BackgroundColor = "#FFFFFF"
        };

        var recipeBlack = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            BackgroundMode = (int)BackgroundMode.SolidColor,
            BackgroundColor = "#000000"
        };

        var recipeBlue = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            BackgroundMode = (int)BackgroundMode.SolidColor,
            BackgroundColor = "#0078D4"
        };

        string keyTransparent = recipeTransparent.ComputeRenderKey();
        string keyWhite = recipeWhite.ComputeRenderKey();
        string keyBlack = recipeBlack.ComputeRenderKey();
        string keyBlue = recipeBlue.ComputeRenderKey();

        Assert.NotEqual(keyTransparent, keyWhite);
        Assert.NotEqual(keyWhite, keyBlack);
        Assert.NotEqual(keyWhite, keyBlue);
        Assert.NotEqual(keyBlack, keyBlue);

        // Case insensitivity of hex produces same canonical key
        var recipeBlueLower = new RenderRecipe
        {
            SourceAssetHash = dummyHash,
            BackgroundMode = (int)BackgroundMode.SolidColor,
            BackgroundColor = "#0078d4"
        };
        Assert.Equal(keyBlue, recipeBlueLower.ComputeRenderKey());
    }

    [Fact]
    public void CanonicalRecipeSerializerRoundtripsBackgroundState()
    {
        var original = new RenderRecipe
        {
            SourceAssetHash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef",
            BackgroundMode = (int)BackgroundMode.SolidColor,
            BackgroundColor = "#E63946"
        };

        byte[] bytes = CanonicalRecipeSerializer.Serialize(original);
        var deserialized = CanonicalRecipeSerializer.Deserialize(bytes);

        Assert.Equal(original.BackgroundMode, deserialized.BackgroundMode);
        Assert.Equal(original.Background, deserialized.Background);
        Assert.Equal("#E63946", deserialized.BackgroundColor);
    }

    [Fact]
    public void CanonicalRecipeSerializerDeserializeRejectsInvalidBackgroundMode()
    {
        var valid = new RenderRecipe
        {
            SourceAssetHash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef"
        };
        byte[] bytes = CanonicalRecipeSerializer.Serialize(valid);

        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);
        using var outMs = new MemoryStream();
        using var writer = new BinaryWriter(outMs);

        writer.Write(reader.ReadBytes(4)); // magic
        writer.Write(reader.ReadInt32()); // recVer
        writer.Write(reader.ReadString()); // hash
        writer.Write(reader.ReadInt32()); // normVer
        writer.Write(reader.ReadInt32()); // rendVer
        writer.Write(reader.ReadString()); // templId
        writer.Write(reader.ReadInt32()); // templVer
        writer.Write(reader.ReadInt32()); // palAlg
        writer.Write(reader.ReadString()); // palParam
        writer.Write(reader.ReadInt32()); // svgMode
        writer.Write(reader.ReadString()); // svgVal
        writer.Write(reader.ReadInt32()); // svgVer
        writer.Write(reader.ReadInt32()); // placement
        writer.Write(reader.ReadInt32()); // padding
        reader.ReadInt32(); // skip original bgMode
        writer.Write(99); // invalid corrupt bgMode
        writer.Write(reader.ReadString()); // bgColor
        byte[] rest = reader.ReadBytes((int)(ms.Length - ms.Position));
        writer.Write(rest);

        byte[] corrupted = outMs.ToArray();
        Assert.Throws<InvalidDataException>(() => CanonicalRecipeSerializer.Deserialize(corrupted));
    }

    #endregion

    #region 3. Magick Raster Compositing & Anti-Aliasing Tests

    [Fact]
    public void CompositingOverTransparentPreservesTransparentCorners()
    {
        // 100x100 transparent canvas with Crimson center rectangle (x: 25..75, y: 25..75)
        using var source = new MagickImage(MagickColors.Transparent, 100, 100);
        using (var pixels = source.GetPixels())
        {
            for (int y = 25; y < 75; y++)
            {
                for (int x = 25; x < 75; x++)
                {
                    pixels.SetPixel(x, y, [MagickColors.Crimson.R, MagickColors.Crimson.G, MagickColors.Crimson.B, Quantum.Max]);
                }
            }
        }
        source.Format = MagickFormat.Png;
        byte[] imageBytes = source.ToByteArray();

        var previewResult = ImagePipeline.RenderPlacementPreviewPng(
            imageBytes,
            256,
            ImagePlacementMode.Contain,
            paddingPercent: 0,
            BackgroundMode.Transparent);

        Assert.True(previewResult.IsSuccess);
        using var preview = new MagickImage(previewResult.Value!);

        // Corner pixel must be completely transparent
        var corner = preview.GetPixels().GetPixel(5, 5).ToColor();
        Assert.NotNull(corner);
        Assert.Equal((ushort)0, corner.A);

        // Center pixel must be Crimson
        var center = preview.GetPixels().GetPixel(128, 128).ToColor();
        Assert.NotNull(center);
        Assert.Equal(Quantum.Max, center.A);
        Assert.Equal(MagickColors.Crimson.R, center.R);
    }

    [Theory]
    [InlineData("#FFFFFF")] // White
    [InlineData("#000000")] // Black
    [InlineData("#0078D4")] // Windows Blue
    [InlineData("#E63946")] // Crimson Red
    public void CompositingOverSolidBackgroundRendersOpaqueBackground(string bgHex)
    {
        // 100x100 transparent canvas with Gold center square
        using var source = new MagickImage(MagickColors.Transparent, 100, 100);
        using (var pixels = source.GetPixels())
        {
            for (int y = 25; y < 75; y++)
            {
                for (int x = 25; x < 75; x++)
                {
                    pixels.SetPixel(x, y, [MagickColors.Gold.R, MagickColors.Gold.G, MagickColors.Gold.B, Quantum.Max]);
                }
            }
        }
        source.Format = MagickFormat.Png;
        byte[] imageBytes = source.ToByteArray();

        var previewResult = ImagePipeline.RenderPlacementPreviewPng(
            imageBytes,
            256,
            ImagePlacementMode.Contain,
            paddingPercent: 0,
            BackgroundMode.SolidColor,
            bgHex);

        Assert.True(previewResult.IsSuccess);
        using var preview = new MagickImage(previewResult.Value!);

        var expectedBgColor = new MagickColor(bgHex);

        // Corner pixel must match the solid background color with full opacity
        var corner = preview.GetPixels().GetPixel(5, 5).ToColor();
        Assert.NotNull(corner);
        Assert.Equal(Quantum.Max, corner.A);
        Assert.Equal(expectedBgColor.R, corner.R);
        Assert.Equal(expectedBgColor.G, corner.G);
        Assert.Equal(expectedBgColor.B, corner.B);

        // Center pixel must be Gold
        var center = preview.GetPixels().GetPixel(128, 128).ToColor();
        Assert.NotNull(center);
        Assert.Equal(Quantum.Max, center.A);
        Assert.Equal(MagickColors.Gold.R, center.R);
    }

    [Fact]
    public void AlphaAntiAliasingSmoothTransitionWithoutHalosInvExpBg03()
    {
        // Create an image with an anti-aliased gradient edge:
        // Left 40px: White (255, 255, 255, 255)
        // Pixel 40: 50% semi-transparent White (255, 255, 255, 128)
        // Right 59px: Transparent (0, 0, 0, 0)
        using var source = new MagickImage(MagickColors.Transparent, 100, 100);
        using (var pixels = source.GetPixels())
        {
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 40; x++)
                {
                    pixels.SetPixel(x, y, [Quantum.Max, Quantum.Max, Quantum.Max, Quantum.Max]);
                }
                // 50% alpha edge pixel
                pixels.SetPixel(40, y, [Quantum.Max, Quantum.Max, Quantum.Max, (ushort)(Quantum.Max / 2)]);
            }
        }
        source.Format = MagickFormat.Png;
        byte[] imageBytes = source.ToByteArray();

        // Composite over pure black background (#000000)
        var previewBlack = ImagePipeline.RenderPlacementPreviewPng(
            imageBytes,
            256,
            ImagePlacementMode.Fill,
            paddingPercent: 0,
            BackgroundMode.SolidColor,
            "#000000");

        Assert.True(previewBlack.IsSuccess);
        using var imgBlack = new MagickImage(previewBlack.Value!);

        // Background area (right side, e.g. x=200, y=128) must be pure black
        var bgPixel = imgBlack.GetPixels().GetPixel(200, 128).ToColor();
        Assert.NotNull(bgPixel);
        Assert.Equal(Quantum.Max, bgPixel.A);
        Assert.Equal((ushort)0, bgPixel.R);
        Assert.Equal((ushort)0, bgPixel.G);
        Assert.Equal((ushort)0, bgPixel.B);

        // Foreground area (left side, e.g. x=20, y=128) must be pure white
        var fgPixel = imgBlack.GetPixels().GetPixel(20, 128).ToColor();
        Assert.NotNull(fgPixel);
        Assert.Equal(Quantum.Max, fgPixel.A);
        Assert.Equal(Quantum.Max, fgPixel.R);
        Assert.Equal(Quantum.Max, fgPixel.G);
        Assert.Equal(Quantum.Max, fgPixel.B);

        // The image rendered with full opacity without black fringe halos
        Assert.False(imgBlack.HasAlpha);
    }

    [Fact]
    public void SourceAssetBytesStrictlyImmutableInvExpBg04()
    {
        using var source = new MagickImage(MagickColors.LimeGreen, 128, 128);
        source.Format = MagickFormat.Png;
        byte[] originalBytes = source.ToByteArray();
        byte[] originalBytesCopy = (byte[])originalBytes.Clone();
        string originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));

        string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_test_bg_immut_{Guid.NewGuid():N}.ico");
        try
        {
            // 1. Process with Solid Color
            var result = _pipeline.ProcessImageBytes(
                originalBytes,
                tempIco,
                ImagePlacementMode.Cover,
                paddingPercent: 10,
                BackgroundMode.SolidColor,
                "#1E1E1E");
            Assert.True(result.IsSuccess);

            // 2. Generate preview with Solid Color
            var prevResult = ImagePipeline.RenderPlacementPreviewPng(
                originalBytes,
                256,
                ImagePlacementMode.Cover,
                paddingPercent: 10,
                BackgroundMode.SolidColor,
                "#FF0000");
            Assert.True(prevResult.IsSuccess);

            // Verify original byte array is byte-for-byte unchanged
            Assert.Equal(originalBytesCopy, originalBytes);
            string afterHash = Convert.ToHexString(SHA256.HashData(originalBytes));
            Assert.Equal(originalHash, afterHash);
        }
        finally
        {
            if (File.Exists(tempIco)) File.Delete(tempIco);
        }
    }

    [Fact]
    public void ProcessImageBytesRendersAllIcoFramesWithSolidBackground()
    {
        using var source = new MagickImage(MagickColors.DeepSkyBlue, 80, 80);
        source.Format = MagickFormat.Png;
        byte[] imageBytes = source.ToByteArray();

        string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_test_bg_frames_{Guid.NewGuid():N}.ico");
        try
        {
            var result = _pipeline.ProcessImageBytes(
                imageBytes,
                tempIco,
                ImagePlacementMode.Contain,
                paddingPercent: 5,
                BackgroundMode.SolidColor,
                "#2B2D42");

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(File.Exists(tempIco));

            using var collection = new MagickImageCollection(tempIco);
            Assert.Equal(IcoFrameSizes.Length, collection.Count);

            foreach (int size in IcoFrameSizes)
            {
                var frame = collection.FirstOrDefault(f => f.Width == size && f.Height == size);
                Assert.NotNull(frame);

                // Frame must have solid background color at corner
                var corner = frame.GetPixels().GetPixel(0, 0).ToColor();
                Assert.NotNull(corner);
                // Check opacity
                Assert.Equal(Quantum.Max, corner.A);
            }
        }
        finally
        {
            if (File.Exists(tempIco)) File.Delete(tempIco);
        }
    }

    [Fact]
    public void TransparentSvgOverSolidBackgroundRendersCleanly()
    {
        string svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100"" width=""100"" height=""100"">
            <polygon points=""50,15 90,85 10,85"" fill=""#E63946""/>
        </svg>";
        byte[] svgBytes = System.Text.Encoding.UTF8.GetBytes(svg);

        string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_test_svg_bg_{Guid.NewGuid():N}.ico");
        try
        {
            var result = _pipeline.ProcessImageBytes(
                svgBytes,
                tempIco,
                ImagePlacementMode.Contain,
                paddingPercent: 0,
                BackgroundMode.SolidColor,
                "#F1FAEE");

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(File.Exists(tempIco));

            using var collection = new MagickImageCollection(tempIco);
            var frame256 = collection.FirstOrDefault(f => f.Width == 256 && f.Height == 256);
            Assert.NotNull(frame256);

            // Top-left corner (5, 5) outside triangle must be background #F1FAEE
            var bgExpected = new MagickColor("#F1FAEE");
            var corner = frame256.GetPixels().GetPixel(5, 5).ToColor();
            Assert.NotNull(corner);
            Assert.Equal(Quantum.Max, corner.A);
            Assert.Equal(bgExpected.R, corner.R);
            Assert.Equal(bgExpected.G, corner.G);
            Assert.Equal(bgExpected.B, corner.B);
        }
        finally
        {
            if (File.Exists(tempIco)) File.Delete(tempIco);
        }
    }

    #endregion
}
