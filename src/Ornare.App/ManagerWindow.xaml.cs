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
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using WinRT.Interop;

public sealed partial class ManagerWindow : Window
{
    private readonly IStorageService _storageService;
    private readonly IFolderCustomizer _folderCustomizer;
    private readonly ObservableCollection<LibraryItemVm> _libraryItems = new();
    private readonly ObservableCollection<RecentItemVm> _recentItems = new();
    private readonly ObservableCollection<LibraryItemVm> _favoriteItems = new();
    private readonly ObservableCollection<FolderItemVm> _folderItems = new();

    private List<LibraryAsset> _allAssets = new();
    private string _currentLanguage = "System";
    private string _currentTheme = "System";
    private static readonly System.Text.Json.JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private bool _isLoaded;

    public ManagerWindow(IStorageService storageService, IFolderCustomizer folderCustomizer)
    {
        _storageService = storageService;
        _folderCustomizer = folderCustomizer;

        InitializeComponent();
        ConfigureWindow();

        LibraryGridView.ItemsSource = _libraryItems;
        RecentGridView.ItemsSource = _recentItems;
        FavoritesGridView.ItemsSource = _favoriteItems;
        FoldersListView.ItemsSource = _folderItems;

        SettingsArchValue.Text = RuntimeInformation.ProcessArchitecture.ToString();

        _ = LoadInitialDataAsync();
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
    }

    private async Task LoadInitialDataAsync()
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
        _allAssets = (await _storageService.GetAllAssetsAsync()).ToList();
        var favIds = (await _storageService.GetFavoritesAsync()).Select(f => f.Sha256).ToHashSet();

        _libraryItems.Clear();
        foreach (var asset in _allAssets)
        {
            _libraryItems.Add(CreateLibraryVm(asset, favIds.Contains(asset.Sha256)));
        }

        LibraryEmptyPanel.Visibility = _libraryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LibraryGridView.Visibility = _libraryItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Progressive search box disclosure: only when >= 13 assets per Visual Reference Spec
        SearchTextBox.Visibility = _allAssets.Count >= 13 ? Visibility.Visible : Visibility.Collapsed;

        UpdateSubtitle();
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

        FoldersEmptyPanel.Visibility = _folderItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FoldersListView.Visibility = _folderItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static LibraryItemVm CreateLibraryVm(LibraryAsset asset, bool isFavorite)
    {
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
                : new SolidColorBrush(global::Windows.UI.Color.FromArgb(120, 128, 128, 128))
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
        FoldersView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;

        SearchTextBox.Visibility = Visibility.Collapsed;
        ClearRecentButton.Visibility = Visibility.Collapsed;

        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

        switch (tag)
        {
            case "Library":
                LibraryView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Kitaplık" : "Library";
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
            case "Folders":
                FoldersView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Özelleştirilmiş Klasörler" : "Customized Folders";
                break;
            case "Settings":
                SettingsView.Visibility = Visibility.Visible;
                SectionTitleText.Text = isTr ? "Ayarlar" : "Settings";
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
        else
        {
            SectionSubtitleText.Text = string.Empty;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            _libraryItems.Clear();
            foreach (var a in _allAssets)
            {
                _libraryItems.Add(CreateLibraryVm(a, isFavorite: false));
            }
        }
        else
        {
            _libraryItems.Clear();
            foreach (var a in _allAssets.Where(a => a.Sha256.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                                   a.SourceFormat.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                _libraryItems.Add(CreateLibraryVm(a, isFavorite: false));
            }
        }
        UpdateSubtitle();
    }

    private async void OnToggleFavoriteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sha256)
        {
            var favs = await _storageService.GetFavoritesAsync();
            bool isFav = favs.Any(f => f.Sha256 == sha256);
            await _storageService.SetFavoriteAsync(sha256, !isFav);

            await LoadLibraryAsync();
            await LoadFavoritesAsync();
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

        string json = System.Text.Json.JsonSerializer.Serialize(report, s_jsonOptions);
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
        NavTextFolders.Text = isTr ? "Özelleştirilmiş Klasörler" : "Customized Folders";
        NavTextSettings.Text = isTr ? "Ayarlar" : "Settings";

        LibraryEmptyTitle.Text = isTr ? "Kitaplıkta simge yok" : "No icons in library";
        LibraryEmptySubtitle.Text = isTr ? "Özelleştirdiğiniz simgeler burada görünecektir" : "Icons you customize will appear here";
        RecentEmptyTitle.Text = isTr ? "Son kullanılan simge yok" : "No recent icons";
        FavoritesEmptyTitle.Text = isTr ? "Favori simge yok" : "No favorite icons";
        FoldersEmptyTitle.Text = isTr ? "Özelleştirilmiş klasör yok" : "No customized folders";

        SettingsAppearanceHeader.Text = isTr ? "Görünüm" : "Appearance";
        SettingsThemeLabel.Text = isTr ? "Uygulama teması" : "App theme";
        SettingsLanguageHeader.Text = isTr ? "Dil" : "Language";
        SettingsLanguageLabel.Text = isTr ? "Uygulama dili" : "App language";
        SettingsDiagnosticsHeader.Text = isTr ? "Tanılama" : "Diagnostics";
        SettingsDiagnosticsLabel.Text = isTr ? "Tanılama Raporu" : "Diagnostic Report";
        SettingsDiagnosticsSubtext.Text = isTr ? "Kişisel yollar gizlenmiş sistem ve sağlık raporunu dışa aktarır." : "Exports system and health status with personal paths anonymized.";
        ExportReportButton.Content = isTr ? "Tanılama Raporunu Dışa Aktar" : "Export Diagnostic Report";
        SettingsAboutHeader.Text = isTr ? "Hakkında" : "About";
        SettingsVersionLabel.Text = isTr ? "Sürüm" : "Version";
        SettingsArchLabel.Text = isTr ? "Mimari" : "Architecture";
        SettingsPrivacyStatement.Text = isTr ? "Ornare tamamen çevrimdışı çalışır. Sıfır telemetri, sıfır bulut takibi, sıfır veri toplama." : "Ornare works completely offline. Zero telemetry, zero cloud tracking, zero data collection.";
        ClearRecentButton.Content = isTr ? "Son Kullanılanları Temizle" : "Clear Recent";
        SearchTextBox.PlaceholderText = isTr ? "Kitaplıkta ara..." : "Search library...";

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
}

public class LibraryItemVm
{
    public required string Sha256 { get; init; }
    public required string CanonicalIcoPath { get; init; }
    public required string SourceFormat { get; init; }
    public BitmapImage? PreviewImage { get; init; }
    public bool IsFavorite { get; set; }
    public string FavoriteGlyph { get; set; } = "\uE734";
    public SolidColorBrush? FavoriteBrush { get; set; }
}

public class RecentItemVm
{
    public required string Sha256 { get; init; }
    public BitmapImage? PreviewImage { get; init; }
    public string TimeAgo { get; init; } = string.Empty;
}

public class FolderItemVm
{
    public required string FolderPath { get; init; }
    public required string FolderName { get; init; }
    public BitmapImage? IconPreview { get; init; }
    public FolderCustomizationState HealthStatus { get; init; }
    public string HealthGlyph { get; init; } = string.Empty;
    public SolidColorBrush? HealthBrush { get; init; }
    public string HealthLabel { get; init; } = string.Empty;
    public Visibility RepairButtonVisibility { get; init; } = Visibility.Collapsed;
    public Visibility KeepExternalButtonVisibility { get; init; } = Visibility.Collapsed;
    public string? ExpectedAssetHash { get; init; }
}
