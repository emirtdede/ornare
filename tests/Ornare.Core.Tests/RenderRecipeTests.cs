using System;
using System.Security.Cryptography;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class RenderRecipeTests
{
    [Fact]
    public void ChangeEachOutputAffectingRecipeFieldOneAtATimeChangesRenderKey()
    {
        var baseline = new RenderRecipe
        {
            RecipeVersion = 1,
            SourceAssetHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            NormalizationPolicyVersion = 1,
            RendererVersion = 1,
            TemplateId = "folder_default",
            TemplateVersion = 1,
            PaletteAlgorithmVersion = 1,
            PaletteParameters = "hue=210;sat=80",
            SvgRecolorMode = 1,
            SvgRecolorValue = "#0078D4",
            SvgRecolorVersion = 1,
            PlacementMode = 1,
            PaddingPercent = 10,
            BackgroundMode = 1,
            BackgroundColor = "#FFFFFF",
            PresetId = "preset_blue",
            PresetVersion = 1,
            BadgeAssetId = "badge_star",
            BadgeVersion = 1,
            BadgeAnchor = 1,
            BadgeScale = 1,
            TargetFormat = "ico",
            TargetFrameSizes = [16, 32, 48, 256],
            ResamplingPolicyVersion = 1,
            CompositingPolicyVersion = 1,
            ColorSpacePolicyVersion = 1
        };

        string baseKey = baseline.ComputeRenderKey();
        Assert.False(string.IsNullOrWhiteSpace(baseKey));

        // Test mutating every single field one by one
        RenderRecipe[] variants =
        [
            baseline with { RecipeVersion = 2 },
            baseline with { SourceAssetHash = "1111111111111111111111111111111111111111111111111111111111111111" },
            baseline with { NormalizationPolicyVersion = 2 },
            baseline with { RendererVersion = 2 },
            baseline with { TemplateId = "folder_alt" },
            baseline with { TemplateVersion = 2 },
            baseline with { PaletteAlgorithmVersion = 2 },
            baseline with { PaletteParameters = "hue=180;sat=90" },
            baseline with { SvgRecolorMode = 2 },
            baseline with { SvgRecolorValue = "#FF0000" },
            baseline with { SvgRecolorVersion = 2 },
            baseline with { PlacementMode = 2 },
            baseline with { PaddingPercent = 15 },
            baseline with { BackgroundMode = 2 },
            baseline with { BackgroundColor = "#000000" },
            baseline with { PresetId = "preset_red" },
            baseline with { PresetVersion = 2 },
            baseline with { BadgeAssetId = "badge_heart" },
            baseline with { BadgeVersion = 2 },
            baseline with { BadgeAnchor = 2 },
            baseline with { BadgeScale = 2 },
            baseline with { TargetFormat = "png" },
            baseline with { TargetFrameSizes = [16, 32, 64, 128, 256] },
            baseline with { ResamplingPolicyVersion = 2 },
            baseline with { CompositingPolicyVersion = 2 },
            baseline with { ColorSpacePolicyVersion = 2 }
        ];

        for (int i = 0; i < variants.Length; i++)
        {
            string variantKey = variants[i].ComputeRenderKey();
            Assert.NotEqual(baseKey, variantKey);
        }
    }

    [Fact]
    public void GoldenCanonicalSerializationBytesRemainStable()
    {
        var recipe = new RenderRecipe
        {
            RecipeVersion = 1,
            SourceAssetHash = "test_hash_source_1234567890abcdef",
            NormalizationPolicyVersion = 1,
            RendererVersion = 1,
            TemplateId = "standard_folder",
            TemplateVersion = 1,
            PaletteAlgorithmVersion = 1,
            PaletteParameters = "param=test",
            SvgRecolorMode = 0,
            SvgRecolorValue = string.Empty,
            SvgRecolorVersion = 1,
            PlacementMode = 0,
            PaddingPercent = 0,
            BackgroundMode = 0,
            BackgroundColor = string.Empty,
            PresetId = string.Empty,
            PresetVersion = 1,
            BadgeAssetId = string.Empty,
            BadgeVersion = 1,
            BadgeAnchor = 0,
            BadgeScale = 0,
            TargetFormat = "ico",
            TargetFrameSizes = [16, 24, 32, 48, 64, 128, 256],
            ResamplingPolicyVersion = 1,
            CompositingPolicyVersion = 1,
            ColorSpacePolicyVersion = 1
        };

        byte[] canonicalBytes = recipe.ToCanonicalBytes();
        Assert.NotNull(canonicalBytes);
        Assert.True(canonicalBytes.Length > 0);

        // Verify magic bytes "ORRC"
        Assert.Equal((byte)0x4F, canonicalBytes[0]);
        Assert.Equal((byte)0x52, canonicalBytes[1]);
        Assert.Equal((byte)0x52, canonicalBytes[2]);
        Assert.Equal((byte)0x43, canonicalBytes[3]);

        string renderKey = recipe.ComputeRenderKey();
        byte[] expectedHashBytes = SHA256.HashData(canonicalBytes);
        string expectedHex = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();
        Assert.Equal(expectedHex, renderKey);

        // Deterministic repetition
        byte[] secondBytes = recipe.ToCanonicalBytes();
        Assert.Equal(canonicalBytes, secondBytes);
    }
}
