using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using WinRT.Interop;

namespace Ornare.App;

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

        // Center on display if presenter available
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        // Pointer tracking for auto-close behavior
        if (Content is FrameworkElement root)
        {
            root.PointerEntered += (_, _) => _isPointerInside = true;
            root.PointerExited += (_, _) => _isPointerInside = false;
        }
    }

    private void SetupKeyboardShortcuts()
    {
        if (Content is FrameworkElement root)
        {
            root.KeyDown += async (_, e) =>
            {
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
            ShowErrorState("Invalid target", validation.ErrorMessage ?? "Folder cannot be customized.");
            return;
        }

        // Query storage for existing baseline
        _currentBaseline = await _storageService.GetLatestBaselineAsync(folderPath);
        FolderCustomizationState state = _folderCustomizer.GetFolderState(folderPath, _currentBaseline);

        if (state == FolderCustomizationState.ExternalIconConflict)
        {
            StatusInfoText.Text = "External modification detected";
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

        ShowIdleState();
    }

    private void OnDropAreaDragOver(object sender, DragEventArgs e)
    {
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
        DropAreaBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
        DropAreaBorder.Background = new SolidColorBrush(Colors.Transparent);
    }

    private async void OnDropAreaDrop(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
        DropAreaBorder.Background = new SolidColorBrush(Colors.Transparent);

        if (e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is global::Windows.Storage.StorageFile file)
            {
                await ApplyImageAsync(file.Path);
            }
        }
    }

    private async void OnDropAreaTapped(object sender, TappedRoutedEventArgs e)
    {
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
            await ApplyImageAsync(file.Path);
        }
    }

    private async Task HandlePasteAsync()
    {
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
                await ApplyImageAsync(file.Path);
            }
        }
        else if (package.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
        {
            var streamRef = await package.GetBitmapAsync();
            using var stream = await streamRef.OpenReadAsync();
            using var netStream = stream.AsStreamForRead();
            using var ms = new MemoryStream();
            await netStream.CopyToAsync(ms);

            await ApplyImageBytesAsync(ms.ToArray());
        }
    }

    private async Task ApplyImageAsync(string imagePath)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(imagePath);
        }
        catch (Exception ex)
        {
            ShowErrorState("Failed to read file", ex.Message);
            return;
        }

        await ApplyImageBytesAsync(bytes);
    }

    private async Task ApplyImageBytesAsync(byte[] imageBytes)
    {
        if (string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            ShowErrorState("Invalid target", "No folder selected.");
            return;
        }

        using var lockHandle = await PerTargetSyncLock.TryAcquireAsync(_currentFolderPath, TimeSpan.FromSeconds(5));
        if (lockHandle == null)
        {
            ShowErrorState("Conflict", "Another operation is currently modifying this folder.");
            return;
        }

        ShowProcessingState();

        Guid operationId = Guid.NewGuid();
        string tempIcoPath = Path.Combine(Path.GetTempPath(), $"ornare_{operationId}.ico");

        // Journal step: Prepared
        JournalEntry journal = new()
        {
            OperationId = operationId,
            Kind = OperationKind.Apply,
            TargetFolderPath = _currentFolderPath,
            State = JournalState.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TargetAssetPath = tempIcoPath
        };
        await _storageService.RecordJournalEntryAsync(journal);

        // 1. Image pipeline execution
        var processResult = _imagePipeline.ProcessImageBytes(imageBytes, tempIcoPath);
        if (!processResult.IsSuccess || processResult.Value == null)
        {
            await _storageService.UpdateJournalStateAsync(operationId, JournalState.Aborted, processResult.ErrorMessage);
            ShowErrorState("Image decode error", processResult.ErrorMessage ?? "Failed to decode image.");
            return;
        }

        await _storageService.UpdateJournalStateAsync(operationId, JournalState.AssetGenerated);

        // 2. Capture baseline prior semantics if not already captured
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

        // 3. Apply folder mutation
        var applyResult = _folderCustomizer.ApplyIcon(_currentFolderPath, tempIcoPath, async state =>
        {
            await _storageService.UpdateJournalStateAsync(operationId, state);
        });

        if (!applyResult.IsSuccess || applyResult.Value == null)
        {
            await _storageService.UpdateJournalStateAsync(operationId, JournalState.NeedsRepair, applyResult.ErrorMessage);
            ShowErrorState("Filesystem error", applyResult.ErrorMessage ?? "Could not apply folder icon.");
            return;
        }

        // 4. Save assignment & baseline to database
        await _storageService.SaveFolderAssignmentAsync(
            _currentFolderPath,
            _currentOwnershipEpochId,
            applyResult.Value,
            _currentBaseline);

        await _storageService.UpdateJournalStateAsync(operationId, JournalState.Completed);

        // Clean up staged temp ICO
        try
        {
            if (File.Exists(tempIcoPath))
            {
                File.Delete(tempIcoPath);
            }
        }
        catch
        {
            // Non-critical temp cleanup failure
        }

        ShowSuccessState();
        ResetButton.Visibility = Visibility.Visible;
        StartAutoCloseTimer();
    }

    private async void OnUndoClicked(object sender, RoutedEventArgs e)
    {
        if (_currentBaseline == null || string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            return;
        }

        ShowProcessingState();

        var result = _folderCustomizer.UndoIcon(_currentFolderPath, _currentBaseline);
        if (result.IsSuccess)
        {
            await _storageService.RemoveFolderAssignmentAsync(_currentFolderPath);
            _currentBaseline = null;
            ResetButton.Visibility = Visibility.Collapsed;
            ShowIdleState();
        }
        else
        {
            ShowErrorState("Undo failed", result.ErrorMessage ?? "Could not undo icon change.");
        }
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (_currentBaseline == null || string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            return;
        }

        ShowProcessingState();

        var result = _folderCustomizer.ResetIcon(_currentFolderPath, _currentBaseline);
        if (result.IsSuccess)
        {
            await _storageService.RemoveFolderAssignmentAsync(_currentFolderPath);
            _currentBaseline = null;
            ResetButton.Visibility = Visibility.Collapsed;
            ShowIdleState();
        }
        else
        {
            ShowErrorState("Reset failed", result.ErrorMessage ?? "Could not reset folder icon.");
        }
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

    private void ShowIdleState()
    {
        IdleStatePanel.Visibility = Visibility.Visible;
        ProcessingStatePanel.Visibility = Visibility.Collapsed;
        SuccessStatePanel.Visibility = Visibility.Collapsed;
        ErrorStatePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowProcessingState()
    {
        IdleStatePanel.Visibility = Visibility.Collapsed;
        ProcessingStatePanel.Visibility = Visibility.Visible;
        SuccessStatePanel.Visibility = Visibility.Collapsed;
        ErrorStatePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowSuccessState()
    {
        IdleStatePanel.Visibility = Visibility.Collapsed;
        ProcessingStatePanel.Visibility = Visibility.Collapsed;
        SuccessStatePanel.Visibility = Visibility.Visible;
        ErrorStatePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowErrorState(string title, string message)
    {
        IdleStatePanel.Visibility = Visibility.Collapsed;
        ProcessingStatePanel.Visibility = Visibility.Collapsed;
        SuccessStatePanel.Visibility = Visibility.Collapsed;
        ErrorStatePanel.Visibility = Visibility.Visible;

        ErrorTitleText.Text = title;
        ErrorMessageText.Text = message;
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

        if (string.Equals(lang, "tr", StringComparison.OrdinalIgnoreCase) || string.Equals(lang, "tr-TR", StringComparison.OrdinalIgnoreCase))
        {
            DropPromptText.Text = "Bir görsel bırak";
            ClickPromptText.Text = "Seçmek için tıkla veya Ctrl+V'ye bas";
            SuccessTitleText.Text = "Simge uygulandı";
            UndoButton.Content = "Geri al";
            ResetButton.Content = "Sıfırla";
            ErrorTitleText.Text = "Simge uygulanamadı";
        }

        if (string.Equals(uiState, "drag-over", StringComparison.OrdinalIgnoreCase))
        {
            ShowIdleState();
            DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            DropAreaBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
        else if (string.Equals(uiState, "success", StringComparison.OrdinalIgnoreCase))
        {
            ShowSuccessState();
            _autoCloseTimer?.Stop();
        }
        else if (string.Equals(uiState, "undo", StringComparison.OrdinalIgnoreCase))
        {
            ShowSuccessState();
            _autoCloseTimer?.Stop();
            UndoButton.Focus(FocusState.Programmatic);
        }
        else if (string.Equals(uiState, "error", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(lang, "tr", StringComparison.OrdinalIgnoreCase) || string.Equals(lang, "tr-TR", StringComparison.OrdinalIgnoreCase))
            {
                ShowErrorState("Görsel çözülemedi", "Desteklenmeyen dosya biçimi veya bozuk görüntü verisi.");
            }
            else
            {
                ShowErrorState("Could not decode image", "The image file appears corrupted or unsupported.");
            }
        }
    }
}
