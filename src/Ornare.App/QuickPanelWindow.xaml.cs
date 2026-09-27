using System.Collections.ObjectModel;
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
using Ornare.Imaging.Templates;
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

public sealed partial class QuickPanelWindow : Window, IDisposable
{
    private readonly IImagePipeline _imagePipeline;
    private readonly IFolderCustomizer _folderCustomizer;
    private readonly IStorageService _storageService;
    private readonly PersonalizationStagingModel _stagingModel = new();

    public ObservableCollection<FolderTemplateCardViewModel> FolderTemplateCards { get; } = new();

    private string _currentFolderPath = string.Empty;
    private FolderBaseline? _currentBaseline;
    private Guid _currentOwnershipEpochId = Guid.NewGuid();
    private DispatcherTimer? _autoCloseTimer;
    private bool _isPointerInside;
    private bool _isProcessing;
    private ProcessingStage _currentStage = ProcessingStage.Idle;
    private string _currentLanguage = AppResources.NormalizeLocale(null);
    private Guid _stagedOperationId;
    private string? _stagedTempIcoPath;
    private NormalizedImageResult? _stagedProcessResult;
    private string? _stagedCustomColorHex;
    private byte[]? _stagedImageBytes;
    private ImagePlacementMode _stagedPlacementMode = ImagePlacementMode.Contain;
    private int _stagedPaddingPercent;
    private BackgroundMode _stagedBackgroundMode = BackgroundMode.Transparent;
    private string _stagedBackgroundColorHex = "#FFFFFF";
    private CancellationTokenSource? _previewDebounceCts;
    private string? _tempPreviewPngPath;

    private string? _snapshotStagedTempIcoPath;
    private NormalizedImageResult? _snapshotStagedProcessResult;
    private Microsoft.UI.Xaml.Media.ImageSource? _snapshotPreviewImageSource;
    private ProcessingStage _snapshotStage;
    private bool _isCustomColorCommitted;
    private string _selectedTemplateId = FolderTemplateCatalog.DefaultTemplateId;
    private int _selectedTemplateVersion = FolderTemplateCatalog.DefaultTemplateVersion;

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
    #endregion

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
        SetupKeyboardAccelerators();
        InitializeColorPalette();
        Closed += (_, _) => Dispose();
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

    private void SetupKeyboardAccelerators()
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }

        // 1. Ctrl+O -> Opens image picker when Quick Panel owns focus
        var ctrlO = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.O,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlO.Invoked += (s, args) =>
        {
            args.Handled = true;
            if (!_isProcessing && _currentStage == ProcessingStage.Idle)
            {
                _ = OpenFilePickerAsync();
            }
        };
        root.KeyboardAccelerators.Add(ctrlO);

        // 2. Ctrl+V -> Invokes Ornare paste only when focus is not inside an editable text control
        var ctrlV = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.V,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlV.Invoked += async (s, args) =>
        {
            if (IsFocusInEditableControl())
            {
                return;
            }

            args.Handled = true;
            if (!_isProcessing && _currentStage == ProcessingStage.Idle)
            {
                await HandlePasteAsync();
            }
        };
        root.KeyboardAccelerators.Add(ctrlV);

        // 3. Ctrl+Z -> Invokes Ornare Undo only when focus is not inside an editable text control
        var ctrlZ = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.Z,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlZ.Invoked += async (s, args) =>
        {
            if (IsFocusInEditableControl())
            {
                return;
            }

            args.Handled = true;
            if (!_isProcessing)
            {
                await HandleUndoAcceleratorAsync();
            }
        };
        root.KeyboardAccelerators.Add(ctrlZ);

        // 4. Ctrl+M -> Opens Manager
        var ctrlM = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.M,
            Modifiers = global::Windows.System.VirtualKeyModifiers.Control
        };
        ctrlM.Invoked += (s, args) =>
        {
            args.Handled = true;
            OnOpenManagerClicked(this, new RoutedEventArgs());
        };
        root.KeyboardAccelerators.Add(ctrlM);

        // 5. Esc -> Closes topmost transient flyout/dialog or cancels a pre-commit editor operation
        var esc = new KeyboardAccelerator
        {
            Key = global::Windows.System.VirtualKey.Escape
        };
        esc.Invoked += (s, args) =>
        {
            args.Handled = true;
            HandleEscapeKey();
        };
        root.KeyboardAccelerators.Add(esc);
    }

    private void OnWindowKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Control) & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        var alt = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Menu) & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

        if (alt)
        {
            switch (e.Key)
            {
                case global::Windows.System.VirtualKey.Number1:
                    e.Handled = true;
                    FolderStyleFlyout?.ShowAt(FolderActionButton);
                    return;
                case global::Windows.System.VirtualKey.Number2:
                    e.Handled = true;
                    IconFlyout?.ShowAt(IconActionButton);
                    return;
                case global::Windows.System.VirtualKey.Number3:
                    e.Handled = true;
                    EmojiFlyout?.ShowAt(EmojiActionButton);
                    return;
                case global::Windows.System.VirtualKey.Number4:
                    e.Handled = true;
                    _ = OpenFilePickerAsync();
                    return;
                case global::Windows.System.VirtualKey.Number5:
                    e.Handled = true;
                    CustomColorFlyout?.ShowAt(ColorActionButton);
                    return;
                case global::Windows.System.VirtualKey.Number6:
                    e.Handled = true;
                    MoreMenuFlyout?.ShowAt(MoreActionButton);
                    return;
            }
        }

        if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            HandleEscapeKey();
            return;
        }

        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            if (_stagingModel.CanApply && !_isProcessing)
            {
                e.Handled = true;
                _ = ApplyStagedPersonalizationAsync();
                return;
            }
        }

        if (e.Key == global::Windows.System.VirtualKey.Space)
        {
            if (DropAreaBorder != null && ReferenceEquals(FocusManager.GetFocusedElement(Content?.XamlRoot), DropAreaBorder))
            {
                e.Handled = true;
                _ = OpenFilePickerAsync();
                return;
            }
        }

        if (ctrl)
        {
            if (e.Key == global::Windows.System.VirtualKey.Z)
            {
                if (!IsFocusInEditableControl())
                {
                    e.Handled = true;
                    _ = HandleUndoAcceleratorAsync();
                    return;
                }
            }
            else if (e.Key == global::Windows.System.VirtualKey.M)
            {
                e.Handled = true;
                OnOpenManagerClicked(this, new RoutedEventArgs());
                return;
            }
            else if (e.Key == global::Windows.System.VirtualKey.V)
            {
                if (!IsFocusInEditableControl())
                {
                    e.Handled = true;
                    _ = HandlePasteAsync();
                    return;
                }
            }
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

    private async Task HandleUndoAcceleratorAsync()
    {
        if (_stagingModel.CanApply)
        {
            _stagingModel.Discard();
            await RefreshLivePreviewAsync();
            ApplyConfirmButton.IsEnabled = false;
            StatusInfoText.Text = _currentBaseline != null
                ? AppResources.GetString("Status_CurrentIcon", _currentLanguage)
                : AppResources.GetString("Status_ReadyToApply", _currentLanguage);
            return;
        }

        if (_currentStage == ProcessingStage.PreviewReady || _currentStage == ProcessingStage.PreparingImage)
        {
            await SetProcessingStageAsync(ProcessingStage.Idle);
            CleanupStagedFiles();
            RestoreFocusAfterTransition(ProcessingStage.Idle);
            return;
        }

        if (_currentStage == ProcessingStage.Done && UndoButton.Visibility == Visibility.Visible)
        {
            OnUndoClicked(this, new RoutedEventArgs());
            return;
        }

        if (_currentStage == ProcessingStage.Idle && _currentBaseline != null && ResetButton.Visibility == Visibility.Visible)
        {
            OnResetClicked(this, new RoutedEventArgs());
        }
    }

    private void HandleEscapeKey()
    {
        if (FolderStyleFlyout != null && FolderStyleFlyout.IsOpen)
        {
            FolderStyleFlyout.Hide();
            FolderActionButton?.Focus(FocusState.Programmatic);
            return;
        }

        if (IconFlyout != null && IconFlyout.IsOpen)
        {
            IconFlyout.Hide();
            IconActionButton?.Focus(FocusState.Programmatic);
            return;
        }

        if (EmojiFlyout != null && EmojiFlyout.IsOpen)
        {
            EmojiFlyout.Hide();
            EmojiActionButton?.Focus(FocusState.Programmatic);
            return;
        }

        if (CustomColorFlyout != null && CustomColorFlyout.IsOpen)
        {
            CustomColorFlyout.Hide();
            ColorActionButton?.Focus(FocusState.Programmatic);
            return;
        }

        if (MoreMenuFlyout != null && MoreMenuFlyout.IsOpen)
        {
            MoreMenuFlyout.Hide();
            MoreActionButton?.Focus(FocusState.Programmatic);
            return;
        }

        if (PlacementOptionsFlyout != null && PlacementOptionsFlyout.IsOpen)
        {
            PlacementOptionsFlyout.Hide();
            PlacementOptionsButton?.Focus(FocusState.Programmatic);
            return;
        }

        if (_stagingModel.CanApply)
        {
            _stagingModel.Discard();
            _ = RefreshLivePreviewAsync();
            ApplyConfirmButton.IsEnabled = false;
            StatusInfoText.Text = _currentBaseline != null
                ? AppResources.GetString("Status_CurrentIcon", _currentLanguage)
                : AppResources.GetString("Status_ReadyToApply", _currentLanguage);
            return;
        }

        if (_currentStage == ProcessingStage.PreviewReady || _currentStage == ProcessingStage.PreparingImage)
        {
            _ = SetProcessingStageAsync(ProcessingStage.Idle);
            CleanupStagedFiles();
            RestoreFocusAfterTransition(ProcessingStage.Idle);
            return;
        }

        if (_currentStage == ProcessingStage.Idle || _currentStage == ProcessingStage.Done)
        {
            Close();
        }
    }

    private void RestoreFocusAfterTransition(ProcessingStage stage)
    {
        try
        {
            if (Content is not FrameworkElement root || root.XamlRoot == null)
            {
                return;
            }

            var currentFocused = FocusManager.GetFocusedElement(root.XamlRoot) as FrameworkElement;
            if (IsElementInteractable(currentFocused))
            {
                return;
            }

            // Fixed fallback order:
            // 1. Operation error/success action if one exists
            if (stage == ProcessingStage.Error && IsElementInteractable(TryAgainButton))
            {
                TryAgainButton.Focus(FocusState.Programmatic);
                return;
            }

            if (stage == ProcessingStage.Done)
            {
                if (IsElementInteractable(UndoButton))
                {
                    UndoButton.Focus(FocusState.Programmatic);
                    return;
                }
                if (IsElementInteractable(CloseSuccessButton))
                {
                    CloseSuccessButton.Focus(FocusState.Programmatic);
                    return;
                }
            }

            if (stage == ProcessingStage.PreviewReady && IsElementInteractable(ApplyConfirmButton))
            {
                ApplyConfirmButton.Focus(FocusState.Programmatic);
                return;
            }

            // 2. Primary input / drop-zone action
            if (stage == ProcessingStage.Idle && IsElementInteractable(DropAreaBorder))
            {
                DropAreaBorder.Focus(FocusState.Programmatic);
                return;
            }

            // 3. First enabled control in logical XAML order
            if (IsElementInteractable(FolderActionButton))
            {
                FolderActionButton.Focus(FocusState.Programmatic);
                return;
            }

            if (IsElementInteractable(ResetButton))
            {
                ResetButton.Focus(FocusState.Programmatic);
                return;
            }

            if (IsElementInteractable(OpenManagerButton))
            {
                OpenManagerButton.Focus(FocusState.Programmatic);
                return;
            }
        }
        catch (Exception ex)
        {
            App.Log($"RestoreFocusAfterTransition failed: {ex}");
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

        string? savedLang = await _storageService.GetSettingAsync("App.Language");
        if (!string.IsNullOrWhiteSpace(savedLang))
        {
            _currentLanguage = AppResources.NormalizeLocale(savedLang);
            AppResources.CurrentLocale = _currentLanguage;
        }

        UpdateLocalizedChrome();

        if (state == FolderCustomizationState.ExternalIconConflict)
        {
            StatusInfoText.Text = AppResources.GetString("ExternalConflictMessage", _currentLanguage);
            ResetButton.Visibility = Visibility.Collapsed;
        }
        else if (_currentBaseline != null)
        {
            ResetButton.Visibility = Visibility.Visible;
            StatusInfoText.Text = AppResources.GetString("Status_CurrentIcon", _currentLanguage);
        }
        else
        {
            ResetButton.Visibility = Visibility.Collapsed;
            StatusInfoText.Text = AppResources.GetString("Status_ReadyToApply", _currentLanguage);
        }

        // Initialize staging model with default Mode B (Classic v2, #0078D4)
        _stagingModel.Initialize(
            PersonalizationMode.FolderTemplate,
            templateState: new FolderTemplateStagingState
            {
                TemplateId = FolderTemplateCatalog.DefaultTemplateId,
                TemplateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
                FolderColorHex = "#0078D4",
                Emblem = FolderEmblemRecipe.None
            });
        _selectedTemplateId = FolderTemplateCatalog.DefaultTemplateId;
        _selectedTemplateVersion = FolderTemplateCatalog.DefaultTemplateVersion;

        // Initialize the 9 template cards
        InitializeTemplateCards();

        // Render initial live preview
        await RefreshLivePreviewAsync();

        await SetProcessingStageAsync(ProcessingStage.Idle);
    }

    private void UpdateLocalizedChrome()
    {
        ToolTipService.SetToolTip(ResetButton, AppResources.GetString("Reset", _currentLanguage));
        AutomationProperties.SetName(ResetButton, AppResources.GetString("Reset", _currentLanguage));
        ToolTipService.SetToolTip(OpenManagerButton, AppResources.GetString("Settings", _currentLanguage));
        AutomationProperties.SetName(OpenManagerButton, AppResources.GetString("Settings", _currentLanguage));

        ToolTipService.SetToolTip(FolderActionButton, AppResources.GetString("Action_Folder", _currentLanguage));
        AutomationProperties.SetName(FolderActionButton, AppResources.GetString("Action_Folder", _currentLanguage));

        ToolTipService.SetToolTip(IconActionButton, AppResources.GetString("Action_Icon", _currentLanguage));
        AutomationProperties.SetName(IconActionButton, AppResources.GetString("Action_Icon", _currentLanguage));

        ToolTipService.SetToolTip(EmojiActionButton, AppResources.GetString("Action_Emoji", _currentLanguage));
        AutomationProperties.SetName(EmojiActionButton, AppResources.GetString("Action_Emoji", _currentLanguage));

        ToolTipService.SetToolTip(ImageActionButton, AppResources.GetString("Action_Image", _currentLanguage));
        AutomationProperties.SetName(ImageActionButton, AppResources.GetString("Action_Image", _currentLanguage));

        ToolTipService.SetToolTip(ColorActionButton, AppResources.GetString("Action_Color", _currentLanguage));
        AutomationProperties.SetName(ColorActionButton, AppResources.GetString("Action_Color", _currentLanguage));

        ToolTipService.SetToolTip(MoreActionButton, AppResources.GetString("Action_More", _currentLanguage));
        AutomationProperties.SetName(MoreActionButton, AppResources.GetString("Action_More", _currentLanguage));

        ApplyConfirmButton.Content = AppResources.GetString("Action_Apply", _currentLanguage);
        ToolTipService.SetToolTip(ApplyConfirmButton, "Apply icon (Enter, Ctrl+Enter)");

        if (FolderFlyoutTitle != null)
        {
            FolderFlyoutTitle.Text = AppResources.GetString("Template_Title", _currentLanguage);
        }

        if (MoreResetMenuItem != null)
        {
            MoreResetMenuItem.Text = $"{AppResources.GetString("Reset", _currentLanguage)} (Ctrl+Z)";
        }
        if (MoreOpenManagerMenuItem != null)
        {
            MoreOpenManagerMenuItem.Text = $"{AppResources.GetString("Settings", _currentLanguage)} (Ctrl+M)";
        }
    }

    private void InitializeTemplateCards()
    {
        FolderTemplateCards.Clear();
        var templates = FolderTemplateCatalog.All;
        string activeTemplateId = _stagingModel.StagedTemplateState.TemplateId;

        foreach (var desc in templates)
        {
            string resKey = desc.Id switch
            {
                "ornare-classic" => "Template_Classic",
                "ornare-soft" => "Template_Soft",
                "ornare-flat" => "Template_Flat",
                "ornare-glass" => "Template_Glass",
                "ornare-aqua" => "Template_Aqua",
                "ornare-grove" => "Template_Grove",
                "ornare-air" => "Template_Air",
                "ornare-flow" => "Template_Flow",
                "ornare-geo" => "Template_Geo",
                _ => "Template_Classic"
            };

            string displayName = AppResources.GetString(resKey, _currentLanguage);
            var card = new FolderTemplateCardViewModel(desc.Id, desc.Version, displayName, desc.SortOrder)
            {
                IsSelected = string.Equals(desc.Id, activeTemplateId, StringComparison.OrdinalIgnoreCase)
            };
            UpdateCardVisualState(card);
            FolderTemplateCards.Add(card);
        }

        _ = RenderTemplateCardThumbnailsAsync();
    }

    private static void UpdateCardVisualState(FolderTemplateCardViewModel card)
    {
        if (card.IsSelected)
        {
            card.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            card.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
        else
        {
            card.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            card.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private async Task RenderTemplateCardThumbnailsAsync()
    {
        string color = _stagingModel.StagedTemplateState.FolderColorHex;
        foreach (var card in FolderTemplateCards)
        {
            try
            {
                var result = await Task.Run(() => FolderTemplateRenderer.RenderPreviewPng(color, card.Id, card.Version));
                if (result.IsSuccess && result.Value != null)
                {
                    var bmp = LoadBitmapFromBytes(result.Value);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        card.Thumbnail = bmp;
                    });
                }
            }
            catch (Exception ex)
            {
                App.Log($"Thumbnail render failed for {card.Id}: {ex.Message}");
            }
        }
    }

    private async Task RefreshLivePreviewAsync(string? overrideTemplateId = null, int? overrideTemplateVersion = null, string? overrideColorHex = null)
    {
        try
        {
            if (_stagingModel.ActiveMode == PersonalizationMode.FolderTemplate)
            {
                string templateId = overrideTemplateId ?? _stagingModel.StagedTemplateState.TemplateId;
                int templateVersion = overrideTemplateVersion ?? _stagingModel.StagedTemplateState.TemplateVersion;
                string colorHex = overrideColorHex ?? _stagingModel.StagedTemplateState.FolderColorHex;

                var result = await Task.Run(() => FolderTemplateRenderer.RenderPreviewPng(colorHex, templateId, templateVersion));
                if (result.IsSuccess && result.Value != null)
                {
                    PreviewIconImage.Source = LoadPreviewBitmapFromBytes(result.Value);
                }

                if (PlacementOptionsButton != null) PlacementOptionsButton.Visibility = Visibility.Collapsed;
                if (CancelPreviewButton != null) CancelPreviewButton.Visibility = Visibility.Collapsed;
            }
            else if (_stagingModel.ActiveMode == PersonalizationMode.CustomImage && _stagingModel.StagedImageState.ImageBytes != null)
            {
                var imgState = _stagingModel.StagedImageState;
                var result = await Task.Run(() => ImagePipeline.RenderPlacementPreviewPng(
                    imgState.ImageBytes, 256, imgState.PlacementMode, imgState.PaddingPercent, imgState.BackgroundMode, imgState.BackgroundColorHex));
                if (result.IsSuccess && result.Value != null)
                {
                    PreviewIconImage.Source = LoadPreviewBitmapFromBytes(result.Value);
                }

                if (PlacementOptionsButton != null) PlacementOptionsButton.Visibility = Visibility.Visible;
                if (CancelPreviewButton != null) CancelPreviewButton.Visibility = Visibility.Visible;
            }

            // Update Apply button and Status text
            ApplyConfirmButton.IsEnabled = _stagingModel.CanApply;
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
        catch (Exception ex)
        {
            App.Log($"RefreshLivePreviewAsync error: {ex.Message}");
        }
    }

    private void OnFolderTemplateCardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FolderTemplateCardViewModel card)
        {
            _ = RefreshLivePreviewAsync(overrideTemplateId: card.Id, overrideTemplateVersion: card.Version);
        }
    }

    private void OnFolderTemplateCardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _ = RefreshLivePreviewAsync();
    }

    private async void OnFolderTemplateCardClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FolderTemplateCardViewModel card)
        {
            _stagingModel.StageFolderTemplate(card.Id, card.Version);
            _selectedTemplateId = card.Id;
            _selectedTemplateVersion = card.Version;

            foreach (var item in FolderTemplateCards)
            {
                item.IsSelected = string.Equals(item.Id, card.Id, StringComparison.OrdinalIgnoreCase);
                UpdateCardVisualState(item);
            }

            await RefreshLivePreviewAsync();
        }
    }

    private void OnImageActionButtonClicked(object sender, RoutedEventArgs e)
    {
        _ = OpenFilePickerAsync();
    }

    private async void OnRemoveEmblemClicked(object sender, RoutedEventArgs e)
    {
        _stagingModel.RemoveEmblem();
        await RefreshLivePreviewAsync();
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

    private void OnDropAreaTapped(object sender, TappedRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            DependencyObject? current = dep;
            while (current != null && current != DropAreaBorder)
            {
                if (current == ColorPaletteContainer || current == CustomColorButton || current is Button)
                {
                    e.Handled = true;
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        _ = OpenFilePickerAsync();
    }

    private void OnDropAreaKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter || e.Key == global::Windows.System.VirtualKey.Space)
        {
            e.Handled = true;
            if (!_isProcessing && _currentStage == ProcessingStage.Idle)
            {
                _ = OpenFilePickerAsync();
            }
        }
    }

    private async Task OpenFilePickerAsync()
    {
        if (_isProcessing)
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
        picker.FileTypeFilter.Add(".svg");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await StageImageAsync(file.Path);
        }
    }

    private async Task HandlePasteAsync()
    {
        if (_isProcessing)
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

        await StageImageBytesAsync(bytes, imagePath);
    }

    private async Task StageImageBytesAsync(byte[] imageBytes, string? imagePath = null)
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

            await SetProcessingStageAsync(ProcessingStage.PreparingImage);
            await Task.Delay(40);

            _stagedOperationId = Guid.NewGuid();
            _stagedTempIcoPath = Path.Combine(Path.GetTempPath(), $"ornare_staged_{_stagedOperationId:N}.ico");

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

            _stagedImageBytes = imageBytes;
            _stagedPlacementMode = ImagePlacementMode.Contain;
            _stagedPaddingPercent = 0;
            _stagedBackgroundMode = BackgroundMode.Transparent;
            _stagedBackgroundColorHex = "#FFFFFF";
            if (PlacementOptionsButton != null) PlacementOptionsButton.Visibility = Visibility.Visible;
            if (CancelPreviewButton != null) CancelPreviewButton.Visibility = Visibility.Visible;
            if (PlacementModeRadioButtons != null) PlacementModeRadioButtons.SelectedIndex = 0;
            if (PlacementFillWarningText != null) PlacementFillWarningText.Visibility = Visibility.Collapsed;
            if (PaddingToggleSwitch != null) PaddingToggleSwitch.IsOn = false;
            if (PaddingSlider != null) PaddingSlider.Value = 8;
            if (PaddingSliderContainer != null) PaddingSliderContainer.Visibility = Visibility.Collapsed;
            if (BackgroundModeRadioButtons != null) BackgroundModeRadioButtons.SelectedIndex = 0;
            if (BackgroundColorContainer != null) BackgroundColorContainer.Visibility = Visibility.Collapsed;
            if (BackgroundColorSwatch != null) BackgroundColorSwatch.Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 255, 255, 255));
            if (BackgroundColorPicker != null) BackgroundColorPicker.Color = global::Windows.UI.Color.FromArgb(255, 255, 255, 255);

            _stagingModel.StageCustomImage(imageBytes, imagePath, processResult.Value.ContentHash);
            ApplyConfirmButton.IsEnabled = _stagingModel.CanApply;
            StatusInfoText.Text = AppResources.GetString("Status_UnappliedChanges", _currentLanguage);

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
        if (ColorPaletteContainer != null)
        {
            ColorPaletteContainer.Children.Clear();
        }

        if (FlyoutColorPaletteContainer != null)
        {
            FlyoutColorPaletteContainer.Children.Clear();
        }

        if (CustomColorPicker != null)
        {
            CustomColorPicker.Color = global::Windows.UI.Color.FromArgb(255, 37, 99, 235);
        }

        if (CustomColorButton != null)
        {
            string customColorText = AppResources.GetString("ColorCustom", _currentLanguage);
            ToolTipService.SetToolTip(CustomColorButton, customColorText);
            AutomationProperties.SetName(CustomColorButton, customColorText);
        }

        foreach (var entry in BuiltInColors.Entries)
        {
            if (!ColorConversion.TryParseHex(entry.HexValue, out byte r, out byte g, out byte b))
            {
                continue;
            }

            string displayName = GetColorDisplayName(entry.Id, _currentLanguage);

            if (ColorPaletteContainer != null)
            {
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
                ToolTipService.SetToolTip(button, displayName);
                AutomationProperties.SetName(button, displayName);
                button.Click += async (s, args) =>
                {
                    if (s is Button btn && btn.Tag is string hex)
                    {
                        _stagingModel.StageFolderColor(hex);
                        await RefreshLivePreviewAsync();
                        _ = RenderTemplateCardThumbnailsAsync();
                    }
                };
                ColorPaletteContainer.Children.Add(button);
            }

            if (FlyoutColorPaletteContainer != null)
            {
                var flyoutBtn = new Button
                {
                    Width = 18,
                    Height = 18,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(9),
                    Margin = new Thickness(0),
                    Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, r, g, b)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(40, 255, 255, 255)),
                    Tag = entry.HexValue
                };
                ToolTipService.SetToolTip(flyoutBtn, displayName);
                AutomationProperties.SetName(flyoutBtn, displayName);
                flyoutBtn.Click += async (s, args) =>
                {
                    if (s is Button btn && btn.Tag is string hex)
                    {
                        _stagingModel.StageFolderColor(hex);
                        await RefreshLivePreviewAsync();
                        _ = RenderTemplateCardThumbnailsAsync();
                        CustomColorFlyout?.Hide();
                    }
                };
                FlyoutColorPaletteContainer.Children.Add(flyoutBtn);
            }
        }
    }

    private static string GetColorDisplayName(string id, string lang) => id switch
    {
        "color.amber" => AppResources.GetString("Color_Amber", lang),
        "color.orange" => AppResources.GetString("Color_Orange", lang),
        "color.red" => AppResources.GetString("Color_Red", lang),
        "color.rose" => AppResources.GetString("Color_Rose", lang),
        "color.magenta" => AppResources.GetString("Color_Magenta", lang),
        "color.purple" => AppResources.GetString("Color_Purple", lang),
        "color.indigo" => AppResources.GetString("Color_Indigo", lang),
        "color.blue" => AppResources.GetString("Color_Blue", lang),
        "color.cyan" => AppResources.GetString("Color_Cyan", lang),
        "color.teal" => AppResources.GetString("Color_Teal", lang),
        "color.green" => AppResources.GetString("Color_Green", lang),
        "color.slate" => AppResources.GetString("Color_Slate", lang),
        _ => AppResources.GetString("Colors", lang)
    };

    private void OnCustomColorFlyoutOpened(object sender, object e)
    {
        _isCustomColorCommitted = false;
        _snapshotStagedTempIcoPath = _stagedTempIcoPath;
        _snapshotStagedProcessResult = _stagedProcessResult;
        _snapshotPreviewImageSource = PreviewIconImage.Source;
        _snapshotStage = _currentStage;

        if (CustomColorTitleText != null) CustomColorTitleText.Text = AppResources.GetString("ColorPicker_CustomColor", _currentLanguage);
        if (CustomColorSubtitleText != null) CustomColorSubtitleText.Text = AppResources.GetString("PreviewSubtitle", _currentLanguage);
        if (RecentColorsTitle != null) RecentColorsTitle.Text = AppResources.GetString("ColorPicker_RecentColors", _currentLanguage);
        if (ApplyCustomColorButton != null) ApplyCustomColorButton.Content = AppResources.GetString("ColorPicker_Apply", _currentLanguage);
        if (CancelCustomColorButton != null) CancelCustomColorButton.Content = AppResources.GetString("ColorPicker_Cancel", _currentLanguage);

        if (CustomColorPicker != null)
        {
            if (CustomColorPicker.Color.A == 0 || (CustomColorPicker.Color.R == 0 && CustomColorPicker.Color.G == 0 && CustomColorPicker.Color.B == 0))
            {
                if (ColorConversion.TryParseHex(_stagingModel.StagedTemplateState.FolderColorHex, out byte r, out byte g, out byte b))
                {
                    CustomColorPicker.Color = global::Windows.UI.Color.FromArgb(255, r, g, b);
                }
                else
                {
                    CustomColorPicker.Color = global::Windows.UI.Color.FromArgb(255, 37, 99, 235);
                }
            }

            var currentColor = CustomColorPicker.Color;
            string currentHex = ColorConversion.ToHex(currentColor.R, currentColor.G, currentColor.B);
            var initialPreview = FolderTemplateRenderer.RenderPreviewPng(currentHex, _stagingModel.StagedTemplateState.TemplateId, _stagingModel.StagedTemplateState.TemplateVersion);
            if (initialPreview.IsSuccess && initialPreview.Value != null && FlyoutPreviewImage != null)
            {
                FlyoutPreviewImage.Source = LoadPreviewBitmapFromBytes(initialPreview.Value);
            }
        }

        _ = LoadRecentCustomColorsToFlyoutAsync();
    }

    private async void OnCustomColorFlyoutClosed(object sender, object e)
    {
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        _previewDebounceCts = null;

        if (!_isCustomColorCommitted)
        {
            _stagedTempIcoPath = _snapshotStagedTempIcoPath;
            _stagedProcessResult = _snapshotStagedProcessResult;
            PreviewIconImage.Source = _snapshotPreviewImageSource;
            if (FlyoutPreviewImage != null)
            {
                FlyoutPreviewImage.Source = null;
            }
            await SetProcessingStageAsync(_snapshotStage);
        }

        ColorActionButton?.Focus(FocusState.Programmatic);
    }

    private void OnCustomColorPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        TriggerCustomColorPreview();
    }

    private void TriggerCustomColorPreview()
    {
        var newColor = CustomColorPicker.Color;
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        _previewDebounceCts = new CancellationTokenSource();
        var token = _previewDebounceCts.Token;

        string templateId = _stagingModel.StagedTemplateState.TemplateId;
        int templateVersion = _stagingModel.StagedTemplateState.TemplateVersion;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(80, token);
                if (token.IsCancellationRequested) return;

                string hex = ColorConversion.ToHex(newColor.R, newColor.G, newColor.B);
                var previewResult = FolderTemplateRenderer.RenderPreviewPng(hex, templateId, templateVersion);
                if (!previewResult.IsSuccess || previewResult.Value == null || token.IsCancellationRequested) return;

                byte[] pngBytes = previewResult.Value;

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;
                    var bitmap = LoadPreviewBitmapFromBytes(pngBytes);
                    if (token.IsCancellationRequested || bitmap == null) return;

                    if (FlyoutPreviewImage != null)
                    {
                        FlyoutPreviewImage.Source = bitmap;
                    }
                    if (PreviewIconImage != null)
                    {
                        PreviewIconImage.Source = bitmap;
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Log($"Debounced color preview error: {ex.Message}");
            }
        }, token);
    }

    private async void OnApplyCustomColorClicked(object sender, RoutedEventArgs e)
    {
        var color = CustomColorPicker.Color;
        string hex = ColorConversion.ToHex(color.R, color.G, color.B);
        _isCustomColorCommitted = true;
        CustomColorFlyout.Hide();

        _stagingModel.StageFolderColor(hex);
        await RefreshLivePreviewAsync();
        _ = RenderTemplateCardThumbnailsAsync();
    }

    private void OnCancelCustomColorClicked(object sender, RoutedEventArgs e)
    {
        _isCustomColorCommitted = false;
        CustomColorFlyout.Hide();
    }

    private async Task LoadRecentCustomColorsToFlyoutAsync()
    {
        if (RecentColorsContainer == null || RecentColorsSection == null) return;

        RecentColorsContainer.Children.Clear();

        try
        {
            var settings = await _storageService.GetUserSettingsAsync();
            var recents = settings.RecentCustomColors;

            if (recents == null || recents.Count == 0)
            {
                RecentColorsSection.Visibility = Visibility.Collapsed;
                return;
            }

            RecentColorsSection.Visibility = Visibility.Visible;
            foreach (var hex in recents)
            {
                if (!ColorConversion.TryParseHex(hex, out byte r, out byte g, out byte b))
                {
                    continue;
                }

                var chip = new Button
                {
                    Width = 22,
                    Height = 22,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, r, g, b)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(50, 255, 255, 255)),
                    Tag = hex
                };

                ToolTipService.SetToolTip(chip, hex);
                AutomationProperties.SetName(chip, hex);

                chip.Click += (s, _) =>
                {
                    if (s is Button b && b.Tag is string chipHex && ColorConversion.TryParseHex(chipHex, out byte cr, out byte cg, out byte cb))
                    {
                        CustomColorPicker.Color = global::Windows.UI.Color.FromArgb(255, cr, cg, cb);
                    }
                };

                RecentColorsContainer.Children.Add(chip);
            }
        }
        catch (Exception ex)
        {
            App.Log($"Error loading recent custom colors: {ex.Message}");
            RecentColorsSection.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnPlacementSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isProcessing || _stagedImageBytes == null || PlacementModeRadioButtons == null)
        {
            return;
        }

        int index = PlacementModeRadioButtons.SelectedIndex;
        var newMode = index switch
        {
            1 => ImagePlacementMode.Cover,
            2 => ImagePlacementMode.Fill,
            _ => ImagePlacementMode.Contain
        };

        if (PlacementFillWarningText != null)
        {
            PlacementFillWarningText.Visibility = (newMode == ImagePlacementMode.Fill)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (_stagedPlacementMode == newMode)
        {
            return;
        }

        _stagedPlacementMode = newMode;
        _stagingModel.StagePlacement(_stagedPlacementMode, _stagedPaddingPercent, _stagedBackgroundMode, _stagedBackgroundColorHex);
        await RefreshLivePreviewAsync();
    }

    private void OnPlacementOptionsFlyoutClosed(object sender, object e)
    {
        PlacementOptionsButton?.Focus(FocusState.Programmatic);
    }

    private void OnPaddingToggled(object sender, RoutedEventArgs e)
    {
        if (PaddingToggleSwitch == null || PaddingSliderContainer == null) return;
        bool isOn = PaddingToggleSwitch.IsOn;
        PaddingSliderContainer.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        int newPercent = isOn ? (int)Math.Round(PaddingSlider?.Value ?? 8) : 0;
        TriggerDebouncedPreview(newPercent);
    }

    private void OnPaddingSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        int newPercent = (int)Math.Round(e.NewValue);
        UpdatePaddingLabel(newPercent);
        if (PaddingToggleSwitch != null && PaddingToggleSwitch.IsOn)
        {
            TriggerDebouncedPreview(newPercent);
        }
    }

    private void UpdatePaddingLabel(int percent)
    {
        if (PaddingLabelText == null) return;
        PaddingLabelText.Text = AppResources.Format("Padding_Label", _currentLanguage, percent);
    }

    private void OnBackgroundSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isProcessing || _stagedImageBytes == null || BackgroundModeRadioButtons == null)
        {
            return;
        }

        int index = BackgroundModeRadioButtons.SelectedIndex;
        var newMode = index == 1 ? BackgroundMode.SolidColor : BackgroundMode.Transparent;

        if (BackgroundColorContainer != null)
        {
            BackgroundColorContainer.Visibility = (newMode == BackgroundMode.SolidColor)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (_stagedBackgroundMode == newMode)
        {
            return;
        }

        _stagedBackgroundMode = newMode;
        TriggerDebouncedPreview();
    }

    private void OnBackgroundColorPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        var color = args.NewColor;
        string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        _stagedBackgroundColorHex = hex;

        if (BackgroundColorSwatch != null)
        {
            BackgroundColorSwatch.Background = new SolidColorBrush(color);
        }

        if (_stagedBackgroundMode == BackgroundMode.SolidColor)
        {
            TriggerDebouncedPreview();
        }
    }

    private void TriggerDebouncedPreview(int? paddingPercent = null)
    {
        if (_isProcessing || _stagedImageBytes == null) return;
        if (paddingPercent.HasValue)
        {
            _stagedPaddingPercent = paddingPercent.Value;
        }

        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        _previewDebounceCts = new CancellationTokenSource();
        var token = _previewDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(80, token);
                if (token.IsCancellationRequested) return;

                var imageBytes = _stagedImageBytes;
                if (imageBytes == null) return;
                var mode = _stagedPlacementMode;
                var pad = _stagedPaddingPercent;
                var bgMode = _stagedBackgroundMode;
                var bgHex = _stagedBackgroundColorHex;

                var previewResult = ImagePipeline.RenderPlacementPreviewPng(imageBytes, 256, mode, pad, bgMode, bgHex);
                if (token.IsCancellationRequested || !previewResult.IsSuccess || previewResult.Value == null) return;

                byte[] previewBytes = previewResult.Value;

                var tempPath = _stagedTempIcoPath;
                NormalizedImageResult? icoResult = null;
                if (!string.IsNullOrWhiteSpace(tempPath))
                {
                    var res = _imagePipeline.ProcessImageBytes(imageBytes, tempPath, mode, pad, bgMode, bgHex);
                    if (res.IsSuccess)
                    {
                        icoResult = res.Value;
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;
                    if (icoResult != null)
                    {
                        _stagedProcessResult = icoResult;
                    }
                    PreviewIconImage.Source = LoadPreviewBitmapFromBytes(previewBytes);
                    _stagingModel.StagePlacement(mode, pad, bgMode, bgHex);
                    ApplyConfirmButton.IsEnabled = _stagingModel.CanApply;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                App.Log($"Debounced preview failed: {ex.Message}");
            }
        }, token);
    }

    private void UpdatePlacementStrings()
    {
        if (PlacementOptionsButton != null)
        {
            PlacementOptionsButton.Content = AppResources.GetString("Placement_Options", _currentLanguage);
            ToolTipService.SetToolTip(PlacementOptionsButton, AppResources.GetString("Placement_Title", _currentLanguage));
            AutomationProperties.SetName(PlacementOptionsButton, AppResources.GetString("Placement_Title", _currentLanguage));
        }
        if (PlacementTitleText != null)
        {
            PlacementTitleText.Text = AppResources.GetString("Placement_Title", _currentLanguage);
        }
        if (RadioPlacementContain != null)
        {
            RadioPlacementContain.Content = AppResources.GetString("Placement_Contain", _currentLanguage);
            ToolTipService.SetToolTip(RadioPlacementContain, AppResources.GetString("Placement_Contain_ToolTip", _currentLanguage));
        }
        if (RadioPlacementCover != null)
        {
            RadioPlacementCover.Content = AppResources.GetString("Placement_Cover", _currentLanguage);
            ToolTipService.SetToolTip(RadioPlacementCover, AppResources.GetString("Placement_Cover_ToolTip", _currentLanguage));
        }
        if (RadioPlacementFill != null)
        {
            RadioPlacementFill.Content = AppResources.GetString("Placement_Fill", _currentLanguage);
            ToolTipService.SetToolTip(RadioPlacementFill, AppResources.GetString("Placement_Fill_ToolTip", _currentLanguage));
        }
        if (PlacementFillWarningText != null)
        {
            PlacementFillWarningText.Text = AppResources.GetString("Placement_Fill_Warning", _currentLanguage);
        }
        if (PaddingToggleSwitch != null)
        {
            PaddingToggleSwitch.Header = AppResources.GetString("Padding_Title", _currentLanguage);
            ToolTipService.SetToolTip(PaddingToggleSwitch, AppResources.GetString("Padding_ToolTip", _currentLanguage));
            AutomationProperties.SetName(PaddingToggleSwitch, AppResources.GetString("Padding_Title", _currentLanguage));
        }
        UpdatePaddingLabel(_stagedPaddingPercent > 0 ? _stagedPaddingPercent : (int)(PaddingSlider?.Value ?? 8));
        if (BackgroundTitleText != null)
        {
            BackgroundTitleText.Text = AppResources.GetString("Background_Title", _currentLanguage);
        }
        if (RadioBackgroundTransparent != null)
        {
            RadioBackgroundTransparent.Content = AppResources.GetString("Background_Transparent", _currentLanguage);
            ToolTipService.SetToolTip(RadioBackgroundTransparent, AppResources.GetString("Background_Transparent_ToolTip", _currentLanguage));
        }
        if (RadioBackgroundSolid != null)
        {
            RadioBackgroundSolid.Content = AppResources.GetString("Background_Solid", _currentLanguage);
            ToolTipService.SetToolTip(RadioBackgroundSolid, AppResources.GetString("Background_Solid_ToolTip", _currentLanguage));
        }
        if (PickBackgroundColorButton != null)
        {
            PickBackgroundColorButton.Content = AppResources.GetString("Background_ChooseColor", _currentLanguage);
            ToolTipService.SetToolTip(PickBackgroundColorButton, AppResources.GetString("Background_ChooseColor_ToolTip", _currentLanguage));
            AutomationProperties.SetName(PickBackgroundColorButton, AppResources.GetString("Background_ChooseColor", _currentLanguage));
        }
    }

    private async void OnApplyConfirmClicked(object sender, RoutedEventArgs e)
    {
        if (_isProcessing || !_stagingModel.CanApply)
        {
            return;
        }

        await ApplyStagedPersonalizationAsync();
    }

    private async Task ApplyStagedPersonalizationAsync()
    {
        if (_isProcessing || !_stagingModel.CanApply)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, "No folder selected.");
            return;
        }

        try
        {
            if (_stagingModel.ActiveMode == PersonalizationMode.FolderTemplate)
            {
                _stagedOperationId = Guid.NewGuid();
                _stagedTempIcoPath = Path.Combine(Path.GetTempPath(), $"ornare_staged_{_stagedOperationId:N}.ico");
                var templateState = _stagingModel.StagedTemplateState;

                await SetProcessingStageAsync(ProcessingStage.PreparingImage);
                await Task.Delay(20);

                await SetProcessingStageAsync(ProcessingStage.GeneratingIcon);
                await Task.Delay(20);

                var tempPath = _stagedTempIcoPath;
                var pipeline = _imagePipeline as ImagePipeline ?? new ImagePipeline();

                var renderResult = await Task.Run(() =>
                {
                    return FolderTemplateRenderer.RenderColoredTemplateIco(
                        pipeline,
                        templateState.FolderColorHex,
                        tempPath,
                        templateState.TemplateId,
                        templateState.TemplateVersion);
                });

                if (!renderResult.IsSuccess || renderResult.Value == null)
                {
                    CleanupStagedFiles();
                    ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, renderResult.ErrorMessage);
                    return;
                }

                _stagedProcessResult = renderResult.Value;
                _stagedCustomColorHex = templateState.FolderColorHex;
                await CommitStagedIconAsync();
                _stagingModel.Commit();
                ApplyConfirmButton.IsEnabled = false;
                StatusInfoText.Text = AppResources.GetString("Status_Applied", _currentLanguage);
            }
            else if (_stagingModel.ActiveMode == PersonalizationMode.CustomImage)
            {
                if (string.IsNullOrWhiteSpace(_stagedTempIcoPath) || _stagedProcessResult == null)
                {
                    return;
                }

                await CommitStagedIconAsync();
                _stagingModel.Commit();
                ApplyConfirmButton.IsEnabled = false;
                StatusInfoText.Text = AppResources.GetString("Status_Applied", _currentLanguage);
            }
        }
        catch (Exception ex)
        {
            App.Log($"ApplyStagedPersonalizationAsync exception: {ex}");
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, ex.Message);
        }
    }

    private async void OnCancelPreviewClicked(object sender, RoutedEventArgs e)
    {
        _stagingModel.Discard();
        CleanupStagedFiles();
        _stagedCustomColorHex = null;
        await RefreshLivePreviewAsync();
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

                // Ensure render_cache entry exists for deduplication and Apply Last (F18/F21)
                if (!string.IsNullOrWhiteSpace(stagedResult.RenderKey))
                {
                    var existingCache = await _storageService.GetRenderCacheEntryAsync(stagedResult.RenderKey);
                    if (existingCache == null)
                    {
                        var fi = new FileInfo(canonicalIcoPath);
                        await _storageService.SaveRenderCacheEntryAsync(new RenderCacheEntry
                        {
                            RenderKey = stagedResult.RenderKey,
                            RecipeVersion = 1,
                            RecipeJson = string.Empty,
                            SourceAssetHash = stagedResult.ContentHash,
                            CanonicalIcoPath = canonicalIcoPath,
                            FrameSizes = string.Join(",", stagedResult.FrameSizes),
                            FileSizeBytes = fi.Length,
                            CreatedAt = DateTimeOffset.UtcNow,
                            LastUsedAt = DateTimeOffset.UtcNow
                        });
                    }
                }

                await _storageService.RecordHistoryAsync(
                    _currentFolderPath,
                    operationId,
                    null,
                    stagedResult.ContentHash,
                    "apply");

                var ntfsId = FolderIdentityService.GetNtfsIdentity(_currentFolderPath);
                if (ntfsId.HasValue)
                {
                    await _storageService.UpdateAssignmentIdentityAsync(_currentFolderPath, ntfsId.Value.VolumeSerialNumber, ntfsId.Value.FileId64);
                }
                else
                {
                    var (vol, fid) = FolderIdentityService.GetFolderIdentity(_currentFolderPath);
                    await _storageService.UpdateAssignmentIdentityAsync(_currentFolderPath, vol, fid);
                }
            }
            catch (Exception ex)
            {
                App.Log($"Non-critical library tracking failure: {ex.Message}");
            }

            await _storageService.UpdateJournalStateAsync(operationId, JournalState.Completed);

            // F21: Update LastAppliedRenderKey pointer only after successful durable apply commit
            if (!string.IsNullOrWhiteSpace(stagedResult.RenderKey))
            {
                try
                {
                    await _storageService.SetLastAppliedRenderKeyAsync(stagedResult.RenderKey);
                }
                catch (Exception ex)
                {
                    App.Log($"Non-critical LastAppliedRenderKey save failure: {ex.Message}");
                }
            }

            // Set final success icon preview from canonical path or staged path
            SuccessIconImage.Source = !string.IsNullOrEmpty(canonicalIcoPath) && File.Exists(canonicalIcoPath)
                ? LoadBitmapSafe(canonicalIcoPath)
                : LoadBitmapSafe(stagedIcoPath);

            // Clean up staged temp ICO
            CleanupStagedFiles();

            // Fire final targeted shell refresh now that all staged files are closed and committed
            ShellRefreshService.NotifyFolderChanged(_currentFolderPath);

            App.Log($"CommitStagedIconAsync completed successfully for '{_currentFolderPath}'");

            // Persist recent custom color on successful folder apply (F02)
            if (!string.IsNullOrWhiteSpace(_stagedCustomColorHex))
            {
                try
                {
                    var userSettings = await _storageService.GetUserSettingsAsync();
                    var updatedSettings = userSettings.WithRecentCustomColor(_stagedCustomColorHex);
                    await _storageService.SaveUserSettingsAsync(updatedSettings);
                }
                catch (Exception ex)
                {
                    App.Log($"Failed to record recent custom color: {ex.Message}");
                }
                finally
                {
                    _stagedCustomColorHex = null;
                }
            }

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
                _stagingModel.Initialize(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
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
                _stagingModel.Initialize(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
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
        _stagedImageBytes = null;
        _stagedPlacementMode = ImagePlacementMode.Contain;
        if (PlacementModeRadioButtons != null)
        {
            PlacementModeRadioButtons.SelectedIndex = 0;
        }
        if (PlacementFillWarningText != null)
        {
            PlacementFillWarningText.Visibility = Visibility.Collapsed;
        }
        _stagedPaddingPercent = 0;
        if (PaddingToggleSwitch != null)
        {
            PaddingToggleSwitch.IsOn = false;
        }
        if (PaddingSlider != null)
        {
            PaddingSlider.Value = 8;
        }
        if (PaddingSliderContainer != null)
        {
            PaddingSliderContainer.Visibility = Visibility.Collapsed;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_tempPreviewPngPath) && File.Exists(_tempPreviewPngPath))
            {
                File.Delete(_tempPreviewPngPath);
            }
        }
        catch { }
        _tempPreviewPngPath = null;
    }

    private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? LoadPreviewBitmapFromBytes(byte[] pngBytes)
    {
        try
        {
            string newTemp = Path.Combine(Path.GetTempPath(), $"ornare_prev_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(newTemp, pngBytes);
            string? oldTemp = _tempPreviewPngPath;
            _tempPreviewPngPath = newTemp;
            if (oldTemp != null && File.Exists(oldTemp))
            {
                try { File.Delete(oldTemp); } catch { }
            }
            return LoadBitmapSafe(newTemp);
        }
        catch
        {
            return null;
        }
    }

    private static Microsoft.UI.Xaml.Media.Imaging.BitmapImage? LoadBitmapFromBytes(byte[] pngBytes)
    {
        try
        {
            string temp = Path.Combine(Path.GetTempPath(), $"ornare_thumb_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(temp, pngBytes);
            return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(temp));
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        _previewDebounceCts = null;
        CleanupStagedFiles();
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

        switch (stage)
        {
            case ProcessingStage.Idle:
                _isProcessing = false;
                PreviewStatePanel.Visibility = Visibility.Visible;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                DropAreaBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                DropAreaBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                break;

            case ProcessingStage.PreparingImage:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = AppResources.GetString("PreparingImage", _currentLanguage);
                break;

            case ProcessingStage.GeneratingIcon:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = AppResources.GetString("GeneratingIcon", _currentLanguage);
                break;

            case ProcessingStage.PreviewReady:
                _isProcessing = false;
                _autoCloseTimer?.Stop();
                PreviewStatePanel.Visibility = Visibility.Visible;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                PreviewTitleText.Text = AppResources.GetString("PreviewTitle", _currentLanguage);
                PreviewSubtitleText.Text = AppResources.GetString("PreviewSubtitle", _currentLanguage);
                UpdatePlacementStrings();
                break;

            case ProcessingStage.ApplyingIcon:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = AppResources.GetString("ApplyingIcon", _currentLanguage);
                break;

            case ProcessingStage.RefreshingExplorer:
                _isProcessing = true;
                _autoCloseTimer?.Stop();
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Visible;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                ProcessingText.Text = AppResources.GetString("RefreshingExplorer", _currentLanguage);
                break;

            case ProcessingStage.Done:
                _isProcessing = false;
                _autoCloseTimer?.Stop();
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Visible;
                ErrorStatePanel.Visibility = Visibility.Collapsed;
                SuccessTitleText.Text = AppResources.GetString("FolderIconUpdated", _currentLanguage);
                SuccessSubtitleText.Text = AppResources.GetString("SuccessSubtitle", _currentLanguage);
                CloseSuccessButton.Content = AppResources.GetString("Close", _currentLanguage);
                UndoButton.Content = AppResources.GetString("Undo", _currentLanguage);
                break;

            case ProcessingStage.Error:
                _isProcessing = false;
                _autoCloseTimer?.Stop();
                PreviewStatePanel.Visibility = Visibility.Collapsed;
                ProcessingStatePanel.Visibility = Visibility.Collapsed;
                SuccessStatePanel.Visibility = Visibility.Collapsed;
                ErrorStatePanel.Visibility = Visibility.Visible;
                TryAgainButton.Content = AppResources.GetString("TryAgain", _currentLanguage);
                break;
        }

        await Task.Delay(30);
        RestoreFocusAfterTransition(stage);
    }

    private void ShowCategorizedError(QuickPanelErrorType errorType, string? detail = null)
    {
        _isProcessing = false;
        _autoCloseTimer?.Stop();

        string title;
        string message;

        switch (errorType)
        {
            case QuickPanelErrorType.UnsupportedImage:
                title = AppResources.GetString("ErrorUnsupportedImageTitle", _currentLanguage);
                message = AppResources.GetString("ErrorUnsupportedImageMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.ImageReadFailed:
                title = AppResources.GetString("ErrorImageReadTitle", _currentLanguage);
                message = !string.IsNullOrWhiteSpace(detail) ? detail : AppResources.GetString("ErrorImageReadMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.IconGenerationFailed:
                title = AppResources.GetString("ErrorIconGenerationTitle", _currentLanguage);
                message = !string.IsNullOrWhiteSpace(detail) ? detail : AppResources.GetString("ErrorIconGenerationMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.FolderApplyFailed:
                title = AppResources.GetString("ErrorFolderApplyTitle", _currentLanguage);
                message = !string.IsNullOrWhiteSpace(detail) ? detail : AppResources.GetString("ErrorFolderApplyMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.ExplorerRefreshFailed:
                title = AppResources.GetString("ErrorExplorerRefreshTitle", _currentLanguage);
                message = !string.IsNullOrWhiteSpace(detail) ? detail : AppResources.GetString("ErrorExplorerRefreshMessage", _currentLanguage);
                break;

            default:
                title = AppResources.GetString("ApplyFailed", _currentLanguage);
                message = !string.IsNullOrWhiteSpace(detail) ? detail : string.Empty;
                break;
        }

        PreviewStatePanel.Visibility = Visibility.Collapsed;
        ProcessingStatePanel.Visibility = Visibility.Collapsed;
        SuccessStatePanel.Visibility = Visibility.Collapsed;
        ErrorStatePanel.Visibility = Visibility.Visible;

        ErrorTitleText.Text = title;
        ErrorMessageText.Text = message;
        TryAgainButton.Content = AppResources.GetString("TryAgain", _currentLanguage);
        RestoreFocusAfterTransition(ProcessingStage.Error);
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
            _currentLanguage = AppResources.NormalizeLocale(lang);
        }

        UpdateLocalizedChrome();

        PreviewTitleText.Text = AppResources.GetString("PreviewTitle", _currentLanguage);
        PreviewSubtitleText.Text = AppResources.GetString("PreviewSubtitle", _currentLanguage);
        SuccessTitleText.Text = AppResources.GetString("FolderIconUpdated", _currentLanguage);
        SuccessSubtitleText.Text = AppResources.GetString("SuccessSubtitle", _currentLanguage);
        CloseSuccessButton.Content = AppResources.GetString("Close", _currentLanguage);
        UndoButton.Content = AppResources.GetString("Undo", _currentLanguage);
        TryAgainButton.Content = AppResources.GetString("TryAgain", _currentLanguage);
        UpdatePlacementStrings();

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
