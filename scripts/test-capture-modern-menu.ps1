Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class ModernMenuCapture {
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
    public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
}
'@

$baseDir = (Resolve-Path ".").Path
$targetFolder = Join-Path $baseDir "OrnareGate1Final"

# Launch Explorer without killing explorer process
Start-Process "explorer.exe" -ArgumentList "/select,`"$targetFolder`""
Start-Sleep -Seconds 3

# Find the explorer window
$procs = Get-Process -Name "explorer"
$targetHwnd = [IntPtr]::Zero
foreach ($p in $procs) {
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
        $targetHwnd = $p.MainWindowHandle
        break
    }
}

if ($targetHwnd -eq [IntPtr]::Zero) {
    Write-Error "Explorer window not found."
    exit 1
}

Write-Host "Explorer MainWindowHandle: $targetHwnd"
[ModernMenuCapture]::SetForegroundWindow($targetHwnd)
Start-Sleep -Milliseconds 500

$root = [System.Windows.Automation.AutomationElement]::FromHandle($targetHwnd)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "OrnareGate1Final")
$item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)

if ($null -ne $item) {
    $rect = $item.Current.BoundingRectangle
    $cx = [int]($rect.Left + $rect.Width / 2)
    $cy = [int]($rect.Top + $rect.Height / 2)
    Write-Host "Target located at: $cx, $cy"

    [ModernMenuCapture]::SetCursorPos($cx, $cy)
    Start-Sleep -Milliseconds 200

    # Genuine mouse right click:
    [ModernMenuCapture]::mouse_event([ModernMenuCapture]::MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [ModernMenuCapture]::mouse_event([ModernMenuCapture]::MOUSEEVENTF_RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
    Write-Host "Mouse right click executed."

    # Wait for modern menu to animate and render
    Start-Sleep -Seconds 2

    $wRect = New-Object ModernMenuCapture+RECT
    [ModernMenuCapture]::GetWindowRect($targetHwnd, [ref]$wRect) | Out-Null
    $width = $wRect.Right - $wRect.Left
    $height = $wRect.Bottom - $wRect.Top

    if ($width -gt 100 -and $height -gt 100) {
        $bmp = New-Object System.Drawing.Bitmap $width, $height
        $gfx = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $gfx.GetHdc()
        [ModernMenuCapture]::PrintWindow($targetHwnd, $hdc, 2) | Out-Null
        $gfx.ReleaseHdc($hdc)
        $gfx.Dispose()

        $outPath = "production_artifacts/design/runtime-screenshots/explorer-modern-context-menu.png"
        $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "Saved real Explorer window with modern context menu to: $outPath ($width x $height)"
    }
}
