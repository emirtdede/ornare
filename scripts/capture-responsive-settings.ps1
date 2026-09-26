# Capture responsive screenshots of Manager Settings
Add-Type @"
using System;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class Win32Snap {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left, Top, Right, Bottom;
    }
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static IntPtr FindManagerHwnd() {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            try {
                var p = System.Diagnostics.Process.GetProcessById((int)pid);
                if (p.ProcessName.Equals("Ornare.App", StringComparison.OrdinalIgnoreCase) && IsWindowVisible(hWnd)) {
                    StringBuilder cls = new StringBuilder(256);
                    GetClassName(hWnd, cls, 256);
                    if (cls.ToString().IndexOf("WinUIDesktopWin32WindowClass", StringComparison.OrdinalIgnoreCase) >= 0) {
                        found = hWnd;
                        return false;
                    }
                }
            } catch {}
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Snap(IntPtr hWnd, string savePath) {
        RECT rect;
        GetWindowRect(hWnd, out rect);
        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;
        if (w <= 0 || h <= 0) return;

        using (Bitmap bmp = new Bitmap(w, h)) {
            using (Graphics g = Graphics.FromImage(bmp)) {
                IntPtr hdc = g.GetHdc();
                PrintWindow(hWnd, hdc, 2);
                g.ReleaseHdc(hdc);
            }
            bmp.Save(savePath, ImageFormat.Png);
        }
    }
}
"@ -ReferencedAssemblies System.Drawing

$outputDir = "production_artifacts/design/runtime-screenshots"
if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Force -Path $outputDir | Out-Null }

Write-Host "Killing any running Ornare process..."
Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

Write-Host "Launching Manager Settings in Turkish..."
Start-Process "ornare://manager?section=Settings&theme=dark&lang=tr-TR"

$hWnd = [IntPtr]::Zero
for ($i = 0; $i -lt 50; $i++) {
    Start-Sleep -Milliseconds 100
    $hWnd = [Win32Snap]::FindManagerHwnd()
    if ($hWnd -ne [IntPtr]::Zero) { break }
}

if ($hWnd -eq [IntPtr]::Zero) { throw "Window not found!" }
[Win32Snap]::SetForegroundWindow($hWnd) | Out-Null
Start-Sleep -Milliseconds 800

# 1. Capture Normal window state (920x640)
$normalSnap = Join-Path $outputDir "manager-settings-responsive-normal-tr.png"
[Win32Snap]::Snap($hWnd, $normalSnap)
Write-Host "Saved normal state snapshot to $normalSnap"

# 2. Maximize and capture Wide window state
Write-Host "Maximizing window..."
[Win32Snap]::ShowWindow($hWnd, 3) | Out-Null
Start-Sleep -Milliseconds 900

$maxSnap = Join-Path $outputDir "manager-settings-responsive-maximized-tr.png"
[Win32Snap]::Snap($hWnd, $maxSnap)
Write-Host "Saved maximized state snapshot to $maxSnap"

# 3. Restore and resize to compact (700x640)
Write-Host "Restoring and resizing to compact (700x640)..."
[Win32Snap]::ShowWindow($hWnd, 9) | Out-Null
Start-Sleep -Milliseconds 600
[Win32Snap]::SetWindowPos($hWnd, [IntPtr]::Zero, 120, 120, 700, 640, 0x0040) | Out-Null
Start-Sleep -Milliseconds 600

$compactSnap = Join-Path $outputDir "manager-settings-responsive-compact-tr.png"
[Win32Snap]::Snap($hWnd, $compactSnap)
Write-Host "Saved compact state snapshot to $compactSnap"

Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "Completed capturing responsive snapshots."
