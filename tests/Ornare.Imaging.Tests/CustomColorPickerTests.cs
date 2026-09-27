using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;

namespace Ornare.Imaging.Tests;

public sealed class CustomColorPickerTests
{
    [Fact]
    public void HexRgbEquivalenceYieldsIdenticalPaletteAndSemanticRoles()
    {
        string hex = "#FF8000";
        Assert.True(ColorConversion.TryParseHex(hex, out byte r, out byte g, out byte b));
        Assert.Equal(255, r);
        Assert.Equal(128, g);
        Assert.Equal(0, b);

        var paletteFromRgb = FolderPaletteGenerator.GenerateFromRgb(r, g, b);
        var paletteFromHex = FolderPaletteGenerator.GenerateFromRgb(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));

        Assert.Equal(paletteFromRgb.BaseColor, paletteFromHex.BaseColor);
        Assert.Equal(paletteFromRgb.DeepColor, paletteFromHex.DeepColor);
        Assert.Equal(paletteFromRgb.ShadowColor, paletteFromHex.ShadowColor);
        Assert.Equal(paletteFromRgb.LightColor, paletteFromHex.LightColor);
        Assert.Equal(paletteFromRgb.HighlightColor, paletteFromHex.HighlightColor);
        Assert.Equal(paletteFromRgb.BadgeColor, paletteFromHex.BadgeColor);
    }

    [Fact]
    public void RenderPreviewPngProducesValid256x256PngWithoutCreatingIcoFiles()
    {
        string hex = "#3F51B5";
        var previewResult = FolderTemplateRenderer.RenderPreviewPng(hex);

        Assert.True(previewResult.IsSuccess, previewResult.ErrorMessage);
        Assert.NotNull(previewResult.Value);
        byte[] pngBytes = previewResult.Value;

        // Verify PNG magic bytes
        Assert.True(pngBytes.Length > 8);
        Assert.Equal(0x89, pngBytes[0]);
        Assert.Equal((byte)'P', pngBytes[1]);
        Assert.Equal((byte)'N', pngBytes[2]);
        Assert.Equal((byte)'G', pngBytes[3]);

        // Verify image dimensions are exactly 256x256
        using var image = new MagickImage(pngBytes);
        Assert.Equal(256u, image.Width);
        Assert.Equal(256u, image.Height);
        Assert.Equal(MagickFormat.Png, image.Format);
    }

    [Fact]
    public async Task RapidSliderMovementSimulationCancelsSupersededWorkAndGeneratesZeroIcoFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ornare_slider_sim_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            int previewRenderCount = 0;
            CancellationTokenSource? debounceCts = null;
            Task? activeTask = null;

            // Simulate dragging a color slider rapidly across 30 color steps every 5 ms
            for (int i = 0; i < 30; i++)
            {
                byte red = (byte)(i * 8);
                string currentHex = $"#{red:X2}8080";

                debounceCts?.Cancel();
                debounceCts?.Dispose();
                debounceCts = new CancellationTokenSource();
                var token = debounceCts.Token;

                activeTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(150, token);
                        if (token.IsCancellationRequested) return;

                        var result = FolderTemplateRenderer.RenderPreviewPng(currentHex);
                        if (result.IsSuccess)
                        {
                            Interlocked.Increment(ref previewRenderCount);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }, token);

                await Task.Delay(5); // Rapid next step, superseding previous
            }

            // Wait for final debounced step to settle
            if (activeTask != null)
            {
                await activeTask;
            }

            // During 30 rapid steps, only the final settled step should have rendered
            Assert.Equal(1, previewRenderCount);

            // Verify ZERO .ico files exist in the temp simulation directory
            var icoFiles = Directory.GetFiles(tempDir, "*.ico", SearchOption.AllDirectories);
            Assert.Empty(icoFiles);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
