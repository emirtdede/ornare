# Ornare - Verify ManagerWindow Maximize & Restore Behavior
$ErrorActionPreference = "Stop"

Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class Win32ManagerTest {
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
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    public static IntPtr FindManagerHwnd() {
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

Write-Host "1. Killing any running Ornare process..."
Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

Write-Host "2. Launching Manager window via protocol: ornare://manager?section=Settings&theme=dark&lang=tr-TR"
Start-Process "ornare://manager?section=Settings&theme=dark&lang=tr-TR"

$hWnd = [IntPtr]::Zero
for ($i = 0; $i -lt 50; $i++) {
    Start-Sleep -Milliseconds 100
    $hWnd = [Win32ManagerTest]::FindManagerHwnd()
    if ($hWnd -ne [IntPtr]::Zero) {
        break
    }
}

if ($hWnd -eq [IntPtr]::Zero) {
    throw "Failed to locate ManagerWindow HWND within 5 seconds!"
}

Write-Host "3. Found ManagerWindow HWND: 0x$($hWnd.ToString('X'))"
[Win32ManagerTest]::SetForegroundWindow($hWnd) | Out-Null
Start-Sleep -Milliseconds 500

Write-Host "4. Maximizing ManagerWindow (SW_MAXIMIZE = 3)..."
[Win32ManagerTest]::ShowWindow($hWnd, 3) | Out-Null
Start-Sleep -Milliseconds 800
$isMax1 = [Win32ManagerTest]::IsZoomed($hWnd)
Write-Host "   IsZoomed after maximize: $isMax1"
if (-not $isMax1) {
    throw "Window did not maximize as expected!"
}

Write-Host "5. Restoring/Shrinking ManagerWindow (SW_RESTORE = 9)..."
[Win32ManagerTest]::ShowWindow($hWnd, 9) | Out-Null
Start-Sleep -Milliseconds 800
$isMax2 = [Win32ManagerTest]::IsZoomed($hWnd)
Write-Host "   IsZoomed after restore: $isMax2"
if ($isMax2) {
    throw "Window did not restore as expected!"
}

Write-Host "6. Repeating Maximize -> Restore cycle (2nd pass)..."
[Win32ManagerTest]::ShowWindow($hWnd, 3) | Out-Null
Start-Sleep -Milliseconds 800
Write-Host "   IsZoomed after 2nd maximize: $([Win32ManagerTest]::IsZoomed($hWnd))"
[Win32ManagerTest]::ShowWindow($hWnd, 9) | Out-Null
Start-Sleep -Milliseconds 800
Write-Host "   IsZoomed after 2nd restore: $([Win32ManagerTest]::IsZoomed($hWnd))"

$proc = Get-Process "Ornare.App" -ErrorAction SilentlyContinue
if (!$proc) {
    throw "CRASH DETECTED: Ornare.App process died during maximize/restore cycle!"
}

Write-Host "7. Process health check:"
Write-Host "   PID: $($proc.Id)"
Write-Host "   Responding: $($proc.Responding)"
Write-Host "   HasExited: $($proc.HasExited)"

# Check EventLog for any new application crashes in the last 2 minutes
$recentCrashes = Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='Application Error'} -MaxEvents 3 -ErrorAction SilentlyContinue | Where-Object {
    $_.TimeCreated -gt (Get-Date).AddMinutes(-2) -and $_.Message -match "Ornare\.App\.exe"
}

if ($recentCrashes) {
    Write-Host "CRASH REPORTED IN EVENT LOG:"
    $recentCrashes | Format-List TimeCreated, Message
    throw "New crash found in Event Log!"
} else {
    Write-Host "8. Windows Event Log check: CLEAN (0 crashes recorded in the last 2 minutes)."
}

Write-Host "`n=== VERIFICATION SUCCEEDED: MANAGER WINDOW MAXIMIZE AND RESTORE ARE COMPLETELY STABLE ==="
