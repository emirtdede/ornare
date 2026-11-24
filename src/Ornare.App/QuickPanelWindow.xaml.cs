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
using Ornare.Imaging.Emblems;
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
    public ObservableCollection<VectorEmblemItemViewModel> VectorEmblemCards { get; } = new();
    public ObservableCollection<EmojiItemViewModel> EmojiCards { get; } = new();

    private List<VectorEmblemItemViewModel>? _allEmblemCards;
    private List<EmojiItemViewModel>? _allEmojiCards;
    private string _selectedEmblemColorHex = "Auto";
    private bool _emblemCatalogInitialized;
    private bool _emojiCatalogInitialized;

    private string _currentFolderPath = string.Empty;
    private FolderBaseline? _currentBaseline;
    private Guid _currentOwnershipEpochId = Guid.NewGuid();
    private DispatcherTimer? _autoCloseTimer;
    private bool _isPointerInside;
    private bool _isProcessing;
    private bool _isCommitting;
    // Authoritative reentrancy gate for image staging. `_isProcessing` is derived UI state:
    // SetProcessingStage sets it *after* the work begins and ShowCategorizedError clears it
    // from an unrelated failure, so it cannot be the lock. This counter is the lock; it is
    // acquired with Interlocked and always released in a finally block.
    private int _stagingGate;
    private volatile bool _disposed;
    private string? _pendingTargetPath;
    private ProcessingStage _currentStage = ProcessingStage.Idle;
    // Seeded from the process locale App resolved at startup, so the panel's first frame is in
    // the user's chosen language instead of the OS language until storage answers.
    private string _currentLanguage = AppResources.CurrentLocale;
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
    private CancellationTokenSource? _previewRenderCts;
    private long _previewRenderSequence;
    private string? _tempPreviewPngPath;
    private readonly List<string> _tempThumbnailPngPaths = new();

    private string? _snapshotFolderColorHex;

    // The ColorPicker raises ColorChanged for programmatic assignments too. Without this
    // guard, seeding the picker with a default (or syncing it from staged state) would be
    // read as a user choice: the panel would mark the folder dirty on open and later apply a
    // colour the user never picked. Every write to CustomColorPicker.Color goes through
    // SetPickerColorSilently.
    private bool _suppressColorPickerEvents;

    /// <summary>
    /// Whether the first-use guidance is currently occupying the preview subtitle slot. Kept on
    /// the instance so a language switch re-renders the hint instead of dropping it.
    /// </summary>
    private bool _firstUseHintVisible;

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

        // The panel has no settings surface of its own: seed theme and copy from what the app
        // already resolved so the first frame already matches the chosen theme and language
        // while the target folder loads.
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = App.CurrentTheme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
        UpdateLocalizedChrome();

        // The panel can outlive a language change made in the manager. Without this
        // subscription the panel keeps the locale it was born with, and its copy silently
        // mixes two languages until the next activation.
        AppResources.CurrentLocaleChanged += OnAppLanguageChanged;

        Closed += (_, _) => Dispose();
    }

    /// <summary>
    /// Runs an <c>async void</c> handler body so an exception cannot escape to the
    /// dispatcher and terminate the process. Cancellation is expected and ignored;
    /// genuine failures are recorded locally and surfaced through the error state.
    /// </summary>
    private async Task GuardAsync(string operation, Func<Task> body)
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
            App.Log($"{operation} failed: {ex}");

            // A handler that was still running when the panel closed must not touch the
            // visual tree: elements are disconnected after Closed and an assignment throws
            // COMException, which would escape this guard through the async void boundary.
            if (!_disposed)
            {
                ShowCategorizedError(QuickPanelErrorType.Generic);
            }
        }
    }

    /// <summary>
    /// Acquires the image-staging gate. Returns false when another staging operation owns it,
    /// which is the expected outcome of a second drag-drop, paste or pick while the first
    /// image is being decoded.
    /// </summary>
    private bool TryBeginStaging()
    {
        return Interlocked.CompareExchange(ref _stagingGate, 1, 0) == 0;
    }

    private void EndStaging()
    {
        Interlocked.Exchange(ref _stagingGate, 0);
    }

    private bool IsStagingInProgress => Volatile.Read(ref _stagingGate) != 0;

    /// <summary>
    /// Resolves a theme brush without throwing when a key is missing (for example under
    /// high contrast or a future theme dictionary change). A missing key must degrade to
    /// a visible fallback, never crash a visual-state update.
    /// </summary>
    private static Brush GetThemeBrush(string key, global::Windows.UI.Color fallback)
    {
        if (Application.Current.Resources.TryGetValue(key, out object? value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
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
            await GuardAsync("QuickPanelPasteAccelerator", async () =>
            {
                if (!_isProcessing && _currentStage == ProcessingStage.Idle)
                {
                    await HandlePasteAsync();
                }
            });
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
            await GuardAsync("QuickPanelUndoAccelerator", async () =>
            {
                if (!_isProcessing)
                {
                    await HandleUndoAcceleratorAsync();
                }
            });
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
            SetProcessingStage(ProcessingStage.Idle);
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
            SetProcessingStage(ProcessingStage.Idle);
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
        // A new shell activation can arrive while a commit is in flight. Swapping the
        // target then would make the commit's filesystem mutation and its storage writes
        // address different folders, so the request is coalesced into one pending target
        // and applied after the commit finishes (INV-CON-002).
        if (_isCommitting)
        {
            _pendingTargetPath = folderPath;
            return;
        }

        await ApplyTargetFolderAsync(folderPath);
    }

    private async Task ApplyTargetFolderAsync(string folderPath)
    {
        // A new target invalidates every artefact bound to the previous folder.
        // Without this the window (reused across activations by App.OnLaunched)
        // would carry the previous folder's ownership epoch and staged ICO into
        // the new target, cross-linking assignments and history rows.
        if (!string.Equals(_currentFolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
        {
            CleanupStagedFiles();
            _currentBaseline = null;
            _currentOwnershipEpochId = Guid.NewGuid();
            _stagedImageBytes = null;
            _stagedProcessResult = null;
            _stagedCustomColorHex = null;

            // A debounced colour preview scheduled for the previous folder must not land on
            // the new one. Without this the target switch races the 80 ms timer and the
            // staging model silently stages a colour for a folder the user never touched.
            _previewDebounceCts?.Cancel();
            _previewDebounceCts?.Dispose();
            _previewDebounceCts = null;
        }

        _currentFolderPath = folderPath;
        FolderNameText.Text = Path.GetFileName(folderPath.TrimEnd('\\', '/'));
        FolderPathText.Text = folderPath;

        OperationResult validation = TargetFolderValidator.ValidateTarget(folderPath);
        if (!validation.IsSuccess)
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Message(AppResources.GetString("ErrorFolderNotCustomizable", _currentLanguage)));
            return;
        }

        // Query storage for existing baseline
        try
        {
            _currentBaseline = await _storageService.GetLatestBaselineAsync(folderPath);
        }
        catch (Exception ex)
        {
            App.Log($"SetTargetFolderAsync: baseline lookup failed: {ex.Message}");
            _currentBaseline = null;
        }

        FolderCustomizationState state = _folderCustomizer.GetFolderState(folderPath, _currentBaseline);

        // The panel language is owned by AppResources.CurrentLocale: the constructor seeds it
        // from the process locale and OnAppLanguageChanged keeps it live. The old
        // "App.Language" setting read here was a dead key nothing ever wrote, so it could only
        // run when the value happened to be null and never reflected a real user choice.
        UpdateLocalizedChrome();

        if (state == FolderCustomizationState.ExternalIconConflict)
        {
            StatusInfoText.Text = AppResources.GetString("ExternalConflictMessage", _currentLanguage);
            ResetButton.Visibility = Visibility.Collapsed;
            FirstUseHintDismissButton.Visibility = Visibility.Collapsed;
        }
        else if (_currentBaseline != null)
        {
            ResetButton.Visibility = Visibility.Visible;
            StatusInfoText.Text = AppResources.GetString("Status_CurrentIcon", _currentLanguage);
            FirstUseHintDismissButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ResetButton.Visibility = Visibility.Collapsed;
            StatusInfoText.Text = AppResources.GetString("Status_ReadyToApply", _currentLanguage);

            // A new user gets one concrete sentence in the preview slot plus a way to hide it.
            // It is not a modal and not a tour, and it stops appearing after the first apply
            // or the first dismissal (audit finding 3.2: the panel gave no first-use guidance).
            string? hintDismissed = await _storageService.GetSettingAsync("QuickPanelHintDismissed");
            ApplyFirstUseHintState(!string.Equals(hintDismissed, "true", StringComparison.OrdinalIgnoreCase));
        }

        // Initialize staging model with default Mode B (Classic v2, #E8A317 / Windows Folder Amber)
        _stagingModel.Initialize(
            PersonalizationMode.FolderTemplate,
            templateState: new FolderTemplateStagingState
            {
                TemplateId = FolderTemplateCatalog.DefaultTemplateId,
                TemplateVersion = FolderTemplateCatalog.DefaultTemplateVersion,
                FolderColorHex = "#E8A317",
                Emblem = FolderEmblemRecipe.None
            });
        // Initialize the 9 template cards
        InitializeTemplateCards();

        // Render initial live preview
        await RefreshLivePreviewAsync();

        SetProcessingStage(ProcessingStage.Idle);
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

        if (FolderTemplateGridView != null && FolderTemplateGridView.ItemsSource == null)
        {
            FolderTemplateGridView.ItemsSource = FolderTemplateCards;
        }

        _ = RenderTemplateCardThumbnailsAsync();
    }

    private static void UpdateCardVisualState(FolderTemplateCardViewModel card)
    {
        if (card.IsSelected)
        {
            card.BorderBrush = GetThemeBrush("AccentFillColorDefaultBrush", Microsoft.UI.Colors.DodgerBlue);
            card.Background = GetThemeBrush("SubtleFillColorSecondaryBrush", Microsoft.UI.Colors.Transparent);
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
        // Preview renders complete out of order: the task that finishes last wins, not the one
        // that was requested last. Hovering across ten template cards would otherwise leave an
        // arbitrary card's preview on screen after the pointer stops. Each call takes a
        // sequence number and a render whose number is stale discards its own result.
        long sequence = Interlocked.Increment(ref _previewRenderSequence);

        try
        {
            if (_stagingModel.ActiveMode == PersonalizationMode.FolderTemplate)
            {
                string templateId = overrideTemplateId ?? _stagingModel.StagedTemplateState.TemplateId;
                int templateVersion = overrideTemplateVersion ?? _stagingModel.StagedTemplateState.TemplateVersion;
                string colorHex = overrideColorHex ?? _stagingModel.StagedTemplateState.FolderColorHex;
                var emblem = _stagingModel.StagedTemplateState.Emblem;

                byte[]? png = TryGetCachedPreview(templateId, templateVersion, colorHex, emblem);
                if (png == null)
                {
                    var result = await Task.Run(() => FolderTemplateRenderer.RenderPreviewPng(colorHex, templateId, templateVersion, emblem: emblem));
                    if (IsStalePreview(sequence))
                    {
                        return;
                    }

                    if (!result.IsSuccess || result.Value == null)
                    {
                        return;
                    }

                    png = result.Value;
                    StoreCachedPreview(templateId, templateVersion, colorHex, emblem, png);
                }

                if (IsStalePreview(sequence))
                {
                    return;
                }

                PreviewIconImage.Source = LoadPreviewBitmapFromBytes(png);

                if (PlacementOptionsButton != null) PlacementOptionsButton.Visibility = Visibility.Collapsed;
                if (CancelPreviewButton != null) CancelPreviewButton.Visibility = Visibility.Collapsed;
            }
            else if (_stagingModel.ActiveMode == PersonalizationMode.CustomImage && _stagingModel.StagedImageState.ImageBytes != null)
            {
                var imgState = _stagingModel.StagedImageState;
                var result = await Task.Run(() => ImagePipeline.RenderPlacementPreviewPng(
                    imgState.ImageBytes, 256, imgState.PlacementMode, imgState.PaddingPercent, imgState.BackgroundMode, imgState.BackgroundColorHex));
                if (IsStalePreview(sequence))
                {
                    return;
                }

                if (result.IsSuccess && result.Value != null)
                {
                    PreviewIconImage.Source = LoadPreviewBitmapFromBytes(result.Value);
                }

                if (PlacementOptionsButton != null) PlacementOptionsButton.Visibility = Visibility.Visible;
                if (CancelPreviewButton != null) CancelPreviewButton.Visibility = Visibility.Visible;
            }

            // Update Apply button and Status text
            ApplyConfirmButton.IsEnabled = _stagingModel.CanApply;
            RefreshStatusInfoText();
        }
        catch (Exception ex)
        {
            App.Log($"RefreshLivePreviewAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// True when a newer preview request has superseded <paramref name="sequence"/>, or the
    /// panel closed while this render was in flight.
    /// </summary>
    private bool IsStalePreview(long sequence)
    {
        return _disposed || sequence != Volatile.Read(ref _previewRenderSequence);
    }

    // Bounded, process-local preview cache. A template preview is a pure function of
    // (template, version, colour, emblem), so hovering the same card twice must not rasterize
    // twice. The dictionary is cleared wholesale when it exceeds the cap rather than tracking
    // LRU order: the working set is a handful of cards, and a full clear costs one re-render.
    private const int PreviewCacheCapacity = 24;
    private static readonly Dictionary<string, byte[]> PreviewCache = new(StringComparer.Ordinal);
    private static readonly object PreviewCacheGate = new();

    private static string PreviewCacheKey(
        string templateId, int templateVersion, string colorHex, FolderEmblemRecipe emblem)
    {
        return string.Concat(
            templateId, "|",
            templateVersion.ToString(CultureInfo.InvariantCulture), "|",
            colorHex, "|",
            ((int)emblem.Kind).ToString(CultureInfo.InvariantCulture), "|",
            emblem.ContentId);
    }

    private static byte[]? TryGetCachedPreview(
        string templateId, int templateVersion, string colorHex, FolderEmblemRecipe emblem)
    {
        string key = PreviewCacheKey(templateId, templateVersion, colorHex, emblem);
        lock (PreviewCacheGate)
        {
            return PreviewCache.TryGetValue(key, out byte[]? cached) ? cached : null;
        }
    }

    private static void StoreCachedPreview(
        string templateId, int templateVersion, string colorHex, FolderEmblemRecipe emblem, byte[] png)
    {
        string key = PreviewCacheKey(templateId, templateVersion, colorHex, emblem);
        lock (PreviewCacheGate)
        {
            if (PreviewCache.Count >= PreviewCacheCapacity)
            {
                PreviewCache.Clear();
            }

            PreviewCache[key] = png;
        }
    }

    /// <summary>
    /// Debounced hover preview. A pointer sweeping across the template grid would otherwise
    /// start one raster per card and freeze the UI thread on the task flood.
    /// </summary>
    private void ScheduleHoverPreview(string? templateId = null, int? templateVersion = null)
    {
        if (_disposed)
        {
            return;
        }

        _previewRenderCts?.Cancel();
        _previewRenderCts?.Dispose();
        var cts = new CancellationTokenSource();
        _previewRenderCts = cts;
        CancellationToken token = cts.Token;

        _ = GuardAsync(nameof(ScheduleHoverPreview), async () =>
        {
            await Task.Delay(120, token);
            token.ThrowIfCancellationRequested();
            await RefreshLivePreviewAsync(templateId, templateVersion);
        });
    }

    private void OnFolderTemplateCardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FolderTemplateCardViewModel card)
        {
            ScheduleHoverPreview(card.Id, card.Version);
        }
    }

    private void OnFolderTemplateCardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        ScheduleHoverPreview();
    }

    private async void OnFolderTemplateCardClicked(object sender, ItemClickEventArgs e)
    {
        await GuardAsync(nameof(OnFolderTemplateCardClicked), async () =>
        {
            if (e.ClickedItem is FolderTemplateCardViewModel card)
            {
                _stagingModel.StageFolderTemplate(card.Id, card.Version);

                foreach (var item in FolderTemplateCards)
                {
                    item.IsSelected = string.Equals(item.Id, card.Id, StringComparison.OrdinalIgnoreCase);
                    UpdateCardVisualState(item);
                }

                FolderStyleFlyout?.Hide();
                await RefreshLivePreviewAsync();
            }
        });
    }

    private async void OnFolderStyleFlyoutOpening(object? sender, object? e)
    {
        await GuardAsync(nameof(OnFolderStyleFlyoutOpening), async () =>
        {
            if (FolderTemplateCards.Count == 0)
            {
                InitializeTemplateCards();
            }
            if (FolderTemplateGridView != null && FolderTemplateGridView.ItemsSource == null)
            {
                FolderTemplateGridView.ItemsSource = FolderTemplateCards;
            }
            if (_stagingModel.ActiveMode != PersonalizationMode.FolderTemplate)
            {
                _stagingModel.SwitchMode(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
            }
        });
    }

    private void OnImageActionButtonClicked(object sender, RoutedEventArgs e)
    {
        _ = OpenFilePickerAsync();
    }

    private async void OnIconFlyoutOpening(object? sender, object? e)
    {
        await GuardAsync(nameof(OnIconFlyoutOpening), async () =>
        {
            EnsureEmblemCatalogInitialized();
            SyncEmblemSelectionState();
            if (_stagingModel.ActiveMode != PersonalizationMode.FolderTemplate)
            {
                _stagingModel.SwitchMode(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
            }
        });
    }


    private async void OnEmojiFlyoutOpening(object? sender, object? e)
    {
        await GuardAsync(nameof(OnEmojiFlyoutOpening), async () =>
        {
            EnsureEmojiCatalogInitialized();
            SyncEmojiSelectionState();
            if (_stagingModel.ActiveMode != PersonalizationMode.FolderTemplate)
            {
                _stagingModel.SwitchMode(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
            }
        });
    }


    private async void OnEmblemColorAutoClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnEmblemColorAutoClicked), async () =>
        {
            _selectedEmblemColorHex = "Auto";
            if (_stagingModel.StagedTemplateState.Emblem.Kind == EmblemKind.VectorIcon)
            {
                _stagingModel.StageVectorEmblem(_stagingModel.StagedTemplateState.Emblem.ContentId, _selectedEmblemColorHex);
                await RefreshLivePreviewAsync();
            }
        });
    }

    private async void OnEmblemColorWhiteClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnEmblemColorWhiteClicked), async () =>
        {
            _selectedEmblemColorHex = "#FFFFFF";
            if (_stagingModel.StagedTemplateState.Emblem.Kind == EmblemKind.VectorIcon)
            {
                _stagingModel.StageVectorEmblem(_stagingModel.StagedTemplateState.Emblem.ContentId, _selectedEmblemColorHex);
                await RefreshLivePreviewAsync();
            }
        });
    }

    private async void OnEmblemColorBlackClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnEmblemColorBlackClicked), async () =>
        {
            _selectedEmblemColorHex = "#000000";
            if (_stagingModel.StagedTemplateState.Emblem.Kind == EmblemKind.VectorIcon)
            {
                _stagingModel.StageVectorEmblem(_stagingModel.StagedTemplateState.Emblem.ContentId, _selectedEmblemColorHex);
                await RefreshLivePreviewAsync();
            }
        });
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
            DropAreaBorder.BorderBrush = GetThemeBrush("AccentFillColorDefaultBrush", Microsoft.UI.Colors.DodgerBlue);
            DropAreaBorder.Background = GetThemeBrush("SubtleFillColorSecondaryBrush", Microsoft.UI.Colors.Transparent);
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

        try
        {
            if (e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is global::Windows.Storage.StorageFile file)
                {
                    await StageImageAsync(file.Path);
                }
            }
        }
        catch (Exception ex)
        {
            // Drag payloads from elevated/virtual sources (Outlook, ZIP-in-Explorer,
            // revoked drag sessions) throw COMException or UnauthorizedAccessException.
            App.Log($"OnDropAreaDrop failed: {ex.Message}");
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed);
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

        try
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
            picker.FileTypeFilter.Add(".svg");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await StageImageAsync(file.Path);
            }
        }
        catch (Exception ex)
        {
            // The picker throws when the window is closing or COM apartment is torn down.
            App.Log($"OpenFilePickerAsync failed: {ex.Message}");
        }
    }

    private async Task HandlePasteAsync()
    {
        if (_isProcessing || IsStagingInProgress)
        {
            return;
        }

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
                if (items.Count > 0 && items[0] is global::Windows.Storage.StorageFile file)
                {
                    await StageImageAsync(file.Path);
                }
            }
            else if (package.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
            {
                var streamRef = await package.GetBitmapAsync();
                using var stream = await streamRef.OpenReadAsync();

                // Check the clipboard payload size before copying it into a MemoryStream: a
                // screenshot of a multi-monitor 8K desktop can be hundreds of megabytes and the
                // copy doubles that residency.
                if (stream.Size > (ulong)OrnareConstants.MaxInputImageBytes)
                {
                    App.Log($"Rejected oversized clipboard image ({stream.Size} bytes).");
                    ShowCategorizedError(QuickPanelErrorType.UnsupportedImage,
                        QuickPanelErrorCopy.Message(AppResources.Format("ErrorImageTooLarge", _currentLanguage,
                            OrnareConstants.MaxInputImageBytes / (1024 * 1024))));
                    return;
                }

                using var netStream = stream.AsStreamForRead();
                using var ms = new MemoryStream();
                await netStream.CopyToAsync(ms);

                await StageImageBytesAsync(ms.ToArray());
            }
        }
        catch (Exception ex)
        {
            // A clipboard held by another process (RDP, Excel, password managers)
            // raises COMException on GetContent/GetStorageItemsAsync.
            App.Log($"HandlePasteAsync failed: {ex.Message}");
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed);
        }
    }

    private async Task StageImageAsync(string imagePath)
    {
        if (_isProcessing || IsStagingInProgress)
        {
            return;
        }

        // Bound the read before it happens. The Magick pipeline caps decoded pixels and
        // memory, but this read copies the compressed file into a single byte[] first, so a
        // multi-gigabyte "image" would exhaust the large object heap before any policy runs.
        long fileLength;
        try
        {
            fileLength = new FileInfo(imagePath).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed,
                QuickPanelErrorCopy.Of(AppResources.GetString("ErrorImageReadMessage", _currentLanguage), ex.Message));
            return;
        }

        if (fileLength <= 0)
        {
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed,
                QuickPanelErrorCopy.Message(AppResources.GetString("ErrorEmptyImage", _currentLanguage)));
            return;
        }

        if (fileLength > OrnareConstants.MaxInputImageBytes)
        {
            App.Log($"Rejected oversized input image ({fileLength} bytes, limit {OrnareConstants.MaxInputImageBytes}).");
            ShowCategorizedError(QuickPanelErrorType.UnsupportedImage,
                QuickPanelErrorCopy.Message(AppResources.Format("ErrorImageTooLarge", _currentLanguage,
                    OrnareConstants.MaxInputImageBytes / (1024 * 1024))));
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(imagePath);
        }
        catch (Exception ex)
        {
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed,
                QuickPanelErrorCopy.Of(AppResources.GetString("ErrorImageReadMessage", _currentLanguage), ex.Message));
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
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Message(AppResources.GetString("ErrorNoFolderSelected", _currentLanguage)));
            return;
        }

        if (imageBytes == null || imageBytes.Length == 0)
        {
            ShowCategorizedError(QuickPanelErrorType.ImageReadFailed, QuickPanelErrorCopy.Message(AppResources.GetString("ErrorEmptyImage", _currentLanguage)));
            return;
        }

        // The clipboard path reaches this method without passing StageImageAsync, so the byte
        // cap has to be enforced here as well; a pasted 200 MB image must fail as a message,
        // not as an OutOfMemoryException in the decoder.
        if (imageBytes.Length > OrnareConstants.MaxInputImageBytes)
        {
            App.Log($"Rejected oversized image payload ({imageBytes.Length} bytes).");
            ShowCategorizedError(QuickPanelErrorType.UnsupportedImage,
                QuickPanelErrorCopy.Message(AppResources.Format("ErrorImageTooLarge", _currentLanguage,
                    OrnareConstants.MaxInputImageBytes / (1024 * 1024))));
            return;
        }

        // Two drag-drops, a paste and a pick can all arrive before SetProcessingStage has run,
        // because the flag it controls is UI state, not a lock. The gate below is the lock.
        if (!TryBeginStaging())
        {
            return;
        }

        try
        {
            CleanupStagedFiles();
            App.Log($"StageImageBytesAsync started for '{_currentFolderPath}', bytes={imageBytes.Length}");

            SetProcessingStage(ProcessingStage.PreparingImage);

            _stagedOperationId = Guid.NewGuid();
            _stagedTempIcoPath = Path.Combine(Path.GetTempPath(), $"ornare_staged_{_stagedOperationId:N}.ico");

            SetProcessingStage(ProcessingStage.GeneratingIcon);

            var tempPath = _stagedTempIcoPath;
            var processResult = await Task.Run(() => _imagePipeline.ProcessImageBytes(imageBytes, tempPath));
            if (!processResult.IsSuccess || processResult.Value == null)
            {
                App.Log($"ProcessImageBytes failed: {processResult.DiagnosticDescription}");
                CleanupStagedFiles();
                if (processResult.ErrorCode == ErrorCode.UnsupportedImageFormat || processResult.ErrorCode == ErrorCode.ImageCorrupted)
                {
                    ShowCategorizedError(QuickPanelErrorType.UnsupportedImage, QuickPanelErrorCopy.Diagnostic(processResult.DiagnosticDescription));
                }
                else
                {
                    ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, QuickPanelErrorCopy.Diagnostic(processResult.DiagnosticDescription));
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

            SetProcessingStage(ProcessingStage.PreviewReady);
        }
        catch (Exception ex)
        {
            App.Log($"StageImageBytesAsync unhandled exception: {ex}");
            CleanupStagedFiles();
            ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, QuickPanelErrorCopy.Diagnostic(ex.Message));
        }
        finally
        {
            // Released on every path: success, decode failure, unexpected exception, and the
            // early return after a failed ProcessImageBytes.
            EndStaging();
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
            SetPickerColorSilently(global::Windows.UI.Color.FromArgb(255, 37, 99, 235));
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
                button.Click += async (s, args) => await GuardAsync("SelectFolderColor", async () =>
                {
                    if (s is Button btn && btn.Tag is string hex)
                    {
                        _stagingModel.StageFolderColor(hex);
                        await RefreshLivePreviewAsync();
                        _ = RenderTemplateCardThumbnailsAsync();
                    }
                });
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
                flyoutBtn.Click += async (s, args) => await GuardAsync("SelectCustomFolderColor", async () =>
                {
                    if (s is Button btn && btn.Tag is string hex)
                    {
                        _stagingModel.StageFolderColor(hex);
                        await RefreshLivePreviewAsync();
                        _ = RenderTemplateCardThumbnailsAsync();
                        CustomColorFlyout?.Hide();
                    }
                });
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

    private async void OnCustomColorFlyoutOpened(object sender, object e)
    {
        await GuardAsync(nameof(OnCustomColorFlyoutOpened), async () =>
        {
            if (_stagingModel.ActiveMode != PersonalizationMode.FolderTemplate)
            {
                _stagingModel.SwitchMode(PersonalizationMode.FolderTemplate);
                await RefreshLivePreviewAsync();
            }

            _snapshotFolderColorHex = _stagingModel.StagedTemplateState.FolderColorHex;

            if (CustomColorTitleText != null) CustomColorTitleText.Text = AppResources.GetString("ColorPicker_CustomColor", _currentLanguage);
            if (CustomColorSubtitleText != null) CustomColorSubtitleText.Text = AppResources.GetString("ColorPicker_Subtitle", _currentLanguage);
            if (RecentColorsTitle != null) RecentColorsTitle.Text = AppResources.GetString("ColorPicker_RecentColors", _currentLanguage);
            if (ApplyCustomColorButton != null) ApplyCustomColorButton.Content = AppResources.GetString("ColorPicker_Apply", _currentLanguage);
            if (CancelCustomColorButton != null) CancelCustomColorButton.Content = AppResources.GetString("ColorPicker_Cancel", _currentLanguage);

            if (CustomColorPicker != null)
            {
                if (ColorConversion.TryParseHex(_stagingModel.StagedTemplateState.FolderColorHex, out byte r, out byte g, out byte b))
                {
                    SetPickerColorSilently(global::Windows.UI.Color.FromArgb(255, r, g, b));
                }
                else
                {
                    SetPickerColorSilently(global::Windows.UI.Color.FromArgb(255, 37, 99, 235));
                }

                var currentColor = CustomColorPicker.Color;
                string currentHex = ColorConversion.ToHex(currentColor.R, currentColor.G, currentColor.B);
                var initialPreview = FolderTemplateRenderer.RenderPreviewPng(
                    currentHex,
                    _stagingModel.StagedTemplateState.TemplateId,
                    _stagingModel.StagedTemplateState.TemplateVersion,
                    emblem: _stagingModel.StagedTemplateState.Emblem);
                if (initialPreview.IsSuccess && initialPreview.Value != null && FlyoutPreviewImage != null)
                {
                    FlyoutPreviewImage.Source = LoadPreviewBitmapFromBytes(initialPreview.Value);
                }
            }

            _ = LoadRecentCustomColorsToFlyoutAsync();
        });
    }

    private void OnCustomColorFlyoutClosed(object sender, object e)
    {
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        _previewDebounceCts = null;

        _ = RenderTemplateCardThumbnailsAsync();
        ColorActionButton?.Focus(FocusState.Programmatic);
    }

    private void OnCustomColorPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        // A programmatic seed (default colour, staged-state sync, flyout open) is not a user
        // choice and must not mark the folder dirty.
        if (_suppressColorPickerEvents)
        {
            return;
        }

        TriggerCustomColorPreview();
    }

    /// <summary>
    /// Shows or hides the first-use guidance. The hint reuses the preview subtitle line, so
    /// there is no extra row and the frozen 420x330 panel geometry does not change.
    /// </summary>
    private void ApplyFirstUseHintState(bool showHint)
    {
        _firstUseHintVisible = showHint;

        if (PreviewSubtitleText is null || FirstUseHintDismissButton is null)
        {
            return;
        }

        PreviewSubtitleText.Text = showHint
            ? AppResources.GetString("QuickPanel_FirstUseHint", _currentLanguage)
            : AppResources.GetString("PreviewSubtitle", _currentLanguage);

        FirstUseHintDismissButton.Visibility = showHint ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Hides the first-use hint for good. A failed write only costs the hint another showing,
    /// so it is logged rather than surfaced: no user action is blocked by a dismissed tip.
    /// </summary>
    private async Task DismissFirstUseHintAsync()
    {
        ApplyFirstUseHintState(showHint: false);

        try
        {
            await _storageService.SetSettingAsync("QuickPanelHintDismissed", "true");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            App.Log($"First-use hint dismissal was not persisted: {ex.Message}");
        }
    }

    private async void OnFirstUseHintDismissClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnFirstUseHintDismissClicked), DismissFirstUseHintAsync);
    }

    /// <summary>
    /// Assigns <see cref="CustomColorPicker"/> without raising the user-intent path. Every
    /// programmatic colour write must go through here; only a real pointer or keyboard change
    /// is allowed to stage a colour.
    /// </summary>
    private void SetPickerColorSilently(global::Windows.UI.Color color)
    {
        if (CustomColorPicker is null)
        {
            return;
        }

        bool previous = _suppressColorPickerEvents;
        _suppressColorPickerEvents = true;
        try
        {
            CustomColorPicker.Color = color;
        }
        finally
        {
            _suppressColorPickerEvents = previous;
        }
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
        var emblem = _stagingModel.StagedTemplateState.Emblem;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(80, token);
                if (token.IsCancellationRequested) return;

                string hex = ColorConversion.ToHex(newColor.R, newColor.G, newColor.B);
                var previewResult = FolderTemplateRenderer.RenderPreviewPng(hex, templateId, templateVersion, emblem: emblem);
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

                    _stagingModel.StageFolderColor(hex);
                    ApplyConfirmButton.IsEnabled = _stagingModel.CanApply;
                    if (_stagingModel.CanApply)
                    {
                        StatusInfoText.Text = AppResources.GetString("Status_UnappliedChanges", _currentLanguage);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Expected: a newer color change superseded this preview. Not an error,
                // so it is not logged; the branch below records real failures.
            }
            catch (Exception ex)
            {
                App.Log($"Debounced color preview error: {ex.Message}");
            }
        }, token);
    }

    private async void OnApplyCustomColorClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnApplyCustomColorClicked), async () =>
        {
            var color = CustomColorPicker.Color;
            string hex = ColorConversion.ToHex(color.R, color.G, color.B);
            CustomColorFlyout.Hide();

            _stagingModel.StageFolderColor(hex);
            await RefreshLivePreviewAsync();
            _ = RenderTemplateCardThumbnailsAsync();
        });
    }

    private async void OnCancelCustomColorClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnCancelCustomColorClicked), async () =>
        {
            CustomColorFlyout.Hide();
            if (!string.IsNullOrWhiteSpace(_snapshotFolderColorHex))
            {
                _stagingModel.StageFolderColor(_snapshotFolderColorHex);
                await RefreshLivePreviewAsync();
                _ = RenderTemplateCardThumbnailsAsync();
            }
        });
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
        await GuardAsync(nameof(OnPlacementSelectionChanged), async () =>
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
        });
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
            catch (OperationCanceledException)
            {
                // Expected: a newer preview superseded this one. Not an error, so it is
                // not logged; the branch below records real failures.
            }
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
        // The apply path performs filesystem writes and journal transitions; a fault
        // escaping here would reach the dispatcher and kill the panel mid-commit.
        await GuardAsync(nameof(OnApplyConfirmClicked), async () =>
        {
            if (_isProcessing || !_stagingModel.CanApply)
            {
                return;
            }

            await ApplyStagedPersonalizationAsync();
        });
    }

    private async Task ApplyStagedPersonalizationAsync()
    {
        if (_isProcessing || !_stagingModel.CanApply)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentFolderPath))
        {
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Message(AppResources.GetString("ErrorNoFolderSelected", _currentLanguage)));
            return;
        }

        try
        {
            if (_stagingModel.ActiveMode == PersonalizationMode.FolderTemplate)
            {
                _stagedOperationId = Guid.NewGuid();
                _stagedTempIcoPath = Path.Combine(Path.GetTempPath(), $"ornare_staged_{_stagedOperationId:N}.ico");
                var templateState = _stagingModel.StagedTemplateState;

                SetProcessingStage(ProcessingStage.PreparingImage);
                SetProcessingStage(ProcessingStage.GeneratingIcon);

                var tempPath = _stagedTempIcoPath;

                var renderResult = await Task.Run(() =>
                {
                    return FolderTemplateRenderer.RenderColoredTemplateIco(
                        _imagePipeline,
                        templateState.FolderColorHex,
                        tempPath,
                        templateState.TemplateId,
                        templateState.TemplateVersion,
                        emblem: templateState.Emblem);
                });

                if (!renderResult.IsSuccess || renderResult.Value == null)
                {
                    CleanupStagedFiles();
                    ShowCategorizedError(QuickPanelErrorType.IconGenerationFailed, QuickPanelErrorCopy.Diagnostic(renderResult.DiagnosticDescription));
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
            ShowCategorizedError(QuickPanelErrorType.FolderApplyFailed, QuickPanelErrorCopy.Diagnostic(ex.Message));
        }
    }

    private async void OnCancelPreviewClicked(object sender, RoutedEventArgs e)
    {
        await GuardAsync(nameof(OnCancelPreviewClicked), async () =>
        {
            _stagingModel.Discard();
            CleanupStagedFiles();
            _stagedCustomColorHex = null;
            await RefreshLivePreviewAsync();
            SetProcessingStage(ProcessingStage.Idle);
        });
    }

    private void OnCloseSuccessClicked(object sender, RoutedEventArgs e)
    {
        Close();
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


    public void ApplyVisualTestState(string? uiState, string? theme, string? lang)
    {
        // Capture-only overrides: a shipped release must not be switched into a fake
        // processing/error state or a different theme/language by an activation.
        if (!App.VisualOverridesAllowed)
        {
            return;
        }

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
            SetProcessingStage(ProcessingStage.Idle);
            DropAreaBorder.BorderBrush = GetThemeBrush("AccentFillColorDefaultBrush", Microsoft.UI.Colors.DodgerBlue);
            DropAreaBorder.Background = GetThemeBrush("SubtleFillColorSecondaryBrush", Microsoft.UI.Colors.Transparent);
        }
        else if (string.Equals(uiState, "preparing", StringComparison.OrdinalIgnoreCase))
        {
            SetProcessingStage(ProcessingStage.PreparingImage);
        }
        else if (string.Equals(uiState, "generating", StringComparison.OrdinalIgnoreCase))
        {
            SetProcessingStage(ProcessingStage.GeneratingIcon);
        }
        else if (string.Equals(uiState, "preview", StringComparison.OrdinalIgnoreCase))
        {
            SetProcessingStage(ProcessingStage.PreviewReady);
        }
        else if (string.Equals(uiState, "applying", StringComparison.OrdinalIgnoreCase))
        {
            SetProcessingStage(ProcessingStage.ApplyingIcon);
        }
        else if (string.Equals(uiState, "refreshing", StringComparison.OrdinalIgnoreCase))
        {
            SetProcessingStage(ProcessingStage.RefreshingExplorer);
        }
        else if (string.Equals(uiState, "success", StringComparison.OrdinalIgnoreCase))
        {
            SetProcessingStage(ProcessingStage.Done);
            _autoCloseTimer?.Stop();
            ResetButton.Visibility = Visibility.Visible;
        }
        else if (string.Equals(uiState, "undo", StringComparison.OrdinalIgnoreCase))
        {
            SetProcessingStage(ProcessingStage.Done);
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
        // Route through the shared host so repeated clicks cannot stack multiple
        // managers over the same SQLite store.
        ManagerWindowHost.Show(_storageService, _folderCustomizer, _imagePipeline);
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        ManagerWindowHost.OpenSettings(_storageService, _folderCustomizer, _imagePipeline);
    }

    private async void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Content is FrameworkElement root && root.XamlRoot != null)
            {
                var dialog = new ContentDialog
                {
                    Title = AppResources.GetString("About_Title", _currentLanguage),
                    CloseButtonText = AppResources.GetString("Close", _currentLanguage),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = root.XamlRoot
                };

                var panel = new StackPanel { Spacing = 10, MaxWidth = 360 };
                panel.Children.Add(new TextBlock
                {
                    Text = $"{AppResources.GetString("ApplicationTitle", _currentLanguage)} v{AppInfo.Version}",
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                panel.Children.Add(new TextBlock
                {
                    Text = AppResources.GetString("About_Description", _currentLanguage),
                    FontSize = 12,
                    Foreground = GetThemeBrush("TextFillColorSecondaryBrush", Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = GetThemeBrush("CardStrokeColorDefaultBrush", Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(0, 4, 0, 4)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = AppResources.GetString("About_AcknowledgementsTitle", _currentLanguage),
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                panel.Children.Add(new TextBlock
                {
                    Text = AppResources.GetString("About_AcknowledgementsBody", _currentLanguage),
                    FontSize = 11,
                    Foreground = GetThemeBrush("TextFillColorSecondaryBrush", Microsoft.UI.Colors.Gray),
                    TextWrapping = TextWrapping.Wrap
                });

                dialog.Content = panel;
                await dialog.ShowAsync();
            }
            else
            {
                ManagerWindowHost.OpenSettings(_storageService, _folderCustomizer);
            }
        }
        catch (Exception ex)
        {
            // ShowAsync throws E_ILLEGAL_METHOD_CALL when another ContentDialog is
            // already open (e.g. double-click on "About"). Never let that crash the app.
            App.Log($"OnAboutClicked failed: {ex.Message}");
        }
    }
}
