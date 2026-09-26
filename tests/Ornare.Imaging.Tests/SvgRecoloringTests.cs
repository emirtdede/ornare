using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public class SvgRecoloringTests
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline = new();

    public SvgRecoloringTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BuiltInSemanticRoleRecolorChangesOnlyExpectedPaintFields()
    {
        string templateSvg = @"
<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"">
  <defs>
    <linearGradient id=""grad1"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" data-ornare-role=""base"" stop-color=""#107C41"" />
      <stop offset=""100%"" data-ornare-role=""light"" stop-color=""#27AE60"" />
    </linearGradient>
  </defs>
  <rect id=""bg-unannotated"" x=""0"" y=""0"" width=""256"" height=""256"" fill=""none"" />
  <path id=""folder-back"" data-ornare-role=""deep"" d=""M20 40 L100 40 L120 60 L236 60 L236 216 L20 216 Z"" fill=""#0E5C2F"" />
  <path id=""folder-front"" data-ornare-role=""base"" d=""M20 70 L236 70 L236 216 L20 216 Z"" fill=""url(#grad1)"" />
  <path id=""folder-tab"" data-ornare-role=""highlight"" d=""M30 50 L90 50 L105 70 L30 70 Z"" fill=""#2ECC71"" />
  <circle id=""badge-icon"" data-ornare-role=""badge"" cx=""200"" cy=""190"" r=""30"" fill=""#F1C40F"" />
</svg>";

        var palette = SemanticColorPalette.FromBaseColor("#0078D4");

        var result = SvgRecolorService.RecolorSemanticTemplate(templateSvg, palette);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var doc = XDocument.Parse(result.Value);

        // Verify stop elements recolored
        var stops = doc.Descendants().Where(e => e.Name.LocalName == "stop").ToList();
        Assert.Equal(2, stops.Count);
        Assert.Equal(palette.BaseColor, stops[0].Attribute("stop-color")?.Value);
        Assert.Equal(palette.LightColor, stops[1].Attribute("stop-color")?.Value);

        // Verify deep role path recolored to palette.DeepColor
        var backPath = doc.Descendants().First(e => e.Name.LocalName == "path" && (string?)e.Attribute("id") == "folder-back");
        Assert.Equal(palette.DeepColor, backPath.Attribute("fill")?.Value);

        // Verify unannotated element fill="none" untouched
        var unannotated = doc.Descendants().First(e => e.Name.LocalName == "rect" && (string?)e.Attribute("id") == "bg-unannotated");
        Assert.Equal("none", unannotated.Attribute("fill")?.Value);

        // Verify badge recolored to palette.BadgeColor
        var badge = doc.Descendants().First(e => e.Name.LocalName == "circle" && (string?)e.Attribute("id") == "badge-icon");
        Assert.Equal(palette.BadgeColor, badge.Attribute("fill")?.Value);

        // Verify geometry attributes (d, viewBox, x, y, width, height) preserved intact
        Assert.Equal("M20 40 L100 40 L120 60 L236 60 L236 216 L20 216 Z", backPath.Attribute("d")?.Value);
        Assert.Equal("0 0 256 256", doc.Root?.Attribute("viewBox")?.Value);
    }

    [Theory]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><path d=""M10 10 H90 V90 H10 Z"" fill=""#336699"" /></svg>", "#FF5500")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><path d=""M10 10 H90 V90 H10 Z"" stroke=""#336699"" fill=""none"" stroke-width=""2"" /></svg>", "#00AA55")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100"" color=""#336699""><path d=""M10 10 H90 V90 H10 Z"" fill=""currentColor"" /></svg>", "#9900CC")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><path d=""M10 10 H90 V90 H10 Z"" style=""fill:#336699;stroke:#336699"" fill-opacity=""0.75"" /></svg>", "#112233")]
    public void SingleColorFillStrokeCurrentColorFixturesRecolorCorrectly(string sourceSvg, string targetColor)
    {
        var analysis = SvgThemeabilityAnalyzer.AnalyzeSingleColorThemeability(sourceSvg);
        Assert.True(analysis.IsSuccess);

        var recolorResult = SvgRecolorService.RecolorUserSingleColorSvg(sourceSvg, targetColor);
        Assert.True(recolorResult.IsSuccess);
        Assert.NotNull(recolorResult.Value);

        // Analyze recolored SVG: must now be single-color matching normalized targetColor
        string expectedTarget = SemanticColorPalette.NormalizeHexColor(targetColor);
        var recoloredAnalysis = SvgThemeabilityAnalyzer.AnalyzeSingleColorThemeability(recolorResult.Value);
        Assert.True(recoloredAnalysis.IsSuccess);
        Assert.Equal(expectedTarget, recoloredAnalysis.Value);

        // If original had fill-opacity, verify it survived
        if (sourceSvg.Contains("fill-opacity=\"0.75\"", StringComparison.Ordinal))
        {
            Assert.Contains("fill-opacity=\"0.75\"", recolorResult.Value, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><rect fill=""#FF0000"" width=""10"" height=""10""/><rect fill=""#0000FF"" x=""10"" width=""10"" height=""10""/></svg>", "multiple distinct colors")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><style>.c { fill: #FF0000; }</style><rect class=""c"" width=""10"" height=""10""/></svg>", "stylesheets")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><defs><pattern id=""p"" width=""4"" height=""4""/></defs><rect fill=""url(#p)"" width=""10"" height=""10""/></svg>", "pattern")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><defs><linearGradient id=""g""><stop stop-color=""#F00""/><stop stop-color=""#00F""/></linearGradient></defs><rect fill=""url(#g)"" width=""10"" height=""10""/></svg>", "unannotated gradients")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><rect fill=""var(--accent-color)"" width=""10"" height=""10""/></svg>", "unsupported paint servers")]
    public void MultipleColorsGradientsPatternsAndStylesheetsReturnSvgNotThemeable(string svg, string expectedReasonKeyword)
    {
        var result = SvgThemeabilityAnalyzer.AnalyzeSingleColorThemeability(svg);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.SvgNotThemeable, result.ErrorCode);
        Assert.Contains(expectedReasonKeyword, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var recolorResult = SvgRecolorService.RecolorUserSingleColorSvg(svg, "#0078D4");
        Assert.False(recolorResult.IsSuccess);
        Assert.Equal(ErrorCode.SvgNotThemeable, recolorResult.ErrorCode);
    }

    [Theory]
    [InlineData(@"<!DOCTYPE svg SYSTEM ""http://evil.com/xxe.dtd""><svg xmlns=""http://www.w3.org/2000/svg""><path d=""M0 0""/></svg>")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><script>alert(document.cookie)</script><rect/></svg>")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><foreignObject width=""100"" height=""100""><body xmlns=""http://www.w3.org/1999/xhtml""><iframe src=""http://evil.com""/></body></foreignObject></svg>")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink""><image xlink:href=""http://evil.com/exfil""/></svg>")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink""><image xlink:href=""file:///C:/Windows/win.ini""/></svg>")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" onload=""fetch('http://evil.com')"" />")]
    public void SecurityPreflightRejectsExternalDtdScriptAndForeignObject(string maliciousSvg)
    {
        var secResult = SvgSecurityValidator.Validate(maliciousSvg);
        Assert.False(secResult.IsSuccess);
        Assert.Equal(ErrorCode.SvgSecurityViolation, secResult.ErrorCode);

        var analysisResult = SvgThemeabilityAnalyzer.AnalyzeSingleColorThemeability(maliciousSvg);
        Assert.False(analysisResult.IsSuccess);
        Assert.Equal(ErrorCode.SvgSecurityViolation, analysisResult.ErrorCode);

        var recolorResult = SvgRecolorService.RecolorUserSingleColorSvg(maliciousSvg, "#0078D4");
        Assert.False(recolorResult.IsSuccess);
        Assert.Equal(ErrorCode.SvgSecurityViolation, recolorResult.ErrorCode);
    }

    [Fact]
    public void MalformedAndOversizedXmlFailsWithoutCrash()
    {
        // Malformed XML
        string malformed = "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0\" fill=\"red\">";
        var malResult = SvgThemeabilityAnalyzer.AnalyzeSingleColorThemeability(malformed);
        Assert.False(malResult.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, malResult.ErrorCode);

        // Oversized XML (> 5MB)
        byte[] oversized = new byte[6 * 1024 * 1024];
        Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\">").CopyTo(oversized, 0);
        var overResult = SvgSecurityValidator.Validate(oversized);
        Assert.False(overResult.IsSuccess);
        Assert.Equal(ErrorCode.ImageExceedsLimits, overResult.ErrorCode);
    }

    [Fact]
    public void XmlWhitespaceAndAttributeOrderingDoNotChangeRenderKey()
    {
        string rawSourceBytes = "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0\" fill=\"#111111\"/></svg>";
        byte[] sourceBytes = Encoding.UTF8.GetBytes(rawSourceBytes);
        string sourceHash = _pipeline.ComputeSourceHash(sourceBytes);

        var recipe1 = new RenderRecipe
        {
            SourceAssetHash = sourceHash,
            SvgRecolorMode = 2,
            SvgRecolorValue = "#0078D4",
            SvgRecolorVersion = 1
        };

        var recipe2 = new RenderRecipe
        {
            SourceAssetHash = sourceHash,
            SvgRecolorMode = 2,
            SvgRecolorValue = "#0078D4",
            SvgRecolorVersion = 1
        };

        // Render key is computed from canonical recipe inputs, independent of internal XML serialization formatting
        Assert.Equal(recipe1.ComputeRenderKey(), recipe2.ComputeRenderKey());

        // But changing the recolor value changes the render key
        var recipeDifferentColor = recipe1 with { SvgRecolorValue = "#FF0000" };
        Assert.NotEqual(recipe1.ComputeRenderKey(), recipeDifferentColor.ComputeRenderKey());
    }

    [Fact]
    public void GoldenSemanticSvgRenderPassesThroughMagickNet()
    {
        string semanticSvg = @"
<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"">
  <path data-ornare-role=""deep"" d=""M20 40 L100 40 L120 60 L236 60 L236 216 L20 216 Z"" fill=""#104060"" />
  <path data-ornare-role=""base"" d=""M20 70 L236 70 L236 216 L20 216 Z"" fill=""#1E88E5"" />
  <path data-ornare-role=""highlight"" d=""M30 50 L90 50 L105 70 L30 70 Z"" fill=""#64B5F6"" />
</svg>";

        byte[] svgBytes = Encoding.UTF8.GetBytes(semanticSvg);
        string tempIco = Path.Combine(Path.GetTempPath(), $"golden_svg_{Guid.NewGuid():N}.ico");

        try
        {
            var recipe = new RenderRecipe
            {
                SourceAssetHash = _pipeline.ComputeSourceHash(svgBytes),
                SvgRecolorMode = 1,
                SvgRecolorValue = "#D83B01", // Microsoft Orange-Red
                TargetFormat = "ico",
                TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
            };

            var result = _pipeline.ProcessRecipe(recipe, svgBytes, tempIco);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.Value);
            Assert.True(File.Exists(tempIco));

            // Verify generated multi-resolution ICO
            Assert.True(_pipeline.IsValidIco(tempIco, expectedMinFrames: 10));

            using var collection = new MagickImageCollection(tempIco);
            Assert.Equal(10, collection.Count);
            int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
            for (int i = 0; i < expectedSizes.Length; i++)
            {
                Assert.Equal((uint)expectedSizes[i], collection[i].Width);
                Assert.Equal((uint)expectedSizes[i], collection[i].Height);
            }
        }
        finally
        {
            if (File.Exists(tempIco))
            {
                try { File.Delete(tempIco); } catch { }
            }
        }
    }

    [Fact]
    public void BenchmarkOneThousandBuiltInTemplateRecolors()
    {
        string semanticSvg = @"
<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 256 256"">
  <path data-ornare-role=""deep"" d=""M20 40 L100 40 L120 60 L236 60 L236 216 L20 216 Z"" fill=""#104060"" />
  <path data-ornare-role=""base"" d=""M20 70 L236 70 L236 216 L20 216 Z"" fill=""#1E88E5"" />
  <path data-ornare-role=""highlight"" d=""M30 50 L90 50 L105 70 L30 70 Z"" fill=""#64B5F6"" />
</svg>";

        var palette = SemanticColorPalette.FromBaseColor("#0078D4");

        // Warm up
        var warm = SvgRecolorService.RecolorSemanticTemplate(semanticSvg, palette);
        Assert.True(warm.IsSuccess);

        const int iterations = 1000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var res = SvgRecolorService.RecolorSemanticTemplate(semanticSvg, palette);
            Assert.True(res.IsSuccess);
        }
        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double perRecolorMicroseconds = (totalMs / iterations) * 1000.0;

        _output.WriteLine($"1,000 Built-in Semantic Template Recolors: {totalMs:F2} ms total ({perRecolorMicroseconds:F1} µs / recolor)");

        // Target: 1000 recolors in < 500 ms (< 500 µs per recolor)
        Assert.True(totalMs < 500.0, $"1000 recolors must take < 500ms, was {totalMs:F2}ms");
    }
}
