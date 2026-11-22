# Ornare - Real Application Runtime Screenshot Capture
# Captures pixel-exact screenshots of running Ornare.App.exe using Win32 PrintWindow(PW_RENDERFULLCONTENT)

param(
    [string]$OutputDir = "production_artifacts/design/runtime-screenshots"
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Capture {
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
}
"@

$exe = (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/Ornare.App.exe").Path
$sampleFolder = (Resolve-Path ".").Path

$scenarios = @(
    @{ Name = "runtime-quick-panel-light-idle.png"; Args = @("--set-folder", $sampleFolder, "--theme", "light", "--ui-state", "idle", "--lang", "en-US") },
    @{ Name = "runtime-quick-panel-light-drag-over.png"; Args = @("--set-folder", $sampleFolder, "--theme", "light", "--ui-state", "drag-over", "--lang", "en-US") },
    @{ Name = "runtime-quick-panel-light-success.png"; Args = @("--set-folder", $sampleFolder, "--theme", "light", "--ui-state", "success", "--lang", "en-US") },
    @{ Name = "runtime-quick-panel-dark-idle.png"; Args = @("--set-folder", $sampleFolder, "--theme", "dark", "--ui-state", "idle", "--lang", "en-US") },
    @{ Name = "runtime-quick-panel-dark-success.png"; Args = @("--set-folder", $sampleFolder, "--theme", "dark", "--ui-state", "success", "--lang", "en-US") },
    @{ Name = "runtime-quick-panel-turkish.png"; Args = @("--set-folder", $sampleFolder, "--theme", "light", "--ui-state", "idle", "--lang", "tr-TR") },
    @{ Name = "runtime-quick-panel-error-state.png"; Args = @("--set-folder", $sampleFolder, "--theme", "dark", "--ui-state", "error", "--lang", "en-US") },
    @{ Name = "runtime-quick-panel-undo-state.png"; Args = @("--set-folder", $sampleFolder, "--theme", "light", "--ui-state", "undo", "--lang", "en-US") }
)

Write-Host "Starting Real Ornare UI capture session across $($scenarios.Count) states..."

foreach ($scenario in $scenarios) {
    Write-Host "Launching scenario: $($scenario.Name)..."
    $p = Start-Process -FilePath $exe -ArgumentList $scenario.Args -PassThru
    
    # Wait for window creation and rendering
    $hWnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 150
        $p.Refresh()
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
            $hWnd = $p.MainWindowHandle
            break
        }
    }

    if ($hWnd -eq [IntPtr]::Zero) {
        Write-Warning "Could not obtain window handle for $($scenario.Name)"
        if (!$p.HasExited) { $p.Kill() }
        continue
    }

    # Ensure window is foregrounded and stabilized
    [Win32Capture]::SetForegroundWindow($hWnd)
    Start-Sleep -Milliseconds 600

    $rect = New-Object Win32Capture+RECT
    [Win32Capture]::GetWindowRect($hWnd, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    if ($width -gt 0 -and $height -gt 0) {
        $bmp = New-Object System.Drawing.Bitmap $width, $height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc()
        # PW_RENDERFULLCONTENT = 2
        $ok = [Win32Capture]::PrintWindow($hWnd, $hdc, 2)
        $g.ReleaseHdc($hdc)
        $g.Dispose()

        $filePath = Join-Path $OutputDir $scenario.Name
        $bmp.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "Captured: $filePath (Size: ${width}x${height}, OK=$ok)"
    } else {
        Write-Warning "Invalid bounds for $($scenario.Name): ${width}x${height}"
    }

    if (!$p.HasExited) {
        $p.Kill()
        $p.WaitForExit()
    }
    Start-Sleep -Milliseconds 300
}

Write-Host "UI runtime screenshot capture complete."
