using System;
using System.IO;

namespace Ornare.Core;

/// <summary>
/// Identifies the active content mode in the Ornare Quick Panel (Section 3).
/// Mode A: Custom Image Icon (user drops/pastes/picks image).
/// Mode B: Folder Personalization (template + color + center emblem).
/// </summary>
public enum PersonalizationMode
{
    CustomImage = 0,
    FolderTemplate = 1
}

/// <summary>
/// Staged state for Mode A (Custom Image Icon).
/// </summary>
public sealed record CustomImageStagingState
{
    public byte[]? ImageBytes { get; init; }
    public string? SourceAssetPath { get; init; }
    public string? SourceAssetHash { get; init; }
    public ImagePlacementMode PlacementMode { get; init; } = ImagePlacementMode.Contain;
    public int PaddingPercent { get; init; }
    public BackgroundMode BackgroundMode { get; init; } = BackgroundMode.Transparent;
    public string BackgroundColorHex { get; init; } = "#FFFFFF";

    public bool HasContent => (ImageBytes != null && ImageBytes.Length > 0) || !string.IsNullOrWhiteSpace(SourceAssetPath);

    public bool ValueEquals(CustomImageStagingState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(SourceAssetHash, other.SourceAssetHash, StringComparison.Ordinal) &&
               string.Equals(SourceAssetPath, other.SourceAssetPath, StringComparison.OrdinalIgnoreCase) &&
               PlacementMode == other.PlacementMode &&
               PaddingPercent == other.PaddingPercent &&
               BackgroundMode == other.BackgroundMode &&
               string.Equals(BackgroundColorHex, other.BackgroundColorHex, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Staged state for Mode B (Folder Personalization: template + color + optional center emblem).
/// </summary>
public sealed record FolderTemplateStagingState
{
    public string TemplateId { get; init; } = "ornare-classic";
    public int TemplateVersion { get; init; } = 2;
    public string FolderColorHex { get; init; } = "#0078D4";
    public FolderEmblemRecipe Emblem { get; init; } = FolderEmblemRecipe.None;
    public string? StatusPresetId { get; init; }
    public string? PriorityPresetId { get; init; }

    public bool ValueEquals(FolderTemplateStagingState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(TemplateId, other.TemplateId, StringComparison.OrdinalIgnoreCase) &&
               TemplateVersion == other.TemplateVersion &&
               string.Equals(FolderColorHex, other.FolderColorHex, StringComparison.OrdinalIgnoreCase) &&
               Emblem == other.Emblem &&
               string.Equals(StatusPresetId, other.StatusPresetId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(PriorityPresetId, other.PriorityPresetId, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Authoritative staging state machine for Quick Panel personalization (Section 3 &amp; Section 5).
/// Enforces strict separation between Mode A and Mode B, preview-only staging,
/// explicit commit/discard semantics, and recipe equality.
/// </summary>
public sealed class PersonalizationStagingModel
{
    public PersonalizationMode ActiveMode { get; private set; } = PersonalizationMode.FolderTemplate;
    public CustomImageStagingState StagedImageState { get; private set; } = new();
    public FolderTemplateStagingState StagedTemplateState { get; private set; } = new();

    public PersonalizationMode CommittedMode { get; private set; } = PersonalizationMode.FolderTemplate;
    public CustomImageStagingState CommittedImageState { get; private set; } = new();
    public FolderTemplateStagingState CommittedTemplateState { get; private set; } = new();

    /// <summary>
    /// Initializes the model with baseline committed values.
    /// </summary>
    public void Initialize(
        PersonalizationMode mode,
        CustomImageStagingState? imageState = null,
        FolderTemplateStagingState? templateState = null)
    {
        CommittedMode = mode;
        CommittedImageState = imageState ?? new CustomImageStagingState();
        CommittedTemplateState = templateState ?? new FolderTemplateStagingState();

        ActiveMode = CommittedMode;
        StagedImageState = CommittedImageState;
        StagedTemplateState = CommittedTemplateState;
    }

    /// <summary>
    /// Explicitly switches content mode without discarding staged settings in either mode.
    /// </summary>
    public void SwitchMode(PersonalizationMode mode)
    {
        ActiveMode = mode;
    }

    /// <summary>
    /// Stages a custom image and activates Mode A (Section 3).
    /// </summary>
    public void StageCustomImage(byte[] imageBytes, string? assetPath = null, string? assetHash = null)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        StagedImageState = StagedImageState with
        {
            ImageBytes = imageBytes,
            SourceAssetPath = assetPath,
            SourceAssetHash = assetHash
        };
        ActiveMode = PersonalizationMode.CustomImage;
    }

    /// <summary>
    /// Stages custom image placement parameters (Mode A).
    /// </summary>
    public void StagePlacement(
        ImagePlacementMode mode,
        int paddingPercent,
        BackgroundMode bgMode,
        string bgHex)
    {
        StagedImageState = StagedImageState with
        {
            PlacementMode = mode,
            PaddingPercent = Math.Clamp(paddingPercent, 0, 30),
            BackgroundMode = bgMode,
            BackgroundColorHex = string.IsNullOrWhiteSpace(bgHex) ? "#FFFFFF" : bgHex
        };
        ActiveMode = PersonalizationMode.CustomImage;
    }

    /// <summary>
    /// Stages a folder style template and activates Mode B (Section 3).
    /// </summary>
    public void StageFolderTemplate(string templateId, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        StagedTemplateState = StagedTemplateState with
        {
            TemplateId = templateId.Trim(),
            TemplateVersion = version
        };
        ActiveMode = PersonalizationMode.FolderTemplate;
    }

    /// <summary>
    /// Stages a folder color and activates Mode B (Section 3).
    /// </summary>
    public void StageFolderColor(string colorHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(colorHex);

        StagedTemplateState = StagedTemplateState with
        {
            FolderColorHex = colorHex.Trim()
        };
        ActiveMode = PersonalizationMode.FolderTemplate;
    }

    /// <summary>
    /// Stages a vector emblem, clearing any existing emoji state, and activates Mode B (Section 16).
    /// </summary>
    public void StageVectorEmblem(
        string contentId,
        string foregroundColor = "Auto",
        double scale = 1.0,
        double offsetX = 0.0,
        double offsetY = 0.0)
    {
        var emblem = FolderEmblemRecipe.CreateVectorIcon(contentId, foregroundColor, scale, offsetX, offsetY);
        StagedTemplateState = StagedTemplateState with { Emblem = emblem };
        ActiveMode = PersonalizationMode.FolderTemplate;
    }

    /// <summary>
    /// Stages an emoji emblem, clearing any existing vector icon state, and activates Mode B (Section 16 &amp; Section 22).
    /// </summary>
    public void StageEmojiEmblem(
        string graphemeSequence,
        double scale = 1.0,
        double offsetX = 0.0,
        double offsetY = 0.0)
    {
        var emblem = FolderEmblemRecipe.CreateEmoji(graphemeSequence, scale, offsetX, offsetY);
        StagedTemplateState = StagedTemplateState with { Emblem = emblem };
        ActiveMode = PersonalizationMode.FolderTemplate;
    }

    /// <summary>
    /// Clears the center emblem, returning to Kind=None (Section 16).
    /// </summary>
    public void RemoveEmblem()
    {
        StagedTemplateState = StagedTemplateState with { Emblem = FolderEmblemRecipe.None };
        ActiveMode = PersonalizationMode.FolderTemplate;
    }

    /// <summary>
    /// Stages optional status / priority badge overlays (F04/F05).
    /// </summary>
    public void StageBadges(string? statusPresetId, string? priorityPresetId)
    {
        StagedTemplateState = StagedTemplateState with
        {
            StatusPresetId = statusPresetId,
            PriorityPresetId = priorityPresetId
        };
        ActiveMode = PersonalizationMode.FolderTemplate;
    }

    /// <summary>
    /// Indicates whether a valid change has been staged that differs from the committed baseline (Section 5).
    /// The Apply button must be enabled if and only if this property is true.
    /// </summary>
    public bool CanApply
    {
        get
        {
            if (ActiveMode != CommittedMode)
            {
                if (ActiveMode == PersonalizationMode.CustomImage)
                {
                    return StagedImageState.HasContent;
                }
                return true;
            }

            if (ActiveMode == PersonalizationMode.CustomImage)
            {
                return StagedImageState.HasContent && !StagedImageState.ValueEquals(CommittedImageState);
            }

            return !StagedTemplateState.ValueEquals(CommittedTemplateState);
        }
    }

    /// <summary>
    /// Discards all unapplied staged changes, resetting staged state to the committed baseline (Section 5).
    /// </summary>
    public void Discard()
    {
        ActiveMode = CommittedMode;
        StagedImageState = CommittedImageState;
        StagedTemplateState = CommittedTemplateState;
    }

    /// <summary>
    /// Commits staged state as the new baseline after a successful durable filesystem apply (Section 5).
    /// </summary>
    public void Commit()
    {
        CommittedMode = ActiveMode;
        CommittedImageState = StagedImageState;
        CommittedTemplateState = StagedTemplateState;
    }

    /// <summary>
    /// Builds the deterministic RenderRecipe for the currently active staged state (Section 34).
    /// </summary>
    public RenderRecipe BuildRenderRecipe(string? sourceAssetHashOverride = null)
    {
        if (ActiveMode == PersonalizationMode.CustomImage)
        {
            string hash = sourceAssetHashOverride ?? StagedImageState.SourceAssetHash ?? string.Empty;
            return new RenderRecipe
            {
                SourceAssetHash = hash,
                PlacementMode = (int)StagedImageState.PlacementMode,
                PaddingPercent = StagedImageState.PaddingPercent,
                BackgroundMode = (int)StagedImageState.BackgroundMode,
                BackgroundColor = StagedImageState.BackgroundColorHex,
                TargetFormat = "ico",
                TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
            };
        }

        string normalizedHex = StagedTemplateState.FolderColorHex.StartsWith('#')
            ? StagedTemplateState.FolderColorHex.ToUpperInvariant()
            : "#" + StagedTemplateState.FolderColorHex.ToUpperInvariant();

        return new RenderRecipe
        {
            SourceAssetHash = sourceAssetHashOverride ?? string.Empty,
            TemplateId = StagedTemplateState.TemplateId,
            TemplateVersion = StagedTemplateState.TemplateVersion,
            PaletteAlgorithmVersion = 1,
            PaletteParameters = normalizedHex,
            SvgRecolorMode = 1, // SemanticTemplate
            SvgRecolorValue = normalizedHex,
            SvgRecolorVersion = 1,
            EmblemKind = (int)StagedTemplateState.Emblem.Kind,
            EmblemContentId = StagedTemplateState.Emblem.ContentId,
            EmblemGraphemeSequence = StagedTemplateState.Emblem.GraphemeSequence,
            EmblemForegroundColor = StagedTemplateState.Emblem.ForegroundColor,
            EmblemScale = StagedTemplateState.Emblem.Scale,
            EmblemOffsetX = StagedTemplateState.Emblem.OffsetX,
            EmblemOffsetY = StagedTemplateState.Emblem.OffsetY,
            PresetId = StagedTemplateState.StatusPresetId ?? string.Empty,
            TargetFormat = "ico",
            TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
        };
    }
}
