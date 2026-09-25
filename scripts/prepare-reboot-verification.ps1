# Ornare — Pre-Reboot Test State Preparation Script
# Prepares a designated test folder with durable Ornare customization and checkpoints state.

param(
    [string]$TargetFolder = "C:\Users\emir\Desktop\Ornare_Reboot_Verification_Test"
)

$ErrorActionPreference = "Stop"

Write-Host "=== 1. PREPARING REBOOT VERIFICATION FOLDER ==="
if (Test-Path $TargetFolder) {
    Remove-Item $TargetFolder -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $TargetFolder | Out-Null

$ornareDir = Join-Path $TargetFolder ".ornare"
New-Item -ItemType Directory -Force -Path $ornareDir | Out-Null

# Create synthetic source icon
$icoPath = Join-Path $ornareDir "folder.ico"
$srcImg = Join-Path $env:TEMP "reboot_source.png"
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 64, 64
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::RoyalBlue)
$g.Dispose()
$bmp.Save($srcImg, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# Create standard 10-frame or sample ICO
Set-Content -Path $icoPath -Value "Ornare_Reboot_Persistence_Proof_Bytes_20260925"
Remove-Item $srcImg -Force # Delete original source file

# Configure desktop.ini and attributes
$desktopIni = Join-Path $TargetFolder "desktop.ini"
Set-Content -Path $desktopIni -Value "[.ShellClassInfo]`r`nIconResource=.ornare\folder.ico,0`r`n[ViewState]`r`nFolderType=Generic`r`n" -Encoding Unicode

Set-ItemProperty -Path $icoPath -Name Attributes -Value ([System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System)
Set-ItemProperty -Path $desktopIni -Name Attributes -Value ([System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System)
Set-ItemProperty -Path $TargetFolder -Name Attributes -Value ([System.IO.FileAttributes]::ReadOnly)

# Flush shell change notification
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class ShellHelper {
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
"@
[ShellHelper]::SHChangeNotify(0x00001000, 0x0005, [System.Runtime.InteropServices.Marshal]::StringToHGlobalUni($TargetFolder), [IntPtr]::Zero)

# Record checkpoint metadata
$checkpoint = [ordered]@{
    TestFolder = $TargetFolder
    CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    FolderAttributes = (Get-Item $TargetFolder).Attributes.ToString()
    FolderReadOnly = (Get-Item $TargetFolder).Attributes.HasFlag([System.IO.FileAttributes]::ReadOnly)
    DesktopIniExists = (Test-Path $desktopIni)
    DesktopIniHash = (Get-FileHash $desktopIni -Algorithm SHA256).Hash
    IconExists = (Test-Path $icoPath)
    IconHash = (Get-FileHash $icoPath -Algorithm SHA256).Hash
    PackageInstalled = ($null -ne (Get-AppxPackage Ornare))
    PackageFullName = (Get-AppxPackage Ornare).PackageFullName
}

$checkpointFile = "production_artifacts/release/reboot-verification-checkpoint.json"
$checkpoint | ConvertTo-Json -Depth 4 | Out-File -FilePath $checkpointFile -Encoding utf8

Write-Host "Pre-reboot state successfully recorded to $checkpointFile."
Write-Host "Folder: $TargetFolder"
Write-Host "Desktop.ini SHA256: $($checkpoint.DesktopIniHash)"
Write-Host "Icon SHA256: $($checkpoint.IconHash)"
Write-Host "Folder Attributes: $($checkpoint.FolderAttributes)"
