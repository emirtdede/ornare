param(
    [string]$Path = "C:\Users\emir\Desktop\TestFolderForIcon"
)

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class ShellIconHelper2
{
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

    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_SYSICONINDEX = 0x000004000;
}
"@ -ErrorAction SilentlyContinue

$sfi = New-Object ShellIconHelper2+SHFILEINFO
$size = [System.Runtime.InteropServices.Marshal]::SizeOf($sfi)
$res = [ShellIconHelper2]::SHGetFileInfo($Path, 0, [ref]$sfi, $size, [ShellIconHelper2]::SHGFI_SYSICONINDEX)
Write-Host "$Path System Icon Index:" $sfi.iIcon
