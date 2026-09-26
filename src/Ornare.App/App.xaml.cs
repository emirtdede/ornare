using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Imaging.Templates;
using Ornare.Storage;
using Ornare.Windows;
using WinRT.Interop;

namespace Ornare.App;

public partial class App : Application, IDisposable
{
    private QuickPanelWindow? _window;
    private ManagerWindow? _managerWindow;
    private Microsoft.UI.Dispatching.DispatcherQueue? _mainDispatcherQueue;
    private readonly ImagePipeline _imagePipeline;
    private readonly FolderCustomizer _folderCustomizer;
    private readonly SqliteStorageService _storageService;
    private bool _disposed;

    public App()
    {
        this.UnhandledException += (sender, e) =>
        {
            Log($"UNHANDLED APP EXCEPTION: {e.Message} | {e.Exception}");
        };

        InitializeComponent();

        _imagePipeline = new ImagePipeline();
        _folderCustomizer = new FolderCustomizer();
        _storageService = new SqliteStorageService();
    }

    internal static void Log(string msg)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ornare");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "activation_debug.log"), $"[{DateTime.UtcNow:O}] [PID:{Environment.ProcessId}] {msg}\n");
        }
        catch { }
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
            Log($"BringToFront: HWND=0x{hwnd.ToInt64():X}");
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
        Log("OnLaunched entered");
        try
        {
            // 1. Single-instance check via AppInstance
            var mainInstance = AppInstance.FindOrRegisterForKey("OrnareSingleInstance");
            Log($"AppInstance registered, IsCurrent: {mainInstance.IsCurrent}");
            if (!mainInstance.IsCurrent)
            {
                // Redirect activation to the existing instance and exit
                var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                Log($"Redirecting activation to existing instance... Kind: {activatedEventArgs.Kind}");
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                Log("Redirect complete. Exiting transient process.");
                Environment.Exit(0);
                return;
            }

            // Listen for future redirected activations
            mainInstance.Activated += (sender, eventArgs) =>
            {
                Log($"mainInstance.Activated fired. Kind: {eventArgs.Kind}");
                // IMPORTANT: Extract payload synchronously while remote process is alive in RedirectActivationToAsync!
                var request = ExtractLaunchRequest(eventArgs);
                Log($"Synchronously extracted request: targetFolder='{request.TargetFolder}', isManager={request.IsManager}, cursor=({request.CursorX},{request.CursorY})");

                var queue = _mainDispatcherQueue ?? _window?.DispatcherQueue ?? _managerWindow?.DispatcherQueue;
                queue?.TryEnqueue(async () =>
                {
                    Log("UI thread executing redirected launch request...");
                    await ApplyLaunchRequestAsync(request);
                });
            };

            // 2. Initialize storage and run idempotent startup reconciliation
            Log("Initializing SQLite storage...");
            await _storageService.InitializeAsync();
            Log("Storage initialized. Reconciling startup...");
            await _storageService.ReconcileStartupAsync();
            Log("Startup reconciliation complete.");

            // 3. Parse launch arguments and determine window mode
            var currentArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            Log($"Handling current activation args. Kind: {currentArgs.Kind}");
            var initialRequest = ExtractLaunchRequest(currentArgs);
            await ApplyLaunchRequestAsync(initialRequest);
            Log("OnLaunched finished successfully.");
        }
        catch (Exception ex)
        {
            Log($"FATAL IN OnLaunched: {ex}");
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
                string? uriString = null;
                if (args.Data is global::Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs concreteProtocol)
                {
                    uriString = concreteProtocol.Uri?.ToString();
                }
                else if (args.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs ifaceProtocol)
                {
                    uriString = ifaceProtocol.Uri?.ToString();
                }

                if (!string.IsNullOrEmpty(uriString))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(uriString, @"[?&]path=([^&]+)");
                    if (match.Success)
                    {
                        request.TargetFolder = Uri.UnescapeDataString(match.Groups[1].Value);
                    }
                    var xMatch = System.Text.RegularExpressions.Regex.Match(uriString, @"[?&]x=(-?\d+)");
                    if (xMatch.Success && int.TryParse(xMatch.Groups[1].Value, out int px))
                    {
                        request.CursorX = px;
                    }
                    var yMatch = System.Text.RegularExpressions.Regex.Match(uriString, @"[?&]y=(-?\d+)");
                    if (yMatch.Success && int.TryParse(yMatch.Groups[1].Value, out int py))
                    {
                        request.CursorY = py;
                    }
                    var uiMatch = System.Text.RegularExpressions.Regex.Match(uriString, @"[?&]ui-state=([^&]+)");
                    if (uiMatch.Success)
                    {
                        request.UiState = Uri.UnescapeDataString(uiMatch.Groups[1].Value);
                    }
                    var themeMatch = System.Text.RegularExpressions.Regex.Match(uriString, @"[?&]theme=([^&]+)");
                    if (themeMatch.Success)
                    {
                        request.Theme = Uri.UnescapeDataString(themeMatch.Groups[1].Value);
                    }
                    var langMatch = System.Text.RegularExpressions.Regex.Match(uriString, @"[?&]lang=([^&]+)");
                    if (langMatch.Success)
                    {
                        request.Lang = Uri.UnescapeDataString(langMatch.Groups[1].Value);
                    }
                    var secMatch = System.Text.RegularExpressions.Regex.Match(uriString, @"[?&]section=([^&]+)");
                    if (secMatch.Success)
                    {
                        request.Section = Uri.UnescapeDataString(secMatch.Groups[1].Value);
                        request.IsManager = true;
                    }
                    if (uriString.StartsWith("ornare://manager", StringComparison.OrdinalIgnoreCase))
                    {
                        request.IsManager = true;
                    }
                    else if (uriString.StartsWith("ornare://apply-last", StringComparison.OrdinalIgnoreCase))
                    {
                        request.IsApplyLast = true;
                    }
                    else if (uriString.StartsWith("ornare://undo", StringComparison.OrdinalIgnoreCase))
                    {
                        request.IsUndo = true;
                    }
                    else if (uriString.StartsWith("ornare://reset", StringComparison.OrdinalIgnoreCase))
                    {
                        request.IsReset = true;
                    }

                    if (string.IsNullOrWhiteSpace(request.TargetFolder))
                    {
                        int pathIndex = uriString.IndexOf("path=", StringComparison.OrdinalIgnoreCase);
                        if (pathIndex >= 0)
                        {
                            string sub = uriString[(pathIndex + 5)..];
                            int ampersand = sub.IndexOf('&');
                            if (ampersand >= 0) sub = sub[..ampersand];
                            request.TargetFolder = Uri.UnescapeDataString(sub);
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

    private async Task ApplyLaunchRequestAsync(ParsedLaunchRequest request)
    {
        Log($"ApplyLaunchRequestAsync: isApplyLast={request.IsApplyLast}, isManager={request.IsManager}, targetFolder='{request.TargetFolder}', uiState='{request.UiState}', cursor=({request.CursorX},{request.CursorY})");
        if (request.IsApplyLast && !string.IsNullOrWhiteSpace(request.TargetFolder))
        {
            Log($"Executing ApplyLastAsync for '{request.TargetFolder}'...");
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
            Log($"Executing UndoAsync for '{request.TargetFolder}'...");
            var result = await UndoService.UndoAsync(
                request.TargetFolder,
                _storageService,
                _folderCustomizer);
            Log($"Undo result: IsSuccess={result.IsSuccess}, ErrorCode={result.ErrorCode}, Message={result.ErrorMessage}");
            return;
        }

        if (request.IsReset && !string.IsNullOrWhiteSpace(request.TargetFolder))
        {
            Log($"Executing ResetAsync for '{request.TargetFolder}'...");
            var result = await ResetService.ResetAsync(
                request.TargetFolder,
                _storageService,
                _folderCustomizer);
            Log($"Reset result: IsSuccess={result.IsSuccess}, ErrorCode={result.ErrorCode}, Message={result.ErrorMessage}");
            return;
        }

        if (request.IsManager || (string.IsNullOrWhiteSpace(request.TargetFolder) && string.IsNullOrEmpty(request.UiState)))
        {
            if (_managerWindow == null)
            {
                Log("Instantiating ManagerWindow...");
                _managerWindow = new ManagerWindow(_storageService, _folderCustomizer);
                _managerWindow.Closed += (_, _) =>
                {
                    Log("ManagerWindow Closed.");
                    _managerWindow = null;
                };
            }
            _managerWindow.ApplyVisualTestState(request.Section ?? "Library", request.Theme, request.Lang);
            _managerWindow.Activate();
            BringToFront(_managerWindow);
            Log("ManagerWindow brought to front.");
        }
        else
        {
            string target = request.TargetFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (_window == null)
            {
                Log($"Instantiating QuickPanelWindow for '{target}'...");
                _window = new QuickPanelWindow(_imagePipeline, _folderCustomizer, _storageService);
                _window.Closed += (_, _) =>
                {
                    Log("QuickPanelWindow Closed.");
                    _window = null;
                };
            }
            Log($"Calling SetTargetFolderAsync on QuickPanelWindow for '{target}'...");
            await _window.SetTargetFolderAsync(target);
            _window.PositionWindow(request.CursorX, request.CursorY);
            _window.ApplyVisualTestState(request.UiState, request.Theme, request.Lang);
            _window.Activate();
            BringToFront(_window);
            Log("QuickPanelWindow brought to front.");
        }
    }

    private async Task<string?> RebuildIcoFromRecipeAsync(RenderCacheEntry entry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.RecipeJson))
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string libraryIco = Path.Combine(localAppData, OrnareConstants.ApplicationName, "library", $"{entry.SourceAssetHash}.ico");
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
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string libraryIco = Path.Combine(localAppData, OrnareConstants.ApplicationName, "library", $"{recipe.SourceAssetHash}.ico");
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

            var renderResult = _imagePipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                entry.CanonicalIcoPath,
                cacheLookup: key => _storageService.GetRenderCacheEntryAsync(key).GetAwaiter().GetResult(),
                cacheStore: cacheEntry => _storageService.SaveRenderCacheEntryAsync(cacheEntry).GetAwaiter().GetResult());

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
            _storageService.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
