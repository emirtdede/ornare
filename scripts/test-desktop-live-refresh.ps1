# Test script for live desktop folder icon refresh
$testFolder = "C:\Users\emir\Desktop\TestFolderForIcon"
if (Test-Path $testFolder) {
    Remove-Item $testFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $testFolder -Force | Out-Null

$ornareDir = Join-Path $testFolder ".ornare"
New-Item -ItemType Directory -Path $ornareDir -Force | Out-Null
(Get-Item $ornareDir -Force).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

$sourceIco = "src\Ornare.App\Assets\app.ico"
$targetIco = Join-Path $ornareDir "folder.ico"
Copy-Item $sourceIco $targetIco -Force
(Get-Item $targetIco -Force).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

$iniPath = Join-Path $testFolder "desktop.ini"
$iniContent = "[.ShellClassInfo]`r`nIconResource=.ornare\folder.ico,0`r`n"
[System.IO.File]::WriteAllText($iniPath, $iniContent, [System.Text.Encoding]::Unicode)
(Get-Item $iniPath -Force).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

# Set ReadOnly on folder
(Get-Item $testFolder).Attributes = (Get-Item $testFolder).Attributes -bor [System.IO.FileAttributes]::ReadOnly
[System.IO.Directory]::SetLastWriteTimeUtc($testFolder, [System.DateTime]::UtcNow)

# P/Invoke definitions
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class ShellLiveTest
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, string dwItem2);

    public const int SHCNE_ATTRIBUTES   = 0x00000800;
    public const int SHCNE_UPDATEDIR    = 0x00001000;
    public const int SHCNE_UPDATEITEM   = 0x00002000;
    public const int SHCNE_ASSOCCHANGED = 0x08000000;

    public const uint SHCNF_PATHW       = 0x0005;
    public const uint SHCNF_FLUSH       = 0x1000;
    public const uint SHCNF_FLUSHNOWAIT = 0x3000;
}
"@

Write-Host "Triggering notifications..."
$flags = [ShellLiveTest]::SHCNF_PATHW -bor [ShellLiveTest]::SHCNF_FLUSHNOWAIT

[ShellLiveTest]::SHChangeNotify([ShellLiveTest]::SHCNE_UPDATEITEM, $flags, $iniPath, $null)
[ShellLiveTest]::SHChangeNotify([ShellLiveTest]::SHCNE_ATTRIBUTES, $flags, $testFolder, $null)
[ShellLiveTest]::SHChangeNotify([ShellLiveTest]::SHCNE_UPDATEITEM, $flags, $testFolder, $null)

$parent = [System.IO.Path]::GetDirectoryName($testFolder)
[ShellLiveTest]::SHChangeNotify([ShellLiveTest]::SHCNE_UPDATEDIR, $flags, $parent, $null)
[ShellLiveTest]::SHChangeNotify([ShellLiveTest]::SHCNE_UPDATEITEM, $flags, $parent, $null)

Write-Host "Notifications sent with FLUSHNOWAIT (0x3005)."
