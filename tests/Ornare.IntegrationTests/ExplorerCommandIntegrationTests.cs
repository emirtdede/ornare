using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

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
    [PreserveSig]
    int GetTitle(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszName);

    [PreserveSig]
    int GetIcon(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszIcon);

    [PreserveSig]
    int GetToolTip(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszInfoTip);

    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);

    [PreserveSig]
    int GetState(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out int pcmdState);

    [PreserveSig]
    int Invoke(IShellItemArray? psiItemArray, IntPtr pbc);

    [PreserveSig]
    int GetFlags(out int pFlags);

    [PreserveSig]
    int EnumSubCommands(out object? ppEnum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000001-0000-0000-C000-000000000046")]
public interface IClassFactory
{
    [PreserveSig]
    int CreateInstance([MarshalAs(UnmanagedType.Interface)] object? pUnkOuter, ref Guid riid, out IntPtr ppvObject);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

internal static class NativeShell
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeLibrary(IntPtr hModule);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int DllGetClassObjectDelegate(ref Guid rclsid, ref Guid riid, out IntPtr ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IShellItem ppv);

    [DllImport("shell32.dll", PreserveSig = false)]
    public static extern void SHCreateShellItemArrayFromShellItem(
        IShellItem psi,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IShellItemArray ppv);
}

public class ExplorerCommandIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private static readonly Guid ClsidOrnareCommand = new("4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E");
    private static readonly Guid IidIClassFactory = new("00000001-0000-0000-C000-000000000046");
    private static readonly Guid IidIExplorerCommand = new("a08ce4d0-fa25-44ab-b57c-c7b1c323e0db");
    private static readonly Guid IidIShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid IidIShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");

    public ExplorerCommandIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void VerifyNativeShellExtensionInProcess()
    {
        // 1. Locate Ornare.ShellExtension.dll
        string localDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\Ornare.ShellExtension\x64\Release\Ornare.ShellExtension.dll"));
        string dllPath = localDll;
        Assert.True(File.Exists(dllPath), $"ShellExtension DLL must exist at {dllPath}");
        _output.WriteLine($"Loading ShellExtension DLL from: {dllPath}");

        IntPtr hModule = NativeShell.LoadLibrary(dllPath);
        Assert.NotEqual(IntPtr.Zero, hModule);

        try
        {
            IntPtr pFunc = NativeShell.GetProcAddress(hModule, "DllGetClassObject");
            Assert.NotEqual(IntPtr.Zero, pFunc);

            var dllGetClassObject = Marshal.GetDelegateForFunctionPointer<NativeShell.DllGetClassObjectDelegate>(pFunc);

            Guid clsid = ClsidOrnareCommand;
            Guid iidFactory = IidIClassFactory;
            int hr = dllGetClassObject(ref clsid, ref iidFactory, out IntPtr pFactory);
            Assert.Equal(0, hr);
            Assert.NotEqual(IntPtr.Zero, pFactory);

            IClassFactory factory = (IClassFactory)Marshal.GetObjectForIUnknown(pFactory);
            Marshal.Release(pFactory);

            Guid iidUnknown = new("00000000-0000-0000-C000-000000000046");
            hr = factory.CreateInstance(null, ref iidUnknown, out IntPtr pUnk);
            _output.WriteLine($"CreateInstance(IUnknown) hr: 0x{hr:X8}");
            Assert.Equal(0, hr);
            Assert.NotEqual(IntPtr.Zero, pUnk);

            IExplorerCommand command = (IExplorerCommand)Marshal.GetObjectForIUnknown(pUnk);
            Marshal.Release(pUnk);

            // 2. Canonical Name
            hr = command.GetCanonicalName(out Guid canonicalGuid);
            Assert.Equal(0, hr);
            Assert.Equal(ClsidOrnareCommand, canonicalGuid);
            _output.WriteLine($"Canonical Name CLSID verified: {canonicalGuid}");

            // 3. Title (English or Turkish)
            hr = command.GetTitle(null, out string? title);
            Assert.Equal(0, hr);
            Assert.False(string.IsNullOrWhiteSpace(title));
            _output.WriteLine($"Title verified: '{title}'");
            Assert.True(title == "Set Folder Icon" || title == "Klasör Simgesini Ayarla");

            // 4. ShellItemArray and GetState for directory
            string testFolder = Path.Combine(Path.GetTempPath(), $"ornare_cmd_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testFolder);

            try
            {
                NativeShell.SHCreateItemFromParsingName(testFolder, IntPtr.Zero, IidIShellItem, out IShellItem shellItem);
                NativeShell.SHCreateShellItemArrayFromShellItem(shellItem, IidIShellItemArray, out IShellItemArray itemArray);

                itemArray.GetCount(out uint itemCount);
                _output.WriteLine($"ShellItemArray count: {itemCount}");
                Assert.Equal(1u, itemCount);

                hr = command.GetState(itemArray, fOkToBeSlow: false, out int cmdState);
                Assert.Equal(0, hr);
                Assert.Equal(0, cmdState); // ECS_ENABLED = 0
                _output.WriteLine($"GetState on valid directory verified: {cmdState} (ECS_ENABLED)");

                // 5. Invoke launches Ornare
                hr = command.Invoke(itemArray, IntPtr.Zero);
                Assert.Equal(0, hr);
                _output.WriteLine($"Invoke completed: HRESULT {hr}");

                Thread.Sleep(2000);
                var procs = Process.GetProcessesByName("Ornare.App");
                _output.WriteLine($"Ornare.App running instances: {procs.Length}");
                Assert.True(procs.Length > 0, "Packaged Ornare.App must launch following IExplorerCommand.Invoke");

                foreach (var p in procs)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit();
                    }
                    catch { }
                }
                // Test GetState on file (must be ECS_DISABLED = 1)
                string testFile = Path.Combine(testFolder, "sample_file.txt");
                File.WriteAllText(testFile, "hello");
                NativeShell.SHCreateItemFromParsingName(testFile, IntPtr.Zero, IidIShellItem, out IShellItem fileItem);
                NativeShell.SHCreateShellItemArrayFromShellItem(fileItem, IidIShellItemArray, out IShellItemArray fileArray);
                hr = command.GetState(fileArray, fOkToBeSlow: false, out int fileState);
                Assert.Equal(0, hr);
                Assert.Equal(1, fileState); // ECS_DISABLED = 1
                _output.WriteLine($"GetState on file verified: {fileState} (ECS_DISABLED)");

                // Test GetState on null array (must be ECS_HIDDEN = 2)
                hr = command.GetState(null, fOkToBeSlow: false, out int nullState);
                Assert.Equal(0, hr);
                Assert.Equal(2, nullState); // ECS_HIDDEN = 2
                _output.WriteLine($"GetState on null verified: {nullState} (ECS_HIDDEN)");
            }
            finally
            {
                if (Directory.Exists(testFolder))
                {
                    Directory.Delete(testFolder, true);
                }
            }
        }
        finally
        {
            NativeShell.FreeLibrary(hModule);
        }
    }
}
