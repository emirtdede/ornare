Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class ChildEnum {
    public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@

$procs = Get-Process -Name "explorer"
$hwnd = [IntPtr]::Zero
foreach ($p in $procs) {
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
        $hwnd = $p.MainWindowHandle
        break
    }
}
Write-Host "MainWindowHandle: $hwnd"

[ChildEnum]::EnumChildWindows($hwnd, {
    param($ch, $l)
    if ([ChildEnum]::IsWindowVisible($ch)) {
        $sb = New-Object System.Text.StringBuilder 256
        [ChildEnum]::GetClassName($ch, $sb, 256) | Out-Null
        $cls = $sb.ToString()
        if ($cls -like "*Xaml*" -or $cls -like "*Menu*" -or $cls -like "*Popup*" -or $cls -like "*Island*" -or $cls -like "*Shell*" -or $cls -like "*View*") {
            Write-Host "Child HWND: $ch Class: $cls"
        }
    }
    return $true
}, [IntPtr]::Zero) | Out-Null
