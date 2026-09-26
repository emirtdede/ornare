# Test script to diagnose ShellRefresh behavior on Windows 11
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class ShellTest
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, string dwItem2);

    public const int SHCNE_RENAMEITEM       = 0x00000001;
    public const int SHCNE_CREATE           = 0x00000002;
    public const int SHCNE_DELETE           = 0x00000004;
    public const int SHCNE_MKDIR            = 0x00000008;
    public const int SHCNE_RMDIR            = 0x00000010;
    public const int SHCNE_ATTRIBUTES       = 0x00000800;
    public const int SHCNE_UPDATEDIR        = 0x00001000;
    public const int SHCNE_UPDATEITEM       = 0x00002000;
    public const int SHCNE_ASSOCCHANGED     = 0x08000000;

    public const uint SHCNF_IDLIST          = 0x0000;
    public const uint SHCNF_PATHW           = 0x0005;
    public const uint SHCNF_FLUSH           = 0x1000;
    public const uint SHCNF_FLUSHNOWAIT     = 0x3000;
}
"@

$testFolder = "C:\Users\emir\Desktop\TestRefreshCheck"
if (Test-Path $testFolder) {
    Remove-Item $testFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $testFolder -Force | Out-Null
Write-Host "Created test folder: $testFolder"

# 1. Setup .ornare folder and icon
$ornareDir = Join-Path $testFolder ".ornare"
New-Item -ItemType Directory -Path $ornareDir -Force | Out-Null
(Get-Item $ornareDir).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

$sourceIco = "src\Ornare.App\Assets\app.ico"
$targetIco = Join-Path $ornareDir "folder.ico"
Copy-Item $sourceIco $targetIco -Force
(Get-Item $targetIco).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

# 2. Write desktop.ini
$iniPath = Join-Path $testFolder "desktop.ini"
$iniContent = "[.ShellClassInfo]`r`nIconResource=.ornare\folder.ico,0`r`n"
[System.IO.File]::WriteAllText($iniPath, $iniContent, [System.Text.Encoding]::Unicode)
(Get-Item $iniPath).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

# 3. Set ReadOnly on folder
(Get-Item $testFolder).Attributes = (Get-Item $testFolder).Attributes -bor [System.IO.FileAttributes]::ReadOnly

Write-Host "Filesystem state ready. Folder ReadOnly:" ((Get-Item $testFolder).Attributes -band [System.IO.FileAttributes]::ReadOnly)
Write-Host "desktop.ini exists:" (Test-Path $iniPath)
Write-Host "folder.ico exists:" (Test-Path $targetIco)

# Test 1: Original notify with SHCNF_PATHW (0x0005)
Write-Host "`nTesting Notify with SHCNF_PATHW..."
[ShellTest]::SHChangeNotify([ShellTest]::SHCNE_ATTRIBUTES, [ShellTest]::SHCNF_PATHW, $testFolder, $null)
[ShellTest]::SHChangeNotify([ShellTest]::SHCNE_UPDATEITEM, [ShellTest]::SHCNF_PATHW, $testFolder, $null)
Write-Host "Done."
