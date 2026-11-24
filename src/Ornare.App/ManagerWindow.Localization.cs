namespace Ornare.App;

using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// Language application for the manager surface.
/// <para>
/// The chrome copy lives here rather than in the window class because it is one long, flat
/// list of assignments that has nothing to do with window behaviour, and because the XAML
/// literals it overwrites are the only reason the English defaults in the markup never reach
/// the screen (ENH-2).
/// </para>
/// </summary>
public sealed partial class ManagerWindow
{
    public void ApplyLanguage(string lang, bool localizeSettingsSurface = true)
    {
        // All six first-class locales must round-trip: collapsing non-Turkish
        // choices to en-US silently discarded the user's selection and persisted
        // it back to disk as en-US.
        string locale = AppResources.NormalizeLocale(lang);
        _currentLanguage = locale;

        // Keep the process-wide locale in sync so helpers that do not receive the locale
        // explicitly (settings surface, cloud status, onboarding) resolve the same language.
        AppResources.CurrentLocale = locale;

        // Nav/chrome copy is resolved per key so every supported locale is honoured
        // instead of the previous two-way tr/en ternary.
        string L(string key) => AppResources.GetString(key, locale);

        NavTextLibrary.Text = L("Nav_Library");
        if (AppSubtitleText != null) AppSubtitleText.Text = L("AppSubtitle");
        if (RejectedItemsLabel != null) RejectedItemsLabel.Text = L("RejectedItemsLabel");

        // Screen readers announce AutomationProperties.Name verbatim; keeping these in
        // English made navigation and settings unusable in the other five locales.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavItemLibrary, L("Nav_Library"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavItemRecent, L("Nav_Recent"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavItemFavorites, L("Nav_Favorites"));
        if (NavItemCollections != null) Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavItemCollections, L("Nav_Collections"));
        if (NavItemPresets != null) Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavItemPresets, L("Nav_Presets"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavItemFolders, L("Nav_Folders"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NavItemSettings, L("Nav_Settings"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SearchTextBox, L("SearchLibrary"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ClearRecentButton, L("ClearRecent"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(TargetTrayDropZone, L("Staging_TrayTitle"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ClearStagingButton, L("Staging_ClearStaging"));
        ToolTipService.SetToolTip(SearchTextBox, L("Tip_SearchLibrary"));
        ToolTipService.SetToolTip(TargetTrayDropZone, L("Tip_AddFolders"));
        ToolTipService.SetToolTip(ClearStagingButton, L("Tip_ClearStaging"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ThemeComboBox, L("Settings_ThemeLabel"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LanguageComboBox, L("Settings_LanguageLabel"));
        // The hex field and the scale picker carried English literals in markup, which a screen
        // reader announced verbatim in all six locales. The parity test used to accept any
        // assignment on the element, so a Text assignment hid a missing accessible name.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AccentHexBox, L("Settings_AccentLabel"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ExportReportButton, L("ExportDiagnosticReport"));
        if (ImportIconButton != null) Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ImportIconButton, L("ImportIcon"));
        if (ImportIconButton != null) ToolTipService.SetToolTip(ImportIconButton, L("Tip_ImportIcon"));
        if (NewCollectionButton != null) Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(NewCollectionButton, L("Collections_NewShort"));
        NavTextRecent.Text = L("Nav_Recent");
        NavTextFavorites.Text = L("Nav_Favorites");
        if (NavTextCollections != null) NavTextCollections.Text = L("Nav_Collections");
        if (NavTextPresets != null) NavTextPresets.Text = L("Nav_Presets");
        NavTextFolders.Text = L("Nav_Folders");
        NavTextSettings.Text = L("Nav_Settings");

        if (CollectionsListHeader != null) CollectionsListHeader.Text = L("Nav_Collections");
        if (NewCollectionButtonText != null) NewCollectionButtonText.Text = L("Collections_NewShort");
        if (CollectionsEmptyTitle != null) CollectionsEmptyTitle.Text = L("Collections_EmptyTitle");
        if (CollectionsEmptySubtitle != null) CollectionsEmptySubtitle.Text = L("Collections_EmptySubtitle");
        if (CollectionAssetsEmptyTitle != null) CollectionAssetsEmptyTitle.Text = L("Collections_NoAssetsTitle");
        if (CollectionAssetsEmptySubtitle != null) CollectionAssetsEmptySubtitle.Text = L("Collections_NoAssetsSubtitle");

        if (PresetTemplateLabel != null) PresetTemplateLabel.Text = L("Preset_StyleLabel");
        if (PresetRadioClassic != null) PresetRadioClassic.Content = L("Template_Classic");
        if (PresetRadioSoft != null) PresetRadioSoft.Content = L("Template_Soft");
        if (PresetRadioFlat != null) PresetRadioFlat.Content = L("Template_Flat");
        if (PresetDisclaimerText != null)
        {
            PresetDisclaimerText.Text = L("Preset_Disclaimer");
        }

        LibraryEmptyTitle.Text = L("Library_EmptyTitle");
        LibraryEmptySubtitle.Text = L("Library_EmptySubtitle");
        if (LibraryEmptyGuidance != null) LibraryEmptyGuidance.Text = L("Library_EmptyGuidance");
        if (EmptyLibraryChooseFolderButton != null)
        {
            string chooseFolder = L("EmptyLibrary_ChooseFolder");
            EmptyLibraryChooseFolderButton.Content = chooseFolder;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(EmptyLibraryChooseFolderButton, chooseFolder);
        }
        RecentEmptyTitle.Text = L("Recent_EmptyTitle");
        if (RecentEmptySubtitle != null) RecentEmptySubtitle.Text = L("Recent_EmptySubtitle");
        FavoritesEmptyTitle.Text = L("EmptyFavoritesTitle");
        FavoritesEmptySubtitle.Text = L("Favorites_EmptySubtitle");
        FoldersEmptyTitle.Text = L("Folders_EmptyTitle");
        if (FoldersEmptySubtitle != null) FoldersEmptySubtitle.Text = L("Folders_EmptySubtitle");
        TargetTrayTitle.Text = L("Staging_TrayTitle");
        TargetTraySubtitle.Text = L("Staging_TraySubtitle");
        ClearStagingButton.Content = L("Staging_ClearStaging");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(TargetTrayDropZone, L("Staging_TrayTitle"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ClearStagingButton, L("Staging_ClearStaging"));

        SettingsAppearanceHeader.Text = L("Settings_Appearance");
        SettingsThemeLabel.Text = L("Settings_ThemeLabel");
        ThemeItemSystem.Content = L("Settings_SystemDefault");
        ThemeItemLight.Content = L("Settings_Light");
        ThemeItemDark.Content = L("Settings_Dark");

        SettingsLanguageHeader.Text = L("Language");
        SettingsLanguageLabel.Text = L("Settings_LanguageLabel");
        LangItemSystem.Content = L("Settings_SystemDefault");

        // A ComboBox paints the selected entry from the item's Content and does not repaint
        // when that Content is replaced in place: the item read "Sistem varsayÄ±lanÄ±" while
        // the closed picker still showed "System default". Re-selecting the current index
        // forces the selection box to render the new text.
        RefreshComboBoxSelection(ThemeComboBox);
        RefreshComboBoxSelection(LanguageComboBox);

        SettingsDiagnosticsHeader.Text = L("Diagnostics");
        SettingsDiagnosticsLabel.Text = L("Settings_DiagnosticsLabel");
        SettingsDiagnosticsSubtext.Text = L("Settings_DiagnosticsSubtext");
        ExportReportButton.Content = L("ExportDiagnosticReport");
        SettingsAboutHeader.Text = L("Settings_About");
        SettingsPublisherLabel.Text = L("Settings_PublisherLabel");
        SettingsWebsiteLabel.Text = L("Settings_WebsiteLabel");
        SettingsVersionLabel.Text = L("Version");
        // Version badge must follow the shipped assembly metadata, not a design-time
        // literal that silently drifts from the installer/packaged version.
        SettingsVersionValue.Text = AppInfo.Version;
        SettingsArchLabel.Text = L("Architecture");
        SettingsPrivacyStatement.Text = L("Settings_PrivacyStatement");
        if (localizeSettingsSurface)
        {
            ApplySettingsSurfaceLocalization();
        }
        ClearRecentButton.Content = L("ClearRecent");
        SearchTextBox.PlaceholderText = L("Search_Placeholder");
        if (ImportIconButton != null) ImportIconButton.Content = L("ImportIcon");

        if (!string.IsNullOrEmpty(_currentSectionTag))
        {
            SwitchSection(_currentSectionTag);
        }
    }

}
