using System;
using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

/// <summary>
/// Phase 4 Staged Personalization State Unit Tests.
/// Validates Mode A/B separation, staged-only editing, Apply/Discard semantics,
/// emblem mutual exclusivity, and recipe construction.
/// </summary>
public sealed class PersonalizationStagingTests
{
    [Fact]
    public void InitialStateHasCanApplyFalseWhenStagedEqualsCommitted()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(
            PersonalizationMode.FolderTemplate,
            templateState: new FolderTemplateStagingState
            {
                TemplateId = "ornare-classic",
                TemplateVersion = 2,
                FolderColorHex = "#0078D4"
            });

        Assert.Equal(PersonalizationMode.FolderTemplate, model.ActiveMode);
        Assert.False(model.CanApply, "CanApply must be false when staged state equals committed baseline.");
    }

    [Fact]
    public void StagingTemplateEnablesCanApplyAndPreservesModeB()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        model.StageFolderTemplate("ornare-aqua", 1);

        Assert.Equal(PersonalizationMode.FolderTemplate, model.ActiveMode);
        Assert.Equal("ornare-aqua", model.StagedTemplateState.TemplateId);
        Assert.Equal(1, model.StagedTemplateState.TemplateVersion);
        Assert.True(model.CanApply, "CanApply must be true after staging a new template.");
    }

    [Fact]
    public void StagingColorEnablesCanApplyAndPreservesModeB()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        model.StageFolderColor("#DC2626");

        Assert.Equal(PersonalizationMode.FolderTemplate, model.ActiveMode);
        Assert.Equal("#DC2626", model.StagedTemplateState.FolderColorHex);
        Assert.True(model.CanApply, "CanApply must be true after staging a new color.");
    }

    [Fact]
    public void StagingVectorEmblemClearsEmojiAndEnablesCanApply()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        // First stage emoji
        model.StageEmojiEmblem("🚀");
        Assert.Equal(EmblemKind.Emoji, model.StagedTemplateState.Emblem.Kind);
        Assert.Equal("🚀", model.StagedTemplateState.Emblem.GraphemeSequence);

        // Now stage vector emblem: must clear emoji
        model.StageVectorEmblem("ic_fluent_folder_24_regular", "White", scale: 1.2);
        Assert.Equal(EmblemKind.VectorIcon, model.StagedTemplateState.Emblem.Kind);
        Assert.Equal("ic_fluent_folder_24_regular", model.StagedTemplateState.Emblem.ContentId);
        Assert.Equal("White", model.StagedTemplateState.Emblem.ForegroundColor);
        Assert.Equal(1.2, model.StagedTemplateState.Emblem.Scale);
        Assert.Empty(model.StagedTemplateState.Emblem.GraphemeSequence);
        Assert.True(model.CanApply);
    }

    [Fact]
    public void StagingEmojiClearsVectorEmblem()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        model.StageVectorEmblem("ic_fluent_star_24_regular");
        Assert.Equal(EmblemKind.VectorIcon, model.StagedTemplateState.Emblem.Kind);

        // Stage emoji
        model.StageEmojiEmblem("📁");
        Assert.Equal(EmblemKind.Emoji, model.StagedTemplateState.Emblem.Kind);
        Assert.Equal("📁", model.StagedTemplateState.Emblem.GraphemeSequence);
        Assert.Empty(model.StagedTemplateState.Emblem.ContentId);
        Assert.True(model.CanApply);
    }

    [Fact]
    public void RemoveEmblemSetsKindNone()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        model.StageVectorEmblem("ic_fluent_code_24_regular");
        Assert.True(model.StagedTemplateState.Emblem.HasEmblem);

        model.RemoveEmblem();
        Assert.Equal(EmblemKind.None, model.StagedTemplateState.Emblem.Kind);
        Assert.False(model.StagedTemplateState.Emblem.HasEmblem);
    }

    [Fact]
    public void StagingCustomImageSwitchesToModeA()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        byte[] fakeImage = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        model.StageCustomImage(fakeImage, "C:\\test\\photo.png", "fake_hash_123");

        Assert.Equal(PersonalizationMode.CustomImage, model.ActiveMode);
        Assert.True(model.StagedImageState.HasContent);
        Assert.Equal("fake_hash_123", model.StagedImageState.SourceAssetHash);
        Assert.True(model.CanApply);
    }

    [Fact]
    public void ModeSeparationRetainsMemoryInBothModes()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        // Modify Mode B
        model.StageFolderTemplate("ornare-grove", 1);
        model.StageFolderColor("#10B981");

        // Switch to Mode A and stage image
        byte[] fakeImage = [0x01, 0x02, 0x03];
        model.StageCustomImage(fakeImage, "photo.png", "img_hash");

        Assert.Equal(PersonalizationMode.CustomImage, model.ActiveMode);

        // Switch back to Mode B without discarding: Mode B staged state is preserved
        model.SwitchMode(PersonalizationMode.FolderTemplate);
        Assert.Equal(PersonalizationMode.FolderTemplate, model.ActiveMode);
        Assert.Equal("ornare-grove", model.StagedTemplateState.TemplateId);
        Assert.Equal("#10B981", model.StagedTemplateState.FolderColorHex);

        // Mode A staged state is also preserved
        Assert.Equal("img_hash", model.StagedImageState.SourceAssetHash);
    }

    [Fact]
    public void DiscardRevertsAllStagedChangesToCommittedBaseline()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(
            PersonalizationMode.FolderTemplate,
            templateState: new FolderTemplateStagingState
            {
                TemplateId = "ornare-classic",
                TemplateVersion = 2,
                FolderColorHex = "#0078D4"
            });

        // Stage various changes
        model.StageFolderTemplate("ornare-glass", 1);
        model.StageFolderColor("#F59E0B");
        model.StageVectorEmblem("ic_star");
        Assert.True(model.CanApply);

        // Discard
        model.Discard();

        Assert.Equal(PersonalizationMode.FolderTemplate, model.ActiveMode);
        Assert.Equal("ornare-classic", model.StagedTemplateState.TemplateId);
        Assert.Equal(2, model.StagedTemplateState.TemplateVersion);
        Assert.Equal("#0078D4", model.StagedTemplateState.FolderColorHex);
        Assert.Equal(EmblemKind.None, model.StagedTemplateState.Emblem.Kind);
        Assert.False(model.CanApply, "CanApply must be false after Discard.");
    }

    [Fact]
    public void CommitUpdatesBaselineAndSetsCanApplyFalse()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        model.StageFolderTemplate("ornare-flow", 1);
        model.StageFolderColor("#6366F1");
        Assert.True(model.CanApply);

        model.Commit();

        Assert.False(model.CanApply, "CanApply must be false immediately after Commit.");
        Assert.Equal("ornare-flow", model.CommittedTemplateState.TemplateId);
        Assert.Equal("#6366F1", model.CommittedTemplateState.FolderColorHex);
    }

    [Fact]
    public void BuildRenderRecipeProducesAccurateRecipeForBothModes()
    {
        var model = new PersonalizationStagingModel();
        model.Initialize(PersonalizationMode.FolderTemplate);

        // Mode B recipe
        model.StageFolderTemplate("ornare-air", 1);
        model.StageFolderColor("#0284C7");
        model.StageVectorEmblem("ic_cloud", "White", scale: 1.1);

        var recipeB = model.BuildRenderRecipe("test_hash");
        Assert.Equal("ornare-air", recipeB.TemplateId);
        Assert.Equal(1, recipeB.TemplateVersion);
        Assert.Equal("#0284C7", recipeB.SvgRecolorValue);
        Assert.Equal((int)EmblemKind.VectorIcon, recipeB.EmblemKind);
        Assert.Equal("ic_cloud", recipeB.EmblemContentId);
        Assert.Equal("White", recipeB.EmblemForegroundColor);
        Assert.Equal(1.1, recipeB.EmblemScale);

        // Mode A recipe
        byte[] fakeImage = [1, 2, 3];
        model.StageCustomImage(fakeImage, "pic.png", "img_hash_456");
        model.StagePlacement(ImagePlacementMode.Cover, paddingPercent: 5, BackgroundMode.SolidColor, "#000000");

        var recipeA = model.BuildRenderRecipe();
        Assert.Equal("img_hash_456", recipeA.SourceAssetHash);
        Assert.Equal(ImagePlacementMode.Cover, recipeA.ImagePlacement);
        Assert.Equal(5, recipeA.PaddingPercent);
        Assert.Equal(BackgroundMode.SolidColor, recipeA.Background);
        Assert.Equal("#000000", recipeA.BackgroundColor);
    }
}
