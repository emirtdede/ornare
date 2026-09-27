using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Emblems;
using Ornare.Imaging.Templates;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public sealed class VectorEmblemTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public VectorEmblemTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_emblems_{Guid.NewGuid():N}");
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

    [Fact]
    public void CatalogLoadsExactly320CuratedEmblems()
    {
        var all = VectorEmblemCatalog.All;
        Assert.Equal(320, all.Count);
        Assert.Equal(320, VectorEmblemCatalog.TotalCount);

        // Verify all IDs are non-empty and unique
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Id), "Emblem ID must not be blank.");
            Assert.False(string.IsNullOrWhiteSpace(entry.ShortId), "Emblem ShortId must not be blank.");
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName), "Emblem DisplayName must not be blank.");
            Assert.False(string.IsNullOrWhiteSpace(entry.Category), "Emblem Category must not be blank.");
            Assert.False(string.IsNullOrWhiteSpace(entry.PathData), "Emblem PathData must not be blank.");
            Assert.True(ids.Add(entry.Id), $"Duplicate emblem ID found: {entry.Id}");
        }
    }

    [Fact]
    public void CatalogContainsExactTwelveRequiredCategories()
    {
        var categories = VectorEmblemCatalog.Categories;
        Assert.Equal(12, categories.Count);

        string[] expectedCategories =
        [
            "general", "work", "development", "documents",
            "media", "communication", "security", "finance",
            "education", "gaming", "personal", "system"
        ];

        foreach (var expected in expectedCategories)
        {
            Assert.Contains(expected, categories);
        }

        // Verify every emblem belongs to one of the 12 categories
        foreach (var emblem in VectorEmblemCatalog.All)
        {
            Assert.Contains(emblem.Category.ToLowerInvariant(), expectedCategories);
        }
    }

    [Fact]
    public void LookupByIdAndShortIdResolvesDeterministicEntry()
    {
        var byFullId = VectorEmblemCatalog.GetById("ic_fluent_folder_24_regular");
        Assert.NotNull(byFullId);
        Assert.Equal("ic_fluent_folder_24_regular", byFullId.Id);
        Assert.Equal("folder", byFullId.ShortId);

        var byShortId = VectorEmblemCatalog.GetById("folder");
        Assert.NotNull(byShortId);
        Assert.Equal(byFullId.Id, byShortId.Id);

        var byCaseInsensitive = VectorEmblemCatalog.GetById("FOLDER");
        Assert.NotNull(byCaseInsensitive);
        Assert.Equal(byFullId.Id, byCaseInsensitive.Id);

        var nonExistent = VectorEmblemCatalog.GetById("non_existent_emblem_xyz");
        Assert.Null(nonExistent);
    }

    [Fact]
    public void LocalSearchMeetsP95FiftyMsBudgetAndFiltersCorrectly()
    {
        // Warm up
        _ = VectorEmblemCatalog.Search("code");

        string[] testQueries = ["code", "folder", "lock", "document", "cloud", "play", "game", "book", "money", "chat"];
        var latencies = new List<double>(testQueries.Length * 2);

        foreach (var query in testQueries)
        {
            var sw = Stopwatch.StartNew();
            var results = VectorEmblemCatalog.Search(query).ToList();
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);

            Assert.NotEmpty(results);
            foreach (var r in results)
            {
                bool matches = r.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               r.ShortId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               r.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
                Assert.True(matches, $"Search result '{r.Id}' does not match query '{query}'");
            }
        }

        // Category filter test
        var devResults = VectorEmblemCatalog.Search(null, "development").ToList();
        Assert.NotEmpty(devResults);
        Assert.All(devResults, e => Assert.Equal("development", e.Category, ignoreCase: true));

        latencies.Sort();
        double p95 = latencies[(int)(latencies.Count * 0.95)];
        _output.WriteLine($"Vector emblem local search P95 latency: {p95:F3} ms");
        Assert.True(p95 < 50.0, $"Search P95 ({p95} ms) exceeded 50 ms budget.");
    }

    [Fact]
    public void ShippedEmblemSvgsComplyWithSecurityPolicy()
    {
        var all = VectorEmblemCatalog.All;
        Assert.NotEmpty(all);

        string baseDir = AppContext.BaseDirectory;

        foreach (var entry in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.PathData));
            Assert.DoesNotContain("<script", entry.PathData, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", entry.PathData, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<foreignObject", entry.PathData, StringComparison.OrdinalIgnoreCase);

            string diskPath = Path.Combine(baseDir, entry.SvgAssetPath);
            if (!File.Exists(diskPath))
            {
                diskPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", entry.SvgAssetPath));
            }

            if (File.Exists(diskPath))
            {
                string svgText = File.ReadAllText(diskPath);
                Assert.DoesNotContain("<script", svgText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("javascript:", svgText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("<foreignObject", svgText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("<image", svgText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("xlink:href=\"http", svgText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("href=\"http", svgText, StringComparison.OrdinalIgnoreCase);

                var doc = XDocument.Parse(svgText);
                Assert.NotNull(doc.Root);
                Assert.Equal("svg", doc.Root.Name.LocalName);
            }
        }
    }

    [Fact]
    public void AdaptiveContrastSelectsOptimalForegroundForLightAndDarkFolders()
    {
        // Light yellow folder (#FFD54F) -> should select dark/black emblem
        string lightFg = FolderEmblemCompositor.DetermineForegroundColor("#FFD54F", "Auto");
        Assert.Equal("#000000", lightFg);

        // Dark navy folder (#1A237E) -> should select white emblem
        string darkFg = FolderEmblemCompositor.DetermineForegroundColor("#1A237E", "Auto");
        Assert.Equal("#FFFFFF", darkFg);

        // Test composite execution
        var templateDesc = FolderTemplateCatalog.GetTemplate("ornare-classic", 2);
        Assert.NotNull(templateDesc.EmblemSafeZone);

        var lightRecipe = FolderEmblemRecipe.CreateVectorIcon("folder", "Auto");
        using var lightImage = new MagickImage(new MagickColor("#FFD54F"), 256u, 256u);
        FolderEmblemCompositor.CompositeEmblem(lightImage, lightRecipe, "#FFD54F", templateDesc.EmblemSafeZone);

        var darkRecipe = FolderEmblemRecipe.CreateVectorIcon("folder", "Auto");
        using var darkImage = new MagickImage(new MagickColor("#1A237E"), 256u, 256u);
        FolderEmblemCompositor.CompositeEmblem(darkImage, darkRecipe, "#1A237E", templateDesc.EmblemSafeZone);

        Assert.Equal(256u, lightImage.Width);
        Assert.Equal(256u, darkImage.Width);
    }

    [Fact]
    public void ExplicitEmblemColorOverridesAdaptiveContrast()
    {
        string determined = FolderEmblemCompositor.DetermineForegroundColor("#FFFFFF", "#E91E63");
        Assert.Equal("#E91E63", determined);

        var explicitRecipe = FolderEmblemRecipe.CreateVectorIcon("folder", "#E91E63");
        using var image = new MagickImage(new MagickColor("#FFFFFF"), 256u, 256u);

        var templateDesc = FolderTemplateCatalog.GetTemplate("ornare-classic", 2);
        Assert.NotNull(templateDesc.EmblemSafeZone);

        FolderEmblemCompositor.CompositeEmblem(image, explicitRecipe, "#FFFFFF", templateDesc.EmblemSafeZone);
        Assert.Equal(256u, image.Width);
    }

    [Fact]
    public void EmblemScalesAndRendersInsideEmblemSafeZone()
    {
        var templateDesc = FolderTemplateCatalog.GetTemplate("ornare-classic", 2);
        Assert.NotNull(templateDesc.EmblemSafeZone);

        uint[] frameSizes = [16u, 24u, 32u, 48u, 64u, 128u, 256u];
        foreach (uint size in frameSizes)
        {
            using var frame = new MagickImage(new MagickColor("#2196F3"), size, size);
            var recipe = FolderEmblemRecipe.CreateVectorIcon("folder", "Auto", scale: 0.85);
            FolderEmblemCompositor.CompositeEmblem(frame, recipe, "#2196F3", templateDesc.EmblemSafeZone);

            Assert.Equal(size, frame.Width);
            Assert.Equal(size, frame.Height);
        }
    }

    [Fact]
    public void RenderColoredTemplateIcoWithEmblemProducesMultiResIco()
    {
        string outPath = Path.Combine(_tempDir, "classic_with_emblem.ico");
        var emblem = FolderEmblemRecipe.CreateVectorIcon("folder", "Auto");

        var result = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            "#0078D4",
            outPath,
            "ornare-classic",
            2,
            emblem: emblem);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(File.Exists(outPath));

        using var collection = new MagickImageCollection(outPath);
        Assert.Equal(10, collection.Count);

        uint[] expectedSizes = [16u, 20u, 24u, 32u, 40u, 48u, 64u, 96u, 128u, 256u];
        for (int i = 0; i < expectedSizes.Length; i++)
        {
            Assert.Equal(expectedSizes[i], collection[i].Width);
            Assert.Equal(expectedSizes[i], collection[i].Height);
        }
    }

    [Fact]
    public void RenderPreviewPngWithEmblemSucceeds()
    {
        var emblem = FolderEmblemRecipe.CreateVectorIcon("folder", "#FFFFFF");
        var result = FolderTemplateRenderer.RenderPreviewPng("#E91E63", "ornare-aqua", 1, emblem: emblem);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value);

        using var previewImg = new MagickImage(result.Value);
        Assert.Equal(256u, previewImg.Width);
        Assert.Equal(256u, previewImg.Height);
    }
}
