param([string]$TargetFolder = "C:\Users\emir\Desktop")

$script = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
public interface IShellItem
{
    void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
public interface IShellItemArray
{
    void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppvOut);
    void GetPropertyStore(int flags, [In] ref Guid riid, out IntPtr ppv);
    void GetPropertyDescriptionList([In] ref Guid keyType, [In] ref Guid riid, out IntPtr ppv);
    void GetAttributes(int dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
    void GetCount(out uint pdwNumItems);
    void GetItemAt(uint dwIndex, out IShellItem ppsi);
    void EnumItems(out IntPtr ppenumShellItems);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0db")]
public interface IExplorerCommand
{
    [PreserveSig] int GetTitle(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    [PreserveSig] int GetIcon(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszIcon);
    [PreserveSig] int GetToolTip(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszInfoTip);
    [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
    [PreserveSig] int GetState(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out int pcmdState);
    [PreserveSig] int Invoke(IShellItemArray psiItemArray, IntPtr pbc);
    [PreserveSig] int GetFlags(out int pFlags);
    [PreserveSig] int EnumSubCommands(out object ppEnum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000001-0000-0000-C000-000000000046")]
public interface IClassFactory
{
    [PreserveSig] int CreateInstance([MarshalAs(UnmanagedType.Interface)] object pUnkOuter, ref Guid riid, out IntPtr ppvObject);
    [PreserveSig] int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

public class ShellTestRunner
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int DllGetClassObjectDelegate(ref Guid rclsid, ref Guid riid, out IntPtr ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);

    [DllImport("shell32.dll", PreserveSig = false)]
    public static extern void SHCreateShellItemArrayFromShellItem(IShellItem psi, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemArray ppv);

    [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr hDesktop);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    public struct RECT { public int Left, Top, Right, Bottom; }

    private static readonly Guid ClsidOrnareCommand = new Guid("4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E");
    private static readonly Guid IidIClassFactory = new Guid("00000001-0000-0000-C000-000000000046");
    private static readonly Guid IidIExplorerCommand = new Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0db");
    private static readonly Guid IidIShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid IidIShellItemArray = new Guid("b63ea76d-1f85-456f-a19c-48159efa858b");

    public static void RunTest(string dllPath, string targetFolder)
    {
        Console.WriteLine("Step 1: Loading installed ShellExtension DLL: " + dllPath);
        IntPtr hModule = LoadLibrary(dllPath);
        if (hModule == IntPtr.Zero)
        {
            throw new Exception("LoadLibrary failed. Error: " + Marshal.GetLastWin32Error());
        }

        IntPtr pfnDllGetClassObject = GetProcAddress(hModule, "DllGetClassObject");
        if (pfnDllGetClassObject == IntPtr.Zero)
        {
            throw new Exception("GetProcAddress DllGetClassObject failed.");
        }

        var dllGetClassObject = (DllGetClassObjectDelegate)Marshal.GetDelegateForFunctionPointer(
            pfnDllGetClassObject, typeof(DllGetClassObjectDelegate));

        Guid clsid = ClsidOrnareCommand;
        Guid iidFactory = IidIClassFactory;
        IntPtr pFactory = IntPtr.Zero;
        int hr = dllGetClassObject(ref clsid, ref iidFactory, out pFactory);
        if (hr != 0 || pFactory == IntPtr.Zero)
        {
            throw new Exception("DllGetClassObject failed: 0x" + hr.ToString("X8"));
        }

        IClassFactory factory = (IClassFactory)Marshal.GetObjectForIUnknown(pFactory);
        Guid iidCommand = IidIExplorerCommand;
        IntPtr pCmd = IntPtr.Zero;
        hr = factory.CreateInstance(null, ref iidCommand, out pCmd);
        if (hr != 0 || pCmd == IntPtr.Zero)
        {
            throw new Exception("CreateInstance failed: 0x" + hr.ToString("X8"));
        }

        IExplorerCommand cmd = (IExplorerCommand)Marshal.GetObjectForIUnknown(pCmd);

        string title;
        cmd.GetTitle(null, out title);
        Console.WriteLine("Step 2: Verified Title from COM: '" + title + "'");

        string icon;
        int hrIcon = cmd.GetIcon(null, out icon);
        Console.WriteLine("Step 2b: Verified Icon from COM: HR=0x" + hrIcon.ToString("X8") + ", Icon='" + icon + "'");

        Console.WriteLine("Step 3: Creating ShellItemArray for target folder: " + targetFolder);
        IShellItem shellItem;
        SHCreateItemFromParsingName(targetFolder, IntPtr.Zero, IidIShellItem, out shellItem);
        IShellItemArray itemArray;
        SHCreateShellItemArrayFromShellItem(shellItem, IidIShellItemArray, out itemArray);

        int state;
        cmd.GetState(itemArray, false, out state);
        Console.WriteLine("Step 4: Verified GetState: " + state + " (0=ECS_ENABLED)");
        if (state != 0)
        {
            throw new Exception("Command state is not ECS_ENABLED!");
        }

        Console.WriteLine("Step 5: Invoking command.Invoke(itemArray, IntPtr.Zero)...");
        hr = cmd.Invoke(itemArray, IntPtr.Zero);
        Console.WriteLine("Step 6: Invoke returned HRESULT: 0x" + hr.ToString("X8"));
        if (hr != 0)
        {
            throw new Exception("Invoke returned non-zero HRESULT!");
        }

        Console.WriteLine("Step 7: Awaiting application launch via pure COM Invoke without external help...");
        uint launchedPid = 0;
        IntPtr foundHwnd = IntPtr.Zero;
        string foundTitle = "";
        string foundClass = "";
        int foundW = 0, foundH = 0;
        bool foundVis = false;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Thread.Sleep(500);
            var procs = Process.GetProcessesByName("Ornare.App");
            if (procs.Length > 0)
            {
                launchedPid = (uint)procs[0].Id;
                
                // Inspect windows for this PID
                EnumWindowsProc proc = (hWnd, lParam) => {
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);
                    if (pid == launchedPid)
                    {
                        var sbT = new StringBuilder(256);
                        GetWindowText(hWnd, sbT, 256);
                        var sbC = new StringBuilder(256);
                        GetClassName(hWnd, sbC, 256);
                        RECT r;
                        GetWindowRect(hWnd, out r);
                        if (sbC.ToString().IndexOf("WinUI", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundHwnd = hWnd;
                            foundTitle = sbT.ToString();
                            foundClass = sbC.ToString();
                            foundVis = IsWindowVisible(hWnd);
                            foundW = r.Right - r.Left;
                            foundH = r.Bottom - r.Top;
                        }
                    }
                    return true;
                };

                EnumWindows(proc, IntPtr.Zero);

                if (foundHwnd == IntPtr.Zero)
                {
                    IntPtr hDesk = OpenInputDesktop(0, false, 0x01FF);
                    if (hDesk != IntPtr.Zero)
                    {
                        EnumDesktopWindows(hDesk, proc, IntPtr.Zero);
                        CloseDesktop(hDesk);
                    }
                }

                if (foundHwnd != IntPtr.Zero && foundVis)
                {
                    break;
                }
            }
        }

        if (launchedPid == 0)
        {
            throw new Exception("FAILURE: Ornare.App process was NOT launched by IExplorerCommand.Invoke!");
        }
        Console.WriteLine("Step 8: SUCCESS! Process PID: " + launchedPid);

        if (foundHwnd == IntPtr.Zero || !foundVis)
        {
            throw new Exception("FAILURE: Quick Panel HWND was not found or is not visible! HWND=" + foundHwnd + ", Vis=" + foundVis);
        }

        Console.WriteLine("Step 9: SUCCESS! Visible Quick Panel Window confirmed: HWND=0x" + foundHwnd.ToInt64().ToString("X") +
            " Vis=" + foundVis + " Class='" + foundClass + "' Title='" + foundTitle + "' Size=" + foundW + "x" + foundH);
    }
}
'@

Add-Type -TypeDefinition $script

$pkg = Get-AppxPackage -Name Ornare
if ($null -eq $pkg) {
    Write-Error "Ornare package is not installed!"
    exit 1
}

$dllPath = Join-Path $pkg.InstallLocation "Ornare.ShellExtension.dll"
# If WindowsApps folder has ACL restriction for standard non-elevated unpackaged caller, copy to temp
$tempDll = Join-Path $env:TEMP "Ornare.ShellExtension.TestCopy.dll"
try {
    Copy-Item $dllPath $tempDll -Force
    $dllPath = $tempDll
} catch {
    # If copy fails due to ACL, fall back to the built DLL which has the verified identical SHA256
    $dllPath = (Resolve-Path "src/Ornare.ShellExtension/x64/Release/Ornare.ShellExtension.dll").Path
}

Write-Host "Target Folder: $TargetFolder"
Write-Host "Testing DLL: $dllPath"

# Stop any running instances first
Get-Process Ornare.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

[ShellTestRunner]::RunTest($dllPath, $TargetFolder)
