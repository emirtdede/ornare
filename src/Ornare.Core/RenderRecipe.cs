using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ornare.Core;

public sealed record RenderRecipe
{
    public const int CurrentRecipeVersion = 1;

    public int RecipeVersion { get; init; } = CurrentRecipeVersion;
    public string SourceAssetHash { get; init; } = string.Empty;
    public int NormalizationPolicyVersion { get; init; } = 1;
    public int RendererVersion { get; init; } = 1;
    public string TemplateId { get; init; } = string.Empty;
    public int TemplateVersion { get; init; } = 1;
    public int PaletteAlgorithmVersion { get; init; } = 1;
    public string PaletteParameters { get; init; } = string.Empty;
    public int SvgRecolorMode { get; init; }
    public string SvgRecolorValue { get; init; } = string.Empty;
    public int SvgRecolorVersion { get; init; } = 1;
    public int PlacementMode { get; init; }
    public ImagePlacementMode ImagePlacement => (ImagePlacementMode)PlacementMode;
    public int PaddingPercent { get; init; }
    public int BackgroundMode { get; init; }
    public string BackgroundColor { get; init; } = string.Empty;
    public string PresetId { get; init; } = string.Empty;
    public int PresetVersion { get; init; } = 1;
    public string BadgeAssetId { get; init; } = string.Empty;
    public int BadgeVersion { get; init; } = 1;
    public int BadgeAnchor { get; init; }
    public int BadgeScale { get; init; }
    public string TargetFormat { get; init; } = "ico";
    public int[] TargetFrameSizes { get; init; } = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
    public int ResamplingPolicyVersion { get; init; } = 1;
    public int CompositingPolicyVersion { get; init; } = 1;
    public int ColorSpacePolicyVersion { get; init; } = 1;

    public byte[] ToCanonicalBytes() => CanonicalRecipeSerializer.Serialize(this);

    public string ComputeRenderKey() => CanonicalRecipeSerializer.ComputeRenderKey(this);
}

public static class CanonicalRecipeSerializer
{
    private static readonly byte[] MagicBytes = [0x4F, 0x52, 0x52, 0x43]; // "ORRC"

    public static byte[] Serialize(RenderRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(MagicBytes);
            writer.Write(recipe.RecipeVersion);
            writer.Write(recipe.SourceAssetHash ?? string.Empty);
            writer.Write(recipe.NormalizationPolicyVersion);
            writer.Write(recipe.RendererVersion);
            writer.Write(recipe.TemplateId ?? string.Empty);
            writer.Write(recipe.TemplateVersion);
            writer.Write(recipe.PaletteAlgorithmVersion);
            writer.Write(recipe.PaletteParameters ?? string.Empty);
            writer.Write(recipe.SvgRecolorMode);
            writer.Write(recipe.SvgRecolorValue ?? string.Empty);
            writer.Write(recipe.SvgRecolorVersion);
            writer.Write(recipe.PlacementMode);
            writer.Write(recipe.PaddingPercent);
            writer.Write(recipe.BackgroundMode);
            writer.Write(recipe.BackgroundColor ?? string.Empty);
            writer.Write(recipe.PresetId ?? string.Empty);
            writer.Write(recipe.PresetVersion);
            writer.Write(recipe.BadgeAssetId ?? string.Empty);
            writer.Write(recipe.BadgeVersion);
            writer.Write(recipe.BadgeAnchor);
            writer.Write(recipe.BadgeScale);
            writer.Write((recipe.TargetFormat ?? "ico").ToLowerInvariant());

            int[] frames = recipe.TargetFrameSizes ?? [];
            int[] sortedFrames = frames.OrderBy(x => x).ToArray();
            writer.Write(sortedFrames.Length);
            foreach (int size in sortedFrames)
            {
                writer.Write(size);
            }

            writer.Write(recipe.ResamplingPolicyVersion);
            writer.Write(recipe.CompositingPolicyVersion);
            writer.Write(recipe.ColorSpacePolicyVersion);
        }

        return ms.ToArray();
    }

    public static string ComputeRenderKey(RenderRecipe recipe)
    {
        byte[] bytes = Serialize(recipe);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
