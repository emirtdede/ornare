namespace Ornare.App;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Ornare.Core;
using Ornare.Storage.Cloud;
using global::Windows.UI;

/// <summary>
/// Settings surface: appearance (accent, icon, scale), cloud backup and the welcome
/// tour entry point. Split from the main window file so the library/folder logic stays
/// readable; both parts share the same instance state.
/// </summary>
public sealed partial class ManagerWindow
{
    private const string CloudRestorePromptKey = "CloudRestorePromptCompleted";

    private readonly CloudSyncService _cloudSync = new();
    private UserSettings _settings = new();
    private CancellationTokenSource? _cloudSyncCts;
    private bool _settingsSurfaceReady;
    private string _cloudBadgeKey = "Settings_CloudStatusDisconnected";

    /// <summary>
    /// Suppresses selection notifications raised by code that repopulates or re-selects a
    /// picker. Handling those as a user choice persisted the settings snapshot captured
    /// before the refresh, which silently reverted a language that had just been applied.
    /// </summary>
    private readonly ReentrancyScope _pickerUpdates = new();

    /// <summary>
    /// Forces a closed <see cref="ComboBox"/> to repaint its selection box after the selected
    /// item's <c>Content</c> was replaced. Re-assigning the same index alone is a no-op, so the
    /// selection is cleared first; the whole cycle runs with selection events suppressed.
    /// </summary>
    private void RefreshComboBoxSelection(ComboBox combo)
    {
        int index = combo.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        // A closed picker paints its selection box when the index changes, not when the
        // selected item's Content is replaced, so the index is reassigned inside the scope.
        _pickerUpdates.Run(() =>
        {
            combo.SelectedIndex = -1;
            combo.SelectedIndex = index;
        });
    }

    /// <summary>Wires controls that are not driven by XAML markup alone.</summary>
    private void InitializeSettingsSurface()
    {
        RepositoryHyperlink.Content = AppInfo.RepositoryDisplayName;
        RepositoryHyperlink.NavigateUri = new Uri(AppInfo.RepositoryUrl, UriKind.Absolute);
        SponsorHyperlink.Content = AppInfo.SponsorDisplayName;
        SponsorHyperlink.NavigateUri = new Uri(AppInfo.SponsorUrl, UriKind.Absolute);
        LicenseHyperlink.Content = AppInfo.LicenseDisplayName;
        LicenseHyperlink.NavigateUri = new Uri(AppInfo.LicenseUrl, UriKind.Absolute);

        ZoomViewport.SizeChanged += (_, _) => ApplyUiScale();

        PopulateLanguageItems();
        PopulateUiScaleItems();
        AccentColorPicker.Color = AppearanceService.ResolveAccent(_settings.AccentColor);
        RebuildAppIconItems();
        _settingsSurfaceReady = true;
    }

    /// <summary>Fills the language picker from the shipped locale catalogue.</summary>
    private void PopulateLanguageItems()
    {
        LanguageComboBox.Items.Clear();
        LanguageComboBox.Items.Add(LangItemSystem);
        foreach (string tag in AppResources.SupportedLocales)
        {
            LanguageComboBox.Items.Add(new ComboBoxItem
            {
                Content = AppResources.GetNativeName(tag),
                Tag = tag
            });
        }
    }

    /// <summary>Selects the picker entry matching a persisted language value.</summary>
    private void SelectLanguageItem(string language)
    {
        if (string.Equals(language, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            LanguageComboBox.SelectedItem = LangItemSystem;
            return;
        }

        string normalized = AppResources.NormalizeLocale(language);
        foreach (object item in LanguageComboBox.Items)
        {
            if (item is ComboBoxItem { Tag: string tag } &&
                string.Equals(tag, normalized, StringComparison.Ordinal))
            {
                LanguageComboBox.SelectedItem = item;
                return;
            }
        }

        LanguageComboBox.SelectedItem = LangItemSystem;
    }

    /// <summary>Applies every appearance setting and refreshes the related controls.</summary>
    private void ApplyAppearance()
    {
        AppearanceService.ApplyAccent(Application.Current, _settings.AccentColor);
        ApplyUiScale();
        ApplyAppIcon();

        Color accent = AppearanceService.ResolveAccent(_settings.AccentColor);
        AccentSwatch.Background = new SolidColorBrush(accent);
        AccentValueText.Text = string.Equals(_settings.AccentColor, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase)
            ? UserSettings.FormatHex(accent.R, accent.G, accent.B)
            : _settings.AccentColor;

        SelectUiScaleItem(_settings.UiScalePercent);
        AccentColorPicker.Color = accent;

        for (int i = 0; i < AppIconCatalog.All.Count; i++)
        {
            if (string.Equals(AppIconCatalog.All[i].Id, _settings.AppIconId, StringComparison.Ordinal))
            {
                AppIconComboBox.SelectedIndex = i;
                break;
            }
        }

        RebuildAppIconItems();
    }

    private void ApplyUiScale()
    {
        int percent = UserSettings.ClampScalePercent(_settings.UiScalePercent);
        AppearanceService.ApplyZoom(ZoomViewport, ZoomSurface, percent);
        SelectUiScaleItem(percent);
    }

    /// <summary>Fills the scale picker from the shared preset list, never from XAML literals.</summary>
    private void PopulateUiScaleItems()
    {
        UiScaleComboBox.Items.Clear();
        foreach (int percent in UserSettings.UiScalePresets)
        {
            UiScaleComboBox.Items.Add(new ComboBoxItem
            {
                Content = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}%",
                    percent),
                Tag = percent.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }
    }

    /// <summary>Selects the preset closest to a stored scale percentage.</summary>
    private void SelectUiScaleItem(int percent)
    {
        int clamped = UserSettings.ClampScalePercent(percent);
        ComboBoxItem? closest = null;
        int closestDelta = int.MaxValue;
        foreach (object item in UiScaleComboBox.Items)
        {
            if (item is not ComboBoxItem { Tag: string tag } ||
                !int.TryParse(tag, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int candidate))
            {
                continue;
            }

            int delta = Math.Abs(candidate - clamped);
            if (delta < closestDelta)
            {
                closest = (ComboBoxItem)item;
                closestDelta = delta;
            }
        }

        // Re-selecting the closest preset mirrors stored state; it is not a user decision.
        _pickerUpdates.Run(() => UiScaleComboBox.SelectedItem = closest);
    }

    private void ApplyAppIcon()
    {
        try
        {
            string iconPath = AppearanceService.EnsureIconFile(
                AppIconCatalog.Resolve(_settings.AppIconId),
                AppearanceService.ResolveAccentHex(_settings.AccentColor));
            if (!string.IsNullOrEmpty(iconPath))
            {
                AppWindow.SetIcon(iconPath);
                ApplyShortcutIcon(iconPath);
                App.NotifyAppIconChanged(_settings);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            App.Log($"Manager icon apply failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Mirrors the chosen app icon onto the desktop shortcut. Only an existing shortcut is
    /// touched: Ornare never creates a desktop shortcut behind the user's back, and a failed
    /// write is logged rather than surfaced, because the icon change itself already succeeded.
    /// </summary>
    private void ApplyShortcutIcon(string iconPath)
    {
        try
        {
            string shortcut = Ornare.Windows.DesktopShortcutService.DefaultShortcutPath;
            if (!Ornare.Windows.DesktopShortcutService.ShortcutExists(shortcut))
            {
                return;
            }

            string description = AppResources.GetString("App_ShortcutDescription", _currentLanguage);
            if (!Ornare.Windows.DesktopShortcutService.EnsureShortcut(shortcut, iconPath, description))
            {
                App.Log("Desktop shortcut icon was not updated.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            App.Log($"Desktop shortcut icon failed: {ex.Message}");
        }
    }

    private void RebuildAppIconItems()
    {
        if (!_settingsSurfaceReady && AppIconComboBox.Items.Count > 0)
        {
            return;
        }

        string accentHex = AppearanceService.ResolveAccentHex(_settings.AccentColor);
        string selectedId = AppIconCatalog.Resolve(_settings.AppIconId).Id;
        AppIconComboBox.Items.Clear();

        int selectedIndex = 0;
        for (int i = 0; i < AppIconCatalog.All.Count; i++)
        {
            AppIconVariant variant = AppIconCatalog.All[i];
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            string preview = AppearanceService.EnsureIconPreview(variant, accentHex);
            if (!string.IsNullOrEmpty(preview))
            {
                content.Children.Add(new Image
                {
                    Width = 20,
                    Height = 20,
                    Source = new BitmapImage(new Uri(preview, UriKind.Absolute))
                });
            }

            string label = AppResources.GetString(variant.DisplayResourceKey, _currentLanguage);
            content.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            var item = new ComboBoxItem
            {
                Content = content,
                Tag = variant.Id
            };

            // The item content is a composed panel, so its text is not exposed automatically.
            // Naming the item keeps the picker readable for screen readers and automation.
            AutomationProperties.SetName(item, label);
            AppIconComboBox.Items.Add(item);

            if (string.Equals(variant.Id, selectedId, StringComparison.Ordinal))
            {
                selectedIndex = i;
            }
        }

        // Selecting the rebuilt entries restores state; it is not a user choice.
        _pickerUpdates.Run(() => AppIconComboBox.SelectedIndex = selectedIndex);
    }

    /// <summary>Persists settings, then schedules a debounced cloud write when enabled.</summary>
    private async Task PersistSettingsAsync(UserSettings updated)
    {
        UserSettings previous = _settings;
        _settings = updated;
        _currentTheme = updated.Theme;
        _currentLanguage = updated.Language;

        try
        {
            await _storageService.SaveUserSettingsAsync(updated);
        }
        catch (Exception ex)
        {
            // Keep the in-memory choice: the UI must not revert while the window is open.
            App.Log($"SaveUserSettingsAsync failed: {ex.Message}");
            return;
        }

        bool cloudRelevantChange =
            previous.Theme != updated.Theme ||
            previous.Language != updated.Language ||
            previous.AccentColor != updated.AccentColor ||
            previous.AppIconId != updated.AppIconId ||
            previous.UiScalePercent != updated.UiScalePercent ||
            previous.CloudSyncEnabled != updated.CloudSyncEnabled;

        if (cloudRelevantChange && updated.CloudSyncEnabled)
        {
            ScheduleCloudSync();
        }
    }

    private void ScheduleCloudSync()
    {
        _cloudSyncCts?.Cancel();
        _cloudSyncCts?.Dispose();
        _cloudSyncCts = new CancellationTokenSource();
        CancellationToken token = _cloudSyncCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await WriteCloudSnapshotAsync(allowEmptyOverwrite: false).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Log($"Background cloud sync failed: {ex.Message}");
            }
        }, token);
    }

    private async Task<CloudSyncWriteResult> WriteCloudSnapshotAsync(bool allowEmptyOverwrite)
    {
        CloudSyncProviderKind kind = CloudSyncService.ParseProvider(_settings.CloudSyncProvider);
        if (kind == CloudSyncProviderKind.None)
        {
            return CloudSyncWriteResult.Fail("NoProvider");
        }

        CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
        CloudSyncSnapshot snapshot = await CloudSyncService
            .BuildSnapshotAsync(_storageService, kind, AppInfo.Version, DateTimeOffset.UtcNow, _settings)
            .ConfigureAwait(false);

        CloudSyncWriteResult result = await _cloudSync
            .WriteSnapshotAsync(location, snapshot, allowEmptyOverwrite)
            .ConfigureAwait(false);

        if (result.Success)
        {
            _settings = _settings with { CloudSyncLastSyncUtc = DateTimeOffset.UtcNow.ToString("O") };
            await _storageService.SaveUserSettingsAsync(_settings).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Refreshes the cloud card from the current provider and snapshot state.</summary>
    private async Task RefreshCloudSurfaceAsync()
    {
        if (!_settingsSurfaceReady)
        {
            return;
        }

        string locale = AppResources.CurrentLocale;
        CloudSyncProviderKind kind = CloudSyncService.ParseProvider(_settings.CloudSyncProvider);
        bool enabled = kind != CloudSyncProviderKind.None;

        SelectCloudProvider(kind);
        CloudAutoSyncToggle.IsOn = _settings.CloudSyncEnabled && enabled;
        CloudAutoSyncToggle.IsEnabled = enabled;
        // Once a provider is bound there is nothing left to connect; the card offers
        // Sync now / Restore / Open folder / Disconnect instead.
        CloudConnectButton.IsEnabled = !enabled;
        CloudSyncNowButton.IsEnabled = enabled && _settings.CloudSyncEnabled;
        CloudRestoreButton.IsEnabled = enabled;
        CloudOpenFolderButton.IsEnabled = enabled;
        CloudDisconnectButton.IsEnabled = enabled;

        if (!enabled)
        {
            SetCloudBadge("Settings_CloudStatusDisconnected", "CardBackgroundFillColorSecondaryBrush");
            CloudSummaryText.Text = AppResources.GetString("Settings_CloudNever", locale);
            return;
        }

        CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
        CloudSyncStatus status = await CloudSyncService.GetStatusAsync(location).ConfigureAwait(true);

        SetCloudBadge(
            location.IsFallback ? "Settings_CloudFallbackBadge" : "Settings_CloudStatusConnected",
            location.IsFallback ? "SystemFillColorCautionBackgroundBrush" : "SystemFillColorSuccessBackgroundBrush");

        string last = status.LastSyncUtc is null
            ? AppResources.GetString("Settings_CloudNever", locale)
            : AppResources.Format(
                "Settings_CloudLastSync",
                locale,
                status.LastSyncUtc.Value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture));

        CloudSummaryText.Text = AppResources.Format(
            "Settings_CloudSummary",
            locale,
            status.FolderCount,
            status.CollectionCount,
            status.BackupFileCount) + Environment.NewLine + last;
    }

    private void SelectCloudProvider(CloudSyncProviderKind kind)
    {
        int index = kind switch
        {
            CloudSyncProviderKind.OneDrive => 1,
            CloudSyncProviderKind.GoogleDrive => 2,
            CloudSyncProviderKind.Dropbox => 3,
            CloudSyncProviderKind.Custom => 4,
            _ => 0
        };

        if (CloudProviderComboBox.SelectedIndex != index)
        {
            // Mirroring stored state must not be read as a provider pick: choosing Custom
            // opens a folder dialog, which a background refresh must never do.
            _pickerUpdates.Run(() => CloudProviderComboBox.SelectedIndex = index);
        }
    }

    private void ShowCloudResult(string message, bool isError)
    {
        CloudResultText.Text = message;
        CloudResultText.Visibility = Visibility.Visible;
        CloudResultText.Foreground = isError
            ? (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
            : (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
    }

    private async Task ConnectCloudAsync()
    {
        CloudSyncProviderKind kind = CloudSyncService.ParseProvider(_settings.CloudSyncProvider);
        if (kind == CloudSyncProviderKind.None)
        {
            // Clicking Connect without picking a provider means "use whatever is available":
            // the first cloud client found on this PC, otherwise a safe local backup folder.
            IReadOnlyList<CloudSyncLocation> detectedProviders = CloudSyncService.DetectAll();
            CloudSyncLocation? detected = detectedProviders.Count > 0 ? detectedProviders[0] : null;
            kind = detected?.Provider ?? CloudSyncProviderKind.Custom;

            string root = detected is not null
                ? _settings.CloudSyncRoot ?? _cloudSync.ResolveLocation(kind.ToString(), null).RootPath
                : _cloudSync.ResolveLocation(nameof(CloudSyncProviderKind.Custom), null).RootPath;

            _settings = _settings with { CloudSyncProvider = kind.ToString(), CloudSyncRoot = root };
            SelectCloudProvider(kind);
        }

        CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
        CloudSyncReadResult existing = await CloudSyncService.ReadSnapshotAsync(location).ConfigureAwait(true);

        bool localEmpty = await IsLocalStateEmptyAsync().ConfigureAwait(true);
        if (existing.HasSnapshot && localEmpty)
        {
            bool restored = await RestoreSnapshotAsync(existing.Snapshot!, location).ConfigureAwait(true);
            ShowCloudResult(
                AppResources.GetString(restored ? "Settings_CloudRestoreDone" : "Settings_CloudEmptyGuard", AppResources.CurrentLocale),
                !restored);
            await RefreshCloudSurfaceAsync().ConfigureAwait(true);
            return;
        }

        _settings = _settings with { CloudSyncEnabled = true, CloudSyncProvider = kind.ToString() };
        CloudSyncWriteResult result = await WriteCloudSnapshotAsync(allowEmptyOverwrite: false).ConfigureAwait(true);

        if (result.Success)
        {
            await PersistSettingsAsync(_settings).ConfigureAwait(true);
            ShowCloudResult(
                AppResources.Format("Settings_CloudConnectedTo", AppResources.CurrentLocale, location.RootPath),
                false);
        }
        else if (string.Equals(result.ErrorCode, "EmptySnapshotGuard", StringComparison.Ordinal))
        {
            ShowCloudResult(AppResources.GetString("Settings_CloudEmptyGuard", AppResources.CurrentLocale), true);
        }
        else
        {
            ShowCloudResult(AppResources.GetString("Settings_CloudStatusFailed", AppResources.CurrentLocale), true);
        }

        await RefreshCloudSurfaceAsync().ConfigureAwait(true);
    }

    private async Task<bool> RestoreSnapshotAsync(CloudSyncSnapshot snapshot, CloudSyncLocation location)
    {
        UserSettings restored = snapshot.Settings with
        {
            CloudSyncEnabled = true,
            CloudSyncProvider = location.Provider.ToString(),
            CloudSyncLastSyncUtc = DateTimeOffset.UtcNow.ToString("O")
        };

        foreach (string collection in snapshot.Collections)
        {
            if (string.IsNullOrWhiteSpace(collection))
            {
                continue;
            }

            if (!LibraryNameNormalizer.TryValidateAndNormalizeDetailed(
                    collection,
                    out _,
                    out string normalized,
                    out _,
                    LibraryNameNormalizer.MaxScalarsPerCollection))
            {
                continue;
            }

            try
            {
                LibraryCollection? existing = await _storageService
                    .GetCollectionByNormalizedNameAsync(normalized)
                    .ConfigureAwait(true);
                if (existing is null)
                {
                    await _storageService.CreateCollectionAsync(collection).ConfigureAwait(true);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                App.Log($"Collection restore skipped for '{collection}': {ex.Message}");
            }
        }

        _settings = restored;
        _currentTheme = restored.Theme;
        _currentLanguage = restored.Language;
        await _storageService.SaveUserSettingsAsync(restored).ConfigureAwait(true);

        AppResources.CurrentLocale = restored.Language;
        ApplyTheme(restored.Theme);
        ApplyLanguage(restored.Language);
        ApplyAppearance();
        ThemeComboBox.SelectedIndex = restored.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };

        await _storageService.SetSettingAsync(CloudRestorePromptKey, "true").ConfigureAwait(true);
        await RefreshAllDataAsync().ConfigureAwait(true);
        return true;
    }

    private async Task<bool> IsLocalStateEmptyAsync()
    {
        try
        {
            IReadOnlyList<FolderAssignment> assignments = await _storageService.GetAllFolderAssignmentsAsync().ConfigureAwait(true);
            if (assignments.Count > 0)
            {
                return false;
            }

            IReadOnlyList<LibraryCollection> collections = await _storageService.GetAllCollectionsAsync().ConfigureAwait(true);
            if (collections.Count > 0)
            {
                return false;
            }

            return _settings.IsDefault();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            App.Log($"IsLocalStateEmptyAsync failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Offers a one-time restore when this PC looks freshly installed but a cloud backup
    /// already exists. Never overwrites the backup and never blocks the window.
    /// </summary>
    private async Task ShowCloudRestorePromptIfNeededAsync()
    {
        CloudSyncProviderKind kind = CloudSyncService.ParseProvider(_settings.CloudSyncProvider);
        if (kind == CloudSyncProviderKind.None || !_settings.CloudSyncEnabled)
        {
            return;
        }

        string? alreadyPrompted = await _storageService.GetSettingAsync(CloudRestorePromptKey).ConfigureAwait(true);
        if (string.Equals(alreadyPrompted, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!await IsLocalStateEmptyAsync().ConfigureAwait(true))
        {
            return;
        }

        CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
        CloudSyncReadResult read = await CloudSyncService.ReadSnapshotAsync(location).ConfigureAwait(true);
        if (!read.HasSnapshot || read.Snapshot!.IsEmpty)
        {
            return;
        }

        string locale = AppResources.CurrentLocale;
        var dialog = new ContentDialog
        {
            XamlRoot = (Content as FrameworkElement)?.XamlRoot,
            Title = AppResources.GetString("Settings_CloudRestorePromptTitle", locale),
            Content = AppResources.GetString("Settings_CloudRestorePromptBody", locale),
            PrimaryButtonText = AppResources.GetString("Settings_CloudRestoreAction", locale),
            CloseButtonText = AppResources.GetString("Settings_CloudFreshStartAction", locale),
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            App.Log($"Cloud restore dialog failed: {ex.Message}");
            return;
        }

        if (result == ContentDialogResult.Primary)
        {
            await RestoreSnapshotAsync(read.Snapshot, location).ConfigureAwait(true);
            ShowCloudResult(AppResources.GetString("Settings_CloudRestoreDone", locale), false);
        }

        await _storageService.SetSettingAsync(CloudRestorePromptKey, "true").ConfigureAwait(true);
        await RefreshCloudSurfaceAsync().ConfigureAwait(true);
    }

    // ---- Event handlers -------------------------------------------------

    private async void OnAccentColorPickerChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!_settingsSurfaceReady)
        {
            return;
        }

        AccentSwatch.Background = new SolidColorBrush(args.NewColor);
        AccentValueText.Text = UserSettings.FormatHex(args.NewColor.R, args.NewColor.G, args.NewColor.B);
        AccentHexBox.Text = AccentValueText.Text;
        await Task.CompletedTask;
    }

    private async void OnAccentApplyClicked(object sender, RoutedEventArgs e)
    {
        if (!UserSettings.TryNormalizeHexColor(AccentHexBox.Text, out string normalized))
        {
            ShowCloudResult(AppResources.GetString("Settings_AccentInvalid", AppResources.CurrentLocale), true);
            return;
        }

        await ApplyAccentAsync(normalized).ConfigureAwait(true);
    }

    private async void OnAccentSystemClicked(object sender, RoutedEventArgs e)
    {
        await ApplyAccentAsync(UserSettings.SystemValue).ConfigureAwait(true);
    }

    private async Task ApplyAccentAsync(string accent)
    {
        _settings = _settings with { AccentColor = accent };
        ApplyAppearance();
        RebuildAppIconItems();
        await PersistSettingsAsync(_settings).ConfigureAwait(true);
        ApplyLanguage(_currentLanguage);
    }

    private async void OnAppIconSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pickerUpdates.IsActive ||
            !_settingsSurfaceReady ||
            AppIconComboBox.SelectedItem is not ComboBoxItem { Tag: string id })
        {
            return;
        }

        // A selection identical to the stored one carries no intent. Rejecting it here keeps a
        // repopulated picker or an automation re-select from rewriting settings at all, which
        // is what used to revert a language that had just been applied.
        if (string.Equals(id, _settings.AppIconId, StringComparison.Ordinal))
        {
            return;
        }

        _settings = _settings with { AppIconId = id };
        ApplyAppIcon();
        await PersistSettingsAsync(_settings).ConfigureAwait(true);
    }

    private async void OnUiScaleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pickerUpdates.IsActive ||
            !_settingsSurfaceReady ||
            UiScaleComboBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        if (!int.TryParse(tag, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int percent))
        {
            return;
        }

        percent = UserSettings.ClampScalePercent(percent);
        if (percent == _settings.UiScalePercent)
        {
            return;
        }

        _settings = _settings with { UiScalePercent = percent };
        ApplyUiScale();
        await PersistSettingsAsync(_settings).ConfigureAwait(true);
    }

    private async void OnUiScaleResetClicked(object sender, RoutedEventArgs e)
    {
        _settings = _settings with { UiScalePercent = 100 };
        ApplyUiScale();
        await PersistSettingsAsync(_settings).ConfigureAwait(true);
    }

    private async void OnCloudProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pickerUpdates.IsActive ||
            !_settingsSurfaceReady ||
            CloudProviderComboBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        CloudSyncProviderKind kind = CloudSyncService.ParseProvider(tag);
        bool autoSyncAfter = kind != CloudSyncProviderKind.None && _settings.CloudSyncEnabled;

        if (string.Equals(kind.ToString(), _settings.CloudSyncProvider, StringComparison.Ordinal) &&
            autoSyncAfter == _settings.CloudSyncEnabled)
        {
            // Same provider and same auto-sync state: nothing to persist, and a Custom entry
            // must not reopen the folder picker just because the surface refreshed its own
            // selection. A provider that is already "none" while auto-sync is still on is not
            // a no-op, so it falls through and turns auto-sync off.
            return;
        }

        _settings = _settings with
        {
            CloudSyncProvider = kind.ToString(),
            CloudSyncEnabled = autoSyncAfter
        };

        if (kind == CloudSyncProviderKind.Custom)
        {
            await PickCloudFolderAsync().ConfigureAwait(true);
        }

        await PersistSettingsAsync(_settings).ConfigureAwait(true);
        await RefreshCloudSurfaceAsync().ConfigureAwait(true);
    }

    private async Task PickCloudFolderAsync()
    {
        var picker = new global::Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        global::Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        if (!CloudSyncService.IsSafeDirectoryPath(folder.Path))
        {
            ShowCloudResult(AppResources.GetString("Settings_CloudStatusFailed", AppResources.CurrentLocale), true);
            return;
        }

        _settings = _settings with { CloudSyncRoot = folder.Path, CloudSyncProvider = nameof(CloudSyncProviderKind.Custom) };
    }

    private async void OnCloudConnectClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnCloudConnectClicked), ConnectCloudAsync);
    }

    private async void OnCloudSyncNowClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnCloudSyncNowClicked), async () =>
        {
            _settings = _settings with { CloudSyncEnabled = true };
            CloudSyncWriteResult result = await WriteCloudSnapshotAsync(allowEmptyOverwrite: false).ConfigureAwait(true);
            string locale = AppResources.CurrentLocale;
            ShowCloudResult(
                AppResources.GetString(result.Success ? "Settings_CloudStatusSynced" : "Settings_CloudStatusFailed", locale),
                !result.Success);
            await PersistSettingsAsync(_settings).ConfigureAwait(true);
            await RefreshCloudSurfaceAsync().ConfigureAwait(true);
        });
    }

    private async void OnCloudRestoreClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnCloudRestoreClicked), async () =>
        {
            CloudSyncProviderKind kind = CloudSyncService.ParseProvider(_settings.CloudSyncProvider);
            if (kind == CloudSyncProviderKind.None)
            {
                ShowCloudResult(AppResources.GetString("Settings_CloudNeedProvider", AppResources.CurrentLocale), true);
                return;
            }

            CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
            CloudSyncReadResult read = await CloudSyncService.ReadSnapshotAsync(location).ConfigureAwait(true);
            string locale = AppResources.CurrentLocale;

            if (!read.HasSnapshot || read.Snapshot is null)
            {
                ShowCloudResult(AppResources.GetString("Settings_CloudNever", locale), true);
                return;
            }

            if (!read.Snapshot.IsEmpty)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = (Content as FrameworkElement)?.XamlRoot,
                    Title = AppResources.GetString("Settings_CloudRestorePromptTitle", locale),
                    Content = AppResources.GetString("Settings_CloudRestorePromptBody", locale),
                    PrimaryButtonText = AppResources.GetString("Settings_CloudRestoreAction", locale),
                    CloseButtonText = AppResources.GetString("Cancel", locale),
                    DefaultButton = ContentDialogButton.Primary
                };

                ContentDialogResult confirm = await dialog.ShowAsync();
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            await RestoreSnapshotAsync(read.Snapshot, location).ConfigureAwait(true);
            ShowCloudResult(AppResources.GetString("Settings_CloudRestoreDone", locale), false);
        });
    }

    private void OnCloudOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
        if (!CloudSyncService.IsSafeDirectoryPath(location.RootPath))
        {
            ShowCloudResult(AppResources.GetString("Settings_CloudStatusFailed", AppResources.CurrentLocale), true);
            return;
        }

        try
        {
            Directory.CreateDirectory(location.RootPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + location.RootPath + "\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            App.Log($"Open cloud folder failed: {ex.Message}");
            ShowCloudResult(AppResources.GetString("Settings_CloudStatusFailed", AppResources.CurrentLocale), true);
        }
    }

    private async void OnCloudDisconnectClicked(object sender, RoutedEventArgs e)
    {
        _cloudSyncCts?.Cancel();
        _settings = _settings with { CloudSyncEnabled = false, CloudSyncProvider = "None" };
        await PersistSettingsAsync(_settings).ConfigureAwait(true);
        await RefreshCloudSurfaceAsync().ConfigureAwait(true);
        ShowCloudResult(AppResources.GetString("Settings_CloudStatusDisconnected", AppResources.CurrentLocale), false);
    }

    private async void OnCloudAutoSyncToggled(object sender, RoutedEventArgs e)
    {
        if (!_settingsSurfaceReady)
        {
            return;
        }

        _settings = _settings with { CloudSyncEnabled = CloudAutoSyncToggle.IsOn };
        await PersistSettingsAsync(_settings).ConfigureAwait(true);
    }

    private async void OnShowWelcomeClicked(object sender, RoutedEventArgs e)
    {
        _settings = _settings with { OnboardingCompleted = false };
        await PersistSettingsAsync(_settings).ConfigureAwait(true);

        var window = new OnboardingWindow(_storageService, _cloudSync, _settings);
        window.Completed += async (_, updated) =>
        {
            _settings = updated;
            AppResources.CurrentLocale = updated.Language;
            ApplyTheme(updated.Theme);
            ApplyLanguage(updated.Language);
            ApplyAppearance();
            await RefreshCloudSurfaceAsync().ConfigureAwait(true);
        };
        window.Activate();
    }

    /// <summary>Localizes the settings controls added by this part.</summary>
    private void ApplySettingsSurfaceLocalization()
    {
        SettingsAccentLabel.Text = L("Settings_AccentLabel");
        SettingsAccentHint.Text = L("Settings_AccentHint");
        AccentApplyButton.Content = L("Settings_AccentApply");
        AccentSystemButton.Content = L("Settings_AccentSystem");
        SettingsAppIconLabel.Text = L("Settings_AppIconLabel");
        SettingsAppIconSubtext.Text = L("Settings_AppIconSubtext");
        SettingsScaleLabel.Text = L("Settings_ScaleLabel");
        SettingsScaleSubtext.Text = L("Settings_ScaleSubtext");
        UiScaleResetButton.Content = L("Settings_Reset");

        SettingsCloudHeader.Text = L("Settings_CloudHeader");
        SettingsCloudProviderLabel.Text = L("Settings_CloudProviderLabel");
        CloudProviderNoneItem.Content = L("Settings_CloudProviderNone");
        CloudProviderOneDriveItem.Content = "OneDrive";
        CloudProviderGoogleItem.Content = "Google Drive";
        CloudProviderDropboxItem.Content = "Dropbox";
        CloudProviderCustomItem.Content = L("Settings_CloudChooseFolder");
        CloudConnectButton.Label = L("Settings_CloudConnect");
        CloudSyncNowButton.Label = L("Settings_CloudSyncNow");
        CloudRestoreButton.Label = L("Settings_CloudRestore");
        CloudOpenFolderButton.Label = L("Settings_CloudOpenFolder");
        CloudDisconnectButton.Label = L("Settings_CloudDisconnect");
        CloudAutoSyncToggle.Header = L("Settings_CloudAutoSync");
        CloudAutoSyncToggle.OnContent = L("Toggle_On");
        CloudAutoSyncToggle.OffContent = L("Toggle_Off");

        SettingsRepositoryLabel.Text = L("Settings_Repository");
        SettingsSponsorLabel.Text = L("Settings_Sponsor");
        SettingsLicenseLabel.Text = L("Settings_License");
        SettingsOnboardingLabel.Text = L("Settings_Onboarding");
        ShowWelcomeButton.Content = L("Settings_ShowWelcome");

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AccentColorButton, L("Settings_AccentLabel"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AppIconComboBox, L("Settings_AppIconLabel"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(UiScaleComboBox, L("Settings_ScaleLabel"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CloudProviderComboBox, L("Settings_CloudProviderLabel"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CloudAutoSyncToggle, L("Settings_CloudAutoSync"));

        RebuildAppIconItems();
        UpdateCloudStatusLocalization();

        // Same repaint rule as the theme and language pickers: the "Off" provider entry keeps
        // its identity while its caption changes, so the closed picker needs a refresh.
        RefreshComboBoxSelection(CloudProviderComboBox);

        // The badge, summary and auto-sync labels are composed from live state, so they need
        // a refresh to pick up the new language instead of keeping the previous one.
        _ = RefreshCloudSurfaceAsync();
    }

    /// <summary>
    /// Sets the cloud status badge from a resource key so a later language switch can
    /// re-resolve the same visual state instead of leaving the old language behind.
    /// </summary>
    private void SetCloudBadge(string resourceKey, string brushKey)
    {
        _cloudBadgeKey = resourceKey;
        CloudStatusText.Text = AppResources.GetString(resourceKey, AppResources.CurrentLocale);
        CloudStatusBadge.Background = (Brush)Application.Current.Resources[brushKey];
    }

    private void UpdateCloudStatusLocalization()
    {
        string locale = AppResources.CurrentLocale;
        CloudProviderNoneItem.Content = L("Settings_CloudProviderNone");
        CloudStatusText.Text = AppResources.GetString(_cloudBadgeKey, locale);
    }

    /// <summary>Runs any pending debounced write before the window closes.</summary>
    private void FlushCloudSync()
    {
        if (_cloudSyncCts is null || !_settings.CloudSyncEnabled)
        {
            return;
        }

        try
        {
            _cloudSyncCts.Cancel();
            Task.Run(() => WriteCloudSnapshotAsync(allowEmptyOverwrite: false))
                .Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException ex)
        {
            App.Log($"Cloud flush failed: {ex.GetBaseException().Message}");
        }
    }
}
