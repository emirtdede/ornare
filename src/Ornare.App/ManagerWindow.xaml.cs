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
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Ornare.Storage;
using Ornare.Windows;
using WinRT.Interop;

public sealed partial class ManagerWindow : Window, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly IFolderCustomizer _folderCustomizer;
    private readonly IImagePipeline _imagePipeline;
    private readonly ObservableCollection<LibraryItemVm> _libraryItems = new();
    private readonly ObservableCollection<RecentItemVm> _recentItems = new();
    private readonly ObservableCollection<LibraryItemVm> _favoriteItems = new();
    private readonly ObservableCollection<FolderItemVm> _folderItems = new();
    private readonly ObservableCollection<PresetItemVm> _presetItems = new();
    private readonly ObservableCollection<StagedFolderItemVm> _stagedFolders = new();
    private readonly ObservableCollection<RejectedFolderItemVm> _rejectedFolders = new();
    private CancellationTokenSource? _stagingCts;
    private string _selectedPresetTemplateId = FolderTemplateCatalog.DefaultTemplateId;
    private string? _selectedFilterTagId;

    private List<LibraryAsset> _allAssets = new();
    private string _currentLanguage = "System";
    private string _currentTheme = "System";
    private string _currentSectionTag = "Library";
    private bool _isLoaded;
    private bool _disposed;
    private bool _closeFlushed;
    private bool _errorDialogOpen;
    private readonly System.Threading.CancellationTokenSource _windowCts = new();
    private System.Threading.CancellationTokenSource? _searchCts;

    public ManagerWindow(IStorageService storageService, IFolderCustomizer folderCustomizer, IImagePipeline? imagePipeline = null)
    {
        _storageService = storageService;
        _folderCustomizer = folderCustomizer;
        _imagePipeline = imagePipeline ?? new ImagePipeline();

        InitializeComponent();
        ConfigureWindow();
        SetupKeyboardAccelerators();
        InitializeSettingsSurface();

        // Seed the first frame from the preferences App resolved before this window existed.
        // Without it the window paints the English/system defaults baked into XAML and only
        // corrects itself once LoadInitialDataAsync finishes its settings round trip.
        _currentLanguage = AppResources.CurrentLocale;
        _currentTheme = App.CurrentTheme;
        ApplyTheme(_currentTheme);
        ApplyLanguage(_currentLanguage, localizeSettingsSurface: false);

        WebsiteHyperlink.Content = AppInfo.WebsiteDisplayName;
        WebsiteHyperlink.NavigateUri = new Uri(AppInfo.WebsiteUrl, UriKind.Absolute);

        StagedFoldersListView.ItemsSource = _stagedFolders;
        RejectedFoldersListView.ItemsSource = _rejectedFolders;

        Closed += (s, e) => Dispose();

        SettingsArchValue.Text = RuntimeInformation.ProcessArchitecture.ToString();

        _ = LoadInitialDataAsync();
    }

    private static void SyncItems<T>(ItemsControl control, IEnumerable<T> items)
    {
        // Assign ItemsSource instead of Items.Clear + one Add per element. Clear/Add
        // raises a change notification per row, forcing the ItemsControl to rebuild its
        // layout repeatedly; binding one source updates the list in a single pass.
        // ObservableCollection sources keep notifying on later mutations; plain lists are
        // snapshotted so a later mutation still gets rendered.
        control.ItemsSource = items is ObservableCollection<T> ? items : items.ToList();
    }

    /// <summary>
    /// Executes an <c>async void</c> handler body so an exception cannot escape to the
    /// dispatcher and terminate the process. Expected cancellation is ignored; genuine
    /// failures are recorded through the diagnostic sink (never shown as raw backend
    /// detail to the user).
    /// </summary>
    private async Task GuardAsync(string operation, Func<Task> body, string? userMessage = null)
    {
        try
        {
            await body();
        }
        catch (OperationCanceledException)
        {
            // User-initiated cancellation is not a failure.
        }
        catch (Exception ex)
        {
            // Raw exception text is diagnostic only; the user gets a localized, generic
            // message through a single dialog instead of silence or a raw stack detail.
            App.Log($"{operation} failed: {ex}");
            await ShowUnexpectedErrorAsync(userMessage);
        }
    }

    /// <summary>
    /// Guard for work the user did not ask for directly (a language-driven surface refresh, a
    /// collections reload). A failure is recorded instead of surfacing as an unobserved task,
    /// and no modal dialog interrupts the window, because the user did not initiate it.
    /// </summary>
    private static async Task GuardBackgroundAsync(string operation, Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (OperationCanceledException)
        {
            // The window closed; expected.
        }
        catch (Exception ex)
        {
            App.Log($"{operation} background failure: {ex}");
        }
    }

    /// <summary>
    /// Shows one non-stacking dialog for an unexpected guarded failure. A burst of failures
    /// from the same event keeps a single dialog open and only logs the rest.
    /// </summary>
    private async Task ShowUnexpectedErrorAsync(string? userMessage)
    {
        if (_errorDialogOpen || Content is not FrameworkElement root || root.XamlRoot is null)
        {
            return;
        }

        _errorDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = root.XamlRoot,
                Title = "Ornare",
                Content = userMessage ?? AppResources.GetString("ErrorUnexpected", _currentLanguage),
                CloseButtonText = AppResources.GetString("Close", _currentLanguage),
                DefaultButton = ContentDialogButton.Close
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Showing the error surface itself failed (for example during teardown); the
            // diagnostic log already has the original failure.
            App.Log($"Unexpected-error dialog failed: {ex.Message}");
        }
        finally
        {
            _errorDialogOpen = false;
        }
    }

    /// <summary>
    /// Maps a machine-readable name-validation failure to localized user copy.
    /// The English diagnostic text produced by <see cref="LibraryNameNormalizer"/>
    /// must never reach the UI, and the raw exception message of a storage failure
    /// must not either: both are logged instead.
    /// </summary>
    /// <summary>
    /// Resolves a resource key for the active locale. Replaces the previous tr/en
    /// ternaries, which silently fell back to English for de-DE, fr-FR, es-ES and pt-BR.
    /// </summary>
    private string L(string key) => AppResources.GetString(key, _currentLanguage);

    private string LocalizedNameError(LibraryNameError error) => error switch
    {
        LibraryNameError.Empty => AppResources.GetString("ErrorNameEmpty", _currentLanguage),
        LibraryNameError.TooShort => AppResources.GetString("ErrorNameTooShort", _currentLanguage),
        LibraryNameError.TooLong => AppResources.GetString("ErrorNameTooLong", _currentLanguage),
        LibraryNameError.ControlCharacter => AppResources.GetString("ErrorNameControlCharacter", _currentLanguage),
        _ => AppResources.GetString("ErrorUnexpected", _currentLanguage),
    };

    /// <summary>
    /// Shows a localized, non-technical failure in a dialog and records the full
    /// exception in the diagnostic log. The raw message can contain SQLite text,
    /// file-system paths or schema names, which must not reach the user.
    /// </summary>
    private void ShowDialogError(TextBlock target, Exception ex, string operation)
    {
        App.Log($"{operation} failed: {ex}");
        target.Text = AppResources.GetString("ErrorUnexpected", _currentLanguage);
        target.Visibility = Visibility.Visible;
    }

    private void SetupKeyboardAccelerators()
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }

        // 1. Ctrl+F -> Focuses/selects the Library search box only while Manager is active
        var ctrlF = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.F,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlF.Invoked += (s, args) =>
        {
            args.Handled = true;
            FocusSearchBox();
        };
        root.KeyboardAccelerators.Add(ctrlF);

        // 2. Ctrl+O -> Opens icon/image picker when manager owns focus
        var ctrlO = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.O,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlO.Invoked += (s, args) =>
        {
            args.Handled = true;
            OnImportIconClicked(this, new RoutedEventArgs());
        };
        root.KeyboardAccelerators.Add(ctrlO);

        // 3. Ctrl+V -> Invokes Ornare paste only when focus is not inside an editable text control
        var ctrlV = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.V,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlV.Invoked += async (s, args) =>
        {
            if (IsFocusInEditableControl())
            {
                return; // Retain native text paste in SearchTextBox or dialog textboxes
            }

            args.Handled = true;
            await GuardAsync("ManagerPasteAccelerator", HandleManagerPasteAsync);
        };
        root.KeyboardAccelerators.Add(ctrlV);

        // 4. Ctrl+Z -> Invokes Ornare Undo only when focus is not inside an editable text control
        var ctrlZ = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.Z,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlZ.Invoked += async (s, args) =>
        {
            if (IsFocusInEditableControl())
            {
                return; // Retain native text-undo in TextBox
            }

            args.Handled = true;
            await GuardAsync("ManagerUndoAccelerator", HandleManagerUndoAsync);
        };
        root.KeyboardAccelerators.Add(ctrlZ);

        // 5. Esc -> Closes topmost transient flyout/dialog or cancels a pre-commit editor operation
        var esc = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.Escape
        };
        esc.Invoked += (s, args) =>
        {
            args.Handled = true;
            HandleManagerEscape();
        };
        root.KeyboardAccelerators.Add(esc);
    }

    private void FocusSearchBox()
    {
        SwitchSection("Library");
        if (SearchTextBox != null)
        {
            SearchTextBox.Visibility = Visibility.Visible;
            SearchTextBox.Focus(FocusState.Programmatic);
            SearchTextBox.SelectAll();
        }
    }

    private bool IsFocusInEditableControl()
    {
        if (Content is FrameworkElement root && root.XamlRoot != null)
        {
            var focused = FocusManager.GetFocusedElement(root.XamlRoot);
            if (focused is TextBox or RichEditBox or AutoSuggestBox)
            {
                return true;
            }
        }
        return false;
    }

    private async void OnTargetTrayDropZoneTapped(object sender, TappedRoutedEventArgs e)
    {
        await GuardAsync(nameof(OnTargetTrayDropZoneTapped), OpenFolderPickerForStagingAsync);
    }

    private async void OnTargetTrayDropZoneKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter || e.Key == global::Windows.System.VirtualKey.Space)
        {
            e.Handled = true;
            await GuardAsync(nameof(OnTargetTrayDropZoneKeyDown), OpenFolderPickerForStagingAsync);
        }
    }

    /// <summary>
    /// First-run path out of the empty library: pick a folder to customize instead of telling
    /// the user to go back to File Explorer. Audit finding 2.2 was that this view offered only
    /// "Import Icon...", which imports an icon but does nothing with a folder.
    /// </summary>
    private async void OnEmptyStateChooseFolderClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnEmptyStateChooseFolderClicked), OpenFolderPickerForStagingAsync);
    }

    private async Task OpenFolderPickerForStagingAsync()
    {
        try
        {
            var picker = new global::Windows.Storage.Pickers.FolderPicker();
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                await ProcessDroppedFolderPathsAsync(new List<string> { folder.Path });
            }
        }
        catch (Exception ex)
        {
            App.Log($"OpenFolderPickerForStagingAsync failed: {ex.Message}");
        }
    }

    private async Task HandleManagerPasteAsync()
    {
        try
        {
            var package = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (package == null)
            {
                return;
            }

            if (package.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await package.GetStorageItemsAsync();
                if (items != null && items.Count > 0)
                {
                    var folderPaths = new List<string>();
                    var filePaths = new List<string>();

                    foreach (var item in items)
                    {
                        if (item is global::Windows.Storage.StorageFolder || Directory.Exists(item.Path))
                        {
                            folderPaths.Add(item.Path);
                        }
                        else if (item is global::Windows.Storage.StorageFile || File.Exists(item.Path))
                        {
                            filePaths.Add(item.Path);
                        }
                    }

                    if (folderPaths.Count > 0)
                    {
                        SwitchSection("Folders");
                        await ProcessDroppedFolderPathsAsync(folderPaths);
                        return;
                    }

                    if (filePaths.Count > 0)
                    {
                        SwitchSection("Library");
                        string iconsDir = OrnareConstants.IconsDirectory;
                        Directory.CreateDirectory(iconsDir);

                        foreach (var filePath in filePaths)
                        {
                            string ext = Path.GetExtension(filePath).ToLowerInvariant();
                            if (ext is ".ico" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp")
                            {
                                try
                                {
                                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
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
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // A single unreadable/unsupported dropped file must not
                                    // abort the whole batch or vanish silently.
                                    App.Log($"Dropped icon import failed for '{Path.GetFileName(filePath)}': {ex.Message}");
                                }
                            }
                        }
                        await LoadLibraryAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"HandleManagerPasteAsync error: {ex.Message}");
        }
    }

    private async Task HandleManagerUndoAsync()
    {
        if (_stagedFolders.Count > 0)
        {
            _stagedFolders.RemoveAt(_stagedFolders.Count - 1);
            StagedCountText.Text = AppResources.Format("StagedFolders_Count", _currentLanguage, _stagedFolders.Count);
            if (_stagedFolders.Count == 0)
            {
                TargetTrayStagedPanel.Visibility = Visibility.Collapsed;
            }
            return;
        }

        if (FoldersListView?.SelectedItem is FolderItemVm selected && !string.IsNullOrEmpty(selected.FolderPath))
        {
            var res = await UndoService.UndoAsync(selected.FolderPath, _storageService, _folderCustomizer);
            if (res.IsSuccess)
            {
                await LoadFoldersAsync();
            }
        }
    }

    private void HandleManagerEscape()
    {
        if (SearchTextBox != null && !string.IsNullOrEmpty(SearchTextBox.Text))
        {
            SearchTextBox.Text = string.Empty;
            return;
        }

        if (Content is FrameworkElement root && root.XamlRoot != null)
        {
            var focused = FocusManager.GetFocusedElement(root.XamlRoot) as FrameworkElement;
            if (focused is TextBox)
            {
                NavListView?.Focus(FocusState.Programmatic);
            }
        }
    }

    private static bool IsElementInteractable(FrameworkElement? element)
    {
        if (element == null || element.Visibility != Visibility.Visible)
        {
            return false;
        }
        if (element is Control control && !control.IsEnabled)
        {
            return false;
        }
        return true;
    }

    private void RestoreManagerFocusAfterTransition()
    {
        try
        {
            if (Content is not FrameworkElement root || root.XamlRoot == null)
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement(root.XamlRoot) as FrameworkElement;
            if (IsElementInteractable(focused))
            {
                return;
            }

            // Fixed order:
            // 1. Operation error/success action if one exists
            if (TargetTrayStagedPanel.Visibility == Visibility.Visible && IsElementInteractable(ClearStagingButton))
            {
                ClearStagingButton.Focus(FocusState.Programmatic);
                return;
            }

            // 2. Primary input / drop-zone action
            if (FoldersView.Visibility == Visibility.Visible && IsElementInteractable(TargetTrayDropZone))
            {
                TargetTrayDropZone.Focus(FocusState.Programmatic);
                return;
            }

            if (LibraryView.Visibility == Visibility.Visible && IsElementInteractable(ImportIconButton))
            {
                ImportIconButton.Focus(FocusState.Programmatic);
                return;
            }

            // 3. First enabled control in logical XAML order
            NavListView?.Focus(FocusState.Programmatic);
        }
        catch (Exception ex)
        {
            App.Log($"RestoreManagerFocusAfterTransition error: {ex.Message}");
        }
    }

    private void ConfigureWindow()
    {
        Title = "Ornare";
        SystemBackdrop = new MicaBackdrop();

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        // Defer the close until the pending cloud snapshot is flushed. Doing this in
        // AppWindow.Closing keeps the UI thread responsive; the previous implementation
        // blocked it for up to three seconds inside Dispose.
        appWindow.Closing += OnAppWindowClosing;

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
            _settings = settings;
            _currentTheme = settings.Theme;
            _currentLanguage = settings.Language;

            ApplyTheme(_currentTheme);
            ApplyLanguage(_currentLanguage);
            ApplyAppearance();

            if (string.Equals(_currentTheme, "Light", StringComparison.OrdinalIgnoreCase)) ThemeComboBox.SelectedIndex = 1;
            else if (string.Equals(_currentTheme, "Dark", StringComparison.OrdinalIgnoreCase)) ThemeComboBox.SelectedIndex = 2;
            else ThemeComboBox.SelectedIndex = 0;

            SelectLanguageItem(settings.Language);

            await RefreshAllDataAsync();
            await RefreshCloudSurfaceAsync();
            _ = GuardAsync(nameof(ShowCloudRestorePromptIfNeededAsync), ShowCloudRestorePromptIfNeededAsync);
            _isLoaded = true;
            NavListView.SelectedIndex = 0;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _storageService.EnsureSearchIndexReadyAsync(_windowCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected: the window closed and cancelled indexing. Not a fault,
                    // so it is not logged as one; the branch below handles real errors.
                }
                catch (Exception ex)
                {
                    App.Log($"EnsureSearchIndexReadyAsync background task failed: {ex.Message}");
                }
            }, _windowCts.Token);

            App.Log("LoadInitialDataAsync completed successfully.");
        }
        catch (Exception ex)
        {
            App.Log($"EXCEPTION in LoadInitialDataAsync: {ex}");

            // Do not leave the user with a silently empty manager window: an
            // unloadable library is a visible, actionable state, not a blank screen.
            LibraryEmptyTitle.Text = L("ErrorUnexpected");
            LibraryEmptySubtitle.Text = L("ErrorLibraryLoadFailed");
            LibraryEmptyPanel.Visibility = Visibility.Visible;
        }
    }

    public async Task RefreshAllDataAsync()
    {
        await LoadLibraryAsync();
        await LoadRecentAsync();
        await LoadFavoritesAsync();
        await LoadFoldersAsync();

        // Any user-visible data change is a backup candidate. The debounce coalesces a
        // burst of mutations (apply, undo, collection edits) into one write.
        if (_isLoaded && _settings.CloudSyncEnabled)
        {
            ScheduleCloudSync();
        }
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
            var currentAssignment = a;
            string folderName = Path.GetFileName(currentAssignment.FolderPath.TrimEnd('\\', '/'));
            string iconPath = Path.Combine(currentAssignment.FolderPath, OrnareConstants.CanonicalFolderName, OrnareConstants.CustomIconFileName);

            // Read-only health check
            var health = FolderHealthEngine.CheckHealth(currentAssignment.FolderPath, currentAssignment);

            // F19: If folder is missing on disk, evaluate NTFS identity reconciliation (moved folder)
            if (health.State == FolderCustomizationState.MissingTarget)
            {
                var decision = FolderReconciliationService.EvaluateAssignment(currentAssignment);
                if (decision.Outcome == ReconciliationOutcome.Relocated && !string.IsNullOrEmpty(decision.RelocatedPath))
                {
                    await _storageService.UpdateAssignmentPathAsync(currentAssignment.FolderPath, decision.RelocatedPath);
                    await _storageService.UpdateAssignmentHealthAsync(decision.RelocatedPath, decision.RecommendedState);
                    currentAssignment = currentAssignment with { FolderPath = decision.RelocatedPath, Status = decision.RecommendedState };
                    folderName = Path.GetFileName(decision.RelocatedPath.TrimEnd('\\', '/'));
                    iconPath = Path.Combine(decision.RelocatedPath, OrnareConstants.CanonicalFolderName, OrnareConstants.CustomIconFileName);
                    health = FolderHealthEngine.CheckHealth(decision.RelocatedPath, currentAssignment);
                }
                else if (decision.Outcome == ReconciliationOutcome.LocationUnknown)
                {
                    health = new FolderHealthCheckResult
                    {
                        FolderPath = currentAssignment.FolderPath,
                        State = FolderCustomizationState.LocationUnknown,
                        ConflictDetails = decision.Reason,
                        CanAutoRepair = false
                    };
                }
            }

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
                    label = AppResources.GetString("StatusHealthy", _currentLanguage);
                    break;
                case FolderCustomizationState.NeedsReconciliation:
                case FolderCustomizationState.ConfigMismatch:
                case FolderCustomizationState.MissingOwnedIcon:
                    glyph = "\uE7BA"; // Warning
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 202, 138, 4));
                    label = AppResources.GetString("StatusNeedsAttention", _currentLanguage);
                    repairVis = Visibility.Visible;
                    break;
                case FolderCustomizationState.ExternalIconConflict:
                    glyph = "\uE783"; // Alert
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 217, 119, 6));
                    label = AppResources.GetString("StatusConflict", _currentLanguage);
                    keepExtVis = Visibility.Visible;
                    break;
                case FolderCustomizationState.LocationUnknown:
                    glyph = "\uE711"; // Question / Error
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 120, 113, 108));
                    label = AppResources.GetString("StatusUnknown", _currentLanguage);
                    break;
                default:
                    glyph = "\uE711"; // Error
                    brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 220, 38, 38));
                    label = AppResources.GetString("StatusMissing", _currentLanguage);
                    break;
            }

            bool isNtfs = FolderIdentityService.IsSupportedFixedNtfsVolume(currentAssignment.FolderPath);
            string portText = isNtfs
                ? AppResources.GetString("StatusPortable", _currentLanguage)
                : string.Empty;
            Visibility portVis = isNtfs ? Visibility.Visible : Visibility.Collapsed;

            _folderItems.Add(new FolderItemVm
            {
                FolderPath = currentAssignment.FolderPath,
                FolderName = folderName,
                IconPreview = LoadBitmapSafe(iconPath),
                HealthStatus = health.State,
                HealthGlyph = glyph,
                HealthBrush = brush,
                HealthLabel = label,
                RepairButtonVisibility = repairVis,
                KeepExternalButtonVisibility = keepExtVis,
                ExpectedAssetHash = currentAssignment.CurrentAssetHash,
                PortabilityLabel = portText,
                PortabilityVisibility = portVis,
                RepairText = L("Repair"),
                KeepExternalText = L("KeepExternal"),
                ReapplyText = L("Reapply"),
                RepairAutomationName = L("RepairFolderAutomation"),
                KeepExternalAutomationName = L("KeepExternalAutomation"),
                ReapplyAutomationName = L("ReapplyAutomation"),
                OpenFolderAutomationName = L("OpenFolderAutomation")
            });
        }
        SyncItems(FoldersListView, _folderItems);

        FoldersEmptyPanel.Visibility = _folderItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FoldersListView.Visibility = _folderItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFoldersViewDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = L("StagedFolders_DropCaption");
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }
        else
        {
            e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
        }
    }

    private async void OnFoldersViewDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items == null || items.Count == 0)
            {
                return;
            }

            var rawPaths = items.Select(i => i.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            await ProcessDroppedFolderPathsAsync(rawPaths);
        }
        catch (Exception ex)
        {
            App.Log($"OnFoldersViewDrop error: {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    public async Task ProcessDroppedFolderPathsAsync(IReadOnlyList<string> rawPaths)
    {
        if (rawPaths == null || rawPaths.Count == 0) return;

        // F20-DEC-05: Reject drop if raw count > 1000 before filesystem work
        if (rawPaths.Count > FolderStagingService.MaxDropItems)
        {
            TargetTrayLimitNotice.Text = AppResources.Format(
                "DropLimitExceeded",
                _currentLanguage,
                FolderStagingService.MaxDropItems,
                rawPaths.Count);
            TargetTrayLimitNotice.Visibility = Visibility.Visible;
            return;
        }

        TargetTrayLimitNotice.Visibility = Visibility.Collapsed;
        StagingProgressRing.IsActive = true;
        StagingProgressRing.Visibility = Visibility.Visible;
        TargetTrayStagedPanel.Visibility = Visibility.Visible;

        _stagingCts?.Cancel();
        _stagingCts?.Dispose();
        _stagingCts = new CancellationTokenSource();

        try
        {
            var result = await FolderStagingService.StageFoldersAsync(rawPaths, _storageService, _stagingCts.Token);

            _stagedFolders.Clear();
            _rejectedFolders.Clear();

            string portText = L("StagedFolders_PortableNtfs");
            string existText = L("StagedFolders_AlreadyCustomized");

            foreach (var item in result.AcceptedItems)
            {
                string name = Path.GetFileName(item.CanonicalPath.TrimEnd('\\', '/'));
                if (string.IsNullOrEmpty(name)) name = item.CanonicalPath;

                bool isNtfs = item.Identity.HasValue && FolderIdentityService.IsSupportedFixedNtfsVolume(item.CanonicalPath);

                _stagedFolders.Add(new StagedFolderItemVm
                {
                    FolderName = name,
                    FolderPath = item.CanonicalPath,
                    ExistingAssignmentId = item.ExistingAssignmentId,
                    ExistingBadgeText = existText,
                    PortabilityText = isNtfs ? portText : string.Empty
                });
            }

            foreach (var rej in result.RejectedItems)
            {
                _rejectedFolders.Add(new RejectedFolderItemVm
                {
                    FolderPath = rej.RawInputPath,
                    Reason = rej.RejectionReason ?? "Rejected"
                });
            }

            string stagedTitle = AppResources.Format("StagedFolders_Count", _currentLanguage, _stagedFolders.Count);
            if (result.IsCancelled)
            {
                stagedTitle += L("StagedFolders_Partial");
            }
            StagedCountText.Text = stagedTitle;

            if (_rejectedFolders.Count > 0)
            {
                ToggleRejectionsButton.Visibility = Visibility.Visible;
                RejectionSummaryText.Text = AppResources.Format("RejectedItems_Count", _currentLanguage, _rejectedFolders.Count);
            }
            else
            {
                ToggleRejectionsButton.Visibility = Visibility.Collapsed;
                RejectionsListPanel.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            StagingProgressRing.IsActive = false;
            StagingProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    private void OnClearStagingClicked(object sender, RoutedEventArgs e)
    {
        _stagingCts?.Cancel();
        _stagedFolders.Clear();
        _rejectedFolders.Clear();
        TargetTrayStagedPanel.Visibility = Visibility.Collapsed;
        RejectionsListPanel.Visibility = Visibility.Collapsed;
        TargetTrayLimitNotice.Visibility = Visibility.Collapsed;
    }

    private void OnToggleRejectionsClicked(object sender, RoutedEventArgs e)
    {
        RejectionsListPanel.Visibility = RejectionsListPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private LibraryItemVm CreateLibraryVm(LibraryAsset asset, bool isFavorite)
    {
        string favToolTip = isFavorite ? L("Unfavorite") : L("Favorite_Add");
        string toggleText = isFavorite ? L("Unfavorite") : L("Favorite_Add");
        string explorerText = L("ShowInFileExplorer");
        string manageTagsText = L("Menu_ManageTags");
        string exportText = L("Export_Ico_Menu");
        string exportPngText = L("Export_Png_Menu");
        string manageCollectionsText = L("Menu_ManageCollections");

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
            ContextApplyToFolderText = L("ApplyToFolder"),
            ContextManageTagsText = manageTagsText,
            ContextManageCollectionsText = manageCollectionsText,
            ContextExportIcoText = exportText,
            ContextExportPngText = exportPngText,
            ExportPng1024Text = L("ExportPng_1024Default"),
            ExportPng512Text = L("ExportPng_512"),
            ExportPng256Text = L("ExportPng_256"),
            RemoveFromCollectionText = L("RemoveFromCollection")
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
        catch (Exception ex)
        {
            // Unreadable/corrupt image: fall back to a null source, but keep a trace
            // so a systematically broken cache path is diagnosable.
            App.Log($"TryLoadBitmap failed for '{Path.GetFileName(path)}': {ex.Message}");
        }
        return null;
    }

    private string FormatTimeAgo(DateTimeOffset time)
    {
        TimeSpan diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalMinutes < 1)
        {
            return AppResources.GetString("TimeJustNow", _currentLanguage);
        }
        if (diff.TotalMinutes < 60)
        {
            return string.Format(
                AppResources.CultureFor(_currentLanguage),
                AppResources.GetString("TimeMinutesAgo", _currentLanguage),
                (int)diff.TotalMinutes);
        }
        if (diff.TotalHours < 24)
        {
            return string.Format(
                AppResources.CultureFor(_currentLanguage),
                AppResources.GetString("TimeHoursAgo", _currentLanguage),
                (int)diff.TotalHours);
        }

        return string.Format(
            AppResources.CultureFor(_currentLanguage),
            AppResources.GetString("TimeDaysAgo", _currentLanguage),
            (int)diff.TotalDays);
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

        // Remembered so a language switch can re-title the visible section without
        // depending on the nav ListView selection still being a ListViewItem.
        _currentSectionTag = tag;

        LibraryView.Visibility = Visibility.Collapsed;
        RecentView.Visibility = Visibility.Collapsed;
        FavoritesView.Visibility = Visibility.Collapsed;
        if (CollectionsView != null) CollectionsView.Visibility = Visibility.Collapsed;
        if (PresetsView != null) PresetsView.Visibility = Visibility.Collapsed;
        FoldersView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;

        SearchTextBox.Visibility = Visibility.Collapsed;
        ClearRecentButton.Visibility = Visibility.Collapsed;
        if (ImportIconButton != null) ImportIconButton.Visibility = Visibility.Collapsed;

        switch (tag)
        {
            case "Library":
                LibraryView.Visibility = Visibility.Visible;
                SectionTitleText.Text = AppResources.GetString("Nav_Library", _currentLanguage);
                if (ImportIconButton != null) ImportIconButton.Visibility = Visibility.Visible;
                if (_allAssets.Count >= 13) SearchTextBox.Visibility = Visibility.Visible;
                break;
            case "Recent":
                RecentView.Visibility = Visibility.Visible;
                SectionTitleText.Text = AppResources.GetString("Nav_Recent", _currentLanguage);
                ClearRecentButton.Visibility = _recentItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "Favorites":
                FavoritesView.Visibility = Visibility.Visible;
                SectionTitleText.Text = AppResources.GetString("Nav_Favorites", _currentLanguage);
                break;
            case "Collections":
                if (CollectionsView != null)
                {
                    CollectionsView.Visibility = Visibility.Visible;
                    SectionTitleText.Text = AppResources.GetString("Nav_Collections", _currentLanguage);
                    _ = GuardBackgroundAsync(nameof(LoadCollectionsViewAsync), LoadCollectionsViewAsync);
                }
                break;
            case "Presets":
                if (PresetsView != null)
                {
                    PresetsView.Visibility = Visibility.Visible;
                    SectionTitleText.Text = AppResources.GetString("Nav_Presets", _currentLanguage);
                    LoadPresetsView();
                }
                break;
            case "Folders":
                FoldersView.Visibility = Visibility.Visible;
                SectionTitleText.Text = AppResources.GetString("Nav_Folders", _currentLanguage);
                break;
            case "Settings":
                SettingsView.Visibility = Visibility.Visible;
                SectionTitleText.Text = AppResources.GetString("Nav_Settings", _currentLanguage);
                SettingsView.ChangeView(null, 0, null, true);
                break;
        }

        UpdateSubtitle();

        // Keyboard users lose focus when the previous section is collapsed. Restore
        // it deterministically instead of leaving focus on a hidden element.
        RestoreManagerFocusAfterTransition();
    }

    private void UpdateSubtitle()
    {
        if (LibraryView.Visibility == Visibility.Visible)
        {
            SectionSubtitleText.Text = _libraryItems.Count == 1
                ? L("Item_Icons_Single")
                : AppResources.Format("Item_Icons_Count", _currentLanguage, _libraryItems.Count);
        }
        else if (FoldersView.Visibility == Visibility.Visible)
        {
            SectionSubtitleText.Text = _folderItems.Count == 1
                ? L("Item_Folders_Single")
                : AppResources.Format("Item_Folders_Count", _currentLanguage, _folderItems.Count);
        }
        else if (PresetsView != null && PresetsView.Visibility == Visibility.Visible)
        {
            SectionSubtitleText.Text = L("Library_Subtitle");
        }
        else
        {
            SectionSubtitleText.Text = string.Empty;
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        string query = SearchTextBox.Text?.Trim() ?? string.Empty;

        try
        {
            // Debounce at 120 ms
            await Task.Delay(120, token);

            if (string.IsNullOrEmpty(query))
            {
                await LoadLibraryAsync();
                return;
            }

            var indexState = await _storageService.GetSearchIndexStateAsync();
            if (!indexState.IsReady)
            {
                _libraryItems.Clear();
                SyncItems(LibraryGridView, _libraryItems);
                LibraryGridView.Visibility = Visibility.Collapsed;
                LibraryEmptyPanel.Visibility = Visibility.Visible;
                LibraryEmptyTitle.Text = AppResources.GetString("SearchIndexingTitle", _currentLanguage);
                LibraryEmptySubtitle.Text = AppResources.GetString("SearchIndexingSubtitle", _currentLanguage);
                SectionSubtitleText.Text = AppResources.GetString("SearchIndexingStatus", _currentLanguage);
                return;
            }

            var results = await _storageService.SearchLibraryAsync(query, limit: 100, cancellationToken: token);

            // Re-check cancellation after the search: a newer keystroke may have
            // cancelled this query while the search was running. Without this the
            // stale result would then overwrite the cleared/newer view below.
            token.ThrowIfCancellationRequested();

            var favIds = (await _storageService.GetFavoritesAsync()).Select(f => f.Sha256).ToHashSet();

            token.ThrowIfCancellationRequested();

            _libraryItems.Clear();
            foreach (var a in results)
            {
                _libraryItems.Add(CreateLibraryVm(a, favIds.Contains(a.Sha256)));
            }
            SyncItems(LibraryGridView, _libraryItems);

            LibraryEmptyPanel.Visibility = _libraryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LibraryGridView.Visibility = _libraryItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_libraryItems.Count == 0)
            {
                LibraryEmptyTitle.Text = AppResources.GetString("SearchNoResultsTitle", _currentLanguage);
                LibraryEmptySubtitle.Text = string.Format(AppResources.CultureFor(_currentLanguage), AppResources.GetString("SearchNoResultsSubtitle", _currentLanguage), query);
                SectionSubtitleText.Text = AppResources.GetString("SearchResultsZero", _currentLanguage);
            }
            else
            {
                SectionSubtitleText.Text = _libraryItems.Count == 1
                    ? string.Format(AppResources.CultureFor(_currentLanguage), AppResources.GetString("SearchResultSingle", _currentLanguage), query)
                    : string.Format(AppResources.CultureFor(_currentLanguage), AppResources.GetString("SearchResultsCount", _currentLanguage), _libraryItems.Count, query);
            }
        }
        catch (OperationCanceledException)
        {
            // Stale query cancelled by subsequent keystrokes
        }
        catch (Exception ex)
        {
            App.Log($"Search failed: {ex.Message}");
        }
    }

    private async void OnToggleFavoriteClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnToggleFavoriteClicked), async () =>
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
        });
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
            catch (Exception ex)
            {
                App.Log($"OnOpenIcoInExplorerClicked failed: {ex.Message}");
            }
        }
    }

    private async void OnClearRecentClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnClearRecentClicked), async () =>
        {
            await _storageService.ClearRecentHistoryAsync();
            await LoadRecentAsync();
            ClearRecentButton.Visibility = Visibility.Collapsed;
        });
    }

    /// <summary>
    /// Applies an icon that already lives in the library to a folder the user picks. Without
    /// this the library was read-only: the only way to bind an icon to a folder was to go back
    /// to Explorer, and "Apply to Folder" existed in the resource dictionary but nothing called
    /// it.
    /// </summary>
    private async void OnApplyToFolderClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnApplyToFolderClicked), async () =>
        {
            if (sender is not MenuFlyoutItem item || item.Tag is not string sha256)
            {
                return;
            }

            LibraryItemVm? vm = _libraryItems.FirstOrDefault(i => i.Sha256 == sha256)
                ?? _favoriteItems.FirstOrDefault(i => i.Sha256 == sha256);
            if (vm is null || string.IsNullOrEmpty(vm.CanonicalIcoPath) || !File.Exists(vm.CanonicalIcoPath))
            {
                return;
            }

            var picker = new global::Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");

            global::Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            string folderPath = folder.Path;
            if (!_folderCustomizer.IsSupportedPath(folderPath))
            {
                await ShowAlertAsync(L("ErrorFolderApplyTitle"), L("ErrorFolderNotCustomizable"));
                return;
            }

            FolderBaseline baseline = new FolderBaseline
            {
                FolderAssignmentId = Guid.NewGuid(),
                OwnershipEpochId = Guid.NewGuid(),
                PriorSemantics = DesktopIniParser.ReadSemantics(folderPath),
                CapturedAt = DateTimeOffset.UtcNow
            };

            OperationResult<FolderIconSemantics> applyResult =
                await Task.Run(() => _folderCustomizer.ApplyIcon(folderPath, vm.CanonicalIcoPath));
            if (!applyResult.IsSuccess || applyResult.Value is null)
            {
                App.Log($"OnApplyToFolderClicked: apply failed: {applyResult.ErrorMessage}");
                await ShowAlertAsync(L("ErrorFolderApplyTitle"), L("ErrorFolderApplyMessage"));
                return;
            }

            await _storageService.SaveFolderAssignmentAsync(
                folderPath,
                baseline.OwnershipEpochId,
                applyResult.Value,
                baseline);

            await _storageService.RecordHistoryAsync(
                folderPath,
                Guid.NewGuid(),
                null,
                sha256,
                "apply");

            await LoadFoldersAsync();
        });
    }

    private async void OnRepairFolderClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnRepairFolderClicked), async () =>
        {
            if (sender is not Button btn || btn.Tag is not string folderPath)
            {
                return;
            }

            var folderVm = _folderItems.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folderVm == null)
            {
                return;
            }

            string canonicalIco = string.Empty;
            if (!string.IsNullOrEmpty(folderVm.ExpectedAssetHash))
            {
                var asset = await _storageService.GetAssetByHashAsync(folderVm.ExpectedAssetHash);
                if (asset != null) canonicalIco = asset.CanonicalIcoPath;
            }

            // RepairFolder performs filesystem writes and can be slow on network or
            // removable media; it must not block the dispatcher.
            var res = await Task.Run(() =>
                RepairEngine.RepairFolder(folderPath, canonicalIco, forceExternalConflictResolution: false));

            if (res.IsSuccess)
            {
                await LoadFoldersAsync();
            }
        });
    }

    private async void OnKeepExternalClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnKeepExternalClicked), async () =>
        {
            if (sender is Button btn && btn.Tag is string folderPath)
            {
                await _storageService.UpdateAssignmentHealthAsync(folderPath, FolderCustomizationState.Healthy, conflictState: null);
                await LoadFoldersAsync();
            }
        });
    }

    private async void OnReapplyFolderClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnReapplyFolderClicked), async () =>
        {
            if (sender is not Button btn || btn.Tag is not string folderPath)
            {
                return;
            }

            var folderVm = _folderItems.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folderVm == null)
            {
                return;
            }

            string canonicalIco = string.Empty;
            if (!string.IsNullOrEmpty(folderVm.ExpectedAssetHash))
            {
                var asset = await _storageService.GetAssetByHashAsync(folderVm.ExpectedAssetHash);
                if (asset != null) canonicalIco = asset.CanonicalIcoPath;
            }

            var res = await Task.Run(() =>
                RepairEngine.RepairFolder(folderPath, canonicalIco, forceExternalConflictResolution: true));

            if (res.IsSuccess)
            {
                await _storageService.UpdateAssignmentHealthAsync(folderPath, FolderCustomizationState.Healthy, conflictState: null);
                await LoadFoldersAsync();
            }
        });
    }

    private void OnOpenFolderInExplorerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string folderPath && Directory.Exists(folderPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                App.Log($"OnOpenFolderClicked failed: {ex.Message}");
            }
        }
    }

    private async void OnExportDiagnosticReportClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnExportDiagnosticReportClicked), ExportDiagnosticReportCoreAsync);
    }

    private async Task ExportDiagnosticReportCoreAsync()
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

        ExportReportButton.Content = L("ReportSaved");
    }

    private async void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pickerUpdates.IsActive || !_isLoaded) return;
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            try
            {
                _currentTheme = theme;
                ApplyTheme(theme);
                await PersistSettingsAsync(_settings with { Theme = theme });
            }
            catch (Exception ex)
            {
                App.Log($"OnThemeSelectionChanged failed: {ex.Message}");
            }
        }
    }

    private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pickerUpdates.IsActive || !_isLoaded) return;
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            try
            {
                // ApplyLanguage normalises to a supported BCP-47 locale; a "System" choice
                // stays persisted as "System" so it keeps following the OS language.
                string persisted = string.Equals(lang, "System", StringComparison.OrdinalIgnoreCase)
                    ? UserSettings.SystemValue
                    : AppResources.NormalizeLocale(lang);
                ApplyLanguage(persisted);
                await PersistSettingsAsync(_settings with { Language = persisted });
            }
            catch (Exception ex)
            {
                // Never crash the window on a settings write failure; the applied
                // language stays in memory and the failure is recorded locally.
                App.Log($"OnLanguageSelectionChanged failed: {ex.Message}");
            }
        }
    }

    public void ApplyTheme(string theme)
    {
        // Keep both the window field and the process-wide value in step: the field drives the
        // caption colour, and the process value seeds windows created later (the quick panel is
        // transient and has no settings surface of its own).
        _currentTheme = theme;
        App.CurrentTheme = theme;

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
            // ActualTheme is only dependable once the visual tree is loaded, and this method
            // also runs while the window is being constructed. The requested theme is known
            // from the very first frame, so the caption colour is derived from it instead.
            bool isDark = IsDarkTheme(_currentTheme);
            var fgColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonForegroundColor = fgColor;
            titleBar.ButtonHoverForegroundColor = fgColor;
            titleBar.ButtonInactiveForegroundColor = isDark ? global::Windows.UI.Color.FromArgb(128, 255, 255, 255) : global::Windows.UI.Color.FromArgb(128, 0, 0, 0);
        }
    }

    /// <summary>
    /// Resolves a stored theme choice to "is the chrome dark". The "System" value follows the
    /// application's resolved theme, which WinUI seeds from Windows before the first window.
    /// </summary>
    private static bool IsDarkTheme(string theme)
    {
        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return global::Microsoft.UI.Xaml.Application.Current?.RequestedTheme == ApplicationTheme.Dark;
    }

    /// <summary>
    /// Applies localized copy. <paramref name="localizeSettingsSurface"/> is false during
    /// construction: the settings surface is already populated there and is localized again
    /// immediately after the stored settings load, so doing it twice would rebuild the app
    /// icon preview list on the UI thread before the first frame is drawn.
    /// </summary>
    public void ApplyVisualTestState(string? section, string? theme, string? lang)
    {
        // Theme and language overrides exist only to capture reference screenshots.
        // Section navigation is a real feature (--section), so it always applies.
        if (App.VisualOverridesAllowed)
        {
            if (!string.IsNullOrEmpty(theme))
            {
                ApplyTheme(theme);
            }
            if (!string.IsNullOrEmpty(lang))
            {
                ApplyLanguage(lang);
            }
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

    private async void OnImportIconClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnImportIconClicked), ImportIconCoreAsync);
    }

    private async Task ImportIconCoreAsync()
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
            string iconsDir = OrnareConstants.IconsDirectory;
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
        await GuardAsync(nameof(OnExportIcoClicked), async () =>
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
        });
    }

    private void OnExportPngClicked(object sender, RoutedEventArgs e)
    {
        _ = GuardAsync(nameof(OnExportPngClicked), () => ExportPngCoreAsync(sender, e));
    }

    private async Task ExportPngCoreAsync(object sender, RoutedEventArgs e)
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

        string cacheDir = OrnareConstants.CacheDirectory;

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

        // RenderPngWithCache invokes the cache callbacks synchronously, so the
        // storage calls inside them must not block on the UI dispatcher: awaiting
        // them with GetAwaiter().GetResult() on the UI thread deadlocks instantly
        // because the storage continuation is queued back to the same dispatcher.
        // Running the whole render on a worker thread removes the dispatcher
        // capture and keeps the UI responsive during a large export.
        var renderResult = await Task.Run(() => _imagePipeline.RenderPngWithCache(
            recipe,
            sourceBytes,
            targetSize,
            cacheDirectory: cacheDir,
            cacheLookup: key => _storageService.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
            cacheStore: entry => _storageService.SaveRenderCacheEntryAsync(entry).GetAwaiter().GetResult()));

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



    private void LoadPresetsView()
    {
        _presetItems.Clear();

        foreach (var preset in StatusPresetCatalog.All)
        {
            string title = GetPresetTitle(preset.Id);
            string desc = GetPresetDescription(preset.Id);
            string applyText = L("Preset_ApplyToFolder");

            BitmapImage? previewBitmap = null;
            try
            {
                // Preset previews are a render cache, not scratch data: keeping them in
                // %TEMP% left orphaned OrnarePresets folders that no code ever removed.
                string previewDir = Path.Combine(OrnareConstants.CacheDirectory, "presets");
                Directory.CreateDirectory(previewDir);
                string previewPath = Path.Combine(previewDir, $"{preset.Id}_{_selectedPresetTemplateId}.png");

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
            catch (Exception ex)
            {
                // A missing preset thumbnail must not hide the preset itself.
                App.Log($"Preset preview render failed for '{preset.Id}': {ex.Message}");
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

    private string GetPresetTitle(string presetId) => presetId switch
    {
        "status.in-progress" => L("Preset_Status_InProgress"),
        "status.done" => L("Preset_Status_Done"),
        "status.archive" => L("Preset_Status_Archive"),
        "status.important" => L("Preset_Status_Important"),
        "priority.high" => L("Preset_Priority_High"),
        "priority.medium" => L("Preset_Priority_Medium"),
        "priority.low" => L("Preset_Priority_Low"),
        _ => presetId
    };

    private string GetPresetDescription(string presetId) => presetId switch
    {
        "status.in-progress" => L("Preset_Status_InProgress_Desc"),
        "status.done" => L("Preset_Status_Done_Desc"),
        "status.archive" => L("Preset_Status_Archive_Desc"),
        "status.important" => L("Preset_Status_Important_Desc"),
        "priority.high" => L("Preset_Priority_High_Desc"),
        "priority.medium" => L("Preset_Priority_Medium_Desc"),
        "priority.low" => L("Preset_Priority_Low_Desc"),
        _ => string.Empty
    };

    private void OnPresetTemplateRadioChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton selectedRadio && selectedRadio.Tag is string tag)
        {
            _selectedPresetTemplateId = tag;

            // XAML assigns the default IsChecked while the tree is still being built, so the
            // preset grid may not exist yet. The initial load fills it once the window is up.
            if (PresetsGridView is not null)
            {
                LoadPresetsView();
            }
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
            var renderResult = FolderTemplateRenderer.RenderPresetIco(
                _imagePipeline,
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
                Directory.CreateDirectory(OrnareConstants.LibraryDirectory);
                string canonicalIco = OrnareConstants.LibraryIcoPath(renderResult.Value.ContentHash);
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

                // Ensure render_cache entry exists and update LastAppliedRenderKey (F18/F21)
                if (!string.IsNullOrWhiteSpace(renderResult.Value.RenderKey))
                {
                    try
                    {
                        var existingCache = await _storageService.GetRenderCacheEntryAsync(renderResult.Value.RenderKey);
                        if (existingCache == null)
                        {
                            var fi = new FileInfo(canonicalIco);
                            await _storageService.SaveRenderCacheEntryAsync(new RenderCacheEntry
                            {
                                RenderKey = renderResult.Value.RenderKey,
                                RecipeVersion = 1,
                                RecipeJson = string.Empty,
                                SourceAssetHash = renderResult.Value.ContentHash,
                                CanonicalIcoPath = canonicalIco,
                                FrameSizes = string.Join(",", renderResult.Value.FrameSizes),
                                FileSizeBytes = fi.Length,
                                CreatedAt = DateTimeOffset.UtcNow,
                                LastUsedAt = DateTimeOffset.UtcNow
                            });
                        }

                        await _storageService.SetLastAppliedRenderKeyAsync(renderResult.Value.RenderKey);
                    }
                    catch (Exception ex)
                    {
                        App.Log($"Non-critical LastAppliedRenderKey save failure: {ex.Message}");
                    }
                }

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
                try
                {
                    File.Delete(tempIco);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Staging cleanup is best-effort and must not fail the export flow.
                    App.Log($"Staging ICO cleanup failed: {ex.Message}");
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose may run twice and a token source may already be disposed by a
        // completed operation; cancellation after disposal is a no-op, not a fault.
        // These are the only intentionally silent catches in this file: the object
        // being disposed is already unusable, so there is nothing to log and no
        // recovery action to take.
        try
        {
            _stagingCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing to cancel.
        }
        _stagingCts?.Dispose();

        try
        {
            _searchCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing to cancel.
        }
        _searchCts?.Dispose();

        try
        {
            _windowCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed; nothing to cancel.
        }
        _windowCts.Dispose();

        GC.SuppressFinalize(this);
    }
}

[Microsoft.UI.Xaml.Data.Bindable]
public class StagedFolderItemVm
{
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string? ExistingAssignmentId { get; set; }
    public Visibility ExistingBadgeVisibility => ExistingAssignmentId != null ? Visibility.Visible : Visibility.Collapsed;
    public string ExistingBadgeText { get; set; } = string.Empty;
    public string PortabilityText { get; set; } = string.Empty;
    public Visibility PortabilityVisibility => !string.IsNullOrEmpty(PortabilityText) ? Visibility.Visible : Visibility.Collapsed;
}

[Microsoft.UI.Xaml.Data.Bindable]
public class RejectedFolderItemVm
{
    public string FolderPath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
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
    public string FavoriteToolTip { get; set; } = string.Empty;
    public string ContextToggleFavoriteText { get; set; } = string.Empty;
    public string ContextOpenExplorerText { get; set; } = string.Empty;
    public string ContextApplyToFolderText { get; set; } = string.Empty;
    public string ContextManageTagsText { get; set; } = string.Empty;
    public string ContextManageCollectionsText { get; set; } = string.Empty;
    public string ContextExportIcoText { get; set; } = string.Empty;
    public string ContextExportPngText { get; set; } = string.Empty;
    public string ExportPng1024Text { get; set; } = string.Empty;
    public string ExportPng512Text { get; set; } = string.Empty;
    public string ExportPng256Text { get; set; } = string.Empty;
    public string RemoveFromCollectionText { get; set; } = string.Empty;
}

[Microsoft.UI.Xaml.Data.Bindable]
public class CollectionItemVm
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long SortKey { get; set; }
    public string AssetCountText { get; set; } = string.Empty;
    public string MoveUpText { get; set; } = string.Empty;
    public string MoveDownText { get; set; } = string.Empty;
    public string RenameText { get; set; } = string.Empty;
    public string DeleteText { get; set; } = string.Empty;
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
    public string PortabilityLabel { get; set; } = string.Empty;
    public Visibility PortabilityVisibility { get; set; } = Visibility.Collapsed;
    public string RepairText { get; set; } = string.Empty;
    public string KeepExternalText { get; set; } = string.Empty;
    public string ReapplyText { get; set; } = string.Empty;
    public string RepairAutomationName { get; set; } = string.Empty;
    public string KeepExternalAutomationName { get; set; } = string.Empty;
    public string ReapplyAutomationName { get; set; } = string.Empty;
    public string OpenFolderAutomationName { get; set; } = string.Empty;
}
