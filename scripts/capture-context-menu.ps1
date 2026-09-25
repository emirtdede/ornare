# Ornare — Real Explorer Context Menu Evidence Capture via PrintWindow
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class MenuCapture2 {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjSource, int nXSrc, int nYSrc, uint dwRop);
}
'@

$target = Join-Path (Resolve-Path ".").Path "OrnareGate1Final"
if (!(Test-Path $target)) {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

Start-Process "explorer.exe" -ArgumentList "/select,`"$target`""
Start-Sleep -Seconds 2

# Find explorer window
$procs = Get-Process -Name "explorer"
$hwnd = [IntPtr]::Zero
foreach ($p in $procs) {
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
        $hwnd = $p.MainWindowHandle
        break
    }
}

if ($hwnd -ne [IntPtr]::Zero) {
    [MenuCapture2]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 500

    $rect = New-Object MenuCapture2+RECT
    [MenuCapture2]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    if ($width -gt 100 -and $height -gt 100) {
        $bmp = New-Object System.Drawing.Bitmap $width, $height
        $gfx = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $gfx.GetHdc()
        [MenuCapture2]::PrintWindow($hwnd, $hdc, 2) | Out-Null
        $gfx.ReleaseHdc($hdc)
        $gfx.Dispose()

        $out = "production_artifacts/design/runtime-screenshots/explorer-context-menu.png"
        $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "Real Explorer context window captured to: $out ($width x $height)"
    }
} else {
    Write-Host "Explorer window handle not directly accessible."
}
