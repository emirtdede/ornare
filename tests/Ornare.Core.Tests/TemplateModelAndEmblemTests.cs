using System;
using System.Text.Json;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public sealed class TemplateModelAndEmblemTests
{
    [Fact]
    public void NormalizedRectValidCoordinatesPropertiesSetCorrectly()
    {
        var rect = new NormalizedRect(0.1, 0.2, 0.5, 0.6);
        Assert.Equal(0.1, rect.X);
        Assert.Equal(0.2, rect.Y);
        Assert.Equal(0.5, rect.Width);
        Assert.Equal(0.6, rect.Height);
    }

    [Theory]
    [InlineData(-0.1, 0.2, 0.5, 0.5)]
    [InlineData(0.1, -0.2, 0.5, 0.5)]
    [InlineData(0.1, 0.2, 0.0, 0.5)]
    [InlineData(0.1, 0.2, 0.5, 0.0)]
    [InlineData(0.8, 0.2, 0.5, 0.5)] // X + Width > 1.0
    [InlineData(0.2, 0.8, 0.5, 0.5)] // Y + Height > 1.0
    public void NormalizedRectInvalidCoordinatesThrowsArgumentException(double x, double y, double w, double h)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NormalizedRect(x, y, w, h));
    }

    [Fact]
    public void NormalizedRectIntersectsDetectsOverlapCorrectly()
    {
        var centerSafeZone = new NormalizedRect(0.25, 0.35, 0.50, 0.50);
        var cornerBadgeZone = new NormalizedRect(0.60, 0.60, 0.35, 0.35); // Bottom-right
        var topTabZone = new NormalizedRect(0.10, 0.10, 0.40, 0.20); // Top-left tab

        Assert.True(centerSafeZone.Intersects(cornerBadgeZone));
        Assert.False(cornerBadgeZone.Intersects(topTabZone));
    }

    [Fact]
    public void NormalizedRectContainsDetectsPointInside()
    {
        var rect = new NormalizedRect(0.2, 0.2, 0.6, 0.6);
        Assert.True(rect.Contains(0.5, 0.5));
        Assert.True(rect.Contains(0.2, 0.2));
        Assert.False(rect.Contains(0.1, 0.5));
        Assert.False(rect.Contains(0.9, 0.9));
    }

    [Fact]
    public void FolderEmblemRecipeNoneHasNoEmblem()
    {
        var recipe = FolderEmblemRecipe.None;
        Assert.Equal(EmblemKind.None, recipe.Kind);
        Assert.False(recipe.HasEmblem);
        Assert.Empty(recipe.ContentId);
        Assert.Empty(recipe.GraphemeSequence);
    }

    [Fact]
    public void FolderEmblemRecipeCreateVectorIconSetsFieldsAndClearsEmoji()
    {
        var iconRecipe = FolderEmblemRecipe.CreateVectorIcon("tabler:code", "#FFFFFF", scale: 1.2, offsetX: 0.05, offsetY: -0.05);

        Assert.Equal(EmblemKind.VectorIcon, iconRecipe.Kind);
        Assert.True(iconRecipe.HasEmblem);
        Assert.Equal("tabler:code", iconRecipe.ContentId);
        Assert.Empty(iconRecipe.GraphemeSequence);
        Assert.Equal("#FFFFFF", iconRecipe.ForegroundColor);
        Assert.Equal(1.2, iconRecipe.Scale);
        Assert.Equal(0.05, iconRecipe.OffsetX);
        Assert.Equal(-0.05, iconRecipe.OffsetY);
    }

    [Fact]
    public void FolderEmblemRecipeCreateEmojiPreservesMultiCodepointGraphemeAndClearsVector()
    {
        // Family emoji: 👨‍👩‍👧‍👦 (multiple codepoints joined by ZWJ)
        string familyEmoji = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";

        var emojiRecipe = FolderEmblemRecipe.CreateEmoji(familyEmoji, scale: 0.9);

        Assert.Equal(EmblemKind.Emoji, emojiRecipe.Kind);
        Assert.True(emojiRecipe.HasEmblem);
        Assert.Equal(familyEmoji, emojiRecipe.GraphemeSequence);
        Assert.Empty(emojiRecipe.ContentId);
        Assert.Equal(0.9, emojiRecipe.Scale);
    }

    [Fact]
    public void RenderRecipeWithEmblemsProducesDistinctRenderKeys()
    {
        var baseRecipe = new RenderRecipe
        {
            TemplateId = "ornare-classic",
            TemplateVersion = 2,
            SvgRecolorValue = "#0078D4",
            PaletteParameters = "#0078D4"
        };

        var vectorRecipe = baseRecipe with
        {
            EmblemKind = (int)EmblemKind.VectorIcon,
            EmblemContentId = "folder-icon",
            EmblemForegroundColor = "#FFFFFF"
        };

        var emojiRecipe = baseRecipe with
        {
            EmblemKind = (int)EmblemKind.Emoji,
            EmblemGraphemeSequence = "\U0001F680" // Rocket emoji
        };

        string keyBase = baseRecipe.ComputeRenderKey();
        string keyVector = vectorRecipe.ComputeRenderKey();
        string keyEmoji = emojiRecipe.ComputeRenderKey();

        Assert.NotEqual(keyBase, keyVector);
        Assert.NotEqual(keyBase, keyEmoji);
        Assert.NotEqual(keyVector, keyEmoji);
    }

    [Fact]
    public void RenderRecipeCanonicalSerializationRoundTripsEmblemFields()
    {
        var original = new RenderRecipe
        {
            TemplateId = "ornare-aqua",
            TemplateVersion = 1,
            PaletteParameters = "#2563EB",
            EmblemKind = (int)EmblemKind.VectorIcon,
            EmblemContentId = "tabler:star",
            EmblemForegroundColor = "#FFD700",
            EmblemScale = 1.1,
            EmblemOffsetX = 0.02,
            EmblemOffsetY = -0.01,
            RenderPolicyVersion = 1
        };

        byte[] bytes = original.ToCanonicalBytes();
        Assert.NotNull(bytes);

        var deserialized = CanonicalRecipeSerializer.Deserialize(bytes);

        Assert.Equal(original.RecipeVersion, deserialized.RecipeVersion);
        Assert.Equal(original.TemplateId, deserialized.TemplateId);
        Assert.Equal(original.EmblemKind, deserialized.EmblemKind);
        Assert.Equal(original.EmblemContentId, deserialized.EmblemContentId);
        Assert.Equal(original.EmblemForegroundColor, deserialized.EmblemForegroundColor);
        Assert.Equal(original.EmblemScale, deserialized.EmblemScale);
        Assert.Equal(original.EmblemOffsetX, deserialized.EmblemOffsetX);
        Assert.Equal(original.EmblemOffsetY, deserialized.EmblemOffsetY);
        Assert.Equal(original.RenderPolicyVersion, deserialized.RenderPolicyVersion);
    }

    [Fact]
    public void SemanticSvgRolesParseNormativeAndLegacyRoles()
    {
        Assert.Equal(SemanticSvgRole.FolderFront, SemanticSvgRoleExtensions.ParseRole("folder-front"));
        Assert.Equal(SemanticSvgRole.FolderFront, SemanticSvgRoleExtensions.ParseRole("base"));
        Assert.Equal(SemanticSvgRole.FolderBack, SemanticSvgRoleExtensions.ParseRole("folder-back"));
        Assert.Equal(SemanticSvgRole.FolderBack, SemanticSvgRoleExtensions.ParseRole("deep"));
        Assert.Equal(SemanticSvgRole.FolderTab, SemanticSvgRoleExtensions.ParseRole("folder-tab"));
        Assert.Equal(SemanticSvgRole.Highlight, SemanticSvgRoleExtensions.ParseRole("highlight"));
        Assert.Equal(SemanticSvgRole.Shadow, SemanticSvgRoleExtensions.ParseRole("shadow"));
        Assert.Equal(SemanticSvgRole.Edge, SemanticSvgRoleExtensions.ParseRole("edge"));
        Assert.Equal(SemanticSvgRole.Edge, SemanticSvgRoleExtensions.ParseRole("light"));
        Assert.Equal(SemanticSvgRole.Detail, SemanticSvgRoleExtensions.ParseRole("detail"));
    }

    [Fact]
    public void SemanticColorPaletteMapsNormativeRolesCorrectly()
    {
        var palette = SemanticColorPalette.FromBaseColor("#2563EB");

        Assert.Equal(palette.BaseColor, palette.GetColor(SemanticSvgRole.FolderFront));
        Assert.Equal(palette.DeepColor, palette.GetColor(SemanticSvgRole.FolderBack));
        Assert.Equal(palette.DeepColor, palette.GetColor(SemanticSvgRole.FolderTab));
        Assert.Equal(palette.HighlightColor, palette.GetColor(SemanticSvgRole.Highlight));
        Assert.Equal(palette.ShadowColor, palette.GetColor(SemanticSvgRole.Shadow));
        Assert.Equal(palette.LightColor, palette.GetColor(SemanticSvgRole.Edge));
    }

    [Fact]
    public void FolderTemplateManifestNativeAotJsonSerializationSucceeds()
    {
        var manifest = new FolderTemplateManifest
        {
            Version = 1,
            Templates =
            [
                new FolderTemplateManifestEntry
                {
                    Id = "ornare-classic",
                    DisplayNameResourceKey = "Template_Classic",
                    Version = 2,
                    SvgAssetPath = "assets/folders/ornare-classic.svg",
                    SortOrder = 1,
                    SupportsColor = true,
                    SupportsEmblem = true,
                    ViewBox = "0 0 256 256",
                    EmblemSafeZone = new NormalizedRect(0.25, 0.35, 0.50, 0.50),
                    ReservedBadgeZones = [new NormalizedRect(0.60, 0.60, 0.35, 0.35)],
                    DefaultPalettePolicy = "Oklab5Tone",
                    RenderPolicyVersion = 1
                }
            ]
        };

        string json = JsonSerializer.Serialize(manifest, FolderJsonContext.Default.FolderTemplateManifest);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var deserialized = JsonSerializer.Deserialize(json, FolderJsonContext.Default.FolderTemplateManifest);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized.Templates);
        Assert.Equal("ornare-classic", deserialized.Templates[0].Id);
        Assert.Equal(2, deserialized.Templates[0].Version);
        Assert.Equal(0.25, deserialized.Templates[0].EmblemSafeZone.X);
    }
}
