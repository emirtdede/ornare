Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class DiagNative {
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

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
}
'@

$baseDir = (Resolve-Path ".").Path
$targetFolder = Join-Path $baseDir "OrnareGate1Final"
$p = Start-Process "explorer.exe" -ArgumentList "/select,`"$targetFolder`"" -PassThru
Start-Sleep -Seconds 3

# Find Explorer window
$procs = Get-Process -Name "explorer"
$hwnd = [IntPtr]::Zero
foreach ($pr in $procs) {
    if ($pr.MainWindowHandle -ne [IntPtr]::Zero) {
        $hwnd = $pr.MainWindowHandle
        break
    }
}

Write-Host "Explorer MainWindowHandle: $hwnd"
[DiagNative]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

# Locate item
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "OrnareGate1Final")
$item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)

if ($null -ne $item) {
    $rect = $item.Current.BoundingRectangle
    $cx = [int]($rect.Left + $rect.Width / 2)
    $cy = [int]($rect.Top + $rect.Height / 2)
    Write-Host "Target located at ($cx, $cy)"

    [DiagNative]::SetCursorPos($cx, $cy)
    Start-Sleep -Milliseconds 200

    # Real mouse right click
    [DiagNative]::mouse_event([DiagNative]::MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [DiagNative]::mouse_event([DiagNative]::MOUSEEVENTF_RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
    Write-Host "Right click executed."

    Start-Sleep -Seconds 2

    # Enumerate all visible windows to find the popup/context menu
    $foundWindows = @()
    [DiagNative]::EnumWindows({
        param($h, $l)
        if ([DiagNative]::IsWindowVisible($h)) {
            $sbClass = New-Object System.Text.StringBuilder 256
            [DiagNative]::GetClassName($h, $sbClass, 256) | Out-Null
            $sbText = New-Object System.Text.StringBuilder 256
            [DiagNative]::GetWindowText($h, $sbText, 256) | Out-Null
            $processId = 0
            [DiagNative]::GetWindowThreadProcessId($h, [ref]$processId) | Out-Null
            $cls = $sbClass.ToString()
            $txt = $sbText.ToString()
            if ($cls -like "*Menu*" -or $cls -like "*Xaml*" -or $cls -like "*Popup*" -or $cls -like "*Island*" -or $cls -eq "#32768" -or $cls -like "*Cabinet*") {
                Write-Host "Window: HWND=$h PID=$processId Class='$cls' Text='$txt'"
                $script:foundWindows += [PSCustomObject]@{ Handle = $h; Class = $cls; Text = $txt }
            }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
