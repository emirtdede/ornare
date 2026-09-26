using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    [JsonIgnore]
    public ImagePlacementMode ImagePlacement => (ImagePlacementMode)PlacementMode;

    private readonly int _paddingPercent;
    public int PaddingPercent
    {
        get => _paddingPercent;
        init
        {
            if (value < 0 || value > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Padding percentage must be between 0 and 30.");
            }
            _paddingPercent = value;
        }
    }

    public int BackgroundMode { get; init; }
    [JsonIgnore]
    public BackgroundMode Background => (BackgroundMode)BackgroundMode;
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

    public string ToJson() => JsonSerializer.Serialize(this);

    public static RenderRecipe? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<RenderRecipe>(json);
        }
        catch
        {
            return null;
        }
    }
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
            if (recipe.BackgroundMode == (int)Core.BackgroundMode.SolidColor && !string.IsNullOrWhiteSpace(recipe.BackgroundColor))
            {
                writer.Write(BackgroundSpec.NormalizeHex(recipe.BackgroundColor));
            }
            else
            {
                writer.Write(string.Empty);
            }
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

    public static RenderRecipe Deserialize(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 4)
        {
            throw new InvalidDataException("Invalid recipe byte stream: too short.");
        }

        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        byte[] magic = reader.ReadBytes(4);
        if (!magic.SequenceEqual(MagicBytes))
        {
            throw new InvalidDataException("Invalid recipe magic bytes.");
        }

        int recipeVersion = reader.ReadInt32();
        string sourceAssetHash = reader.ReadString();
        int normVer = reader.ReadInt32();
        int rendVer = reader.ReadInt32();
        string templateId = reader.ReadString();
        int templVer = reader.ReadInt32();
        int palAlgVer = reader.ReadInt32();
        string palParams = reader.ReadString();
        int svgRecolorMode = reader.ReadInt32();
        string svgRecolorVal = reader.ReadString();
        int svgRecolorVer = reader.ReadInt32();
        int placementMode = reader.ReadInt32();
        int paddingPercent = reader.ReadInt32();
        if (paddingPercent < 0 || paddingPercent > 30)
        {
            throw new InvalidDataException($"Invalid padding percentage in recipe: {paddingPercent}. Accepted range is 0 to 30.");
        }
        int bgMode = reader.ReadInt32();
        if (bgMode < 0 || bgMode > 1)
        {
            throw new InvalidDataException($"Invalid background mode in recipe: {bgMode}. Accepted values are 0 (Transparent) or 1 (SolidColor).");
        }
        string bgColor = reader.ReadString();
        if (bgMode == (int)Core.BackgroundMode.SolidColor)
        {
            bgColor = BackgroundSpec.NormalizeHex(bgColor);
        }
        else
        {
            bgColor = string.Empty;
        }
        string presetId = reader.ReadString();
        int presetVer = reader.ReadInt32();
        string badgeAssetId = reader.ReadString();
        int badgeVer = reader.ReadInt32();
        int badgeAnchor = reader.ReadInt32();
        int badgeScale = reader.ReadInt32();
        string targetFormat = reader.ReadString();
        int frameCount = reader.ReadInt32();
        int[] frameSizes = new int[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frameSizes[i] = reader.ReadInt32();
        }
        int resampVer = reader.ReadInt32();
        int compVer = reader.ReadInt32();
        int colorSpaceVer = reader.ReadInt32();

        return new RenderRecipe
        {
            RecipeVersion = recipeVersion,
            SourceAssetHash = sourceAssetHash,
            NormalizationPolicyVersion = normVer,
            RendererVersion = rendVer,
            TemplateId = templateId,
            TemplateVersion = templVer,
            PaletteAlgorithmVersion = palAlgVer,
            PaletteParameters = palParams,
            SvgRecolorMode = svgRecolorMode,
            SvgRecolorValue = svgRecolorVal,
            SvgRecolorVersion = svgRecolorVer,
            PlacementMode = placementMode,
            PaddingPercent = paddingPercent,
            BackgroundMode = bgMode,
            BackgroundColor = bgColor,
            PresetId = presetId,
            PresetVersion = presetVer,
            BadgeAssetId = badgeAssetId,
            BadgeVersion = badgeVer,
            BadgeAnchor = badgeAnchor,
            BadgeScale = badgeScale,
            TargetFormat = targetFormat,
            TargetFrameSizes = frameSizes,
            ResamplingPolicyVersion = resampVer,
            CompositingPolicyVersion = compVer,
            ColorSpacePolicyVersion = colorSpaceVer
        };
    }

    public static string ComputeRenderKey(RenderRecipe recipe)
    {
        byte[] bytes = Serialize(recipe);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
