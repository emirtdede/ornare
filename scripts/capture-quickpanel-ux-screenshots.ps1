# Ornare — Quick Panel Polish Screenshots Capture Script
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class Win32CaptureUx {
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

    public static IntPtr FindQuickPanelHwnd() {
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
}
"@

$outputDir = "production_artifacts/design/runtime-screenshots"
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

$testFolder = "C:\Users\emir\Desktop\Ornare"

$scenarios = @(
    @{ Name = "quick-panel-idle-dark.png"; Theme = "dark"; UiState = "idle"; Lang = "en-US"; Mode = "quick-panel" },
    @{ Name = "quick-panel-idle-light.png"; Theme = "light"; UiState = "idle"; Lang = "en-US"; Mode = "quick-panel" },
    @{ Name = "quick-panel-idle-turkish.png"; Theme = "dark"; UiState = "idle"; Lang = "tr-TR"; Mode = "quick-panel" },
    @{ Name = "quick-panel-preparing-dark.png"; Theme = "dark"; UiState = "preparing"; Lang = "en-US"; Mode = "quick-panel" },
    @{ Name = "quick-panel-generating-turkish.png"; Theme = "dark"; UiState = "generating"; Lang = "tr-TR"; Mode = "quick-panel" },
    @{ Name = "quick-panel-applying-light.png"; Theme = "light"; UiState = "applying"; Lang = "en-US"; Mode = "quick-panel" },
    @{ Name = "quick-panel-refreshing-turkish.png"; Theme = "light"; UiState = "refreshing"; Lang = "tr-TR"; Mode = "quick-panel" },
    @{ Name = "quick-panel-success-dark.png"; Theme = "dark"; UiState = "success"; Lang = "en-US"; Mode = "quick-panel" },
    @{ Name = "quick-panel-success-turkish.png"; Theme = "light"; UiState = "success"; Lang = "tr-TR"; Mode = "quick-panel" },
    @{ Name = "quick-panel-error-unsupported.png"; Theme = "dark"; UiState = "error-unsupported"; Lang = "en-US"; Mode = "quick-panel" },
    @{ Name = "quick-panel-error-unsupported-turkish.png"; Theme = "dark"; UiState = "error-unsupported"; Lang = "tr-TR"; Mode = "quick-panel" },
    @{ Name = "quick-panel-error-apply-light.png"; Theme = "light"; UiState = "error-apply"; Lang = "en-US"; Mode = "quick-panel" },
    @{ Name = "manager-library-dark.png"; Theme = "dark"; Section = "Library"; Lang = "en-US"; Mode = "manager" },
    @{ Name = "manager-library-light.png"; Theme = "light"; Section = "Library"; Lang = "en-US"; Mode = "manager" },
    @{ Name = "manager-settings-dark.png"; Theme = "dark"; Section = "Settings"; Lang = "en-US"; Mode = "manager" },
    @{ Name = "manager-settings-turkish.png"; Theme = "dark"; Section = "Settings"; Lang = "tr-TR"; Mode = "manager" },
    @{ Name = "manager-favorites-turkish.png"; Theme = "dark"; Section = "Favorites"; Lang = "tr-TR"; Mode = "manager" },
    @{ Name = "manager-library-turkish.png"; Theme = "dark"; Section = "Library"; Lang = "tr-TR"; Mode = "manager" }
)

Write-Host "Capturing $($scenarios.Count) UI states via MSIX protocol activation..."

foreach ($s in $scenarios) {
    # Ensure any running Ornare process is killed first
    Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 250

    $protocolUri = if ($s.Mode -eq "manager") {
        "ornare://manager?section=$($s.Section)&theme=$($s.Theme)&lang=$($s.Lang)"
    } else {
        "ornare://set-folder?v=1&path=$([Uri]::EscapeDataString($testFolder))&theme=$($s.Theme)&ui-state=$($s.UiState)&lang=$($s.Lang)"
    }
    Start-Process $protocolUri

    $hWnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 50; $i++) {
        Start-Sleep -Milliseconds 100
        $hWnd = [Win32CaptureUx]::FindQuickPanelHwnd()
        if ($hWnd -ne [IntPtr]::Zero) {
            break
        }
    }

    if ($hWnd -eq [IntPtr]::Zero) {
        Write-Warning "Could not find window for $($s.Name)"
        Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        continue
    }

    [Win32CaptureUx]::SetForegroundWindow($hWnd)
    Start-Sleep -Milliseconds 600

    $rect = New-Object Win32CaptureUx+RECT
    [Win32CaptureUx]::GetWindowRect($hWnd, [ref]$rect)
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top

    if ($w -gt 0 -and $h -gt 0) {
        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc()
        [Win32CaptureUx]::PrintWindow($hWnd, $hdc, 2)
        $g.ReleaseHdc($hdc)
        $g.Dispose()

        $filePath = Join-Path $outputDir $s.Name
        $bmp.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "  -> Captured: $($s.Name) (${w}x${h})"
    }

    Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 200
}

Write-Host "Quick Panel UI capture completed successfully."
