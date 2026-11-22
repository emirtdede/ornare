using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Ornare.Core;
using Ornare.Storage;
using Ornare.Storage.Cloud;
using Windows.Graphics;
using WinRT.Interop;

namespace Ornare.App;

/// <summary>
/// First-run walkthrough. Every decision is a separate step on purpose: language first so
/// the rest of the flow is readable, then appearance, then the greeting, then a short tour
/// that also offers the cloud backup opt-in. Closing the window at any point keeps the
/// choices already made and remembers that onboarding was seen.
/// </summary>
public sealed partial class OnboardingWindow : Window
{
    private const int LastStepIndex = 3;

    private static readonly string[] AccentPresets =
    {
        "#0F6CBD", "#1F7A4C", "#C2410C", "#6B21A8", "#A16207", "#BE123C", "#0891B2", "#3F3F46"
    };

    private readonly IStorageService _storageService;
    private readonly CloudSyncService _cloudSync;
    private readonly List<LocaleOption> _locales;
    private readonly List<Button> _swatches = new();

    private UserSettings _settings;
    private int _step;
    private readonly bool _initializing = true;

    /// <summary>Raised after the walkthrough closes, with the final settings.</summary>
    public event EventHandler<UserSettings>? Completed;

    public OnboardingWindow(IStorageService storageService, CloudSyncService cloudSync, UserSettings settings)
    {
        _storageService = storageService;
        _cloudSync = cloudSync;
        _settings = settings;
        _locales = AppResources.SupportedLocales
            .Select(tag => new LocaleOption(tag, AppResources.GetNativeName(tag)))
            .ToList();

        InitializeComponent();
        ConfigureWindow();

        LanguageList.ItemsSource = _locales;
        BuildSwatches();
        PopulateScalePresets();

        _initializing = false;
        ApplyLocalization();
        SelectLanguage(_settings.Language);
        SelectTheme(_settings.Theme);
        AccentHexBox.Text = string.Equals(_settings.AccentColor, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase)
            ? AppearanceService.ResolveAccentHex(null)
            : _settings.AccentColor;
        SelectScale(_settings.UiScalePercent);
        CloudToggle.IsOn = _settings.CloudSyncEnabled;

        UpdateStep();
        UpdateWelcomeMark();

        Activated += (_, _) =>
        {
            if (Content is FrameworkElement root)
            {
                AppearanceService.ApplyZoom(ZoomViewport, ZoomSurface, _settings.UiScalePercent);
                _ = root;
            }
        };
        ZoomViewport.SizeChanged += (_, _) =>
            AppearanceService.ApplyZoom(ZoomViewport, ZoomSurface, _settings.UiScalePercent);
    }

    private void ConfigureWindow()
    {
        AppWindow appWindow = this.AppWindow;
        appWindow.Resize(new SizeInt32(780, 660));

        DisplayArea area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        int x = area.WorkArea.X + ((area.WorkArea.Width - 780) / 2);
        int y = area.WorkArea.Y + ((area.WorkArea.Height - 660) / 2);
        appWindow.Move(new PointInt32(x, y));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragArea);

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        string iconPath = AppearanceService.EnsureIconFile(
            AppIconCatalog.Resolve(_settings.AppIconId),
            AppearanceService.ResolveAccentHex(_settings.AccentColor));
        if (!string.IsNullOrEmpty(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }

        Closed += (_, _) => Completed?.Invoke(this, _settings);
    }

    private void BuildSwatches()
    {
        foreach (string hex in AccentPresets)
        {
            var swatch = new Button
            {
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(AppearanceService.FromHex(hex)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Black),
                Tag = hex,
                CornerRadius = new CornerRadius(15)
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(swatch, hex);
            swatch.Click += OnSwatchClicked;
            _swatches.Add(swatch);
            AccentSwatchPanel.Children.Add(swatch);
        }
    }

    private void OnSwatchClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            AccentHexBox.Text = hex;
            ApplyAccent(hex);
        }
    }

    private void OnAccentApplyClicked(object sender, RoutedEventArgs e)
    {
        if (!UserSettings.TryNormalizeHexColor(AccentHexBox.Text, out string normalized))
        {
            AccentHint.Text = AppResources.GetString("Settings_AccentInvalid", AppResources.CurrentLocale);
            AccentHint.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            return;
        }

        AccentHexBox.Text = normalized;
        ApplyAccent(normalized);
    }

    private void ApplyAccent(string hex)
    {
        if (!UserSettings.TryNormalizeHexColor(hex, out string normalized))
        {
            return;
        }

        _settings = _settings with { AccentColor = normalized };
        AppearanceService.ApplyAccent(Application.Current, normalized);
        AppearanceService.ApplyZoom(ZoomViewport, ZoomSurface, _settings.UiScalePercent);
        AccentHint.Text = AppResources.Format("Settings_AccentApplied", AppResources.CurrentLocale, normalized);
        AccentHint.Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
        UpdateWelcomeMark();
        ApplyAppIcon();
    }

    /// <summary>Fills the scale picker from the shared preset list, never from XAML literals.</summary>
    private void PopulateScalePresets()
    {
        ScaleComboBox.Items.Clear();
        foreach (int percent in UserSettings.UiScalePresets)
        {
            ScaleComboBox.Items.Add(new ComboBoxItem
            {
                Content = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}%",
                    percent),
                Tag = percent.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        }
    }

    private void OnScaleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ScaleComboBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        if (!int.TryParse(tag, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int percent))
        {
            return;
        }

        percent = UserSettings.ClampScalePercent(percent);
        _settings = _settings with { UiScalePercent = percent };
        ScaleValueText.Text = $"{percent}%";
        AppearanceService.ApplyZoom(ZoomViewport, ZoomSurface, percent);
    }

    private void SelectScale(int percent)
    {
        int clamped = UserSettings.ClampScalePercent(percent);
        ScaleValueText.Text = $"{clamped}%";

        ComboBoxItem? closest = null;
        int closestDelta = int.MaxValue;
        foreach (object item in ScaleComboBox.Items)
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

        ScaleComboBox.SelectedItem = closest;
    }

    private void OnThemeModeChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { Tag: string theme })
        {
            return;
        }

        _settings = _settings with { Theme = theme };
        ApplyTheme(theme);
    }

    private void SelectTheme(string theme)
    {
        if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            ThemeLightRadio.IsChecked = true;
        }
        else if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            ThemeDarkRadio.IsChecked = true;
        }
        else
        {
            ThemeSystemRadio.IsChecked = true;
        }

        ApplyTheme(theme);
    }

    private void ApplyTheme(string theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            bool isDark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
            AppWindow.TitleBar.ButtonForegroundColor = isDark ? Colors.White : Colors.Black;
        }

        UpdateWelcomeMark();
    }

    private void SelectLanguage(string language)
    {
        string normalized = AppResources.NormalizeLocale(language);
        LocaleOption? match = _locales.FirstOrDefault(l => string.Equals(l.Tag, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        LanguageList.SelectedItem = match;
        if (!string.Equals(_settings.Language, UserSettings.SystemValue, StringComparison.OrdinalIgnoreCase))
        {
            _settings = _settings with { Language = normalized };
        }
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageList.SelectedItem is not LocaleOption option)
        {
            return;
        }

        _settings = _settings with { Language = option.Tag };
        AppResources.CurrentLocale = option.Tag;
        ApplyLocalization();
        UpdateStep();
    }

    private void UpdateStep()
    {
        bool isLanguageStep = _step == 0;
        LanguageStepPanel.Visibility = isLanguageStep ? Visibility.Visible : Visibility.Collapsed;
        ContentScroller.Visibility = isLanguageStep ? Visibility.Collapsed : Visibility.Visible;
        ThemeStepPanel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        WelcomeStepPanel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        TourStepPanel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        string locale = AppResources.CurrentLocale;
        StepCounterText.Text = AppResources.Format("Onboarding_StepCounter", locale, _step + 1, LastStepIndex + 1);
        BackButton.IsEnabled = _step > 0;
        NextButton.Content = _step == LastStepIndex
            ? AppResources.GetString("Onboarding_Finish", locale)
            : AppResources.GetString("Onboarding_Next", locale);
        SkipButton.Visibility = _step == LastStepIndex ? Visibility.Collapsed : Visibility.Visible;

        if (_step == 3)
        {
            UpdateCloudPathText();
        }
    }

    private void ApplyLocalization()
    {
        string locale = AppResources.CurrentLocale;
        string L(string key) => AppResources.GetString(key, locale);

        LanguageHeading.Text = L("Onboarding_LanguageHeading");
        LanguageSubheading.Text = L("Onboarding_LanguageSubheading");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LanguageList, L("Settings_LanguageLabel"));

        ThemeHeading.Text = L("Onboarding_ThemeHeading");
        ThemeSubheading.Text = L("Onboarding_ThemeSubheading");
        ThemeModeLabel.Text = L("Settings_ThemeLabel");
        ThemeSystemRadio.Content = L("Settings_SystemDefault");
        ThemeLightRadio.Content = L("Settings_Light");
        ThemeDarkRadio.Content = L("Settings_Dark");
        AccentLabel.Text = L("Settings_AccentLabel");
        AccentApplyButton.Content = L("Settings_AccentApply");
        AccentHint.Text = L("Settings_AccentHint");
        AccentHint.Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
        ScaleLabel.Text = L("Settings_ScaleLabel");

        WelcomeHeading.Text = L("Onboarding_WelcomeHeading");
        WelcomeBody.Text = L("Onboarding_WelcomeBody");
        WelcomePointOne.Text = L("Onboarding_WelcomePointOne");
        WelcomePointTwo.Text = L("Onboarding_WelcomePointTwo");
        WelcomePointThree.Text = L("Onboarding_WelcomePointThree");

        TourHeading.Text = L("Onboarding_TourHeading");
        TourSubheading.Text = L("Onboarding_TourSubheading");
        TourPointOne.Text = L("Onboarding_TourPointOne");
        TourPointTwo.Text = L("Onboarding_TourPointTwo");
        TourPointThree.Text = L("Onboarding_TourPointThree");
        TourPointFour.Text = L("Onboarding_TourPointFour");
        CloudToggle.Header = L("Onboarding_CloudToggle");
        CloudToggle.OnContent = L("Toggle_On");
        CloudToggle.OffContent = L("Toggle_Off");
        CloudDescription.Text = L("Onboarding_CloudDescription");

        SkipButton.Content = L("Onboarding_Skip");
        BackButton.Content = L("Onboarding_Back");
        StepCounterText.Text = AppResources.Format("Onboarding_StepCounter", locale, _step + 1, LastStepIndex + 1);
        NextButton.Content = _step == LastStepIndex
            ? L("Onboarding_Finish")
            : L("Onboarding_Next");
    }

    private void UpdateWelcomeMark()
    {
        string previewPath = AppearanceService.EnsureIconPreview(
            AppIconCatalog.Resolve(_settings.AppIconId),
            AppearanceService.ResolveAccentHex(_settings.AccentColor));
        if (string.IsNullOrEmpty(previewPath))
        {
            WelcomeMark.Visibility = Visibility.Collapsed;
            return;
        }

        WelcomeMark.Visibility = Visibility.Visible;
        WelcomeMark.Source = new BitmapImage(new Uri(previewPath, UriKind.Absolute));
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (_step > 0)
        {
            _step--;
            UpdateStep();
        }
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        if (_step < LastStepIndex)
        {
            _step++;
            UpdateStep();
            return;
        }

        Finish();
    }

    private void OnSkipClicked(object sender, RoutedEventArgs e) => Finish();

    private async void OnCloudToggleToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        bool enabled = CloudToggle.IsOn;
        _settings = _settings with
        {
            CloudSyncEnabled = enabled,
            CloudSyncProvider = enabled
                ? (_settings.CloudSyncProvider == nameof(CloudSyncProviderKind.None)
                    ? nameof(CloudSyncProviderKind.OneDrive)
                    : _settings.CloudSyncProvider)
                : nameof(CloudSyncProviderKind.None)
        };

        if (enabled)
        {
            await ConnectCloudAsync().ConfigureAwait(true);
        }

        UpdateCloudPathText();
    }

    private async Task ConnectCloudAsync()
    {
        CloudSyncProviderKind kind = CloudSyncService.ParseProvider(_settings.CloudSyncProvider);
        if (kind == CloudSyncProviderKind.None)
        {
            CloudPathText.Text = AppResources.GetString("Settings_CloudNeedProvider", AppResources.CurrentLocale);
            return;
        }

        CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
        string locale = AppResources.CurrentLocale;

        CloudSyncReadResult existing = await CloudSyncService.ReadSnapshotAsync(location).ConfigureAwait(true);
        if (existing.HasSnapshot && _settings.IsDefault())
        {
            // A real backup exists while local data is still pristine: never overwrite it.
            CloudPathText.Text = AppResources.GetString("Settings_CloudBackupFound", locale);
            return;
        }

        CloudSyncSnapshot snapshot = await CloudSyncService.BuildSnapshotAsync(
            _storageService,
            kind,
            AppInfo.Version,
            DateTimeOffset.UtcNow,
            _settings).ConfigureAwait(true);

        CloudSyncWriteResult result = await _cloudSync
            .WriteSnapshotAsync(location, snapshot, allowEmptyOverwrite: true)
            .ConfigureAwait(true);

        if (result.Success)
        {
            _settings = _settings with { CloudSyncLastSyncUtc = DateTimeOffset.UtcNow.ToString("O") };
            CloudPathText.Text = AppResources.Format("Settings_CloudConnectedTo", locale, location.RootPath);
        }
        else
        {
            CloudPathText.Text = AppResources.GetString("Settings_CloudStatusFailed", locale);
        }
    }

    private void UpdateCloudPathText()
    {
        string locale = AppResources.CurrentLocale;
        if (!CloudToggle.IsOn)
        {
            CloudPathText.Text = string.Empty;
            return;
        }

        CloudSyncLocation location = _cloudSync.ResolveLocation(_settings.CloudSyncProvider, _settings.CloudSyncRoot);
        CloudPathText.Text = location.IsFallback
            ? AppResources.Format("Settings_CloudFallbackPath", locale, location.RootPath)
            : AppResources.Format("Settings_CloudConnectedTo", locale, location.RootPath);
    }

    private async void Finish()
    {
        _settings = _settings with { OnboardingCompleted = true };

        try
        {
            await _storageService.SaveUserSettingsAsync(_settings).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            App.Log($"Onboarding settings save failed: {ex.Message}");
        }

        Close();
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
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            App.Log($"Onboarding icon apply failed: {ex.Message}");
        }
    }

    private sealed record LocaleOption(string Tag, string NativeName)
    {
        public override string ToString() => NativeName;
    }
}