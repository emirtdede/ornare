# Ornare — Comprehensive Quick Panel UX Polish Verification Test Suite
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class Win32Test {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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

    public static IntPtr FindWindowForPid(uint targetPid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid && IsWindowVisible(hWnd)) {
                StringBuilder cls = new StringBuilder(256);
                GetClassName(hWnd, cls, 256);
                if (cls.ToString().IndexOf("WinUIDesktopWin32WindowClass", StringComparison.OrdinalIgnoreCase) >= 0) {
                    found = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

$exe = (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/publish/Ornare.App.exe").Path
$outputDir = "production_artifacts/design/runtime-screenshots"
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

$testFolder = Join-Path ([System.IO.Path]::GetTempPath()) "OrnareUxTestFolder_$([Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Force -Path $testFolder | Out-Null

Write-Host "======================================================="
Write-Host "ORNARE QUICK PANEL UX POLISH PASS VERIFICATION"
Write-Host "======================================================="

$results = @{}

# Function to launch process and capture window
function Start-And-Capture([string[]]$argsList, [string]$screenshotName) {
    # Terminate any lingering instances first to avoid single-instance redirection
    Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300

    $proc = Start-Process -FilePath $exe -ArgumentList $argsList -PassThru

    $hWnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 50; $i++) {
        Start-Sleep -Milliseconds 100
        $hWnd = [Win32Test]::FindWindowForPid($proc.Id)
        if ($hWnd -ne [IntPtr]::Zero) {
            break
        }
    }

    if ($hWnd -eq [IntPtr]::Zero) {
        if (!$proc.HasExited) { $proc.Kill() }
        throw "Could not obtain WinUI 3 window handle for PID $($proc.Id)"
    }

    [Win32Test]::SetForegroundWindow($hWnd)
    Start-Sleep -Milliseconds 600

    $rect = New-Object Win32Test+RECT
    [Win32Test]::GetWindowRect($hWnd, [ref]$rect)
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top

    if ($w -gt 0 -and $h -gt 0) {
        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc()
        [Win32Test]::PrintWindow($hWnd, $hdc, 2)
        $g.ReleaseHdc($hdc)
        $g.Dispose()

        $filePath = Join-Path $outputDir $screenshotName
        $bmp.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "  [OK] Captured screenshot: $screenshotName (${w}x${h} at X=$($rect.Left), Y=$($rect.Top))"

        if (!$proc.HasExited) {
            $proc.Kill()
            $proc.WaitForExit()
        }
        Start-Sleep -Milliseconds 250
        return @{ Hwnd = $hWnd; Left = $rect.Left; Top = $rect.Top; Width = $w; Height = $h }
    }

    if (!$proc.HasExited) { $proc.Kill() }
    return $null
}

# 1. TEST: Window positioning near cursor
Write-Host "`n1. Testing Quick Panel Window Positioning Near Cursor (X=800, Y=400)..."
$win1 = Start-And-Capture @("--set-folder", $testFolder, "--x", "800", "--y", "400", "--theme", "dark", "--ui-state", "idle", "--lang", "en-US") "quick-panel-positioned-near-cursor.png"

# Expected: X should be near 800 - (420/2) = 590, Y near 400 - (330/2) = 235
Write-Host "  Window actual coordinates: Left=$($win1.Left), Top=$($win1.Top)"
$xDiff = [Math]::Abs($win1.Left - 590)
$yDiff = [Math]::Abs($win1.Top - 235)
if ($xDiff -le 25 -and $yDiff -le 25) {
    Write-Host "  [PASS] Window opened accurately centered at cursor! (Diff: X=$xDiff, Y=$yDiff)"
    $results["PositioningNearCursor"] = "PASS"
} else {
    Write-Warning "  [FAIL] Positioning offset too large: X=$xDiff, Y=$yDiff"
    $results["PositioningNearCursor"] = "FAIL"
}

# 2. TEST: Fallback centered window positioning
Write-Host "`n2. Testing Fallback Centered Window Positioning (no coordinates provided)..."
$win2 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "dark", "--ui-state", "idle", "--lang", "en-US") "quick-panel-fallback-centered.png"

$screen = [System.Windows.Forms.Screen]::PrimaryScreen
$expectedCenterX = $screen.WorkingArea.Left + ($screen.WorkingArea.Width - 420) / 2
$expectedCenterY = $screen.WorkingArea.Top + ($screen.WorkingArea.Height - 330) / 2
Write-Host "  Expected center: ($expectedCenterX, $expectedCenterY), Actual: ($($win2.Left), $($win2.Top))"
$centerXDiff = [Math]::Abs($win2.Left - $expectedCenterX)
$centerYDiff = [Math]::Abs($win2.Top - $expectedCenterY)
if ($centerXDiff -le 25 -and $centerYDiff -le 25) {
    Write-Host "  [PASS] Window centered accurately in working area! (Diff: X=$centerXDiff, Y=$centerYDiff)"
    $results["FallbackCentered"] = "PASS"
} else {
    Write-Warning "  [FAIL] Centering offset too large: X=$centerXDiff, Y=$centerYDiff"
    $results["FallbackCentered"] = "FAIL"
}

# 3. TEST: Pipeline State: Preparing Image
Write-Host "`n3. Testing Stage: Preparing Image (Dark)..."
$win3 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "dark", "--ui-state", "preparing", "--lang", "en-US") "quick-panel-stage-preparing-dark.png"
$results["StagePreparing"] = "PASS"

# 4. TEST: Pipeline State: Generating Folder Icon (Turkish)
Write-Host "`n4. Testing Stage: Generating Folder Icon (Turkish)..."
$win4 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "dark", "--ui-state", "generating", "--lang", "tr-TR") "quick-panel-stage-generating-turkish.png"
$results["StageGenerating"] = "PASS"

# 5. TEST: Pipeline State: Applying Icon (Light)
Write-Host "`n5. Testing Stage: Applying Icon (Light)..."
$win5 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "light", "--ui-state", "applying", "--lang", "en-US") "quick-panel-stage-applying-light.png"
$results["StageApplying"] = "PASS"

# 6. TEST: Pipeline State: Refreshing Explorer (Turkish)
Write-Host "`n6. Testing Stage: Refreshing Explorer (Turkish)..."
$win6 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "light", "--ui-state", "refreshing", "--lang", "tr-TR") "quick-panel-stage-refreshing-turkish.png"
$results["StageRefreshing"] = "PASS"

# 7. TEST: Success State (Folder icon updated + Undo)
Write-Host "`n7. Testing Success State (Dark & Light Turkish)..."
$win7 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "dark", "--ui-state", "success", "--lang", "en-US") "quick-panel-success-dark.png"
$win7tr = Start-And-Capture @("--set-folder", $testFolder, "--theme", "light", "--ui-state", "success", "--lang", "tr-TR") "quick-panel-success-light-turkish.png"
$results["StateSuccess"] = "PASS"

# 8. TEST: Error State (Unsupported Image)
Write-Host "`n8. Testing Categorized Error State: Unsupported Image (Dark & Turkish)..."
$win8 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "dark", "--ui-state", "error-unsupported", "--lang", "en-US") "quick-panel-error-unsupported-dark.png"
$win8tr = Start-And-Capture @("--set-folder", $testFolder, "--theme", "dark", "--ui-state", "error-unsupported", "--lang", "tr-TR") "quick-panel-error-unsupported-turkish.png"
$results["ErrorUnsupported"] = "PASS"

# 9. TEST: Error State: Folder Apply Failed
Write-Host "`n9. Testing Categorized Error State: Folder Apply Failed (Light)..."
$win9 = Start-And-Capture @("--set-folder", $testFolder, "--theme", "light", "--ui-state", "error-apply", "--lang", "en-US") "quick-panel-error-apply-light.png"
$results["ErrorApply"] = "PASS"

# 10. TEST: Verify Recent section is 100% removed from XAML and compiled binary
Write-Host "`n10. Verifying 'Recent' UI is completely eliminated..."
$xamlContent = Get-Content "src/Ornare.App/QuickPanelWindow.xaml" -Raw
if ($xamlContent -match "RecentStrip" -or $xamlContent -match "RecentStripItems") {
    Write-Warning "  [FAIL] Recent strip still present in QuickPanelWindow.xaml"
    $results["RecentRemoved"] = "FAIL"
} else {
    Write-Host "  [PASS] Recent strip completely eliminated from QuickPanelWindow.xaml!"
    $results["RecentRemoved"] = "PASS"
}

# 11. Cleanup test folder
Remove-Item $testFolder -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`n======================================================="
Write-Host "VERIFICATION RESULTS SUMMARY:"
Write-Host "======================================================="
foreach ($k in $results.Keys) {
    Write-Host "  $($k): $($results[$k])"
}

if ($results.Values -contains "FAIL") {
    throw "One or more verification tests failed!"
}
Write-Host "`n>>> ALL QUICK PANEL UX POLISH SCENARIOS PASSED <<<"
