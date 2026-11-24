namespace Ornare.App;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

/// <summary>
/// Processing-state and error surfaces for the quick panel.
/// <para>
/// Every branch here maps a domain outcome to localized copy while keeping raw exception text
/// out of the visual tree (INV-UI-004). It was extracted from the window class so the rule that
/// decides what a user may see lives in one readable file, and so a reviewer can confirm at a
/// glance that no branch writes a diagnostic to a TextBlock.
/// </para>
/// </summary>
public sealed partial class QuickPanelWindow
{
    private void SetProcessingStage(ProcessingStage stage)
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

                // The success state is transient: the panel closes itself once the
                // pointer leaves, which StartAutoCloseTimer schedules.
                StartAutoCloseTimer();
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

        // Stage changes only toggle visibility on elements the panel already owns, so the
        // next layout pass happens naturally. A forced UpdateLayout() here cost a full
        // synchronous measure/arrange on every transition for no user-visible benefit.
        RestoreFocusAfterTransition(stage);
    }

    /// <summary>
    /// Service layers report failure text for diagnostics, not for end users. When they do
    /// supply a message it is logged; the user always sees the localized fallback copy.
    /// </summary>
    private void ShowServiceError(QuickPanelErrorType errorType, string? serviceErrorMessage, string fallbackKey)
    {
        // The user always sees the localized fallback copy. Service messages are
        // diagnostics only and are logged, never rendered: they can contain paths and
        // storage/schema detail (INV-UI-004).
        string localized = AppResources.GetString(fallbackKey, _currentLanguage);
        ShowCategorizedError(errorType, QuickPanelErrorCopy.Of(localized, serviceErrorMessage));
    }

    /// <summary>
    /// Caller-supplied error copy, split into the two channels that must never be confused.
    /// <para>
    /// The previous signature was <c>(type, string? localizedMessage, string? diagnosticDetail)</c>.
    /// Both parameters are strings, so <c>ShowCategorizedError(type, ex.Message)</c> compiled and
    /// rendered a raw exception message -- with paths and schema names -- to the user. Naming the
    /// channels removes the possibility: the diagnostic channel is only reachable through
    /// <see cref="Diagnostic"/>, which is logged and never drawn (INV-UI-004).
    /// </para>
    /// </summary>
    private readonly record struct QuickPanelErrorCopy(string? UserMessage, string? DiagnosticDetail)
    {
        /// <summary>Localized user copy resolved from <see cref="AppResources"/>.</summary>
        public static QuickPanelErrorCopy Message(string localizedMessage) => new(localizedMessage, null);

        /// <summary>Diagnostic text (exception or Win32 detail) that is logged, never shown.</summary>
        public static QuickPanelErrorCopy Diagnostic(string? diagnosticDetail) => new(null, diagnosticDetail);

        public static QuickPanelErrorCopy Of(string? localizedMessage, string? diagnosticDetail)
            => new(localizedMessage, diagnosticDetail);
    }

    /// <summary>
    /// Shows a category-specific, fully localized error.
    /// <paramref name="copy"/> carries the optional localized user message and the diagnostic
    /// detail separately; see <see cref="QuickPanelErrorCopy"/> for why they are not two strings.
    /// </summary>
    private void ShowCategorizedError(QuickPanelErrorType errorType, QuickPanelErrorCopy copy = default)
    {
        _isProcessing = false;
        _autoCloseTimer?.Stop();

        if (!string.IsNullOrWhiteSpace(copy.DiagnosticDetail))
        {
            App.Log($"QuickPanel error ({errorType}): {copy.DiagnosticDetail}");
        }

        string title;
        string message;

        switch (errorType)
        {
            case QuickPanelErrorType.UnsupportedImage:
                title = AppResources.GetString("ErrorUnsupportedImageTitle", _currentLanguage);
                message = copy.UserMessage ?? AppResources.GetString("ErrorUnsupportedImageMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.ImageReadFailed:
                title = AppResources.GetString("ErrorImageReadTitle", _currentLanguage);
                message = copy.UserMessage ?? AppResources.GetString("ErrorImageReadMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.IconGenerationFailed:
                title = AppResources.GetString("ErrorIconGenerationTitle", _currentLanguage);
                message = copy.UserMessage ?? AppResources.GetString("ErrorIconGenerationMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.FolderApplyFailed:
                title = AppResources.GetString("ErrorFolderApplyTitle", _currentLanguage);
                message = copy.UserMessage ?? AppResources.GetString("ErrorFolderApplyMessage", _currentLanguage);
                break;

            case QuickPanelErrorType.ExplorerRefreshFailed:
                title = AppResources.GetString("ErrorExplorerRefreshTitle", _currentLanguage);
                message = copy.UserMessage ?? AppResources.GetString("ErrorExplorerRefreshMessage", _currentLanguage);
                break;

            default:
                title = AppResources.GetString("ApplyFailed", _currentLanguage);
                message = copy.UserMessage ?? AppResources.GetString("ErrorUnexpected", _currentLanguage);
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
}