namespace Ornare.App;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Ornare.Storage;
using Ornare.Windows;
using WinRT.Interop;

public sealed partial class ManagerWindow : Window
{
    private readonly IStorageService _storageService;
    private readonly IFolderCustomizer _folderCustomizer;
    private readonly IImagePipeline _imagePipeline;
    private readonly ObservableCollection<LibraryItemVm> _libraryItems = new();
    private readonly ObservableCollection<RecentItemVm> _recentItems = new();
    private readonly ObservableCollection<LibraryItemVm> _favoriteItems = new();
    private readonly ObservableCollection<FolderItemVm> _folderItems = new();
    private readonly ObservableCollection<PresetItemVm> _presetItems = new();
    private string _selectedPresetTemplateId = FolderTemplateCatalog.DefaultTemplateId;
    private string? _selectedFilterTagId;

    private List<LibraryAsset> _allAssets = new();
    private string _currentLanguage = "System";
    private string _currentTheme = "System";
    private bool _isLoaded;

    public ManagerWindow(IStorageService storageService, IFolderCustomizer folderCustomizer, IImagePipeline? imagePipeline = null)
    {
        _storageService = storageService;
        _folderCustomizer = folderCustomizer;
        _imagePipeline = imagePipeline ?? new ImagePipeline();

        InitializeComponent();
        ConfigureWindow();

        SettingsArchValue.Text = RuntimeInformation.ProcessArchitecture.ToString();

        _ = LoadInitialDataAsync();
    }

    private static void SyncItems<T>(ItemsControl control, IEnumerable<T> items)
    {
        control.Items.Clear();
        foreach (var item in items)
        {
            control.Items.Add(item);
        }
    }

    private void ConfigureWindow()
    {
        Title = "Ornare";
        SystemBackdrop = new MicaBackdrop();

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        // Effective target: 920x640 per Visual Reference Spec
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(920, 640));

        string icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(icoPath))
        {
            appWindow.SetIcon(icoPath);
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBarDragArea);

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = global::Windows.UI.Color.FromArgb(20, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = global::Windows.UI.Color.FromArgb(40, 255, 255, 255);
            titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            var settings = await _storageService.GetUserSettingsAsync();
            _currentTheme = settings.Theme;
            _currentLanguage = settings.Language;

            ApplyTheme(_currentTheme);
            ApplyLanguage(_currentLanguage);

            if (string.Equals(_currentTheme, "Light", StringComparison.OrdinalIgnoreCase)) ThemeComboBox.SelectedIndex = 1;
            else if (string.Equals(_currentTheme, "Dark", StringComparison.OrdinalIgnoreCase)) ThemeComboBox.SelectedIndex = 2;
            else ThemeComboBox.SelectedIndex = 0;

            if (string.Equals(_currentLanguage, "tr-TR", StringComparison.OrdinalIgnoreCase) || string.Equals(_currentLanguage, "tr", StringComparison.OrdinalIgnoreCase)) LanguageComboBox.SelectedIndex = 1;
            else if (string.Equals(_currentLanguage, "en-US", StringComparison.OrdinalIgnoreCase) || string.Equals(_currentLanguage, "en", StringComparison.OrdinalIgnoreCase)) LanguageComboBox.SelectedIndex = 2;
            else LanguageComboBox.SelectedIndex = 0;

            await RefreshAllDataAsync();
            _isLoaded = true;
            NavListView.SelectedIndex = 0;
            App.Log("LoadInitialDataAsync completed successfully.");
        }
        catch (Exception ex)
        {
            App.Log($"EXCEPTION in LoadInitialDataAsync: {ex}");
        }
    }

    public async Task RefreshAllDataAsync()
    {
        await LoadLibraryAsync();
        await LoadRecentAsync();
        await LoadFavoritesAsync();
        await LoadFoldersAsync();
    }

    private async Task LoadLibraryAsync()
    {
        if (!string.IsNullOrEmpty(_selectedFilterTagId))
        {
            _allAssets = (await _storageService.GetAssetsByTagAsync(_selectedFilterTagId)).ToList();
        }
        else
        {
            _allAssets = (await _storageService.GetAllAssetsAsync()).ToList();
        }

        var favIds = (await _storageService.GetFavoritesAsync()).Select(f => f.Sha256).ToHashSet();

        _libraryItems.Clear();
        foreach (var asset in _allAssets)
        {
            _libraryItems.Add(CreateLibraryVm(asset, favIds.Contains(asset.Sha256)));
        }
        SyncItems(LibraryGridView, _libraryItems);

        LibraryEmptyPanel.Visibility = _libraryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LibraryGridView.Visibility = _libraryItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Progressive search box disclosure: only when >= 13 assets per Visual Reference Spec
        SearchTextBox.Visibility = _allAssets.Count >= 13 ? Visibility.Visible : Visibility.Collapsed;

        UpdateSubtitle();
        await RefreshTagFilterBarAsync();
    }

    private async Task LoadRecentAsync()
    {
        var recents = await _storageService.GetRecentAssetsAsync(20);
        _recentItems.Clear();

        foreach (var r in recents)
        {
            _recentItems.Add(new RecentItemVm
            {
                Sha256 = r.Sha256,
                PreviewImage = LoadBitmapSafe(r.CanonicalIcoPath),
                TimeAgo = FormatTimeAgo(r.LastUsedAt)
            });
        }
        SyncItems(RecentGridView, _recentItems);

        RecentEmptyPanel.Visibility = _recentItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentGridView.Visibility = _recentItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadFavoritesAsync()
    {
        var favs = await _storageService.GetFavoritesAsync();
        _favoriteItems.Clear();

        foreach (var f in favs)
        {
            _favoriteItems.Add(CreateLibraryVm(f, isFavorite: true));
        }
        SyncItems(FavoritesGridView, _favoriteItems);

        FavoritesEmptyPanel.Visibility = _favoriteItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FavoritesGridView.Visibility = _favoriteItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadFoldersAsync()
    {
        var assignments = await _storageService.GetAllFolderAssignmentsAsync();
        _folderItems.Clear();

        foreach (var a in assignments)
        {
            string folderName = Path.GetFileName(a.FolderPath.TrimEnd('\\', '/'));
            string iconPath = Path.Combine(a.FolderPath, OrnareConstants.CanonicalFolderName, OrnareConstants.CustomIconFileName);

            // Read-only health check
            var health = FolderHealthEngine.CheckHealth(a.FolderPath, a);

            string glyph;
            SolidColorBrush brush;
            string label;
            Visibility repairVis = Visibility.Collapsed;
            Visibility keepExtVis = Visibility.Collapsed;

            switch (health.State)
            {
                case FolderCustomizationState.Healthy:
                    glyph = "\uE73E"; // Checkmark
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 16, 137, 62));
                    label = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "Sağlıklı" : "Healthy";
                    break;
                case FolderCustomizationState.NeedsReconciliation:
                case FolderCustomizationState.ConfigMismatch:
                case FolderCustomizationState.MissingOwnedIcon:
                    glyph = "\uE7BA"; // Warning
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 202, 138, 4));
                    label = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "İlgi Gerektiriyor" : "Needs Attention";
                    repairVis = Visibility.Visible;
                    break;
                case FolderCustomizationState.ExternalIconConflict:
                    glyph = "\uE783"; // Alert
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 217, 119, 6));
                    label = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "Dış Değişiklik Çakışması" : "External Icon Conflict";
                    keepExtVis = Visibility.Visible;
                    break;
                default:
                    glyph = "\uE711"; // Error
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 220, 38, 38));
                    label = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "Klasör Bulunamadı" : "Folder Missing";
                    break;
            }

            _folderItems.Add(new FolderItemVm
            {
                FolderPath = a.FolderPath,
                FolderName = folderName,
                IconPreview = LoadBitmapSafe(iconPath),
                HealthStatus = health.State,
                HealthGlyph = glyph,
                HealthBrush = brush,
                HealthLabel = label,
                RepairButtonVisibility = repairVis,
                KeepExternalButtonVisibility = keepExtVis,
                ExpectedAssetHash = a.CurrentAssetHash
            });
        }
        SyncItems(FoldersListView, _folderItems);

        FoldersEmptyPanel.Visibility = _folderItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FoldersListView.Visibility = _folderItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private LibraryItemVm CreateLibraryVm(LibraryAsset asset, bool isFavorite)
    {
        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        string favToolTip = isFavorite
            ? (isTr ? "Favorilerden Kaldır" : "Remove from Favorites")
            : (isTr ? "Favorilere Ekle" : "Add to Favorites");
        string toggleText = isFavorite
            ? (isTr ? "Favorilerden Kaldır" : "Remove from Favorites")
            : (isTr ? "Favorilere Ekle" : "Add to Favorites");
        string explorerText = isTr ? "Dosya Gezgini'nde Göster" : "Show in File Explorer";
        string manageTagsText = isTr ? "Etiketleri Yönet..." : "Manage Tags...";
        string exportText = isTr ? "Dışa Aktar (.ico)..." : "Export Icon (.ico)...";
        string exportPngText = isTr ? "Dışa Aktar (.png)" : "Export PNG (.png)";

        return new LibraryItemVm
        {
            Sha256 = asset.Sha256,
            CanonicalIcoPath = asset.CanonicalIcoPath,
            SourceFormat = asset.SourceFormat.ToUpperInvariant(),
            PreviewImage = LoadBitmapSafe(asset.CanonicalIcoPath),
            IsFavorite = isFavorite,
            FavoriteGlyph = isFavorite ? "\uE735" : "\uE734",
            FavoriteBrush = isFavorite
                ? new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 234, 179, 8))
                : new SolidColorBrush(global::Windows.UI.Color.FromArgb(220, 200, 200, 200)),
            FavoriteToolTip = favToolTip,
            ContextToggleFavoriteText = toggleText,
            ContextOpenExplorerText = explorerText,
            ContextManageTagsText = manageTagsText,
            ContextExportIcoText = exportText,
            ContextExportPngText = exportPngText
        };
    }

    private static BitmapImage? LoadBitmapSafe(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new BitmapImage(new Uri(path));
            }
        }
        catch
        {
            // Fallback gracefully on unreadable file
        }
        return null;
    }

    private static string FormatTimeAgo(DateTimeOffset time)
    {
        TimeSpan diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        if (NavListView.SelectedItem is ListViewItem item && item.Tag is string tag)
        {
            SwitchSection(tag);
        }
    }

    public void SwitchSection(string tag)
    {
        if (LibraryView == null || RecentView == null || FavoritesView == null || FoldersView == null || SettingsView == null || SectionTitleText == null)
            return;

        LibraryView.Visibility = Visibility.Collapsed;
        RecentView.Visibility = Visibility.Collapsed;
        FavoritesView.Visibility = Visibility.Collapsed;
        if (PresetsView != null) PresetsView.Visibility = Visibility.Collapsed;
        FoldersView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;

        SearchTextBox.Visibility = Visibility.Collapsed;
        ClearRecentButton.Visibility = Visibility.Collapsed;
        if (ImportIconButton != null) ImportIconButton.Visibility = Visibility.Collapsed;

        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

        switch (tag)
        {
            case "Library":
                LibraryView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Kitaplık" : "Library";
                if (ImportIconButton != null) ImportIconButton.Visibility = Visibility.Visible;
                if (_allAssets.Count >= 13) SearchTextBox.Visibility = Visibility.Visible;
                break;
            case "Recent":
                RecentView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Son Kullanılanlar" : "Recent";
                ClearRecentButton.Visibility = _recentItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "Favorites":
                FavoritesView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Favoriler" : "Favorites";
                break;
            case "Presets":
                if (PresetsView != null)
                {
                    PresetsView.Visibility = Visibility.Visible;
                    SectionTitleText.Text = isTr ? "Durum Şablonları" : "Status Presets";
                    LoadPresetsView();
                }
                break;
            case "Folders":
                FoldersView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Özelleştirilmiş Klasörler" : "Customized Folders";
                break;
            case "Settings":
                SettingsView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Ayarlar" : "Settings";
                SettingsView.ChangeView(null, 0, null, true);
                break;
        }

        UpdateSubtitle();
    }

    private void UpdateSubtitle()
    {
        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        if (LibraryView.Visibility == Visibility.Visible)
        {
            SectionSubtitleText.Text = _libraryItems.Count == 1
                ? (isTr ? "1 simge" : "1 icon")
                : (isTr ? $"{_libraryItems.Count} simge" : $"{_libraryItems.Count} icons");
        }
        else if (FoldersView.Visibility == Visibility.Visible)
        {
            SectionSubtitleText.Text = _folderItems.Count == 1
                ? (isTr ? "1 klasör" : "1 folder")
                : (isTr ? $"{_folderItems.Count} klasör" : $"{_folderItems.Count} folders");
        }
        else if (PresetsView != null && PresetsView.Visibility == Visibility.Visible)
        {
            SectionSubtitleText.Text = isTr ? "İş akışı ve durum için simgeler" : "Semantic workflow and status icons";
        }
        else
        {
            SectionSubtitleText.Text = string.Empty;
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchTextBox.Text?.Trim() ?? string.Empty;
        var favIds = (await _storageService.GetFavoritesAsync()).Select(f => f.Sha256).ToHashSet();
        if (string.IsNullOrEmpty(query))
        {
            _libraryItems.Clear();
            foreach (var a in _allAssets)
            {
                _libraryItems.Add(CreateLibraryVm(a, favIds.Contains(a.Sha256)));
            }
        }
        else
        {
            _libraryItems.Clear();
            foreach (var a in _allAssets.Where(a => a.Sha256.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                                   a.SourceFormat.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                _libraryItems.Add(CreateLibraryVm(a, favIds.Contains(a.Sha256)));
            }
        }
        SyncItems(LibraryGridView, _libraryItems);
        UpdateSubtitle();
    }

    private async void OnToggleFavoriteClicked(object sender, RoutedEventArgs e)
    {
        string? sha256 = null;
        if (sender is Button btn && btn.Tag is string s1) sha256 = s1;
        else if (sender is MenuFlyoutItem mfi && mfi.Tag is string s2) sha256 = s2;

        if (!string.IsNullOrEmpty(sha256))
        {
            var favs = await _storageService.GetFavoritesAsync();
            bool isFav = favs.Any(f => f.Sha256 == sha256);
            await _storageService.SetFavoriteAsync(sha256, !isFav);

            await LoadLibraryAsync();
            await LoadFavoritesAsync();
        }
    }

    private void OnOpenIcoInExplorerClicked(object sender, RoutedEventArgs e)
    {
        string? path = null;
        if (sender is MenuFlyoutItem mfi && mfi.Tag is string p)
        {
            path = p;
        }

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch { }
        }
    }

    private async void OnClearRecentClicked(object sender, RoutedEventArgs e)
    {
        await _storageService.ClearRecentHistoryAsync();
        await LoadRecentAsync();
        ClearRecentButton.Visibility = Visibility.Collapsed;
    }

    private async void OnRepairFolderClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string folderPath)
        {
            var folderVm = _folderItems.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folderVm == null) return;

            string canonicalIco = string.Empty;
            if (!string.IsNullOrEmpty(folderVm.ExpectedAssetHash))
            {
                var asset = await _storageService.GetAssetByHashAsync(folderVm.ExpectedAssetHash);
                if (asset != null) canonicalIco = asset.CanonicalIcoPath;
            }

            var expectedSemantics = new FolderIconSemantics
            {
                IconResource = @$"{OrnareConstants.CanonicalFolderName}\{OrnareConstants.CustomIconFileName},0"
            };

            var res = RepairEngine.RepairFolder(folderPath, canonicalIco, forceExternalConflictResolution: false);
            if (res.IsSuccess)
            {
                await LoadFoldersAsync();
            }
        }
    }

    private async void OnKeepExternalClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string folderPath)
        {
            await _storageService.UpdateAssignmentHealthAsync(folderPath, FolderCustomizationState.Healthy, conflictState: null);
            await LoadFoldersAsync();
        }
    }

    private async void OnReapplyFolderClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string folderPath)
        {
            var folderVm = _folderItems.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folderVm == null) return;

            string canonicalIco = string.Empty;
            if (!string.IsNullOrEmpty(folderVm.ExpectedAssetHash))
            {
                var asset = await _storageService.GetAssetByHashAsync(folderVm.ExpectedAssetHash);
                if (asset != null) canonicalIco = asset.CanonicalIcoPath;
            }

            var res = RepairEngine.RepairFolder(folderPath, canonicalIco, forceExternalConflictResolution: true);
            if (res.IsSuccess)
            {
                await _storageService.UpdateAssignmentHealthAsync(folderPath, FolderCustomizationState.Healthy, conflictState: null);
                await LoadFoldersAsync();
            }
        }
    }

    private void OnOpenFolderInExplorerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string folderPath && Directory.Exists(folderPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private async void OnExportDiagnosticReportClicked(object sender, RoutedEventArgs e)
    {
        var settings = await _storageService.GetUserSettingsAsync();
        var assignments = await _storageService.GetAllFolderAssignmentsAsync();
        var healthCounts = new Dictionary<string, int>
        {
            ["Healthy"] = _folderItems.Count(f => f.HealthStatus == FolderCustomizationState.Healthy),
            ["NeedsAttention"] = _folderItems.Count(f => f.HealthStatus == FolderCustomizationState.NeedsReconciliation || f.HealthStatus == FolderCustomizationState.MissingOwnedIcon || f.HealthStatus == FolderCustomizationState.ConfigMismatch),
            ["ExternalConflict"] = _folderItems.Count(f => f.HealthStatus == FolderCustomizationState.ExternalIconConflict),
            ["MissingTarget"] = _folderItems.Count(f => f.HealthStatus == FolderCustomizationState.MissingTarget)
        };

        var report = DiagnosticExportService.BuildReport(
            settings,
            assignments.Count,
            healthCounts,
            _allAssets.Count,
            _recentItems.Count);

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string reportDir = Path.Combine(localAppData, OrnareConstants.ApplicationName, "diagnostics");
        Directory.CreateDirectory(reportDir);
        string reportFile = Path.Combine(reportDir, $"ornare-diag-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");

        string json = System.Text.Json.JsonSerializer.Serialize(report, FolderJsonContext.Default.DiagnosticReport);
        await File.WriteAllTextAsync(reportFile, json);

        ExportReportButton.Content = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "Rapor Kaydedildi!" : "Report Exported!";
    }

    private async void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            _currentTheme = theme;
            ApplyTheme(theme);
            await _storageService.SaveUserSettingsAsync(new UserSettings { Theme = _currentTheme, Language = _currentLanguage });
        }
    }

    private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            _currentLanguage = lang;
            ApplyLanguage(lang);
            await _storageService.SaveUserSettingsAsync(new UserSettings { Theme = _currentTheme, Language = _currentLanguage });
        }
    }

    public void ApplyTheme(string theme)
    {
        if (Content is FrameworkElement root)
        {
            if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
            {
                root.RequestedTheme = ElementTheme.Light;
            }
            else if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                root.RequestedTheme = ElementTheme.Dark;
            }
            else
            {
                root.RequestedTheme = ElementTheme.Default;
            }
        }
        UpdateTitleBarTheme();
    }

    private void UpdateTitleBarTheme()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero) return;
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow != null && AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = appWindow.TitleBar;
            bool isDark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
            var fgColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonForegroundColor = fgColor;
            titleBar.ButtonHoverForegroundColor = fgColor;
            titleBar.ButtonInactiveForegroundColor = isDark ? global::Windows.UI.Color.FromArgb(128, 255, 255, 255) : global::Windows.UI.Color.FromArgb(128, 0, 0, 0);
        }
    }

    public void ApplyLanguage(string lang)
    {
        bool isTr = string.Equals(lang, "tr-TR", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(lang, "tr", StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(lang, "System", StringComparison.OrdinalIgnoreCase) && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase));

        _currentLanguage = isTr ? "tr-TR" : "en-US";

        NavTextLibrary.Text = isTr ? "Kitaplık" : "Library";
        NavTextRecent.Text = isTr ? "Son Kullanılanlar" : "Recent";
        NavTextFavorites.Text = isTr ? "Favoriler" : "Favorites";
        if (NavTextPresets != null) NavTextPresets.Text = isTr ? "Durum Şablonları" : "Status Presets";
        NavTextFolders.Text = isTr ? "Özelleştirilmiş Klasörler" : "Customized Folders";
        NavTextSettings.Text = isTr ? "Ayarlar" : "Settings";

        if (PresetTemplateLabel != null) PresetTemplateLabel.Text = isTr ? "Stil Şablonu:" : "Style Template:";
        if (PresetRadioClassic != null) PresetRadioClassic.Content = isTr ? "Klasik" : "Classic";
        if (PresetRadioSoft != null) PresetRadioSoft.Content = isTr ? "Yumuşak" : "Soft";
        if (PresetRadioFlat != null) PresetRadioFlat.Content = isTr ? "Düz" : "Flat";
        if (PresetDisclaimerText != null)
        {
            PresetDisclaimerText.Text = isTr
                ? "Öncelik şablonları klasörleri görsel olarak düzenlemek içindir; Windows dosya sistemi önceliğini veya Gezgin sıralamasını değiştirmez."
                : "Priority presets provide visual folder badges for organization and do not alter Windows filesystem priority or shell sorting.";
        }

        LibraryEmptyTitle.Text = isTr ? "Kitaplıkta simge yok" : "No icons in library";
        LibraryEmptySubtitle.Text = isTr ? "Özelleştirdiğiniz simgeler burada görünecektir" : "Icons you customize will appear here";
        RecentEmptyTitle.Text = isTr ? "Son kullanılan simge yok" : "No recent icons";
        FavoritesEmptyTitle.Text = isTr ? "Favori simge yok" : "No favorite icons";
        FavoritesEmptySubtitle.Text = isTr
            ? "Kitaplık'taki simgelerin üzerindeki yıldıza tıklayarak veya sağ tıklayıp 'Favorilere Ekle'yi seçerek ekleyebilirsiniz."
            : "Click the star on any icon in the Library or right-click and select 'Add to Favorites'.";
        FoldersEmptyTitle.Text = isTr ? "Özelleştirilmiş klasör yok" : "No customized folders";

        SettingsAppearanceHeader.Text = isTr ? "Görünüm" : "Appearance";
        SettingsThemeLabel.Text = isTr ? "Uygulama teması" : "App theme";
        ThemeItemSystem.Content = isTr ? "Sistem varsayılanı" : "System default";
        ThemeItemLight.Content = isTr ? "Açık" : "Light";
        ThemeItemDark.Content = isTr ? "Koyu" : "Dark";

        SettingsLanguageHeader.Text = isTr ? "Dil" : "Language";
        SettingsLanguageLabel.Text = isTr ? "Uygulama dili" : "App language";
        LangItemSystem.Content = isTr ? "Sistem varsayılanı" : "System default";

        SettingsDiagnosticsHeader.Text = isTr ? "Tanılama" : "Diagnostics";
        SettingsDiagnosticsLabel.Text = isTr ? "Tanılama Raporu" : "Diagnostic Report";
        SettingsDiagnosticsSubtext.Text = isTr ? "Kişisel yollar gizlenmiş sistem ve sağlık raporunu dışa aktarır." : "Exports system and health status with personal paths anonymized.";
        ExportReportButton.Content = isTr ? "Tanılama Raporunu Dışa Aktar" : "Export Diagnostic Report";
        SettingsAboutHeader.Text = isTr ? "Hakkında" : "About";
        SettingsPublisherLabel.Text = isTr ? "Geliştirici & Yayıncı" : "Developer & Publisher";
        SettingsWebsiteLabel.Text = isTr ? "Web Sitesi" : "Website";
        SettingsVersionLabel.Text = isTr ? "Sürüm" : "Version";
        SettingsArchLabel.Text = isTr ? "Mimari" : "Architecture";
        SettingsPrivacyStatement.Text = isTr ? "Ornare tamamen çevrimdışı çalışır. Sıfır telemetri, sıfır bulut takibi, sıfır veri toplama." : "Ornare works completely offline. Zero telemetry, zero cloud tracking, zero data collection.";
        ClearRecentButton.Content = isTr ? "Son Kullanılanları Temizle" : "Clear Recent";
        SearchTextBox.PlaceholderText = isTr ? "Kitaplıkta ara..." : "Search library...";
        if (ImportIconButton != null) ImportIconButton.Content = isTr ? "Simge İçe Aktar..." : "Import Icon...";

        if (NavListView.SelectedItem is ListViewItem cur)
        {
            SwitchSection((string)cur.Tag);
        }
    }

    public void ApplyVisualTestState(string? section, string? theme, string? lang)
    {
        if (!string.IsNullOrEmpty(theme))
        {
            ApplyTheme(theme);
        }
        if (!string.IsNullOrEmpty(lang))
        {
            ApplyLanguage(lang);
        }
        if (!string.IsNullOrEmpty(section))
        {
            SwitchSection(section);
            // Also select in listview
            foreach (var item in NavListView.Items.OfType<ListViewItem>())
            {
                if (string.Equals(item.Tag as string, section, StringComparison.OrdinalIgnoreCase))
                {
                    NavListView.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void OnLibraryItemClick(object sender, ItemClickEventArgs e)
    {
        // Reserved for future selection/details if needed
    }

    private async void OnImportIconClicked(object sender, RoutedEventArgs e)
    {
        var picker = new global::Windows.Storage.Pickers.FileOpenPicker();
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = global::Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".ico");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".bmp");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(file.Path);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string iconsDir = Path.Combine(appData, "Ornare", "icons");
            Directory.CreateDirectory(iconsDir);

            string ext = Path.GetExtension(file.Path).ToLowerInvariant();
            if (ext == ".ico" || (fileBytes.Length >= 4 && fileBytes[0] == 0 && fileBytes[1] == 0 && fileBytes[2] == 1 && fileBytes[3] == 0))
            {
                string sha256 = IcoImportService.ComputeSourceHash(fileBytes);
                string canonicalIco = Path.Combine(iconsDir, $"{sha256}.ico");

                var importResult = _imagePipeline.ImportIco(fileBytes, canonicalIco);
                if (importResult.IsSuccess && importResult.Value != null)
                {
                    await _storageService.AddOrUpdateAssetAsync(
                        importResult.Value.CanonicalHash,
                        importResult.Value.CanonicalPath,
                        "ico");
                    await LoadLibraryAsync();
                }
                else
                {
                    App.Log($"ICO import failed: {importResult.ErrorMessage}");
                }
            }
            else
            {
                string sha256 = _imagePipeline.ComputeSourceHash(fileBytes);
                string canonicalIco = Path.Combine(iconsDir, $"{sha256}.ico");
                var result = _imagePipeline.ProcessImageBytes(fileBytes, canonicalIco);
                if (result.IsSuccess && result.Value != null)
                {
                    await _storageService.AddOrUpdateAssetAsync(
                        result.Value.ContentHash,
                        result.Value.GeneratedIcoPath,
                        result.Value.SourceFormat);
                    await LoadLibraryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"Import icon failed: {ex.Message}");
        }
    }

    private async void OnExportIcoClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not string canonicalIcoPath || string.IsNullOrEmpty(canonicalIcoPath))
        {
            return;
        }

        if (!File.Exists(canonicalIcoPath))
        {
            return;
        }

        var picker = new global::Windows.Storage.Pickers.FileSavePicker();
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        picker.FileTypeChoices.Add("Windows Icon", new List<string>() { ".ico" });
        picker.DefaultFileExtension = ".ico";
        picker.SuggestedFileName = "icon.ico";

        var file = await picker.PickSaveFileAsync();
        if (file == null)
        {
            // Cancel performs zero writes
            return;
        }

        var exportResult = AtomicExportWriter.ExportAtomically(
            canonicalIcoPath,
            file.Path,
            customValidator: path => _imagePipeline.IsValidIco(path, expectedMinFrames: 10));

        if (!exportResult.IsSuccess)
        {
            App.Log($"Atomic export failed: {exportResult.ErrorMessage}");
        }
    }

    private async void OnExportPngClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item)
        {
            return;
        }

        int targetSize = ExportPngConstants.DefaultSize;
        if (item.Tag is string sizeStr && int.TryParse(sizeStr, out int parsedSize) && ExportPngConstants.IsAllowedSize(parsedSize))
        {
            targetSize = parsedSize;
        }

        var vm = (item.DataContext as LibraryItemVm) ?? ((item.Parent as FrameworkElement)?.DataContext as LibraryItemVm);
        string? canonicalIcoPath = vm?.CanonicalIcoPath ?? ((item.Parent as MenuFlyoutSubItem)?.Tag as string);

        if (string.IsNullOrEmpty(canonicalIcoPath) || !File.Exists(canonicalIcoPath))
        {
            return;
        }

        var picker = new global::Windows.Storage.Pickers.FileSavePicker();
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        picker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
        picker.DefaultFileExtension = ".png";
        picker.SuggestedFileName = $"icon_{targetSize}.png";

        var file = await picker.PickSaveFileAsync();
        if (file == null)
        {
            // Cancel performs zero writes
            return;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string cacheDir = Path.Combine(appData, "Ornare", "cache");

        byte[]? sourceBytes = null;
        try
        {
            sourceBytes = await File.ReadAllBytesAsync(canonicalIcoPath);
        }
        catch (Exception ex)
        {
            App.Log($"Failed to read icon bytes for PNG export: {ex.Message}");
            return;
        }

        string sourceHash = vm?.Sha256 ?? _imagePipeline.ComputeSourceHash(sourceBytes);
        var recipe = new RenderRecipe
        {
            SourceAssetHash = sourceHash,
            PlacementMode = (int)ImagePlacementMode.Contain,
            PaddingPercent = 0,
            BackgroundMode = (int)BackgroundMode.Transparent,
            TargetFormat = "png",
            TargetFrameSizes = [targetSize]
        };

        var renderResult = _imagePipeline.RenderPngWithCache(
            recipe,
            sourceBytes,
            targetSize,
            cacheDirectory: cacheDir,
            cacheLookup: key => _storageService.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
            cacheStore: entry => _storageService.SaveRenderCacheEntryAsync(entry).GetAwaiter().GetResult());

        if (!renderResult.IsSuccess || renderResult.Value == null)
        {
            App.Log($"PNG render failed: {renderResult.ErrorMessage}");
            return;
        }

        var exportResult = AtomicExportWriter.ExportPngAtomically(
            renderResult.Value.PngBytes,
            file.Path,
            targetSize,
            isFromCache: renderResult.Value.IsFromCache,
            renderKey: renderResult.Value.RenderKey);

        if (!exportResult.IsSuccess)
        {
            App.Log($"Atomic PNG export failed: {exportResult.ErrorMessage}");
        }
    }

    #region F15: Library Tags UI

    private async Task RefreshTagFilterBarAsync()
    {
        if (_storageService == null) return;

        var tags = await _storageService.GetAllTagsAsync();
        if (tags.Count == 0 && string.IsNullOrEmpty(_selectedFilterTagId))
        {
            TagFilterScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        TagFilterScrollViewer.Visibility = Visibility.Visible;
        TagFilterBar.Children.Clear();

        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        string allLabel = isTr ? "Tümü" : "All";

        // "All" pill
        var allBtn = CreateFilterPill(allLabel, null, _selectedFilterTagId == null);
        TagFilterBar.Children.Add(allBtn);

        foreach (var tag in tags)
        {
            var btn = CreateFilterPill(tag.DisplayName, tag.Id, _selectedFilterTagId == tag.Id);
            TagFilterBar.Children.Add(btn);
        }
    }

    private Button CreateFilterPill(string label, string? tagId, bool isSelected)
    {
        var btn = new Button
        {
            Content = label,
            Tag = tagId,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 4, 12, 4),
            FontSize = 12,
            FontWeight = isSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
        };

        if (isSelected)
        {
            if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? accentBrush))
            {
                btn.Background = (Brush)accentBrush;
            }
            if (Application.Current.Resources.TryGetValue("TextOnAccentFillColorPrimaryBrush", out object? textAccentBrush))
            {
                btn.Foreground = (Brush)textAccentBrush;
            }
        }
        else
        {
            if (Application.Current.Resources.TryGetValue("SubtleFillColorSecondaryBrush", out object? subtleBrush))
            {
                btn.Background = (Brush)subtleBrush;
            }
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? textBrush))
            {
                btn.Foreground = (Brush)textBrush;
            }
        }

        btn.Click += async (s, e) =>
        {
            _selectedFilterTagId = tagId;
            await LoadLibraryAsync();
        };
        return btn;
    }

    private async void OnManageTagsClicked(object sender, RoutedEventArgs e)
    {
        if (_storageService == null) return;
        string? sha256 = null;
        if (sender is MenuFlyoutItem mfi && mfi.Tag is string s) sha256 = s;
        if (string.IsNullOrEmpty(sha256)) return;

        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

        var dialog = new ContentDialog
        {
            Title = isTr ? "Varlık Etiketlerini Yönet" : "Manage Asset Tags",
            CloseButtonText = isTr ? "Kapat" : "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var rootPanel = new StackPanel { Spacing = 12, Width = 360 };

        var tagsWrap = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var tagsScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = tagsWrap
        };

        var noTagsText = new TextBlock
        {
            Text = isTr ? "Bu varlığa henüz bir etiket atanmamış." : "No tags assigned to this asset yet.",
            FontSize = 12,
            Visibility = Visibility.Collapsed
        };
        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secTextBrush))
        {
            noTagsText.Foreground = (Brush)secTextBrush;
        }

        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = new TextBox
        {
            PlaceholderText = isTr ? "Yeni etiket (1-40 karakter)..." : "Add tag (1-40 characters)...",
            MaxLength = 40
        };
        Grid.SetColumn(textBox, 0);

        var addBtn = new Button
        {
            Content = isTr ? "Ekle" : "Add",
            Margin = new Thickness(8, 0, 0, 0)
        };
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out object? accentStyle))
        {
            addBtn.Style = (Style)accentStyle;
        }
        Grid.SetColumn(addBtn, 1);

        inputGrid.Children.Add(textBox);
        inputGrid.Children.Add(addBtn);

        var errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 239, 68, 68)),
            FontSize = 12,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap
        };

        rootPanel.Children.Add(tagsScroll);
        rootPanel.Children.Add(noTagsText);
        rootPanel.Children.Add(inputGrid);
        rootPanel.Children.Add(errorText);
        dialog.Content = rootPanel;

        async Task RefreshDialogTagsAsync()
        {
            tagsWrap.Children.Clear();
            var currentTags = await _storageService.GetTagsForAssetAsync(sha256);
            if (currentTags.Count == 0)
            {
                noTagsText.Visibility = Visibility.Visible;
                tagsScroll.Visibility = Visibility.Collapsed;
            }
            else
            {
                noTagsText.Visibility = Visibility.Collapsed;
                tagsScroll.Visibility = Visibility.Visible;
                foreach (var t in currentTags)
                {
                    var pill = new Border
                    {
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(8, 3, 4, 3),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 4, 4)
                    };
                    if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? cardBg))
                    {
                        pill.Background = (Brush)cardBg;
                    }
                    if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out object? cardStroke))
                    {
                        pill.BorderBrush = (Brush)cardStroke;
                    }

                    var pillStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    pillStack.Children.Add(new TextBlock { Text = t.DisplayName, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                    var removeBtn = new Button
                    {
                        Padding = new Thickness(0),
                        Width = 20,
                        Height = 20,
                        CornerRadius = new CornerRadius(10),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Content = new FontIcon { Glyph = "\uE711", FontSize = 10 }
                    };
                    removeBtn.Click += async (s, args) =>
                    {
                        await _storageService.DetachTagFromAssetAsync(sha256, t.Id);
                        await RefreshDialogTagsAsync();
                        await RefreshTagFilterBarAsync();
                    };
                    pillStack.Children.Add(removeBtn);
                    pill.Child = pillStack;
                    tagsWrap.Children.Add(pill);
                }
            }
        }

        async Task TryAddTagAsync()
        {
            errorText.Visibility = Visibility.Collapsed;
            string raw = textBox.Text;
            if (!LibraryNameNormalizer.TryValidateAndNormalize(raw, out string display, out _, out string? err))
            {
                errorText.Text = err ?? "Invalid tag name.";
                errorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var tag = await _storageService.CreateOrGetTagAsync(display);
                await _storageService.AttachTagToAssetAsync(sha256, tag.Id);
                textBox.Text = string.Empty;
                await RefreshDialogTagsAsync();
                await RefreshTagFilterBarAsync();
            }
            catch (Exception ex)
            {
                errorText.Text = ex.Message;
                errorText.Visibility = Visibility.Visible;
            }
        }

        addBtn.Click += async (s, args) => await TryAddTagAsync();
        textBox.KeyDown += async (s, args) =>
        {
            if (args.Key == global::Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                await TryAddTagAsync();
            }
        };

        await RefreshDialogTagsAsync();
        await dialog.ShowAsync();
    }

    #endregion

    private void LoadPresetsView()
    {
        _presetItems.Clear();
        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

        foreach (var preset in StatusPresetCatalog.All)
        {
            string title = GetPresetTitle(preset.Id, isTr);
            string desc = GetPresetDescription(preset.Id, isTr);
            string applyText = isTr ? "Klasöre Uygula..." : "Apply to Folder...";

            BitmapImage? previewBitmap = null;
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "OrnarePresets");
                Directory.CreateDirectory(tempDir);
                string previewPath = Path.Combine(tempDir, $"{preset.Id}_{_selectedPresetTemplateId}.png");

                if (!File.Exists(previewPath))
                {
                    var previewResult = FolderTemplateRenderer.RenderPresetPreviewPng(preset, _selectedPresetTemplateId);
                    if (previewResult.IsSuccess && previewResult.Value != null)
                    {
                        File.WriteAllBytes(previewPath, previewResult.Value);
                    }
                }

                previewBitmap = LoadBitmapSafe(previewPath);
            }
            catch
            {
                // Fallback gracefully
            }

            _presetItems.Add(new PresetItemVm
            {
                PresetId = preset.Id,
                Title = title,
                Description = desc,
                ApplyButtonText = applyText,
                PreviewImage = previewBitmap
            });
        }

        SyncItems(PresetsGridView, _presetItems);
    }

    private static string GetPresetTitle(string presetId, bool isTr) => presetId switch
    {
        "status.in-progress" => isTr ? "Devam Ediyor" : "In Progress",
        "status.done" => isTr ? "Tamamlandı" : "Done",
        "status.archive" => isTr ? "Arşiv" : "Archive",
        "status.important" => isTr ? "Önemli" : "Important",
        "priority.high" => isTr ? "Yüksek Öncelik" : "High Priority",
        "priority.medium" => isTr ? "Orta Öncelik" : "Medium Priority",
        "priority.low" => isTr ? "Düşük Öncelik" : "Low Priority",
        _ => presetId
    };

    private static string GetPresetDescription(string presetId, bool isTr) => presetId switch
    {
        "status.in-progress" => isTr ? "Aktif devam eden işleri takip eder" : "Tracks active, ongoing tasks",
        "status.done" => isTr ? "Tamamlanan görevleri ve projeleri belirtir" : "Marks completed tasks and projects",
        "status.archive" => isTr ? "Geçmiş veya referans materyalleri depolar" : "Stores historical or reference material",
        "status.important" => isTr ? "Acil veya kritik klasörleri vurgular" : "Highlights urgent or critical folders",
        "priority.high" => isTr ? "Yüksek öncelikli ögeleri belirtir (yalnızca görsel düzen)" : "Marks high urgency items (visual organization only)",
        "priority.medium" => isTr ? "Normal öncelikli ögeleri belirtir (yalnızca görsel düzen)" : "Marks normal priority items (visual organization only)",
        "priority.low" => isTr ? "Düşük öncelikli ögeleri belirtir (yalnızca görsel düzen)" : "Marks low urgency items (visual organization only)",
        _ => string.Empty
    };

    private void OnPresetTemplateRadioChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton selectedRadio && selectedRadio.Tag is string tag)
        {
            _selectedPresetTemplateId = tag;
            LoadPresetsView();
        }
    }

    private async void OnApplyPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string presetId) return;

        var preset = StatusPresetCatalog.GetPreset(presetId);
        if (preset == null) return;

        try
        {
            var picker = new global::Windows.Storage.Pickers.FolderPicker();
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                await ApplyPresetToFolderAsync(preset, folder.Path);
            }
        }
        catch (Exception ex)
        {
            App.Log($"Preset folder picking or apply failed: {ex.Message}");
        }
    }

    private async Task ApplyPresetToFolderAsync(StatusPreset preset, string folderPath)
    {
        if (!_folderCustomizer.IsSupportedPath(folderPath)) return;

        string tempIco = Path.Combine(Path.GetTempPath(), $"ornare_preset_apply_{Guid.NewGuid():N}.ico");
        try
        {
            var pipeline = _imagePipeline as ImagePipeline ?? new ImagePipeline();
            var renderResult = FolderTemplateRenderer.RenderPresetIco(
                pipeline,
                preset,
                tempIco,
                _selectedPresetTemplateId);

            if (!renderResult.IsSuccess || renderResult.Value == null)
            {
                App.Log($"RenderPresetIco failed: {renderResult.ErrorMessage}");
                return;
            }

            Guid operationId = Guid.NewGuid();
            FolderBaseline priorBaseline = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = Guid.NewGuid(),
                PriorSemantics = DesktopIniParser.ReadSemantics(folderPath),
                CapturedAt = DateTimeOffset.UtcNow
            };

            var applyResult = await Task.Run(() => _folderCustomizer.ApplyIcon(folderPath, tempIco));
            if (applyResult.IsSuccess && applyResult.Value != null)
            {
                await _storageService.SaveFolderAssignmentAsync(
                    folderPath,
                    priorBaseline.OwnershipEpochId,
                    applyResult.Value,
                    priorBaseline);

                // Copy to library & add asset
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string libraryDir = Path.Combine(localAppData, OrnareConstants.ApplicationName, "library");
                Directory.CreateDirectory(libraryDir);
                string canonicalIco = Path.Combine(libraryDir, $"{renderResult.Value.ContentHash}.ico");
                if (!File.Exists(canonicalIco))
                {
                    File.Copy(tempIco, canonicalIco, overwrite: true);
                }

                await _storageService.AddOrUpdateAssetAsync(
                    renderResult.Value.ContentHash,
                    canonicalIco,
                    "svg");

                await _storageService.RecordHistoryAsync(
                    folderPath,
                    operationId,
                    null,
                    renderResult.Value.ContentHash,
                    "apply");

                await RefreshAllDataAsync();
            }
        }
        catch (Exception ex)
        {
            App.Log($"ApplyPresetToFolderAsync failed: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempIco))
            {
                try { File.Delete(tempIco); } catch { }
            }
        }
    }
}

[Microsoft.UI.Xaml.Data.Bindable]
public class PresetItemVm
{
    public string PresetId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ApplyButtonText { get; set; } = string.Empty;
    public BitmapImage? PreviewImage { get; set; }
}

[Microsoft.UI.Xaml.Data.Bindable]
public class LibraryItemVm
{
    public string Sha256 { get; set; } = string.Empty;
    public string CanonicalIcoPath { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = string.Empty;
    public BitmapImage? PreviewImage { get; set; }
    public bool IsFavorite { get; set; }
    public string FavoriteGlyph { get; set; } = "\uE734";
    public SolidColorBrush? FavoriteBrush { get; set; }
    public string FavoriteToolTip { get; set; } = "Favorilere Ekle";
    public string ContextToggleFavoriteText { get; set; } = "Favorilere Ekle";
    public string ContextOpenExplorerText { get; set; } = "Dosya Gezgini'nde Göster";
    public string ContextManageTagsText { get; set; } = "Etiketleri Yönet...";
    public string ContextExportIcoText { get; set; } = "Dışa Aktar (.ico)...";
    public string ContextExportPngText { get; set; } = "Dışa Aktar (.png)";
}

[Microsoft.UI.Xaml.Data.Bindable]
public class RecentItemVm
{
    public string Sha256 { get; set; } = string.Empty;
    public BitmapImage? PreviewImage { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}

[Microsoft.UI.Xaml.Data.Bindable]
public class FolderItemVm
{
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public BitmapImage? IconPreview { get; set; }
    public FolderCustomizationState HealthStatus { get; set; }
    public string HealthGlyph { get; set; } = string.Empty;
    public SolidColorBrush? HealthBrush { get; set; }
    public string HealthLabel { get; set; } = string.Empty;
    public Visibility RepairButtonVisibility { get; set; } = Visibility.Collapsed;
    public Visibility KeepExternalButtonVisibility { get; set; } = Visibility.Collapsed;
    public string? ExpectedAssetHash { get; set; }
}
