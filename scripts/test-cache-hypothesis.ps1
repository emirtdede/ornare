# Test whether Explorer caches by filename
$testDir = "C:\Users\emir\Desktop\TestCacheHypothesis"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

$ornareDir = Join-Path $testDir ".ornare"
New-Item -ItemType Directory -Path $ornareDir -Force | Out-Null
(Get-Item $ornareDir -Force).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

$ico1 = "src\Ornare.App\Assets\app.ico"
$targetIco = Join-Path $ornareDir "folder.ico"
Copy-Item $ico1 $targetIco -Force
(Get-Item $targetIco -Force).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

$iniPath = Join-Path $testDir "desktop.ini"
$iniContent = "[.ShellClassInfo]`r`nIconResource=.ornare\folder.ico,0`r`n"
[System.IO.File]::WriteAllText($iniPath, $iniContent, [System.Text.Encoding]::Unicode)
(Get-Item $iniPath -Force).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

(Get-Item $testDir).Attributes = (Get-Item $testDir).Attributes -bor [System.IO.FileAttributes]::ReadOnly

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class CacheTestHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, string dwItem2);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
}
"@ -ErrorAction SilentlyContinue

[CacheTestHelper]::SHChangeNotify(0x00000800, 0x0005 -bor 0x3000, $testDir, $null)
[CacheTestHelper]::SHChangeNotify(0x00002000, 0x0005 -bor 0x3000, $testDir, $null)

$sfi1 = New-Object CacheTestHelper+SHFILEINFO
$size1 = [System.Runtime.InteropServices.Marshal]::SizeOf($sfi1)
[CacheTestHelper]::SHGetFileInfo($testDir, 0, [ref]$sfi1, $size1, 0x000004000)
Write-Host "Icon index with First ICO:" $sfi1.iIcon

# Now simulate changing to a different ICO, but keeping the exact same filename .ornare\folder.ico
# We copy a different ICO (or modify the ICO bytes)
$ico2Bytes = [System.IO.File]::ReadAllBytes($ico1)
# Invert some bytes so it's a completely different icon
$ico2Bytes[50] = ($ico2Bytes[50] + 1) -band 0xFF

(Get-Item $targetIco -Force).Attributes = [System.IO.FileAttributes]::Normal
[System.IO.File]::WriteAllBytes($targetIco, $ico2Bytes)
(Get-Item $targetIco -Force).Attributes = [System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System

# Trigger same notifications as ShellRefreshService
[CacheTestHelper]::SHChangeNotify(0x00000800, 0x0005 -bor 0x3000, $testDir, $null)
[CacheTestHelper]::SHChangeNotify(0x00002000, 0x0005 -bor 0x3000, $testDir, $null)

$sfi2 = New-Object CacheTestHelper+SHFILEINFO
[CacheTestHelper]::SHGetFileInfo($testDir, 0, [ref]$sfi2, $size1, 0x000004000)
Write-Host "Icon index after overwriting .ornare\folder.ico:" $sfi2.iIcon
Write-Host "Did icon index change? " ($sfi1.iIcon -ne $sfi2.iIcon)
