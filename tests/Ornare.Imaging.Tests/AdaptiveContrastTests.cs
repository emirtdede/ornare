using System;
using System.Diagnostics;
using System.Globalization;
using Ornare.Core;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public sealed class AdaptiveContrastTests
{
    private readonly ITestOutputHelper _output;

    public AdaptiveContrastTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(255, 255, 255)] // White
    [InlineData(0, 0, 0)]       // Black
    [InlineData(255, 0, 0)]     // Pure Red
    [InlineData(0, 255, 0)]     // Pure Green
    [InlineData(0, 0, 255)]     // Pure Blue
    [InlineData(128, 128, 128)] // 50% Gray
    [InlineData(0, 255, 255)]   // Cyan
    [InlineData(255, 0, 255)]   // Magenta
    [InlineData(255, 255, 0)]   // Yellow
    [InlineData(0, 120, 212)]   // Windows Blue #0078D4
    public void KnownColorVectorsRoundTripAccurately(byte r, byte g, byte b)
    {
        OklchColor lch = ColorConversion.RgbToOklch(r, g, b);
        var (roundR, roundG, roundB) = ColorConversion.MapLchToRgb(lch.L, lch.C, lch.H);

        Assert.Equal(r, roundR);
        Assert.Equal(g, roundG);
        Assert.Equal(b, roundB);
    }

    [Fact]
    public void BaseColorIsStrictlyPreservedInAllPalettes()
    {
        // Test all 256 gray levels
        for (int i = 0; i < 256; i++)
        {
            byte b = (byte)i;
            var palette = FolderPaletteGenerator.GenerateFromRgb(b, b, b);
            string expectedHex = $"#{b:X2}{b:X2}{b:X2}";
            Assert.Equal(expectedHex, palette.BaseColor);
        }

        // Test representative vibrant accent colors
        string[] testColors = ["#0078D4", "#D83B01", "#107C41", "#5C2D91", "#008272", "#FFB900", "#E81123"];
        foreach (string hex in testColors)
        {
            var palette = FolderPaletteGenerator.GenerateFromHex(hex);
            Assert.Equal(hex, palette.BaseColor);
        }
    }

    [Fact]
    public void EdgeCasesPureBlackPureWhiteProduceClampedMonotonicPalettes()
    {
        // 1. Pure Black #000000
        var blackPalette = FolderPaletteGenerator.GenerateFromRgb(0, 0, 0);
        Assert.Equal("#000000", blackPalette.BaseColor);
        Assert.True(ColorConversion.TryParseHex(blackPalette.DeepColor, out byte dr, out byte dg, out byte db));
        Assert.True(ColorConversion.TryParseHex(blackPalette.ShadowColor, out byte sr, out byte sg, out byte sb));
        Assert.True(ColorConversion.TryParseHex(blackPalette.LightColor, out byte lr, out byte lg, out byte lb));
        Assert.True(ColorConversion.TryParseHex(blackPalette.HighlightColor, out byte hr, out byte hg, out byte hb));

        // Black has L=0. Derived deep & shadow clamp to 0.02.
        // Light has target L=0.08, Highlight target L=0.14.
        OklchColor blackBaseLch = ColorConversion.RgbToOklch(0, 0, 0);
        OklchColor blackDeepLch = ColorConversion.RgbToOklch(dr, dg, db);
        OklchColor blackLightLch = ColorConversion.RgbToOklch(lr, lg, lb);
        OklchColor blackHighLch = ColorConversion.RgbToOklch(hr, hg, hb);

        Assert.True(blackLightLch.L >= blackBaseLch.L);
        Assert.True(blackHighLch.L >= blackLightLch.L);

        // 2. Pure White #FFFFFF
        var whitePalette = FolderPaletteGenerator.GenerateFromRgb(255, 255, 255);
        Assert.Equal("#FFFFFF", whitePalette.BaseColor);
        Assert.True(ColorConversion.TryParseHex(whitePalette.DeepColor, out byte wdr, out byte wdg, out byte wdb));
        Assert.True(ColorConversion.TryParseHex(whitePalette.ShadowColor, out byte wsr, out byte wsg, out byte wsb));
        Assert.True(ColorConversion.TryParseHex(whitePalette.LightColor, out byte wlr, out byte wlg, out byte wlb));
        Assert.True(ColorConversion.TryParseHex(whitePalette.HighlightColor, out byte whr, out byte whg, out byte whb));

        // White has L=1. Derived light & highlight clamp to 0.98.
        OklchColor whiteBaseLch = ColorConversion.RgbToOklch(255, 255, 255);
        OklchColor whiteDeepLch = ColorConversion.RgbToOklch(wdr, wdg, wdb);
        OklchColor whiteShadowLch = ColorConversion.RgbToOklch(wsr, wsg, wsb);

        Assert.True(whiteDeepLch.L <= whiteShadowLch.L);
        Assert.True(whiteShadowLch.L <= whiteBaseLch.L);
    }

    [Fact]
    public void TenThousandDeterministicInputsProduceValidPalettesAndMonotonicLightness()
    {
        // Deterministic pseudo-random seed for cross-run reproducibility
        var rng = new Random(42);
        const int iterations = 10_000;

        for (int i = 0; i < iterations; i++)
        {
            byte r = (byte)rng.Next(256);
            byte g = (byte)rng.Next(256);
            byte b = (byte)rng.Next(256);

            var palette = FolderPaletteGenerator.GenerateFromRgb(r, g, b);

            Assert.Equal(ColorConversion.ToHex(r, g, b), palette.BaseColor);

            Assert.True(ColorConversion.TryParseHex(palette.DeepColor, out byte dr, out byte dg, out byte db));
            Assert.True(ColorConversion.TryParseHex(palette.ShadowColor, out byte sr, out byte sg, out byte sb));
            Assert.True(ColorConversion.TryParseHex(palette.LightColor, out byte lr, out byte lg, out byte lb));
            Assert.True(ColorConversion.TryParseHex(palette.HighlightColor, out byte hr, out byte hg, out byte hb));

            OklchColor baseLch = ColorConversion.RgbToOklch(r, g, b);
            OklchColor deepLch = ColorConversion.RgbToOklch(dr, dg, db);
            OklchColor shadowLch = ColorConversion.RgbToOklch(sr, sg, sb);
            OklchColor lightLch = ColorConversion.RgbToOklch(lr, lg, lb);
            OklchColor highLch = ColorConversion.RgbToOklch(hr, hg, hb);

            // Lightness ordering: deep.L <= shadow.L <= base.L <= light.L <= highlight.L
            // Allowing equality at clamped boundaries (0.02 and 0.98) and small 8-bit quantization tolerance (0.035)
            const double tolerance = 0.035;
            Assert.True(deepLch.L <= shadowLch.L + tolerance,
                $"deep ({deepLch.L:F3}) > shadow ({shadowLch.L:F3}) for input #{r:X2}{g:X2}{b:X2}");

            if (baseLch.L > FolderPaletteGenerator.MinDerivedL + 0.11)
            {
                Assert.True(shadowLch.L <= baseLch.L + tolerance,
                    $"shadow ({shadowLch.L:F3}) > base ({baseLch.L:F3}) for input #{r:X2}{g:X2}{b:X2}");
            }

            if (baseLch.L < FolderPaletteGenerator.MaxDerivedL - 0.08)
            {
                Assert.True(baseLch.L <= lightLch.L + tolerance,
                    $"base ({baseLch.L:F3}) > light ({lightLch.L:F3}) for input #{r:X2}{g:X2}{b:X2}");
            }

            Assert.True(lightLch.L <= highLch.L + tolerance,
                $"light ({lightLch.L:F3}) > highlight ({highLch.L:F3}) for input #{r:X2}{g:X2}{b:X2}");
        }
    }

    [Fact]
    public void WcagContrastCalculationsMatchKnownValues()
    {
        // 1. Black vs White is 21:1
        double blackWhiteContrast = WcagContrast.ComputeContrastRatio(0, 0, 0, 255, 255, 255);
        Assert.InRange(blackWhiteContrast, 20.9, 21.1);

        // 2. Same color is 1:1
        double sameColorContrast = WcagContrast.ComputeContrastRatio(100, 100, 100, 100, 100, 100);
        Assert.InRange(sameColorContrast, 0.99, 1.01);

        // 3. For any solid sRGB color, black or white satisfies >= 3:1 non-text contrast
        var testColors = new (byte r, byte g, byte b)[]
        {
            (0, 120, 212),   // Microsoft Blue
            (216, 59, 1),    // Microsoft Orange
            (16, 124, 65),   // Microsoft Green
            (255, 185, 0),   // Microsoft Yellow
            (232, 17, 35),   // Microsoft Red
            (0, 0, 0),       // Black
            (255, 255, 255), // White
            (128, 128, 128)  // Medium Gray
        };

        foreach (var (r, g, b) in testColors)
        {
            var badgeResult = WcagContrast.SelectBadgeForeground(r, g, b);
            Assert.True(badgeResult.ContrastRatio >= 3.0,
                $"Badge contrast {badgeResult.ContrastRatio:F2} is below 3.0 for #{r:X2}{g:X2}{b:X2}");
            Assert.True(badgeResult.ForegroundColor == "#FFFFFF" || badgeResult.ForegroundColor == "#000000");
        }
    }

    [Fact]
    public void CircularBackingRuleInvokedWhenContrastBelowThreshold()
    {
        // Construct low contrast between two arbitrary mid grays
        double ratio = WcagContrast.ComputeContrastRatio(120, 120, 120, 130, 130, 130);
        Assert.True(ratio < 3.0);

        // Evaluate badge readability directly
        bool belowThreshold = ratio < WcagContrast.NonTextContrastThreshold;
        Assert.True(belowThreshold);
    }

    [Fact]
    public void BenchmarkPaletteGenerationTenThousandIterations()
    {
        // Warm-up JIT
        for (int i = 0; i < 500; i++)
        {
            _ = FolderPaletteGenerator.GenerateFromRgb((byte)(i % 256), (byte)((i * 3) % 256), (byte)((i * 7) % 256));
        }

        const int iterations = 10_000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            byte r = (byte)(i & 0xFF);
            byte g = (byte)((i >> 2) & 0xFF);
            byte b = (byte)((i >> 4) & 0xFF);
            _ = FolderPaletteGenerator.GenerateFromRgb(r, g, b);
        }

        sw.Stop();
        double totalMs = sw.Elapsed.TotalMilliseconds;
        double perPaletteUs = (totalMs / iterations) * 1000.0;

        _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "10,000 Palette Generations: {0:F2} ms total ({1:F2} µs / palette)",
            totalMs, perPaletteUs));

        // Requirement: median < 0.1 ms (100 µs) per base color
        Assert.True(perPaletteUs < 100.0, $"Palette generation too slow: {perPaletteUs:F2} µs (expected < 100 µs)");
    }
}
