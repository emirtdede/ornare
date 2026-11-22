using System.Runtime.InteropServices;

namespace Ornare.Windows;

/// <summary>
/// The notification-area presence of a running Ornare process. It exists so the icon the user
/// picked is honoured in the tray as well as in the window and the taskbar, and so the manager
/// can be reopened with a single click while the process is alive.
/// <para>
/// The icon is created on the calling (UI) thread and uses a message-only window, so it depends
/// on the thread's existing message pump and never starts one of its own. It disappears when the
/// process exits, which keeps Ornare from becoming a background agent.
/// </para>
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int WM_DESTROY = 0x0002;
    private const int WM_APP = 0x8000;
    private const int CallbackMessage = WM_APP + 1;
    private const int MenuOpen = 1;
    private const int MenuExit = 2;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;

    private static readonly nint HwndMessage = new(-3);

    private readonly string _className;
    private readonly WindowProcedure _procedure;
    private readonly uint _taskbarCreatedMessage;
    private nint _window;
    private nint _icon;
    private bool _added;
    private bool _disposed;

    /// <summary>
    /// Creates the tray icon. <paramref name="labels"/> supplies the localized menu text at the
    /// moment the menu opens, so a language change is reflected without recreating the icon.
    /// </summary>
    public TrayIcon(string iconPath, string tooltip, Func<TrayMenuLabels> labels, Action openRequested, Action exitRequested)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(openRequested);
        ArgumentNullException.ThrowIfNull(exitRequested);

        Labels = labels;
        OpenRequested = openRequested;
        ExitRequested = exitRequested;
        Tooltip = string.IsNullOrWhiteSpace(tooltip) ? "Ornare" : tooltip;

        _className = "OrnareTrayWindow_" + Guid.NewGuid().ToString("N");
        _procedure = WindowMessageHandler;
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

        RegisterWindowClass();
        _window = CreateWindowExW(
            0,
            _className,
            "OrnareTray",
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            0,
            0,
            0);

        if (_window == 0)
        {
            return;
        }

        UpdateIcon(iconPath);
    }

    /// <summary>Supplies the localized menu labels.</summary>
    public Func<TrayMenuLabels> Labels { get; }

    /// <summary>Raised when the user asks for the manager window.</summary>
    public Action OpenRequested { get; }

    /// <summary>Raised when the user asks the application to exit.</summary>
    public Action ExitRequested { get; }

    /// <summary>Tooltip shown on hover.</summary>
    public string Tooltip { get; private set; }

    /// <summary>True when the shell accepted the icon; false when the tray was unavailable.</summary>
    public bool IsVisible => _added;

    /// <summary>
    /// Points the tray at another icon file. The file name changes with the variant and ink
    /// colour, so the shell cannot serve a stale cached image.
    /// </summary>
    public void UpdateIcon(string iconPath)
    {
        if (_disposed || _window == 0 || string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return;
        }

        nint loaded = LoadImageW(
            0,
            iconPath,
            IMAGE_ICON,
            GetSystemMetrics(SM_CXSMICON),
            GetSystemMetrics(SM_CYSMICON),
            LR_LOADFROMFILE);

        if (loaded == 0)
        {
            return;
        }

        if (_icon != 0)
        {
            DestroyIconW(_icon);
        }

        _icon = loaded;
        AddOrModify(_added ? NIM_MODIFY : NIM_ADD);
    }

    /// <summary>Updates the tooltip, for example after a language change.</summary>
    public void UpdateTooltip(string tooltip)
    {
        if (string.IsNullOrWhiteSpace(tooltip))
        {
            return;
        }

        Tooltip = tooltip;
        if (_added)
        {
            AddOrModify(NIM_MODIFY);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_added && _window != 0)
        {
            var data = CreateData();
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _added = false;
        }

        if (_icon != 0)
        {
            DestroyIconW(_icon);
            _icon = 0;
        }

        if (_window != 0)
        {
            DestroyWindow(_window);
            _window = 0;
        }

        UnregisterClassW(_className, 0);
    }

    private void AddOrModify(int message)
    {
        var data = CreateData();
        _added = Shell_NotifyIconW(message, ref data);
    }

    private NotifyIconData CreateData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        Window = _window,
        Id = 1,
        Flags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        CallbackMessage = (uint)CallbackMessage,
        Icon = _icon,
        Tip = Tooltip
    };

    private void RegisterWindowClass()
    {
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Style = 0,
            Procedure = _procedure,
            Instance = GetModuleHandleW(null),
            ClassName = _className
        };

        RegisterClassExW(ref windowClass);
    }

    private nint WindowMessageHandler(nint hWnd, uint message, nint wParam, nint lParam)
    {
        if (message == CallbackMessage)
        {
            switch ((int)lParam)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    OpenRequested();
                    return 0;
                case WM_CONTEXTMENU:
                case WM_RBUTTONUP:
                    ShowMenu();
                    return 0;
            }
        }
        else if (message == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            // Explorer restarted: the shell forgot every icon, so register ours again.
            _added = false;
            if (_icon != 0)
            {
                AddOrModify(NIM_ADD);
            }

            return 0;
        }
        else if (message == WM_DESTROY)
        {
            return 0;
        }

        return DefWindowProcW(hWnd, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        nint menu = CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }

        TrayMenuLabels labels = Labels();
        try
        {
            AppendMenuW(menu, MF_STRING, MenuOpen, labels.Open);
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, MenuExit, labels.Exit);

            GetCursorPos(out NativePoint point);

            // The owning window must be foreground, otherwise the menu stays open when the
            // user clicks elsewhere. Posting WM_NULL afterwards is the documented workaround.
            SetForegroundWindow(_window);
            int command = (int)TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.X, point.Y, 0, _window, 0);
            PostMessageW(_window, 0, 0, 0);

            if (command == MenuOpen)
            {
                OpenRequested();
            }
            else if (command == MenuExit)
            {
                ExitRequested();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid Item;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WindowProcedure Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private delegate nint WindowProcedure(nint hWnd, uint message, nint wParam, nint lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(int message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string className, nint instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(nint hWnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessageW(string message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenuW(nint menu, uint flags, nint id, string? item);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hWnd, nint rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hWnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImageW(nint instance, string name, uint type, int width, int height, uint flags);

    // DestroyIcon is exported without a W suffix; an entry point named DestroyIconW does not exist.
    [DllImport("user32.dll", EntryPoint = "DestroyIcon")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIconW(nint icon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}

/// <summary>Localized tray menu text.</summary>
public readonly record struct TrayMenuLabels(string Open, string Exit);
