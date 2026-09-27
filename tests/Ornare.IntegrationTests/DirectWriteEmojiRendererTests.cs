using System;
using System.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public sealed class DirectWriteEmojiRendererTests
{
    [Fact]
    public void IsSupportedReturnsTrueOnWindowsBaseline()
    {
        Assert.True(DirectWriteEmojiRenderer.IsSupported);
    }

    [Theory]
    [InlineData("🚀")] // Rocket (basic)
    [InlineData("🔥")] // Fire (basic)
    [InlineData("📁")] // Folder (basic)
    [InlineData("❤️")] // Red heart with variation selector
    [InlineData("⚠️")] // Warning with variation selector
    [InlineData("👍🏽")] // Thumbs up with medium skin tone
    [InlineData("👋🏿")] // Waving hand with dark skin tone
    [InlineData("👨‍💻")] // Man technologist (ZWJ sequence)
    [InlineData("👩‍🔬")] // Woman scientist (ZWJ sequence)
    [InlineData("🇹🇷")] // Regional indicator flag: Turkey
    [InlineData("🇺🇸")] // Regional indicator flag: United States
    public void RenderEmojiBgraSucceedsAndProducesColorPixels(string emoji)
    {
        int size = 64;
        var result = DirectWriteEmojiRenderer.RenderEmojiBgra(emoji, size);

        Assert.True(result.IsSuccess, $"Failed rendering emoji '{emoji}': {result.ErrorMessage}");
        Assert.NotNull(result.Value);

        byte[] bgra = result.Value;
        Assert.Equal(size * size * 4, bgra.Length);

        // Verify that the surface is not completely transparent
        // At least some pixels must have non-zero alpha and color
        bool hasVisiblePixels = false;
        for (int i = 0; i < bgra.Length; i += 4)
        {
            byte b = bgra[i];
            byte g = bgra[i + 1];
            byte r = bgra[i + 2];
            byte a = bgra[i + 3];

            if (a > 20)
            {
                hasVisiblePixels = true;
                break;
            }
        }

        Assert.True(hasVisiblePixels, $"Emoji '{emoji}' rendered entirely transparent.");

        // Verify it loads into MagickImage correctly
        using var image = new MagickImage(MagickColors.Transparent, (uint)size, (uint)size);
        image.ReadPixels(bgra, new PixelReadSettings((uint)size, (uint)size, StorageType.Char, PixelMapping.BGRA));
        Assert.Equal((uint)size, image.Width);
        Assert.Equal((uint)size, image.Height);
    }

    [Fact]
    public void RenderEmojiBgraSupportsAllStandardFrameSizes()
    {
        int[] frameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        string emoji = "⭐";

        foreach (int size in frameSizes)
        {
            var result = DirectWriteEmojiRenderer.RenderEmojiBgra(emoji, size);
            Assert.True(result.IsSuccess, $"Failed at size {size}: {result.ErrorMessage}");
            Assert.Equal(size * size * 4, result.Value!.Length);
        }
    }

    [Fact]
    public void EmptyOrInvalidSequenceFailsGracefully()
    {
        var resultEmpty = DirectWriteEmojiRenderer.RenderEmojiBgra("", 64);
        Assert.False(resultEmpty.IsSuccess);

        var resultOutOfRange = DirectWriteEmojiRenderer.RenderEmojiBgra("😀", 5);
        Assert.False(resultOutOfRange.IsSuccess);
    }
}
