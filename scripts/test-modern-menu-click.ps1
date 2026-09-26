# Ornare — Real Modern Windows 11 Explorer Context Menu Item Click & Activation Test
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class ModernMenuClicker {
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
}
'@

$baseDir = (Resolve-Path ".").Path
$targetFolder = Join-Path $baseDir "OrnareGate1Final"

# Ensure target folder exists
if (!(Test-Path $targetFolder)) {
    New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
}

Write-Host "=== 1. Starting Explorer targeting $targetFolder ==="
Start-Process "explorer.exe" -ArgumentList "/select,`"$targetFolder`""
Start-Sleep -Seconds 3

# Find the Explorer MainWindowHandle
$hwnd = [IntPtr]::Zero
$procs = Get-Process -Name "explorer"
foreach ($p in $procs) {
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
        $hwnd = $p.MainWindowHandle
        break
    }
}
Write-Host "Explorer Window Handle: $hwnd"

[ModernMenuClicker]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "OrnareGate1Final")
$item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)

if ($null -eq $item) {
    Write-Error "Could not locate target folder in Explorer list view."
    exit 1
}

$rect = $item.Current.BoundingRectangle
$cx = [int]($rect.Left + $rect.Width / 2)
$cy = [int]($rect.Top + $rect.Height / 2)
Write-Host "Folder item position: ($cx, $cy)"

[ModernMenuClicker]::SetCursorPos($cx, $cy)
Start-Sleep -Milliseconds 200

# Genuine mouse right-click (NO Shift, NO F10, NO menu key)
[ModernMenuClicker]::mouse_event([ModernMenuClicker]::MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[ModernMenuClicker]::mouse_event([ModernMenuClicker]::MOUSEEVENTF_RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
Write-Host "Dispatched genuine mouse right-click."

Start-Sleep -Milliseconds 1200

Write-Host "=== 2. Searching for Ornare command item in active UI Automation hierarchy ==="
$desktopRoot = [System.Windows.Automation.AutomationElement]::RootElement

# Look for MenuItem with Name matching "Klasör Simgesini Ayarla" or "Set Folder Icon"
$foundItem = $null
$menuItemCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)
$allMenuItems = $desktopRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants, $menuItemCond)

Write-Host "Found $($allMenuItems.Count) menu items in active desktop tree."
foreach ($mi in $allMenuItems) {
    $name = $mi.Current.Name
    if ($name -like "*Klasör Simgesini Ayarla*" -or $name -like "*Set Folder Icon*") {
        Write-Host "Found Ornare Modern Context Menu Item: '$name'!"
        $foundItem = $mi
        break
    }
}

if ($null -ne $foundItem) {
    $miRect = $foundItem.Current.BoundingRectangle
    $clickX = [int]($miRect.Left + $miRect.Width / 2)
    $clickY = [int]($miRect.Top + $miRect.Height / 2)
    Write-Host "Clicking Ornare menu item at: ($clickX, $clickY)..."
    [ModernMenuClicker]::SetCursorPos($clickX, $clickY)
    Start-Sleep -Milliseconds 200
    [ModernMenuClicker]::mouse_event([ModernMenuClicker]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [ModernMenuClicker]::mouse_event([ModernMenuClicker]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    Write-Host "Mouse click sent to Ornare context menu item."
} else {
    Write-Error "FAIL: Ornare context menu item was not found in Explorer modern menu! Cannot proceed."
    exit 1
}

Write-Host "=== 3. Verifying Packaged Activation & Quick Panel Visibility ==="
$appLaunched = $false
$targetAppHwnd = [IntPtr]::Zero
$targetAppPid = 0

for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Milliseconds 500
    $appProcs = Get-Process -Name "Ornare.App" -ErrorAction SilentlyContinue
    if ($null -ne $appProcs -and $appProcs.Count -gt 0) {
        $targetAppPid = $appProcs[0].Id
        $appLaunched = $true
        break
    }
}

if (-not $appLaunched) {
    Write-Error "FAIL: Ornare.App process was NOT launched by Explorer context menu click!"
    exit 1
}

Write-Host "Ornare.App process is running: PID $targetAppPid. Verifying visible WinUI 3 Quick Panel window..."
powershell -ExecutionPolicy Bypass -File scripts/dump-windows.ps1 -TargetPid $targetAppPid

# Check if dump-windows confirmed a visible window
$winOutput = powershell -ExecutionPolicy Bypass -File scripts/dump-windows.ps1 -TargetPid $targetAppPid
$hasVisWin = $false
foreach ($line in $winOutput) {
    if ($line -match "Vis: True" -and $line -match "WinUIDesktopWin32WindowClass") {
        $hasVisWin = $true
        Write-Host "CONFIRMED: $line"
        break
    }
}

if (-not $hasVisWin) {
    Write-Error "FAIL: Ornare.App launched but no visible WinUI 3 window was found!"
    exit 1
}

Write-Host "SUCCESS: True end-to-end Explorer click -> App activation -> Visible Quick Panel verified!"

# Dismiss any remaining menu
[ModernMenuClicker]::keybd_event(0x1B, 0, 0, 0)
[ModernMenuClicker]::keybd_event(0x1B, 0, 2, 0)
Write-Host "ACTIVATION TEST COMPLETE: PASS"
