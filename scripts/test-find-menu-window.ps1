Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class MenuFinder {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
}
'@

$baseDir = (Resolve-Path ".").Path
$targetFolder = Join-Path $baseDir "OrnareGate1Final"
Start-Process "explorer.exe" -ArgumentList "/select,`"$targetFolder`""
Start-Sleep -Seconds 3

# Find Explorer window
$procs = Get-Process -Name "explorer"
$hwnd = [IntPtr]::Zero
foreach ($p in $procs) {
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
        $hwnd = $p.MainWindowHandle
        break
    }
}
Write-Host "Explorer MainWindowHandle: $hwnd"

[MenuFinder]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "OrnareGate1Final")
$item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)

if ($null -ne $item) {
    $rect = $item.Current.BoundingRectangle
    $cx = [int]($rect.Left + $rect.Width / 2)
    $cy = [int]($rect.Top + $rect.Height / 2)
    Write-Host "Target located at: $cx, $cy"

    [MenuFinder]::SetCursorPos($cx, $cy)
    Start-Sleep -Milliseconds 200

    # Genuine mouse right click:
    [MenuFinder]::mouse_event([MenuFinder]::MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [MenuFinder]::mouse_event([MenuFinder]::MOUSEEVENTF_RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
    Write-Host "Mouse right-click sent."

    Start-Sleep -Milliseconds 1200

    # List all windows created by explorer.exe or visible popup
    [MenuFinder]::EnumWindows({
        param($h, $l)
        if ([MenuFinder]::IsWindowVisible($h)) {
            $wProcId = 0
            [MenuFinder]::GetWindowThreadProcessId($h, [ref]$wProcId) | Out-Null
            $sbClass = New-Object System.Text.StringBuilder 256
            [MenuFinder]::GetClassName($h, $sbClass, 256) | Out-Null
            $sbText = New-Object System.Text.StringBuilder 256
            [MenuFinder]::GetWindowText($h, $sbText, 256) | Out-Null
            $cls = $sbClass.ToString()
            $txt = $sbText.ToString()

            $pr = Get-Process -Id $wProcId -ErrorAction SilentlyContinue
            if ($pr -and $pr.Name -eq "explorer") {
                Write-Host "Explorer Window: HWND=$h Class='$cls' Text='$txt'"
            }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
