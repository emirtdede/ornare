using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Emblems;
using Ornare.Imaging.Templates;
using Ornare.Storage;
using Ornare.Storage.Cloud;
using Ornare.Windows;
using WinRT.Interop;

namespace Ornare.App;

public partial class App : Application, IDisposable
{
    /// <summary>
    /// True only when capture-only visual overrides (forced UI state, theme and language)
    /// may be applied. They exist for reference-screenshot runs, so they are enabled in a
    /// Debug build or when the <c>ORNARE_VISUAL_TEST</c> environment variable is set to
    /// <c>1</c>; a shipped release must never let an activation switch its theme or fake a
    /// processing state.
    /// </summary>
    internal static bool VisualOverridesAllowed { get; } =
#if DEBUG
        true;
#else
        string.Equals(
            Environment.GetEnvironmentVariable("ORNARE_VISUAL_TEST"),
            "1",
            StringComparison.Ordinal);
#endif

    private QuickPanelWindow? _window;
    private Microsoft.UI.Dispatching.DispatcherQueue? _mainDispatcherQueue;
    private bool _isExiting;
    private ImagePipeline _imagePipeline = null!;
    private FolderCustomizer _folderCustomizer = null!;
    private SqliteStorageService _storageService = null!;
    private bool _disposed;
    private AppTrayIcon? _trayIcon;

    public App()
    {
        this.UnhandledException += (sender, e) =>
        {
            Log($"UNHANDLED APP EXCEPTION: {e.Message} | {e.Exception}");

            // Bounded safety net for the UI dispatcher. Transient environmental faults
            // (I/O, sharing violations, COM/picker failures) must not terminate the app
            // from an async event handler. Genuine programmer/fatal faults are allowed
            // to propagate so they stay visible during development and in crash reports.
            if (IsRecoverableDispatcherFault(e.Exception))
            {
                e.Handled = true;
                Log("Unhandled dispatcher exception classified as recoverable; process kept alive.");
            }
        };

        InitializeComponent();

        // Fire-and-forget work is used for startup data loading; an unobserved fault must
        // still be recorded instead of disappearing when the finalizer collects the task.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
            e.SetObserved();
        };

        // Heavy service initialization is deferred to OnLaunched after the
        // single-instance check. Transient redirect processes skip it entirely,
        // reducing warm activation latency from ~300 ms to < 200 ms.
    }

    /// <summary>
    /// Returns true for exception types that represent transient environmental
    /// failures rather than invariant violations. Deliberately excludes
    /// <see cref="OutOfMemoryException"/>, <see cref="StackOverflowException"/>,
    /// <see cref="AccessViolationException"/> and <see cref="NullReferenceException"/>
    /// so real defects still fail loudly.
    /// </summary>
    private static bool IsRecoverableDispatcherFault(Exception? exception)
    {
        // Only genuinely transient environmental faults are recovered. Programming
        // faults (InvalidOperationException, ArgumentException, NullReferenceException,
        // etc.) must stay visible: swallowing them would hide real defects.
        return exception switch
        {
            null => false,
            OperationCanceledException => true,
            ObjectDisposedException => true,
            IOException => true,
            UnauthorizedAccessException => true,
            System.Runtime.InteropServices.COMException => true,
            _ => false
        };
    }

    private void InitializeServices()
    {
        FolderEmblemCompositor.EmojiRasterizer = new DirectWriteEmojiRasterizer();
        _imagePipeline = new ImagePipeline();
        _folderCustomizer = new FolderCustomizer();
        _storageService = new SqliteStorageService();
    }

    internal static void Log(string msg)
    {
        // Local-first diagnostic sink: bounded in-memory ring buffer with path
        // redaction; verbose on-disk logging is opt-in only (see docs/LOGGING_DIAGNOSTICS.md).
        DiagnosticLog.Write(msg);
    }

    /// <summary>
    /// Trace-level breadcrumbs (startup steps, activation routing, window hand-offs). They are
    /// retained only during an explicit diagnostic session so the 256-entry ring buffer always
    /// has room for the failure that actually needs to be diagnosed.
    /// </summary>
    internal static void LogVerbose(string msg)
    {
        if (DiagnosticLog.PersistentSessionEnabled)
        {
            DiagnosticLog.Write(msg);
        }
    }

    /// <summary>
    /// Called by the last tracked window when it closes. The app owns services that must
    /// be disposed deterministically (SQLite handles, pending diagnostic writes), so the
    /// process exits explicitly once no Ornare window remains.
    /// </summary>
    internal static void NotifyWindowClosed()
    {
        if (Current is App app)
        {
            app.TryExitIfIdle();
        }
    }

    private void TryExitIfIdle()
    {
        if (_isExiting || _window is not null || ManagerWindowHost.IsOpen)
        {
            return;
        }

        // Application.Exit() closes every window, which raises Closed again. The flag
        // makes the re-entrant call a no-op instead of a second Dispose/Exit.
        _isExiting = true;
        Dispose();
        Exit();
    }

    /// <summary>
    /// Creates the tray icon once per process. It is created after the first window so the
    /// close-to-exit behaviour is unchanged: the icon lives exactly as long as the process.
    /// </summary>
    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        UserSettings settings;
        try
        {
            settings = _storageService.GetUserSettingsAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Log($"Tray icon skipped: {exception.Message}");
            return;
        }

        _trayIcon = AppTrayIcon.TryCreate(
            settings,
            OpenManagerFromTray,
            () =>
            {
                Log("Exit requested from the tray menu.");
                _isExiting = true;
                Dispose();
                Exit();
            });
    }

    private void OpenManagerFromTray()
    {
        try
        {
            if (ManagerWindowHost.IsOpen)
            {
                ManagerWindowHost.Current?.Activate();
                return;
            }

            ManagerWindowHost.Show(_storageService, _folderCustomizer, _imagePipeline, "Library");
            if (ManagerWindowHost.Current is { } manager)
            {
                BringToFront(manager);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException or IOException)
        {
            Log($"Tray open failed: {exception.Message}");
        }
    }

    /// <summary>Keeps the tray icon in step with the chosen app icon.</summary>
    internal static void NotifyAppIconChanged(UserSettings settings)
    {
        if (Current is App app)
        {
            app._trayIcon?.UpdateIcon(settings);
        }
    }

    /// <summary>
    /// Theme resolved for the process: the persisted choice at startup, or the latest choice
    /// made in a settings surface. Windows seed their initial theme from this so the first
    /// frame is already correct; <see cref="UserSettings.SystemValue"/> means "follow Windows".
    /// </summary>
    internal static string CurrentTheme { get; set; } = UserSettings.SystemValue;

    /// <summary>
    /// Publishes the language and theme the user chose in a previous session. Called once,
    /// after storage is ready and before any window exists, so localization and theming do not
    /// depend on a window's asynchronous load finishing before its first paint.
    /// </summary>
    private async Task ApplyPersistedPreferencesAsync()
    {
        try
        {
            UserSettings settings = await _storageService.GetUserSettingsAsync();
            AppResources.CurrentLocale = settings.Language;
            CurrentTheme = settings.Theme;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Preferences are cosmetic. A read failure must not stop startup; the window then
            // falls back to the system language and theme, and the failure is recorded.
            Log($"Persisted preferences could not be read: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static void BringToFront(Window window)
    {
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(window);
            LogVerbose($"BringToFront: HWND=0x{hwnd.ToInt64():X}");
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            Log($"Error in BringToFront: {ex}");
        }
    }

    private sealed class ParsedLaunchRequest
    {
        public string? TargetFolder { get; set; }
        public string? UiState { get; set; }
        public string? Theme { get; set; }
        public string? Lang { get; set; }
        public string? Section { get; set; }
        public int? CursorX { get; set; }
        public int? CursorY { get; set; }
        public bool IsManager { get; set; }
        public bool IsApplyLast { get; set; }
        public bool IsUndo { get; set; }
        public bool IsReset { get; set; }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        LogVerbose("OnLaunched entered");
        try
        {
            // 1. Single-instance check via AppInstance
            var mainInstance = AppInstance.FindOrRegisterForKey("OrnareSingleInstance");
            LogVerbose($"AppInstance registered, IsCurrent: {mainInstance.IsCurrent}");
            if (!mainInstance.IsCurrent)
            {
                // Redirect activation to the existing instance, then exit this transient
                // process. The exit is unconditional: if the redirect throws (for example the
                // primary instance is unresponsive) the transient process must still not linger
                // as a windowless ghost.
                try
                {
                    var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                    await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log($"Activation redirect failed: {ex}");
                }
                finally
                {
                    Dispose();
                    Environment.Exit(0);
                }

                return;
            }

            // Listen for future redirected activations
            mainInstance.Activated += (sender, eventArgs) =>
            {
                // IMPORTANT: Extract payload synchronously while remote process is alive in
                // RedirectActivationToAsync; the remote process is gone after this callback.
                ParsedLaunchRequest request = ExtractLaunchRequest(eventArgs);

                Microsoft.UI.Dispatching.DispatcherQueue? queue =
                    _mainDispatcherQueue ?? _window?.DispatcherQueue ?? ManagerWindowHost.Current?.DispatcherQueue;

                // DispatcherQueueHandler is a void delegate: passing an async lambda here would
                // produce an async void whose exceptions kill the process. Dispatch a plain
                // callback that owns its own error handling instead.
                bool dispatched = queue is not null && queue.TryEnqueue(() => RunRedirectedLaunch(request));
                if (!dispatched)
                {
                    // A silently lost activation means the user clicked "Customize folder" in
                    // Explorer and nothing happened. Say so and record it.
                    Log("Redirected activation could not be dispatched to the UI thread.");
                    ShowActivationFailureHint();
                }
            };

            // 2. Initialize heavy services now that we know this is the primary instance
            InitializeServices();

            // 3. Initialize storage and run idempotent startup reconciliation
            await _storageService.InitializeAsync();
            int reconciled = await _storageService.ReconcileStartupAsync();
            if (reconciled > 0)
            {
                // Reconciliation is silent on the happy path; only an actual repair is worth
                // a log line, so the log stays readable when nothing was pending.
                Log($"Startup reconciliation repaired {reconciled} pending journal entr{(reconciled == 1 ? "y" : "ies")}.");
            }

            // 4. Publish the persisted language and theme before any window exists, so the
            // first rendered frame is already localized and themed instead of flashing the
            // English/system defaults baked into XAML.
            await ApplyPersistedPreferencesAsync();

            // 5. Parse launch arguments and determine window mode
            var currentArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            var initialRequest = ExtractLaunchRequest(currentArgs);
            await ApplyLaunchRequestAsync(initialRequest);
            EnsureTrayIcon();
            LogVerbose("OnLaunched finished successfully.");
        }
        catch (Exception ex)
        {
            Log($"FATAL IN OnLaunched: {ex}");

            // Never leave the user with no window and no explanation. A startup failure
            // also must not be reported as a healthy running app.
            ShowFatalStartupError();
            Dispose();
            Environment.Exit(1);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;

    private static void ShowFatalStartupError()
    {
        try
        {
            string message = AppResources.GetString("ErrorStartupFailed", AppResources.CurrentLocale);
            if (MessageBoxW(IntPtr.Zero, message, "Ornare", MB_OK | MB_ICONERROR) == 0)
            {
                // MessageBoxW returns zero only when it fails (for example, no interactive
                // window station). Nothing further can be shown, so the process exit path
                // remains the last resort.
                System.Diagnostics.Trace.TraceWarning("Fatal startup MessageBoxW failed.");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A failed error UI must not mask the original startup failure.
        }
    }

    private static ParsedLaunchRequest ExtractLaunchRequest(AppActivationArguments args)
    {
        var request = new ParsedLaunchRequest();
        try
        {
            if (args.Kind == ExtendedActivationKind.Launch)
            {
                string[] cmdLineArgs = Environment.GetCommandLineArgs();
                for (int i = 0; i < cmdLineArgs.Length; i++)
                {
                    if (string.Equals(cmdLineArgs[i], "--manager", StringComparison.OrdinalIgnoreCase))
                    {
                        request.IsManager = true;
                    }
                    else if (string.Equals(cmdLineArgs[i], "--apply-last", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.IsApplyLast = true;
                        request.TargetFolder = cmdLineArgs[i + 1];
                    }
                    else if (string.Equals(cmdLineArgs[i], "--undo", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.IsUndo = true;
                        request.TargetFolder = cmdLineArgs[i + 1];
                    }
                    else if (string.Equals(cmdLineArgs[i], "--reset", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.IsReset = true;
                        request.TargetFolder = cmdLineArgs[i + 1];
                    }
                    else if (string.Equals(cmdLineArgs[i], "--set-folder", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.TargetFolder = cmdLineArgs[i + 1];
                    }
                    else if (string.Equals(cmdLineArgs[i], "--ui-state", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.UiState = cmdLineArgs[i + 1];
                    }
                    else if (string.Equals(cmdLineArgs[i], "--theme", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.Theme = cmdLineArgs[i + 1];
                    }
                    else if (string.Equals(cmdLineArgs[i], "--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.Lang = cmdLineArgs[i + 1];
                    }
                    else if (string.Equals(cmdLineArgs[i], "--section", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        request.Section = cmdLineArgs[i + 1];
                        request.IsManager = true;
                    }
                    else if (string.Equals(cmdLineArgs[i], "--x", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        if (int.TryParse(cmdLineArgs[i + 1], out int cx))
                        {
                            request.CursorX = cx;
                        }
                    }
                    else if (string.Equals(cmdLineArgs[i], "--y", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                    {
                        if (int.TryParse(cmdLineArgs[i + 1], out int cy))
                        {
                            request.CursorY = cy;
                        }
                    }
                }
            }
            else if (args.Kind == ExtendedActivationKind.Protocol)
            {
                Uri? protocolUri = null;
                if (args.Data is global::Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs concreteProtocol)
                {
                    protocolUri = concreteProtocol.Uri;
                }
                else if (args.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs ifaceProtocol)
                {
                    protocolUri = ifaceProtocol.Uri;
                }

                if (protocolUri is not null)
                {
                    // Parse the query properly instead of matching key by key with regex:
                    // regex parsing missed '&' inside encoded values, ignored '+' encoding
                    // and silently accepted malformed percent escapes.
                    Dictionary<string, string> query = ParseQueryString(protocolUri.Query);

                    if (query.TryGetValue("path", out string? path))
                    {
                        request.TargetFolder = path;
                    }
                    if (query.TryGetValue("x", out string? rawX) &&
                        int.TryParse(rawX, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int px))
                    {
                        request.CursorX = px;
                    }
                    if (query.TryGetValue("y", out string? rawY) &&
                        int.TryParse(rawY, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int py))
                    {
                        request.CursorY = py;
                    }
                    if (query.TryGetValue("ui-state", out string? uiState))
                    {
                        request.UiState = uiState;
                    }
                    if (query.TryGetValue("theme", out string? theme))
                    {
                        request.Theme = theme;
                    }
                    if (query.TryGetValue("lang", out string? lang))
                    {
                        request.Lang = lang;
                    }
                    if (query.TryGetValue("section", out string? section))
                    {
                        request.Section = section;
                        request.IsManager = true;
                    }

                    if (protocolUri.Scheme.Equals("ornare", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (protocolUri.Host.ToLowerInvariant())
                        {
                            case "manager":
                                request.IsManager = true;
                                break;
                            case "apply-last":
                                request.IsApplyLast = true;
                                break;
                            case "undo":
                                request.IsUndo = true;
                                break;
                            case "reset":
                                request.IsReset = true;
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error extracting launch request: {ex}");
        }
        return request;
    }

    /// <summary>
    /// Parses an RFC 3986 query string into a case-insensitive map. Malformed percent
    /// escapes are skipped rather than thrown, because the value arrives from an external
    /// protocol activation and must never crash the app.
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        string trimmed = query[0] == '?' ? query[1..] : query;

        foreach (string pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=');
            string rawKey = separator >= 0 ? pair[..separator] : pair;
            string rawValue = separator >= 0 ? pair[(separator + 1)..] : string.Empty;

            if (!TryDecodeQueryComponent(rawKey, out string key) ||
                !TryDecodeQueryComponent(rawValue, out string value))
            {
                continue;
            }

            if (key.Length > 0)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static bool TryDecodeQueryComponent(string raw, out string decoded)
    {
        try
        {
            decoded = Uri.UnescapeDataString(raw.Replace('+', ' '));
            return true;
        }
        catch (UriFormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Shows the four-step welcome walkthrough once per install. The settings it collects are
    /// persisted by the window itself, so a crash midway still keeps the user's choices.
    /// </summary>
    private async Task ShowFirstRunOnboardingIfNeededAsync()
    {
        try
        {
            UserSettings settings = await _storageService.GetUserSettingsAsync();
            if (settings.OnboardingCompleted)
            {
                return;
            }

            settings = InstallerLanguagePreference.Apply(settings);
            AppResources.CurrentLocale = settings.Language;
            AppearanceService.ApplyAccent(Current, settings.AccentColor);

            var completion = new TaskCompletionSource();
            var onboarding = new OnboardingWindow(_storageService, new CloudSyncService(), settings);
            onboarding.Closed += (_, _) => completion.TrySetResult();
            onboarding.Activate();
            await completion.Task;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Onboarding is a courtesy, never a gate: a failure must not stop the app.
            Log($"First-run onboarding skipped: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs a redirected activation on the UI thread. This is the single place that owns the
    /// failure contract for redirections, so a broken activation surfaces instead of vanishing.
    /// </summary>
    private async void RunRedirectedLaunch(ParsedLaunchRequest request)
    {
        try
        {
            await ApplyLaunchRequestAsync(request);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log($"Redirected launch failed: {ex}");
            ShowActivationFailureHint();
        }
    }

    private static void ShowActivationFailureHint()
    {
        try
        {
            string message = AppResources.GetString("ErrorStartupFailed", AppResources.CurrentLocale);
            if (MessageBoxW(IntPtr.Zero, message, "Ornare", MB_OK | MB_ICONERROR) == 0)
            {
                System.Diagnostics.Trace.TraceWarning("Activation failure MessageBoxW failed.");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Trace.TraceWarning($"Activation failure hint failed: {ex.Message}");
        }
    }

    private async Task ApplyLaunchRequestAsync(ParsedLaunchRequest request)
    {
        LogVerbose($"ApplyLaunchRequestAsync: isApplyLast={request.IsApplyLast}, isManager={request.IsManager}, targetFolder='{request.TargetFolder}', uiState='{request.UiState}', cursor=({request.CursorX},{request.CursorY})");
        if (request.IsApplyLast && !string.IsNullOrWhiteSpace(request.TargetFolder))
        {
            LogVerbose($"Executing ApplyLastAsync for '{request.TargetFolder}'...");
            var result = await ApplyLastService.ApplyLastAsync(
                request.TargetFolder,
                _storageService,
                _folderCustomizer,
                rebuildIcoFunc: async entry => await RebuildIcoFromRecipeAsync(entry));
            Log($"ApplyLast result: IsSuccess={result.IsSuccess}, ErrorCode={result.ErrorCode}, Message={result.ErrorMessage}");
            return;
        }

        if (request.IsUndo && !string.IsNullOrWhiteSpace(request.TargetFolder))
        {
            LogVerbose($"Executing UndoAsync for '{request.TargetFolder}'...");
            var result = await UndoService.UndoAsync(
                request.TargetFolder,
                _storageService,
                _folderCustomizer);
            Log($"Undo result: IsSuccess={result.IsSuccess}, ErrorCode={result.ErrorCode}, Message={result.ErrorMessage}");
            return;
        }

        if (request.IsReset && !string.IsNullOrWhiteSpace(request.TargetFolder))
        {
            LogVerbose($"Executing ResetAsync for '{request.TargetFolder}'...");
            var result = await ResetService.ResetAsync(
                request.TargetFolder,
                _storageService,
                _folderCustomizer);
            Log($"Reset result: IsSuccess={result.IsSuccess}, ErrorCode={result.ErrorCode}, Message={result.ErrorMessage}");
            return;
        }

        if (request.IsManager || (string.IsNullOrWhiteSpace(request.TargetFolder) && string.IsNullOrEmpty(request.UiState)))
        {
            await ShowFirstRunOnboardingIfNeededAsync();

            // Route through the single process-wide host. Creating a second manager here
            // produced two windows mutating the same SQLite store (INV-CON-003).
            ManagerWindowHost.Show(
                _storageService,
                _folderCustomizer,
                _imagePipeline,
                request.Section ?? "Library");

            if (ManagerWindowHost.Current is { } manager)
            {
                manager.ApplyVisualTestState(request.Section ?? "Library", request.Theme, request.Lang);
                BringToFront(manager);
            }

            LogVerbose("ManagerWindow brought to front.");
        }
        else
        {
            string target = request.TargetFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (_window == null)
            {
                LogVerbose($"Instantiating QuickPanelWindow for '{target}'...");
                _window = new QuickPanelWindow(_imagePipeline, _folderCustomizer, _storageService);
                _window.Closed += (_, _) =>
                {
                    LogVerbose("QuickPanelWindow Closed.");
                    _window = null;
                    NotifyWindowClosed();
                };
            }
            _window.PositionWindow(request.CursorX, request.CursorY);
            _window.Activate();
            BringToFront(_window);
            LogVerbose("QuickPanelWindow activated and brought to front.");

            LogVerbose($"Calling SetTargetFolderAsync on QuickPanelWindow for '{target}'...");
            await _window.SetTargetFolderAsync(target);
            _window.ApplyVisualTestState(request.UiState, request.Theme, request.Lang);
        }
    }

    private async Task<string?> RebuildIcoFromRecipeAsync(RenderCacheEntry entry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.RecipeJson))
            {
                string libraryIco = OrnareConstants.LibraryIcoPath(entry.SourceAssetHash);
                if (File.Exists(libraryIco))
                {
                    string? outDir = Path.GetDirectoryName(entry.CanonicalIcoPath);
                    if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                    File.Copy(libraryIco, entry.CanonicalIcoPath, overwrite: true);
                    return entry.CanonicalIcoPath;
                }
                return null;
            }

            var recipe = RenderRecipe.FromJson(entry.RecipeJson);
            if (recipe == null)
            {
                return null;
            }

            byte[]? sourceBytes = null;
            if (!string.IsNullOrEmpty(recipe.TemplateId))
            {
                var descriptor = FolderTemplateCatalog.GetTemplate(recipe.TemplateId, recipe.TemplateVersion);
                sourceBytes = descriptor.GetBytes();
            }
            else if (!string.IsNullOrEmpty(recipe.SourceAssetHash))
            {
                var asset = await _storageService.GetAssetByHashAsync(recipe.SourceAssetHash);
                if (asset != null && File.Exists(asset.CanonicalIcoPath))
                {
                    sourceBytes = await File.ReadAllBytesAsync(asset.CanonicalIcoPath);
                }
                else
                {
                    string libraryIco = OrnareConstants.LibraryIcoPath(recipe.SourceAssetHash);
                    if (File.Exists(libraryIco))
                    {
                        sourceBytes = await File.ReadAllBytesAsync(libraryIco);
                    }
                }
            }

            if (sourceBytes == null || sourceBytes.Length == 0)
            {
                return null;
            }

            string? dir = Path.GetDirectoryName(entry.CanonicalIcoPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // ProcessRecipe invokes the cache callbacks synchronously. This method is
            // entered from OnLaunched on the UI thread, so blocking those callbacks on
            // storage with GetAwaiter().GetResult() would deadlock the dispatcher.
            // Dispatch the render to a worker thread so the continuations never need
            // the UI thread.
            var renderResult = await Task.Run(() => _imagePipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                entry.CanonicalIcoPath,
                cacheLookup: key => _storageService.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                cacheStore: cacheEntry => _storageService.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult()));

            if (renderResult.IsSuccess && File.Exists(entry.CanonicalIcoPath))
            {
                return entry.CanonicalIcoPath;
            }
        }
        catch (Exception ex)
        {
            Log($"RebuildIcoFromRecipeAsync failed: {ex.Message}");
        }
        return null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _trayIcon?.Dispose();
            _trayIcon = null;
            DiagnosticLog.Flush();
            _storageService.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
