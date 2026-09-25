Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WindowEnum {
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
}
'@

[WindowEnum]::EnumWindows({
    param($h, $l)
    if ([WindowEnum]::IsWindowVisible($h)) {
        $sbClass = New-Object System.Text.StringBuilder 256
        [WindowEnum]::GetClassName($h, $sbClass, 256) | Out-Null
        $sbText = New-Object System.Text.StringBuilder 256
        [WindowEnum]::GetWindowText($h, $sbText, 256) | Out-Null
        $procId = 0
        [WindowEnum]::GetWindowThreadProcessId($h, [ref]$procId) | Out-Null
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        $cls = $sbClass.ToString()
        $txt = $sbText.ToString()
        if ($p -and ($p.Name -eq "explorer" -or $cls -like "*Menu*" -or $cls -like "*Xaml*" -or $cls -like "*Popup*")) {
            Write-Host "HWND: $h | PID: $procId ($($p.Name)) | Class: $cls | Text: $txt"
        }
    }
    return $true
}, [IntPtr]::Zero) | Out-Null
