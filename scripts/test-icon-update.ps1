# Test changing icon of an already customized folder
$testFolder = "C:\Users\emir\Desktop\TestRefreshCheck"
$targetIco = Join-Path $testFolder ".ornare\folder.ico"

# Create a distinctive red ICO using ImageMagick in C#
Add-Type -Path "src\Ornare.Imaging\bin\Debug\net10.0\Magick.NET-Q16-AnyCPU.dll"
$img = New-Object ImageMagick.MagickImage([ImageMagick.MagickColors]::Blue, 64, 64)
$tempIco = Join-Path $testFolder "blue.ico"
$img.Write($tempIco, [ImageMagick.MagickFormat]::Ico)
$img.Dispose()

Write-Host "Overwriting .ornare\folder.ico with blue icon..."
(Get-Item $targetIco).Attributes = [System.IO.FileAttributes]::Normal
Copy-Item $tempIco $targetIco -Force
(Get-Item $targetIco).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System
Remove-Item $tempIco -Force

# Now notify
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class NotifyTest
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, string dwItem2);
}
"@

Write-Host "Sending SHChangeNotify..."
[NotifyTest]::SHChangeNotify(0x00002000, 0x0005, $testFolder, $null) # SHCNE_UPDATEITEM, SHCNF_PATHW
[NotifyTest]::SHChangeNotify(0x00000800, 0x0005, $testFolder, $null) # SHCNE_ATTRIBUTES, SHCNF_PATHW

$desktop = "C:\Users\emir\Desktop"
[NotifyTest]::SHChangeNotify(0x00001000, 0x0005, $desktop, $null) # SHCNE_UPDATEDIR
[NotifyTest]::SHChangeNotify(0x00002000, 0x0005, $desktop, $null) # SHCNE_UPDATEITEM

# Check icon index now
& powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check-icon-index.ps1
