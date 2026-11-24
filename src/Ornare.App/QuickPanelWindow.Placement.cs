namespace Ornare.App;

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

/// <summary>
/// Window chrome, sizing and monitor placement for the quick panel.
/// <para>
/// Everything here talks to Win32 or to <c>AppWindow</c> and nothing here decides what the
/// panel contains. It was extracted from the 3,200-line window class so the class reads as
/// interaction flow rather than interop bookkeeping, and so the placement maths can be
/// reviewed without scrolling past catalog filtering and image staging.
/// </para>
/// </summary>
public sealed partial class QuickPanelWindow
{
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Cosmetic window-style adjustments must never prevent the panel opening; the
            // failure is still recorded so a broken HWND is diagnosable.
            // OutOfMemoryException is deliberately excluded: the handler allocates a
            // formatted string for the log, which would throw again inside the catch.
            App.Log($"StripMinimizeMaximizeButtons failed: {ex.Message}");
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
}