using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;

namespace Ornare.App;

public partial class App : Application, IDisposable
{
    private QuickPanelWindow? _window;
    private readonly ImagePipeline _imagePipeline;
    private readonly FolderCustomizer _folderCustomizer;
    private readonly SqliteStorageService _storageService;
    private bool _disposed;

    public App()
    {
        InitializeComponent();

        _imagePipeline = new ImagePipeline();
        _folderCustomizer = new FolderCustomizer();
        _storageService = new SqliteStorageService();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 1. Single-instance check via AppInstance
        var mainInstance = AppInstance.FindOrRegisterForKey("OrnareSingleInstance");
        if (!mainInstance.IsCurrent)
        {
            // Redirect activation to the existing instance and exit
            var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            await mainInstance.RedirectActivationToAsync(activatedEventArgs);
            Process.GetCurrentProcess().Kill();
            return;
        }

        // Listen for future redirected activations
        mainInstance.Activated += (sender, eventArgs) =>
        {
            _window?.DispatcherQueue.TryEnqueue(async () =>
            {
                await HandleActivationArgsAsync(eventArgs);
                _window?.Activate();
            });
        };

        // 2. Initialize storage and run idempotent startup reconciliation
        await _storageService.InitializeAsync();
        await _storageService.ReconcileStartupAsync();

        // 3. Parse launch arguments and determine window mode
        var currentArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        await HandleActivationArgsAsync(currentArgs);
    }

    private ManagerWindow? _managerWindow;

    private async Task HandleActivationArgsAsync(AppActivationArguments args)
    {
        string? targetFolder = null;
        string? uiState = null;
        string? theme = null;
        string? lang = null;
        string? section = null;
        bool isManager = false;

        if (args.Kind == ExtendedActivationKind.Launch)
        {
            string[] cmdLineArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < cmdLineArgs.Length; i++)
            {
                if (string.Equals(cmdLineArgs[i], "--manager", StringComparison.OrdinalIgnoreCase))
                {
                    isManager = true;
                }
                else if (string.Equals(cmdLineArgs[i], "--set-folder", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                {
                    targetFolder = cmdLineArgs[i + 1];
                }
                else if (string.Equals(cmdLineArgs[i], "--ui-state", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                {
                    uiState = cmdLineArgs[i + 1];
                }
                else if (string.Equals(cmdLineArgs[i], "--theme", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                {
                    theme = cmdLineArgs[i + 1];
                }
                else if (string.Equals(cmdLineArgs[i], "--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                {
                    lang = cmdLineArgs[i + 1];
                }
                else if (string.Equals(cmdLineArgs[i], "--section", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdLineArgs.Length)
                {
                    section = cmdLineArgs[i + 1];
                    isManager = true;
                }
            }
        }
        else if (args.Kind == ExtendedActivationKind.Protocol && args.Data is global::Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
        {
            // Parse protocol: ornare://set-folder?v=1&path=C:\...
            string uriString = protocolArgs.Uri.ToString();
            int pathIndex = uriString.IndexOf("path=", StringComparison.OrdinalIgnoreCase);
            if (pathIndex > 0)
            {
                targetFolder = Uri.UnescapeDataString(uriString[(pathIndex + 5)..]);
            }
        }

        if (isManager || (string.IsNullOrWhiteSpace(targetFolder) && string.IsNullOrEmpty(uiState)))
        {
            _managerWindow ??= new ManagerWindow(_storageService, _folderCustomizer);
            _managerWindow.ApplyVisualTestState(section ?? "Library", theme, lang);
            _managerWindow.Activate();
        }
        else
        {
            targetFolder ??= Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            _window ??= new QuickPanelWindow(_imagePipeline, _folderCustomizer, _storageService);
            await _window.SetTargetFolderAsync(targetFolder);
            _window.ApplyVisualTestState(uiState, theme, lang);
            _window.Activate();
        }
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
