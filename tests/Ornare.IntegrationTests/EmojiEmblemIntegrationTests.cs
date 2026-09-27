using System;
using System.IO;
using System.Linq;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Emblems;
using Ornare.Imaging.Templates;
using Ornare.Windows;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

public sealed class EmojiEmblemIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline;
    private readonly string _tempDir;

    public EmojiEmblemIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _pipeline = new ImagePipeline();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ornare_test_emoji_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Ensure DirectWrite rasterizer is registered
        FolderEmblemCompositor.EmojiRasterizer = new DirectWriteEmojiRasterizer();
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

    [Theory]
    [InlineData("ornare-classic", "🔥")]
    [InlineData("ornare-soft", "⭐")]
    [InlineData("ornare-flat", "🚀")]
    [InlineData("ornare-glass", "❤️")]
    [InlineData("ornare-aqua", "👍🏽")]
    [InlineData("ornare-grove", "🌱")]
    [InlineData("ornare-air", "✈️")]
    [InlineData("ornare-flow", "🌊")]
    [InlineData("ornare-geo", "💎")]
    public void CompositeEmojiOnAllNineTemplatesProducesValidIco(string templateId, string emoji)
    {
        var descriptor = FolderTemplateCatalog.GetTemplate(templateId, 2);
        Assert.NotNull(descriptor);

        var emblemRecipe = new FolderEmblemRecipe
        {
            Kind = EmblemKind.Emoji,
            GraphemeSequence = emoji,
            Scale = 1.0,
            OffsetX = 0.0,
            OffsetY = 0.0
        };

        string outIcoPath = Path.Combine(_tempDir, $"{templateId}_emoji.ico");

        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            "#0078D4",
            outIcoPath,
            templateId,
            2,
            emblem: emblemRecipe);

        Assert.True(renderResult.IsSuccess, $"Render failed for {templateId}: {renderResult.ErrorMessage}");
        Assert.True(File.Exists(outIcoPath));

        using var collection = new MagickImageCollection(outIcoPath);
        Assert.Equal(10, collection.Count);

        int[] expectedSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        for (int i = 0; i < expectedSizes.Length; i++)
        {
            Assert.Equal((uint)expectedSizes[i], collection[i].Width);
            Assert.Equal((uint)expectedSizes[i], collection[i].Height);
        }
    }

    [Fact]
    public void EmojiEmblemCoexistsWithStatusAndPriorityBadges()
    {
        var emblemRecipe = new FolderEmblemRecipe
        {
            Kind = EmblemKind.Emoji,
            GraphemeSequence = "⚡",
            Scale = 1.0
        };

        string outIcoPath = Path.Combine(_tempDir, "aqua_emoji_status_priority.ico");

        var renderResult = FolderTemplateRenderer.RenderColoredTemplateIco(
            _pipeline,
            "#2B88D8",
            outIcoPath,
            "ornare-aqua",
            2,
            emblem: emblemRecipe,
            badgeAssetId: "badge-in-progress");

        Assert.True(renderResult.IsSuccess, $"Render failed: {renderResult.ErrorMessage}");
        Assert.True(File.Exists(outIcoPath));

        using var collection = new MagickImageCollection(outIcoPath);
        Assert.Equal(10, collection.Count);
    }

    [Fact]
    public void NativePickerAndFallbackQualification20Cycles()
    {
        // Section 23 qualification test:
        // Execute 20 activation/selection cycles verifying:
        // - multi-codepoint grapheme sequences are preserved intact
        // - no crash
        // - local catalog search and grapheme sequence resolution
        string[] testGraphemes =
        [
            "😀", "🚀", "🔥", "❤️", "⭐",
            "👍🏽", "👋🏿", "👩‍🔬", "⚠️", "📁",
            "0️⃣", "☠️", "✨", "🎉", "💡",
            "🌱", "🌊", "💎", "⚡", "☕"
        ];

        Assert.Equal(20, testGraphemes.Length);

        for (int cycle = 0; cycle < 20; cycle++)
        {
            string expectedGrapheme = testGraphemes[cycle];

            // 1. Resolve from local catalog or native input
            var searchResults = EmojiCatalog.Search(expectedGrapheme);
            Assert.NotEmpty(searchResults);
            var selected = searchResults[0];

            // 2. Stage into PersonalizationStagingModel
            var staging = new PersonalizationStagingModel();
            staging.StageEmojiEmblem(expectedGrapheme);
            Assert.Equal(EmblemKind.Emoji, staging.StagedTemplateState.Emblem.Kind);
            Assert.Equal(expectedGrapheme, staging.StagedTemplateState.Emblem.GraphemeSequence);

            // 3. Render DirectWrite BGRA surface (cycle verification)
            var renderResult = DirectWriteEmojiRenderer.RenderEmojiBgra(expectedGrapheme, 64);
            Assert.True(renderResult.IsSuccess, $"Cycle {cycle + 1} failed for '{expectedGrapheme}': {renderResult.ErrorMessage}");
            Assert.Equal(64 * 64 * 4, renderResult.Value!.Length);

            _output.WriteLine($"Qualification cycle {cycle + 1}/20: '{expectedGrapheme}' (matched {selected.Name}) - PASSED");
        }
    }
}
