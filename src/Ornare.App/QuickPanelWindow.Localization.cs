namespace Ornare.App;

using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// Localized copy for the quick panel.
/// <para>
/// This is one flat list of assignments with no window behaviour, kept out of the window class
/// so the class itself reads as flow control. Every entry here exists because the corresponding
/// XAML literal is an English design-time fallback that must never reach a non-English user
/// (ENH-2).
/// </para>
/// </summary>
public sealed partial class QuickPanelWindow
{
    /// <summary>
    /// Follows a language change made elsewhere in the app. Locale changes are raised from the
    /// UI thread, but the dispatcher check keeps this safe if a future caller raises it from a
    /// worker while a settings write completes.
    /// </summary>
    private void OnAppLanguageChanged(object? sender, string locale)
    {
        if (_disposed)
        {
            return;
        }

        if (DispatcherQueue is not null && !DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => OnAppLanguageChanged(sender, locale));
            return;
        }

        _currentLanguage = AppResources.NormalizeLocale(locale);

        // Repaint the chrome and the transient status line in the new language, then re-render
        // the preview so its cached key (which is language independent) is still valid but the
        // surrounding labels are not stale.
        UpdateLocalizedChrome();
        RefreshStatusInfoText();
    }

    private void RefreshStatusInfoText()
    {
        if (StatusInfoText is null)
        {
            return;
        }

        if (_stagingModel.CanApply)
        {
            StatusInfoText.Text = AppResources.GetString("Status_UnappliedChanges", _currentLanguage);
        }
        else
        {
            StatusInfoText.Text = _currentBaseline != null
                ? AppResources.GetString("Status_CurrentIcon", _currentLanguage)
                : AppResources.GetString("Status_ReadyToApply", _currentLanguage);
        }
    }

    private void UpdateLocalizedChrome()
    {
        ToolTipService.SetToolTip(ResetButton, AppResources.GetString("Reset", _currentLanguage));
        AutomationProperties.SetName(ResetButton, AppResources.GetString("Reset", _currentLanguage));
        ToolTipService.SetToolTip(OpenManagerButton, AppResources.GetString("Settings", _currentLanguage));
        AutomationProperties.SetName(OpenManagerButton, AppResources.GetString("Settings", _currentLanguage));

        ToolTipService.SetToolTip(FolderActionButton, $"{AppResources.GetString("Template_Title", _currentLanguage)} (Alt+1)");
        AutomationProperties.SetName(FolderActionButton, AppResources.GetString("Template_Title", _currentLanguage));

        ToolTipService.SetToolTip(IconActionButton, $"{AppResources.GetString("Icon_Title", _currentLanguage)} (Alt+2)");
        AutomationProperties.SetName(IconActionButton, AppResources.GetString("Icon_Title", _currentLanguage));

        ToolTipService.SetToolTip(EmojiActionButton, $"{AppResources.GetString("Emoji_Title", _currentLanguage)} (Alt+3)");
        AutomationProperties.SetName(EmojiActionButton, AppResources.GetString("Emoji_Title", _currentLanguage));

        ToolTipService.SetToolTip(ImageActionButton, AppResources.GetString("Action_Image", _currentLanguage));
        AutomationProperties.SetName(ImageActionButton, AppResources.GetString("Action_Image", _currentLanguage));

        ToolTipService.SetToolTip(ColorActionButton, AppResources.GetString("Action_Color", _currentLanguage));
        AutomationProperties.SetName(ColorActionButton, AppResources.GetString("Action_Color", _currentLanguage));

        ToolTipService.SetToolTip(MoreActionButton, AppResources.GetString("Action_More", _currentLanguage));
        AutomationProperties.SetName(MoreActionButton, AppResources.GetString("Action_More", _currentLanguage));

        ApplyConfirmButton.Content = AppResources.GetString("Action_Apply", _currentLanguage);
        ToolTipService.SetToolTip(ApplyConfirmButton, AppResources.GetString("Apply_Icon_ToolTip", _currentLanguage));
        AutomationProperties.SetName(ApplyConfirmButton, AppResources.GetString("Action_Apply", _currentLanguage));

        // Accessibility names are user-facing copy too: screen readers announce them
        // verbatim, so they must follow the selected locale like any visible label.
        AutomationProperties.SetName(DropAreaBorder, AppResources.GetString("A11y_DropArea", _currentLanguage));
        AutomationProperties.SetName(PaddingSlider, AppResources.GetString("A11y_PaddingSlider", _currentLanguage));
        AutomationProperties.SetName(IconSearchTextBox, AppResources.GetString("A11y_SearchVectorIcons", _currentLanguage));
        AutomationProperties.SetName(IconCategoryComboBox, AppResources.GetString("A11y_FilterCategory", _currentLanguage));
        AutomationProperties.SetName(IconGridView, AppResources.GetString("A11y_VectorGrid", _currentLanguage));
        AutomationProperties.SetName(EmojiSearchTextBox, AppResources.GetString("A11y_SearchEmoji", _currentLanguage));
        AutomationProperties.SetName(EmojiCategoryComboBox, AppResources.GetString("A11y_FilterEmojiCategory", _currentLanguage));
        AutomationProperties.SetName(EmojiGridView, AppResources.GetString("A11y_EmojiGrid", _currentLanguage));
        AutomationProperties.SetName(ImageActionButton, AppResources.GetString("Action_CustomImage", _currentLanguage));
        AutomationProperties.SetName(ColorActionButton, AppResources.GetString("Action_FolderColor", _currentLanguage));
        if (EmblemColorWhiteButton != null) AutomationProperties.SetName(EmblemColorWhiteButton, AppResources.GetString("A11y_EmblemWhite", _currentLanguage));
        if (EmblemColorBlackButton != null) AutomationProperties.SetName(EmblemColorBlackButton, AppResources.GetString("A11y_EmblemBlack", _currentLanguage));
        if (EmblemColorAutoButton != null)
        {
            EmblemColorAutoButton.Content = AppResources.GetString("Color_Auto", _currentLanguage);
            AutomationProperties.SetName(EmblemColorAutoButton, AppResources.GetString("EmblemColorAutoAutomation", _currentLanguage));
            ToolTipService.SetToolTip(EmblemColorAutoButton, AppResources.GetString("Tip_AutoContrast", _currentLanguage));
        }
        if (EmblemColorWhiteButton != null) ToolTipService.SetToolTip(EmblemColorWhiteButton, AppResources.GetString("Tip_White", _currentLanguage));
        if (EmblemColorBlackButton != null) ToolTipService.SetToolTip(EmblemColorBlackButton, AppResources.GetString("Tip_Black", _currentLanguage));
        ToolTipService.SetToolTip(DropAreaBorder, AppResources.GetString("Tip_DropImage", _currentLanguage));
        ToolTipService.SetToolTip(CloseSuccessButton, AppResources.GetString("Tip_CloseEsc", _currentLanguage));
        ToolTipService.SetToolTip(UndoButton, AppResources.GetString("Tip_Undo", _currentLanguage));
        if (RemoveEmblemButton != null) ToolTipService.SetToolTip(RemoveEmblemButton, AppResources.GetString("Tip_RemoveEmblem", _currentLanguage));
        if (RemoveEmojiEmblemButton != null)
        {
            RemoveEmojiEmblemButton.Content = AppResources.GetString("Action_RemoveEmblem", _currentLanguage);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                RemoveEmojiEmblemButton, AppResources.GetString("Action_RemoveEmblem", _currentLanguage));
            ToolTipService.SetToolTip(RemoveEmojiEmblemButton, AppResources.GetString("Tip_RemoveEmblem", _currentLanguage));
        }

        if (PreviewTitleText != null) PreviewTitleText.Text = AppResources.GetString("PreviewTitle", _currentLanguage);
        if (PreviewSubtitleText != null)
        {
            // The subtitle slot also carries the first-use hint while it is live, so a language
            // switch must not silently replace the guidance with the generic line.
            PreviewSubtitleText.Text = _firstUseHintVisible
                ? AppResources.GetString("QuickPanel_FirstUseHint", _currentLanguage)
                : AppResources.GetString("PreviewSubtitle", _currentLanguage);
        }
        if (FirstUseHintDismissButton != null)
        {
            FirstUseHintDismissButton.Content = AppResources.GetString("QuickPanel_FirstUseDismiss", _currentLanguage);
            AutomationProperties.SetName(FirstUseHintDismissButton, AppResources.GetString("QuickPanel_FirstUseDismissAccessible", _currentLanguage));
            ToolTipService.SetToolTip(FirstUseHintDismissButton, AppResources.GetString("QuickPanel_FirstUseDismissAccessible", _currentLanguage));
        }

        if (CancelPreviewButton != null)
        {
            CancelPreviewButton.Content = AppResources.GetString("Cancel", _currentLanguage);
            ToolTipService.SetToolTip(CancelPreviewButton, AppResources.GetString("CancelPreviewToolTip", _currentLanguage));
            AutomationProperties.SetName(CancelPreviewButton, AppResources.GetString("Cancel", _currentLanguage));
        }
        if (MoreActionButton != null)
        {
            ToolTipService.SetToolTip(MoreActionButton, AppResources.GetString("More_Options_ToolTip", _currentLanguage));
            AutomationProperties.SetName(MoreActionButton, AppResources.GetString("Action_More", _currentLanguage));
        }
        if (TemplateTitleText != null) TemplateTitleText.Text = AppResources.GetString("Template_Title", _currentLanguage);
        if (RadioTemplateClassic != null) RadioTemplateClassic.Content = AppResources.GetString("Template_Classic", _currentLanguage);
        if (RadioTemplateSoft != null) RadioTemplateSoft.Content = AppResources.GetString("Template_Soft", _currentLanguage);
        if (RadioTemplateFlat != null) RadioTemplateFlat.Content = AppResources.GetString("Template_Flat", _currentLanguage);
        if (PaletteTitleText != null) PaletteTitleText.Text = AppResources.GetString("ColorPicker_Palette", _currentLanguage);
        if (ApplyCustomColorButton != null) ApplyCustomColorButton.Content = AppResources.GetString("ColorPicker_Apply", _currentLanguage);
        if (CancelCustomColorButton != null) CancelCustomColorButton.Content = AppResources.GetString("ColorPicker_Cancel", _currentLanguage);

        if (FolderFlyoutTitle != null)
        {
            FolderFlyoutTitle.Text = AppResources.GetString("Template_Title", _currentLanguage);
        }

        if (MoreRemoveEmblemMenuItem != null)
        {
            MoreRemoveEmblemMenuItem.Text = AppResources.GetString("Action_RemoveEmblem", _currentLanguage);
        }
        if (MoreResetMenuItem != null)
        {
            MoreResetMenuItem.Text = $"{AppResources.GetString("More_ResetFolder", _currentLanguage)} (Ctrl+Z)";
        }
        if (RemoveEmblemButton != null)
        {
            RemoveEmblemButton.Content = AppResources.GetString("Action_RemoveEmblem", _currentLanguage);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                RemoveEmblemButton, AppResources.GetString("Action_RemoveEmblem", _currentLanguage));
        }
        if (IconSearchTextBox != null)
        {
            IconSearchTextBox.PlaceholderText = AppResources.GetString("Placeholder_SearchIcons", _currentLanguage);
        }
        if (IconFlyoutTitleText != null)
        {
            IconFlyoutTitleText.Text = AppResources.GetString("Icon_Title", _currentLanguage);
        }
        if (EmojiFlyoutTitleText != null)
        {
            EmojiFlyoutTitleText.Text = AppResources.GetString("Emoji_Title", _currentLanguage);
        }
        if (EmojiSearchTextBox != null)
        {
            EmojiSearchTextBox.PlaceholderText = AppResources.GetString("Placeholder_SearchEmoji", _currentLanguage);
        }
        if (MoreOpenManagerMenuItem != null)
        {
            MoreOpenManagerMenuItem.Text = $"{AppResources.GetString("More_OpenManager", _currentLanguage)} (Ctrl+M)";
        }
        if (MoreSettingsMenuItem != null)
        {
            MoreSettingsMenuItem.Text = AppResources.GetString("More_Settings", _currentLanguage);
        }
        if (MoreAboutMenuItem != null)
        {
            MoreAboutMenuItem.Text = AppResources.GetString("More_About", _currentLanguage);
        }
    }
}
