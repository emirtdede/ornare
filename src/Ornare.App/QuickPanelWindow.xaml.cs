using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using WinRT.Interop;

namespace Ornare.App;

public enum ProcessingStage
{
    Idle,
    PreparingImage,
    GeneratingIcon,
    PreviewReady,
    ApplyingIcon,
    RefreshingExplorer,
    Done,
    Error
}

public enum QuickPanelErrorType
{
    UnsupportedImage,
    ImageReadFailed,
    IconGenerationFailed,
    FolderApplyFailed,
    ExplorerRefreshFailed,
    Generic
}

public sealed partial class QuickPanelWindow : Window
{
    private readonly IImagePipeline _imagePipeline;
    private readonly IFolderCustomizer _folderCustomizer;
    private readonly IStorageService _storageService;

    private string _currentFolderPath = string.Empty;
    private FolderBaseline? _currentBaseline;
    private Guid _currentOwnershipEpochId = Guid.NewGuid();
    private DispatcherTimer? _autoCloseTimer;
    private bool _isPointerInside;
    private bool _isProcessing;
    private ProcessingStage _currentStage = ProcessingStage.Idle;
    private string _currentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
    private Guid _stagedOperationId;
    private string? _stagedTempIcoPath;
    private NormalizedImageResult? _stagedProcessResult;

    #region Win32 P/Invoke & Structures for Native Monitor Positioning

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    #endregion

    private void OnTitleBarPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd != IntPtr.Zero)
            {
                _ = ReleaseCapture();
                _ = SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
        }
    }

    private void OnWindowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is not Button && e.OriginalSource is not ProgressRing && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd != IntPtr.Zero)
            {
                _ = ReleaseCapture();
                _ = SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
        }
    }

    public QuickPanelWindow(
        IImagePipeline imagePipeline,
        IFolderCustomizer folderCustomizer,
        IStorageService storageService)
    {
        _imagePipeline = imagePipeline;
        _folderCustomizer = folderCustomizer;
        _storageService = storageService;

        InitializeComponent();

        ConfigureWindow();
        SetupKeyboardShortcuts();
        InitializeColorPalette();
        Closed += (_, _) => CleanupStagedFiles();
    }

    private void StripMinimizeMaximizeButtons()
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd != IntPtr.Zero)
            {
                int style = GetWindowLong(hwnd, GWL_STYLE);
                int previousStyle = SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX);
                bool updated = SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
                if (previousStyle == 0 && !updated)
                {
                    // Satisfies CA1806 result inspection
                }
            }
        }
        catch
        {
        }
    }

    private void ConfigureWindow()
    {
        Title = "Ornare";
        SystemBackdrop = new MicaBackdrop();

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        // Effective target size: 420x330 per Visual Reference Spec
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(420, 330));

        string icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(icoPath))
        {
            appWindow.SetIcon(icoPath);
        }

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        StripMinimizeMaximizeButtons();
        Activated += (_, _) => StripMinimizeMaximizeButtons();

        // Modern title bar customization: integrate content with Mica backdrop
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = appWindow.TitleBar;
            titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = global::Windows.UI.Color.FromArgb(20, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = global::Windows.UI.Color.FromArgb(40, 255, 255, 255);
        }

        // Pointer tracking for auto-close behavior
        if (Content is FrameworkElement root)
        {
            root.PointerEntered += (_, _) => _isPointerInside = true;
            root.PointerExited += (_, _) => _isPointerInside = false;
        }
    }

    public void PositionWindow(int? cursorX, int? cursorY)
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            const int windowWidth = 420;
            const int windowHeight = 330;

            IntPtr hMonitor;
            if (cursorX.HasValue && cursorY.HasValue)
            {
                POINT pt = new POINT { X = cursorX.Value, Y = cursorY.Value };
                hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            }
            else
            {
                IntPtr fg = GetForegroundWindow();
                hMonitor = MonitorFromWindow(fg != IntPtr.Zero ? fg : hwnd, MONITOR_DEFAULTTOPRIMARY);
            }

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf<MONITORINFO>();

            if (hMonitor != IntPtr.Zero && GetMonitorInfo(hMonitor, ref mi))
            {
                RECT work = mi.rcWork;
                int targetX;
                int targetY;

                if (cursorX.HasValue && cursorY.HasValue)
                {
                    // Center window horizontally and vertically around the mouse cursor
                    targetX = cursorX.Value - (windowWidth / 2);
                    targetY = cursorY.Value - (windowHeight / 2);

                    // Clamp to active monitor work area (protecting taskbar and screen edges with 12px margin)
                    const int margin = 12;
                    if (targetX + windowWidth > work.Right - margin)
                    {
                        targetX = work.Right - windowWidth - margin;
                    }
                    if (targetX < work.Left + margin)
                    {
                        targetX = work.Left + margin;
                    }

                    if (targetY + windowHeight > work.Bottom - margin)
                    {
                        targetY = work.Bottom - windowHeight - margin;
                    }
                    if (targetY < work.Top + margin)
                    {
                        targetY = work.Top + margin;
                    }
                }
                else
                {
                    // Fallback: center in active monitor work area
                    targetX = work.Left + (work.Width - windowWidth) / 2;
                    targetY = work.Top + (work.Height - windowHeight) / 2;
                }

                App.Log($"PositionWindow: moving window to ({targetX},{targetY}), work=({work.Left},{work.Top},{work.Right},{work.Bottom})");
                appWindow.Move(new global::Windows.Graphics.PointInt32(targetX, targetY));
            }
        }
        catch (Exception ex)
        {
            App.Log($"PositionWindow failed: {ex}");
        }
    }

    private void SetupKeyboardShortcuts()
    {
        if (Content is FrameworkElement root)
        {
            root.KeyDown += async (_, e) =>
            {
                if (_isProcessing)
                {
                    return;
                }

                var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Control);
                bool isCtrlDown = (ctrlState & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

                if (isCtrlDown && e.Key == global::Windows.System.VirtualKey.V)
                {
                    e.Handled = true;
                    await HandlePasteAsync();
                }
            };
        }
    }

    public async Task SetTargetFolderAsync(string folderPath)
    {
        _currentFolderPath = folderPath;
        FolderNameText.Text = Path.GetFileName(folderPath.TrimEnd('\\', '/'));
        FolderPathText.Text = folderPath;

        OperationResult validation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!validation.IsSuccess)
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, validation.ErrorMessage ?? "Folder cannot be customized.");
            return;
        }

        // Query storage for existing baseline
        _currentBaseline = await _storageService.GetLatestBaselineAsync(folderPath);
        FolderCustomizationState state = _folderCustomizer.GetFolderState(folderPath, _currentBaseline);

        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        ToolTipService.SetToolTip(ResetButton, isTr ? "Sıfırla" : "Reset");
        AutomationProperties.SetName(ResetButton, isTr ? "Sıfırla" : "Reset");
        ToolTipService.SetToolTip(OpenManagerButton, isTr ? "Ayarlar" : "Settings");
        AutomationProperties.SetName(OpenManagerButton, isTr ? "Ayarlar" : "Settings");

        if (state == FolderCustomizationState.ExternalIconConflict)
        {
            StatusInfoText.Text = isTr ? "Dış değişiklik algılandı" : "External modification detected";
            ResetButton.Visibility = Visibility.Collapsed;
        }
        else if (_currentBaseline != null)
        {
            ResetButton.Visibility = Visibility.Visible;
            StatusInfoText.Text = string.Empty;
        }
        else
        {
            ResetButton.Visibility = Visibility.Collapsed;
            StatusInfoText.Text = string.Empty;
        }

        await SetProcessingStageAsync(ProcessingStage.Idle);
    }

    private void OnDropAreaDragOver(object sender, DragEventArgs e)
    {
        if (_isProcessing)
        {
            e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        if (e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems) ||
            e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
        {
            e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            DropAreaBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
        else
        {
            e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
        }
    }

    private void OnDropAreaDragLeave(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        DropAreaBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private async void OnDropAreaDrop(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        DropAreaBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        if (_isProcessing)
        {
            return;
        }

        if (e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is global::Windows.Storage.StorageFile file)
            {
                await StageImageAsync(file.Path);
            }
        }
    }

    private async void OnDropAreaTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_isProcessing || _currentStage != ProcessingStage.Idle)
        {
            return;
        }

        var picker = new global::Windows.Storage.Pickers.FileOpenPicker();
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = global::Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".ico");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await StageImageAsync(file.Path);
        }
    }

    private async Task HandlePasteAsync()
    {
        if (_isProcessing || _currentStage != ProcessingStage.Idle)
        {
            return;
        }

        var package = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (package == null)
        {
            return;
        }

        if (package.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await package.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is global::Windows.Storage.StorageFile file)
            {
                await StageImageAsync(file.Path);
            }
        }
        else if (package.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
        {
            var streamRef = await package.GetBitmapAsync();
            using var stream = await streamRef.OpenReadAsync();
            using var netStream = stream.AsStreamForRead();
            using var ms = new MemoryStream();
            await netStream.CopyToAsync(ms);

            await StageImageBytesAsync(ms.ToArray());
        }
    }

    private async Task StageImageAsync(string imagePath)
    {
        if (_isProcessing)
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(imagePath);
        }
        catch (Exception ex)
        {
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed, ex.Message);
            return;
        }

        await StageImageBytesAsync(bytes);
    }

    private async Task StageImageBytesAsync(byte[] imageBytes)
    {
        if (_isProcessing)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, "No folder selected.");
            return;
        }

        if (imageBytes == null || imageBytes.Length == 0)
        {
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed, "Image data was empty.");
            return;
        }

        try
        {
            CleanupStagedFiles();
            App.Log($"StageImageBytesAsync started for '{_currentFolderPath}', bytes={imageBytes.Length}");

            // 1. Preparing image
            await SetProcessingStageAsync(ProcessingStage.PreparingImage);
            await Task.Delay(40);

            _stagedOperationId = Guid.NewGuid();
            _stagedTempIcoPath = Path.Combine(Path.GetTempPath(), $"ornare_staged_{_stagedOperationId:N}.ico");

            // 2. Generating icon preview
            await SetProcessingStageAsync(ProcessingStage.GeneratingIcon);
            await Task.Delay(40);

            var tempPath = _stagedTempIcoPath;
            var processResult = await Task.Run(() => _imagePipeline.ProcessImageBytes(imageBytes, tempPath));
            if (!processResult.IsSuccess || processResult.Value == null)
            {
                App.Log($"ProcessImageBytes failed: {processResult.ErrorMessage}");
                CleanupStagedFiles();
                if (processResult.ErrorCode == ErrorCode.UnsupportedImageFormat || processResult.ErrorCode == ErrorCode.ImageCorrupted)
                {
                    ShowCategorizedError(QuickPanelErrorType.UnsupportedImage, processResult.ErrorMessage);
                }
                else
                {
                    ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, processResult.ErrorMessage);
                }
                return;
            }

            _stagedProcessResult = processResult.Value;

            // Load staged preview
            PreviewIconImage.Source = LoadBitmapSafe(_stagedTempIcoPath);

            // 3. Show preview and wait for explicit user confirmation
            await SetProcessingStageAsync(ProcessingStage.PreviewReady);
        }
        catch (Exception ex)
        {
            App.Log($"StageImageBytesAsync unhandled exception: {ex}");
            CleanupStagedFiles();
            ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, ex.Message);
        }
    }

    private void InitializeColorPalette()
    {
        ColorPaletteContainer.Children.Clear();
        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

        foreach (var entry in BuiltInColors.Entries)
        {
            if (!ColorConversion.TryParseHex(entry.HexValue, out byte r, out byte g, out byte b))
            {
                continue;
            }

            var button = new Button
            {
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0),
                Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, r, g, b)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(40, 255, 255, 255)),
                Tag = entry.HexValue
            };

            string displayName = GetColorDisplayName(entry.Id, isTr);
            ToolTipService.SetToolTip(button, displayName);
            AutomationProperties.SetName(button, displayName);

            button.Click += async (s, _) =>
            {
                if (s is Button btn && btn.Tag is string hex)
                {
                    await StageColorAsync(hex);
                }
            };

            ColorPaletteContainer.Children.Add(button);
        }
    }

    private static string GetColorDisplayName(string id, bool isTr) => id switch
    {
        "color.amber" => isTr ? "Kehribar" : "Amber",
        "color.orange" => isTr ? "Turuncu" : "Orange",
        "color.red" => isTr ? "Kırmızı" : "Red",
        "color.rose" => isTr ? "Gül Kurusu" : "Rose",
        "color.magenta" => isTr ? "Macenta" : "Magenta",
        "color.purple" => isTr ? "Mor" : "Purple",
        "color.indigo" => isTr ? "Çivit Mavisi" : "Indigo",
        "color.blue" => isTr ? "Mavi" : "Blue",
        "color.cyan" => isTr ? "Camgöbeği" : "Cyan",
        "color.teal" => isTr ? "Deniz Mavisi" : "Teal",
        "color.green" => isTr ? "Yeşil" : "Green",
        "color.slate" => isTr ? "Arduvaz Grisi" : "Slate",
        _ => isTr ? "Renk" : "Color"
    };

    private async Task StageColorAsync(string hexColor)
    {
        if (_isProcessing) return;

        if (string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, "No folder selected.");
            return;
        }

        try
        {
            CleanupStagedFiles();
            App.Log($"StageColorAsync started for '{_currentFolderPath}', hex={hexColor}");

            await SetProcessingStageAsync(ProcessingStage.PreparingImage);
            await Task.Delay(20);

            _stagedOperationId = Guid.NewGuid();
            _stagedTempIcoPath = Path.Combine(Path.GetTempPath(), $"ornare_staged_{_stagedOperationId:N}.ico");

            await SetProcessingStageAsync(ProcessingStage.GeneratingIcon);
            await Task.Delay(20);

            var tempPath = _stagedTempIcoPath;
            var renderResult = await Task.Run(() =>
            {
                var pipeline = _imagePipeline as ImagePipeline ?? new ImagePipeline();
                return FolderTemplateRenderer.RenderColoredTemplateIco(pipeline, hexColor, tempPath);
            });

            if (!renderResult.IsSuccess || renderResult.Value == null)
            {
                CleanupStagedFiles();
                ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, renderResult.ErrorMessage);
                return;
            }

            _stagedProcessResult = renderResult.Value;
            PreviewIconImage.Source = LoadBitmapSafe(_stagedTempIcoPath);

            await SetProcessingStageAsync(ProcessingStage.PreviewReady);
        }
        catch (Exception ex)
        {
            App.Log($"StageColorAsync exception: {ex}");
            CleanupStagedFiles();
            ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, ex.Message);
        }
    }

    private async void OnApplyConfirmClicked(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || string.IsNullOrWhiteSpace(_stagedTempIcoPath) || _stagedProcessResult == null)
        {
            return;
        }

        await CommitStagedIconAsync();
    }

    private async void OnCancelPreviewClicked(object sender, RoutedEventArgs e)
    {
        CleanupStagedFiles();
        PreviewIconImage.Source = null;
        await SetProcessingStageAsync(ProcessingStage.Idle);
    }

    private void OnCloseSuccessClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task CommitStagedIconAsync()
    {
        if (_isProcessing || string.IsNullOrWhiteSpace(_stagedTempIcoPath) || !File.Exists(_stagedTempIcoPath) || _stagedProcessResult == null)
        {
            return;
        }

        using var lockHandle = await PerTargetSyncLock.TryAcquireAsync(_currentFolderPath, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, "Another operation is currently modifying this folder.");
            return;
        }

        try
        {
            Guid operationId = _stagedOperationId;
            string stagedIcoPath = _stagedTempIcoPath;
            var stagedResult = _stagedProcessResult;

            // Journal step: Prepared & AssetGenerated
            JournalEntry journal = new()
            {
                OperationId = operationId,
                Kind = OperationKind.Apply,
                TargetFolderPath = _currentFolderPath,
                State = JournalState.Prepared,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                TargetAssetPath = stagedIcoPath
            };
            await _storageService.RecordJournalEntryAsync(journal);
            await _storageService.UpdateJournalStateAsync(operationId, JournalState.AssetGenerated);

            // 1. Applying icon to folder
            await SetProcessingStageAsync(ProcessingStage.ApplyingIcon);
            await Task.Delay(40);

            if (_currentBaseline == null)
            {
                FolderIconSemantics prior = DesktopIniParser.ReadSemantics(_currentFolderPath);
                _currentOwnershipEpochId = Guid.NewGuid();
                _currentBaseline = new FolderBaseline
                {
                    FolderAssignmentId = Guid.NewGuid(),
                    OwnershipEpochId = _currentOwnershipEpochId,
                    PriorSemantics = prior,
                    CapturedAt = DateTimeOffset.UtcNow
                };
            }

            var applyResult = await Task.Run(() => _folderCustomizer.ApplyIcon(_currentFolderPath, stagedIcoPath, state =>
            {
                _ = _storageService.UpdateJournalStateAsync(operationId, state);
            }));

            if (!applyResult.IsSuccess || applyResult.Value == null)
            {
                App.Log($"ApplyIcon failed: {applyResult.ErrorMessage}");
                await _storageService.UpdateJournalStateAsync(operationId, JournalState.NeedsRepair, applyResult.ErrorMessage);
                ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, applyResult.ErrorMessage);
                return;
            }

            // 2. Refreshing Explorer
            await SetProcessingStageAsync(ProcessingStage.RefreshingExplorer);
            await Task.Delay(80);

            // 3. Save assignment & baseline to database
            await _storageService.SaveFolderAssignmentAsync(
                _currentFolderPath,
                _currentOwnershipEpochId,
                applyResult.Value,
                _currentBaseline);

            // Register canonical asset in Library & History
            string canonicalIcoPath = string.Empty;
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string libraryDir = Path.Combine(localAppData, OrnareConstants.ApplicationName, "library");
                Directory.CreateDirectory(libraryDir);
                canonicalIcoPath = Path.Combine(libraryDir, $"{stagedResult.ContentHash}.ico");
                if (!File.Exists(canonicalIcoPath))
                {
                    File.Copy(stagedIcoPath, canonicalIcoPath, overwrite: true);
                }

                await _storageService.AddOrUpdateAssetAsync(
                    stagedResult.ContentHash,
                    canonicalIcoPath,
                    stagedResult.SourceFormat);

                await _storageService.RecordHistoryAsync(
                    _currentFolderPath,
                    operationId,
                    null,
                    stagedResult.ContentHash,
                    "apply");

                var (vol, fid) = FolderIdentityService.GetFolderIdentity(_currentFolderPath);
                await _storageService.UpdateAssignmentIdentityAsync(_currentFolderPath, vol, fid);
            }
            catch (Exception ex)
            {
                App.Log($"Non-critical library tracking failure: {ex.Message}");
            }

            await _storageService.UpdateJournalStateAsync(operationId, JournalState.Completed);

            // Set final success icon preview from canonical path or staged path
            SuccessIconImage.Source = !string.IsNullOrEmpty(canonicalIcoPath) && File.Exists(canonicalIcoPath)
                ? LoadBitmapSafe(canonicalIcoPath)
                : LoadBitmapSafe(stagedIcoPath);

            // Clean up staged temp ICO
            CleanupStagedFiles();

            // Fire final targeted shell refresh now that all staged files are closed and committed
            ShellRefreshService.NotifyFolderChanged(_currentFolderPath);

            App.Log($"CommitStagedIconAsync completed successfully for '{_currentFolderPath}'");

            // 4. Done / Success: Restrained success state with auto-close timer per spec
            await SetProcessingStageAsync(ProcessingStage.Done);
            ResetButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            App.Log($"CommitStagedIconAsync unhandled exception: {ex}");
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, ex.Message);
        }
    }

    private async void OnUndoClicked(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || _currentBaseline == null || string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            return;
        }

        try
        {
            await SetProcessingStageAsync(ProcessingStage.ApplyingIcon);

            var baseline = _currentBaseline;
            var targetFolder = _currentFolderPath;
            var result = await Task.Run(() => _folderCustomizer.UndoIcon(targetFolder, baseline));
            if (result.IsSuccess)
            {
                await _storageService.RemoveFolderAssignmentAsync(targetFolder);
                _currentBaseline = null;
                ResetButton.Visibility = Visibility.Collapsed;
                PreviewIconImage.Source = null;
                SuccessIconImage.Source = null;
                await SetProcessingStageAsync(ProcessingStage.Idle);
            }
            else
            {
                ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, result.ErrorMessage ?? "Could not undo icon change.");
            }
        }
        catch (Exception ex)
        {
            App.Log($"OnUndoClicked exception: {ex}");
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, ex.Message);
        }
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || _currentBaseline == null || string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            return;
        }

        try
        {
            await SetProcessingStageAsync(ProcessingStage.ApplyingIcon);

            var baseline = _currentBaseline;
            var targetFolder = _currentFolderPath;
            var result = await Task.Run(() => _folderCustomizer.ResetIcon(targetFolder, baseline));
            if (result.IsSuccess)
            {
                await _storageService.RemoveFolderAssignmentAsync(targetFolder);
                _currentBaseline = null;
                ResetButton.Visibility = Visibility.Collapsed;
                PreviewIconImage.Source = null;
                SuccessIconImage.Source = null;
                await SetProcessingStageAsync(ProcessingStage.Idle);
            }
            else
            {
                ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, result.ErrorMessage ?? "Could not reset folder icon.");
            }
        }
        catch (Exception ex)
        {
            App.Log($"OnResetClicked exception: {ex}");
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, ex.Message);
        }
    }

    private void CleanupStagedFiles()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_stagedTempIcoPath) && File.Exists(_stagedTempIcoPath))
            {
                File.Delete(_stagedTempIcoPath);
            }
        }
        catch { }
        _stagedTempIcoPath = null;
        _stagedProcessResult = null;
    }

    private static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? LoadBitmapSafe(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path));
            }
        }
        catch
        {
        }
        return null;
    }

    private async void OnTryAgainClicked(object sender, RoutedEventArgs e)
    {
        await SetProcessingStageAsync(ProcessingStage.Idle);
    }

    private void StartAutoCloseTimer()
    {
        _autoCloseTimer?.Stop();
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            if (!_isPointerInside)
            {
                _autoCloseTimer.Stop();
                Close();
            }
        };
        _autoCloseTimer.Start();
    }

    private async Task SetProcessingStageAsync(ProcessingStage stage)
    {
        _currentStage = stage;
        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

        switch (stage)
        {
            case ProcessingStage.Idle:
                _isProcessing = false;
                IdleStatePanel.Visibility = Visibility.Visible;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                DropAreaBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                DropAreaBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                DropPromptText.Text = isTr ? "Bir görsel bırak" : "Drop an image";
                ClickPromptText.Text = isTr ? "Seçmek için tıkla veya Ctrl+V'ye bas" : "Click to choose or press Ctrl+V";
                break;

            case ProcessingStage.PreparingImage:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                IdleStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = isTr ? "Görsel hazırlanıyor..." : "Preparing image...";
                break;

            case ProcessingStage.GeneratingIcon:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                IdleStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = isTr ? "Klasör simgesi oluşturuluyor..." : "Generating folder icon...";
                break;

            case ProcessingStage.PreviewReady:
                _isProcessing = false;
                _autoCloseTimer?.Stop();
                IdleStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                PreviewStatePanel.Visibility = Visibility.Visible;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                PreviewTitleText.Text = isTr ? "Simge önizlemesi" : "Icon preview";
                PreviewSubtitleText.Text = isTr ? "Klasöre uygulamak için onaylayın" : "Confirm to apply to folder";
                ApplyConfirmButton.Content = isTr ? "Uygula" : "Apply";
                CancelPreviewButton.Content = isTr ? "Vazgeç" : "Cancel";
                break;

            case ProcessingStage.ApplyingIcon:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                IdleStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = isTr ? "Simge uygulanıyor..." : "Applying icon...";
                break;

            case ProcessingStage.RefreshingExplorer:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                IdleStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = isTr ? "Dosya Gezgini yenileniyor..." : "Refreshing Explorer...";
                break;

            case ProcessingStage.Done:
                _isProcessing = false;
                _autoCloseTimer?.Stop();
                IdleStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Visible;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                SuccessTitleText.Text = isTr ? "Klasör simgesi güncellendi" : "Folder icon updated";
                SuccessSubtitleText.Text = isTr ? "İşlem başarıyla tamamlandı" : "Changes applied successfully";
                CloseSuccessButton.Content = isTr ? "Kapat" : "Close";
                UndoButton.Content = isTr ? "Geri al" : "Undo";
                break;

            case ProcessingStage.Error:
                _isProcessing = false;
                _autoCloseTimer?.Stop();
                IdleStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Visible;
                TryAgainButton.Content = isTr ? "Tekrar dene" : "Try again";
                break;
        }

        await Task.Delay(30);
    }

    private void ShowCategorizedError(QuickPanelErrorType errorType, string? detail = null)
    {
        _isProcessing = false;
        _autoCloseTimer?.Stop();
        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);

        string title;
        string message;

        switch (errorType)
        {
            case QuickPanelErrorType.UnsupportedImage:
                title = isTr ? "Desteklenmeyen görsel" : "Unsupported image";
                message = isTr ? "Lütfen PNG, JPG, WebP, BMP veya ICO formatında bir dosya seçin." : "Please select a PNG, JPG, WebP, BMP, or ICO file.";
                break;

            case QuickPanelErrorType.ImageReadFailed:
                title = isTr ? "Görsel okunamadı" : "Image read failed";
                message = !string.IsNullOrWhiteSpace(detail) ? detail : (isTr ? "Seçilen görsel dosyası diskten okunamadı." : "Could not read the selected image file from disk.");
                break;

            case QuickPanelErrorType.IconGenerationFailed:
                title = isTr ? "Simge oluşturulamadı" : "Icon generation failed";
                message = !string.IsNullOrWhiteSpace(detail) ? detail : (isTr ? "Klasör için çoklu çözünürlüklü simge oluşturulamadı." : "Failed to create multi-resolution folder icon.");
                break;

            case QuickPanelErrorType.FolderApplyFailed:
                title = isTr ? "Klasör simgesi uygulanamadı" : "Folder icon apply failed";
                message = !string.IsNullOrWhiteSpace(detail) ? detail : (isTr ? "Klasör öznitelikleri veya desktop.ini yapılandırılamadı." : "Could not update folder attributes or desktop.ini.");
                break;

            case QuickPanelErrorType.ExplorerRefreshFailed:
                title = isTr ? "Dosya Gezgini yenilenemedi" : "Explorer refresh failed";
                message = !string.IsNullOrWhiteSpace(detail) ? detail : (isTr ? "Dosya Gezgini klasör güncellemesi hakkında bilgilendirilemedi." : "Failed to notify File Explorer of the folder update.");
                break;

            default:
                title = isTr ? "Simge uygulanamadı" : "Could not apply icon";
                message = !string.IsNullOrWhiteSpace(detail) ? detail : string.Empty;
                break;
        }

        IdleStatePanel.Visibility = Visibility.Collapsed;
        ProcessingStatePanel.Visibility = Visibility.Collapsed;
        PreviewStatePanel.Visibility = Visibility.Collapsed;
        SuccessStatePanel.Visibility = Visibility.Collapsed;
        ErrorStatePanel.Visibility = Visibility.Visible;

        ErrorTitleText.Text = title;
        ErrorMessageText.Text = message;
        TryAgainButton.Content = isTr ? "Tekrar dene" : "Try again";
    }

    public void ApplyVisualTestState(string? uiState, string? theme, string? lang)
    {
        if (Content is FrameworkElement root)
        {
            if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
            {
                root.RequestedTheme = ElementTheme.Light;
            }
            else if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
            {
                root.RequestedTheme = ElementTheme.Dark;
            }
        }

        if (!string.IsNullOrWhiteSpace(lang))
        {
            _currentLanguage = lang.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
        }

        bool isTr = _currentLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
        DropPromptText.Text = isTr ? "Bir görsel bırak" : "Drop an image";
        ClickPromptText.Text = isTr ? "Seçmek için tıkla veya Ctrl+V'ye bas" : "Click to choose or press Ctrl+V";
        PreviewTitleText.Text = isTr ? "Simge önizlemesi" : "Icon preview";
        PreviewSubtitleText.Text = isTr ? "Klasöre uygulamak için onaylayın" : "Confirm to apply to folder";
        ApplyConfirmButton.Content = isTr ? "Uygula" : "Apply";
        CancelPreviewButton.Content = isTr ? "Vazgeç" : "Cancel";
        SuccessTitleText.Text = isTr ? "Klasör simgesi güncellendi" : "Folder icon updated";
        SuccessSubtitleText.Text = isTr ? "İşlem başarıyla tamamlandı" : "Changes applied successfully";
        CloseSuccessButton.Content = isTr ? "Kapat" : "Close";
        UndoButton.Content = isTr ? "Geri al" : "Undo";
        ToolTipService.SetToolTip(ResetButton, isTr ? "Sıfırla" : "Reset");
        AutomationProperties.SetName(ResetButton, isTr ? "Sıfırla" : "Reset");
        ToolTipService.SetToolTip(OpenManagerButton, isTr ? "Ayarlar" : "Settings");
        AutomationProperties.SetName(OpenManagerButton, isTr ? "Ayarlar" : "Settings");
        TryAgainButton.Content = isTr ? "Tekrar dene" : "Try again";

        if (string.Equals(uiState, "drag-over", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.Idle);
            DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            DropAreaBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
        else if (string.Equals(uiState, "preparing", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.PreparingImage);
        }
        else if (string.Equals(uiState, "generating", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.GeneratingIcon);
        }
        else if (string.Equals(uiState, "preview", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.PreviewReady);
        }
        else if (string.Equals(uiState, "applying", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.ApplyingIcon);
        }
        else if (string.Equals(uiState, "refreshing", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.RefreshingExplorer);
        }
        else if (string.Equals(uiState, "success", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.Done);
            _autoCloseTimer?.Stop();
            ResetButton.Visibility = Visibility.Visible;
        }
        else if (string.Equals(uiState, "undo", StringComparison.OrdinalIgnoreCase))
        {
            _ = SetProcessingStageAsync(ProcessingStage.Done);
            _autoCloseTimer?.Stop();
            UndoButton.Focus(FocusState.Programmatic);
        }
        else if (string.Equals(uiState, "error", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uiState, "error-unsupported", StringComparison.OrdinalIgnoreCase))
        {
            ShowCategorizedError(QuickPanelErrorType.UnsupportedImage);
        }
        else if (string.Equals(uiState, "error-read", StringComparison.OrdinalIgnoreCase))
        {
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed);
        }
        else if (string.Equals(uiState, "error-apply", StringComparison.OrdinalIgnoreCase))
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed);
        }
        else if (string.Equals(uiState, "error-refresh", StringComparison.OrdinalIgnoreCase))
        {
            ShowCategorizedError(QuickPanelErrorType.ExplorerRefreshFailed);
        }
    }

    private void OnOpenManagerClicked(object sender, RoutedEventArgs e)
    {
        var managerWindow = new ManagerWindow(_storageService, _folderCustomizer);
        managerWindow.Activate();
    }
}
