using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a8882650-0b6f-4791-9f13-6cadc4d79449")]
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Windows Shell COM interface name must match native IEnumExplorerCommand::Next")]
public interface IEnumExplorerCommand
{
    [PreserveSig]
    int Next(uint celt, out IntPtr pUICommand, out uint pceltFetched);

    [PreserveSig]
    int Skip(uint celt);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out IntPtr ppenum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0db")]
public interface IFlyoutExplorerCommand
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
    int EnumSubCommands(out IntPtr ppEnum);
}

public class ShellExtensionFlyoutTests
{
    private readonly ITestOutputHelper _output;
    private static readonly Guid ClsidOrnareFlyoutCommand = new("A5E650D8-04B2-4A82-9B7C-295822368940");
    private static readonly Guid IidIClassFactory = new("00000001-0000-0000-C000-000000000046");
    private const int ECF_HASSUBCOMMANDS = 0x1;
    private const int ECF_DEFAULT = 0x0;

    public ShellExtensionFlyoutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void VerifyOrnareFlyoutCommandImplementsHasSubCommandsAndEnumeratesSingleApplyLastChild()
    {
        string localDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\Ornare.ShellExtension\x64\Release\Ornare.ShellExtension.dll"));
        Assert.True(File.Exists(localDll), $"ShellExtension DLL must exist at {localDll}");

        IntPtr hModule = NativeShell.LoadLibrary(localDll);
        Assert.NotEqual(IntPtr.Zero, hModule);

        try
        {
            IntPtr pFunc = NativeShell.GetProcAddress(hModule, "DllGetClassObject");
            Assert.NotEqual(IntPtr.Zero, pFunc);

            var dllGetClassObject = Marshal.GetDelegateForFunctionPointer<NativeShell.DllGetClassObjectDelegate>(pFunc);

            // 1. Instantiate Flyout Factory
            Guid clsid = ClsidOrnareFlyoutCommand;
            Guid iidFactory = IidIClassFactory;
            int hr = dllGetClassObject(ref clsid, ref iidFactory, out IntPtr pFactory);
            Assert.Equal(0, hr);
            Assert.NotEqual(IntPtr.Zero, pFactory);

            IClassFactory factory = (IClassFactory)Marshal.GetObjectForIUnknown(pFactory);
            Marshal.Release(pFactory);

            // 2. Create Instance of Flyout Command
            Guid iidUnknown = new("00000000-0000-0000-C000-000000000046");
            hr = factory.CreateInstance(null, ref iidUnknown, out IntPtr pUnk);
            Assert.Equal(0, hr);
            Assert.NotEqual(IntPtr.Zero, pUnk);

            IFlyoutExplorerCommand flyoutCommand = (IFlyoutExplorerCommand)Marshal.GetObjectForIUnknown(pUnk);
            Marshal.Release(pUnk);

            // 3. Verify Title is "Ornare"
            hr = flyoutCommand.GetTitle(null, out string? title);
            Assert.Equal(0, hr);
            Assert.Equal("Ornare", title);

            // 4. Verify Canonical Name
            hr = flyoutCommand.GetCanonicalName(out Guid canonicalGuid);
            Assert.Equal(0, hr);
            Assert.Equal(ClsidOrnareFlyoutCommand, canonicalGuid);

            // 5. Verify Flags include ECF_HASSUBCOMMANDS (0x2)
            hr = flyoutCommand.GetFlags(out int flags);
            Assert.Equal(0, hr);
            Assert.Equal(ECF_HASSUBCOMMANDS, flags & ECF_HASSUBCOMMANDS);

            // 6. Enumerate Subcommands
            hr = flyoutCommand.EnumSubCommands(out IntPtr pEnumPtr);
            Assert.Equal(0, hr);
            Assert.NotEqual(IntPtr.Zero, pEnumPtr);

            IEnumExplorerCommand enumCmd = (IEnumExplorerCommand)Marshal.GetObjectForIUnknown(pEnumPtr);
            Marshal.Release(pEnumPtr);

            // F22 requirement: Exactly two child commands in fixed order:
            // 1. "Apply last icon" ("Son simgeyi uygula")
            // 2. "Undo" ("Geri Al")

            // Child 1: Apply last icon
            hr = enumCmd.Next(1, out IntPtr pChild1, out uint fetched1);
            Assert.Equal(0, hr); // S_OK
            Assert.Equal(1u, fetched1);
            Assert.NotEqual(IntPtr.Zero, pChild1);

            IFlyoutExplorerCommand childCmd1 = (IFlyoutExplorerCommand)Marshal.GetObjectForIUnknown(pChild1);
            Marshal.Release(pChild1);

            hr = childCmd1.GetTitle(null, out string? childTitle1);
            Assert.Equal(0, hr);
            Assert.True(childTitle1 == "Apply last icon" || childTitle1 == "Son simgeyi uygula", $"Unexpected child 1 title: {childTitle1}");

            hr = childCmd1.GetFlags(out int childFlags1);
            Assert.Equal(0, hr);
            Assert.Equal(ECF_DEFAULT, childFlags1);

            // Child 2: Undo
            hr = enumCmd.Next(1, out IntPtr pChild2, out uint fetched2);
            Assert.Equal(0, hr); // S_OK
            Assert.Equal(1u, fetched2);
            Assert.NotEqual(IntPtr.Zero, pChild2);

            IFlyoutExplorerCommand childCmd2 = (IFlyoutExplorerCommand)Marshal.GetObjectForIUnknown(pChild2);
            Marshal.Release(pChild2);

            hr = childCmd2.GetTitle(null, out string? childTitle2);
            Assert.Equal(0, hr);
            Assert.True(childTitle2 == "Undo" || childTitle2 == "Geri Al", $"Unexpected child 2 title: {childTitle2}");

            hr = childCmd2.GetFlags(out int childFlags2);
            Assert.Equal(0, hr);
            Assert.Equal(ECF_DEFAULT, childFlags2);

            // Verify no additional children in F22
            hr = enumCmd.Next(1, out IntPtr pExtra, out uint extraFetched);
            Assert.Equal(1, hr); // S_FALSE
            Assert.Equal(0u, extraFetched);
            if (pExtra != IntPtr.Zero)
            {
                Marshal.Release(pExtra);
            }

            Marshal.ReleaseComObject(childCmd1);
            Marshal.ReleaseComObject(childCmd2);
            Marshal.ReleaseComObject(enumCmd);
            Marshal.ReleaseComObject(flyoutCommand);
            Marshal.ReleaseComObject(factory);
        }
        finally
        {
            NativeShell.FreeLibrary(hModule);
        }
    }

    [Fact]
    public void VerifyMenuConstructionPerformanceWithin10MsBudget()
    {
        string localDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\Ornare.ShellExtension\x64\Release\Ornare.ShellExtension.dll"));
        IntPtr hModule = NativeShell.LoadLibrary(localDll);
        Assert.NotEqual(IntPtr.Zero, hModule);

        try
        {
            IntPtr pFunc = NativeShell.GetProcAddress(hModule, "DllGetClassObject");
            var dllGetClassObject = Marshal.GetDelegateForFunctionPointer<NativeShell.DllGetClassObjectDelegate>(pFunc);

            Guid clsid = ClsidOrnareFlyoutCommand;
            Guid iidFactory = IidIClassFactory;
            dllGetClassObject(ref clsid, ref iidFactory, out IntPtr pFactory);
            IClassFactory factory = (IClassFactory)Marshal.GetObjectForIUnknown(pFactory);
            Marshal.Release(pFactory);

            Guid iidUnknown = new("00000000-0000-0000-C000-000000000046");

            // Warm up
            factory.CreateInstance(null, ref iidUnknown, out IntPtr pWarm);
            IFlyoutExplorerCommand warmCmd = (IFlyoutExplorerCommand)Marshal.GetObjectForIUnknown(pWarm);
            Marshal.Release(pWarm);
            warmCmd.GetFlags(out _);
            warmCmd.EnumSubCommands(out IntPtr pWarmEnum);
            Marshal.Release(pWarmEnum);
            Marshal.ReleaseComObject(warmCmd);

            // Benchmark 100 iterations of menu construction
            var sw = Stopwatch.StartNew();
            const int iterations = 100;
            for (int i = 0; i < iterations; i++)
            {
                factory.CreateInstance(null, ref iidUnknown, out IntPtr pUnk);
                IFlyoutExplorerCommand cmd = (IFlyoutExplorerCommand)Marshal.GetObjectForIUnknown(pUnk);
                Marshal.Release(pUnk);

                cmd.GetTitle(null, out _);
                cmd.GetFlags(out _);
                cmd.EnumSubCommands(out IntPtr pEnum);
                IEnumExplorerCommand e = (IEnumExplorerCommand)Marshal.GetObjectForIUnknown(pEnum);
                Marshal.Release(pEnum);

                e.Next(1, out IntPtr pChild, out _);
                IFlyoutExplorerCommand c = (IFlyoutExplorerCommand)Marshal.GetObjectForIUnknown(pChild);
                Marshal.Release(pChild);
                c.GetTitle(null, out _);

                Marshal.ReleaseComObject(c);
                Marshal.ReleaseComObject(e);
                Marshal.ReleaseComObject(cmd);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            _output.WriteLine($"Menu construction average latency: {avgMs:F3} ms over {iterations} iterations");

            // Invariant: Menu construction must be strictly within 10 ms budget (typically < 0.2 ms)
            Assert.True(avgMs < 10.0, $"Average latency {avgMs:F3} ms exceeded 10 ms budget!");

            Marshal.ReleaseComObject(factory);
        }
        finally
        {
            NativeShell.FreeLibrary(hModule);
        }
    }
}
