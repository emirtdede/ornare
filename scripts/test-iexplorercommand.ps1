Add-Type @"
using System;
using System.Runtime.InteropServices;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0db")]
public interface IExplorerCommand
{
    [PreserveSig]
    int GetTitle([MarshalAs(UnmanagedType.Interface)] object psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    [PreserveSig]
    int GetIcon([MarshalAs(UnmanagedType.Interface)] object psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszIcon);
    [PreserveSig]
    int GetToolTip([MarshalAs(UnmanagedType.Interface)] object psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszInfoTip);
    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);
    [PreserveSig]
    int GetState([MarshalAs(UnmanagedType.Interface)] object psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out int pcmdState);
    [PreserveSig]
    int Invoke([MarshalAs(UnmanagedType.Interface)] object psiItemArray, [MarshalAs(UnmanagedType.Interface)] object pbc);
    [PreserveSig]
    int GetFlags(out int pFlags);
    [PreserveSig]
    int EnumSubCommands(out object ppEnum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
public interface IShellItem
{
    void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}

public class ShellApi
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [DllImport("shell32.dll", PreserveSig = false)]
    public static extern void SHCreateShellItemArrayFromShellItem(
        [MarshalAs(UnmanagedType.Interface)] IShellItem psi,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);
}
"@

$clsid = [Guid]"4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E"
$type = [Type]::GetTypeFromCLSID($clsid)
$comInstance = [Activator]::CreateInstance($type)

Write-Host "Created COM instance of Ornare Shell Extension."
$cmd = [IExplorerCommand]$comInstance

# 1. GetTitle
$title = ""
$hr = $cmd.GetTitle($null, [ref]$title)
Write-Host "GetTitle HRESULT: $hr, Title: '$title'"

# 2. GetCanonicalName
$guid = [Guid]::Empty
$hr = $cmd.GetCanonicalName([ref]$guid)
Write-Host "GetCanonicalName HRESULT: $hr, Canonical GUID: $guid"

# 3. Create real folder and ShellItemArray
$testFolder = Join-Path (Resolve-Path ".").Path "OrnareGate1Final"
if (!(Test-Path $testFolder)) {
    New-Item -ItemType Directory -Path $testFolder -Force | Out-Null
}

$shellItemGuid = [Guid]"43826d1e-e718-42ee-bc55-a1e261c37bfe"
$itemArrayGuid = [Guid]"b63ea76d-1f85-456f-a19c-48159efa858b"

$item = $null
[ShellApi]::SHCreateItemFromParsingName($testFolder, [IntPtr]::Zero, $shellItemGuid, [ref]$item)
Write-Host "Created IShellItem for folder: $testFolder"

$itemArray = $null
[ShellApi]::SHCreateShellItemArrayFromShellItem($item, $itemArrayGuid, [ref]$itemArray)
Write-Host "Created IShellItemArray for folder."

# 4. GetState
$state = 0
$hr = $cmd.GetState($itemArray, $false, [ref]$state)
# ECS_ENABLED = 0, ECS_DISABLED = 1, ECS_HIDDEN = 2
Write-Host "GetState HRESULT: $hr, State: $state (0=ENABLED, 1=DISABLED, 2=HIDDEN)"

# 5. Invoke
Write-Host "Invoking command via IExplorerCommand.Invoke..."
$hr = $cmd.Invoke($itemArray, $null)
Write-Host "Invoke HRESULT: $hr"

Start-Sleep -Seconds 2
$ornareProcs = Get-Process -Name "Ornare.App" -ErrorAction SilentlyContinue
Write-Host "Ornare.App running processes count: $($ornareProcs.Count)"
if ($ornareProcs.Count -gt 0) {
    Write-Host "SUCCESS: Packaged Ornare activated via IExplorerCommand.Invoke! PID: $($ornareProcs[0].Id)"
    # Cleanup process
    $ornareProcs | Stop-Process -Force
} else {
    Write-Host "WARNING: Ornare.App did not launch or terminated early."
}
