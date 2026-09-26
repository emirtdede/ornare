using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.IntegrationTests;

public class ExplorerCommandStressTests
{
    private readonly ITestOutputHelper _output;
    private static readonly Guid ClsidOrnareCommand = new("4B1F8E1C-6C4D-4E10-9F1C-9E29DA21A77E");
    private static readonly Guid IidIClassFactory = new("00000001-0000-0000-C000-000000000046");
    private static readonly Guid IidIShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid IidIShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");

    public ExplorerCommandStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void StressTestShellExtension1000GetStateAndLeakCheck()
    {
        string localDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\Ornare.ShellExtension\x64\Release\Ornare.ShellExtension.dll"));
        Assert.True(File.Exists(localDll), $"ShellExtension DLL must exist at {localDll}");

        IntPtr hModule = NativeShell.LoadLibrary(localDll);
        Assert.NotEqual(IntPtr.Zero, hModule);

        IntPtr pFunc = NativeShell.GetProcAddress(hModule, "DllGetClassObject");
        Assert.NotEqual(IntPtr.Zero, pFunc);

        var dllGetClassObject = Marshal.GetDelegateForFunctionPointer<NativeShell.DllGetClassObjectDelegate>(pFunc);

        Guid clsid = ClsidOrnareCommand;
        Guid iidFactory = IidIClassFactory;
        int hr = dllGetClassObject(ref clsid, ref iidFactory, out IntPtr pFactory);
        Assert.Equal(0, hr);

        IClassFactory factory = (IClassFactory)Marshal.GetObjectForIUnknown(pFactory);
        Marshal.Release(pFactory);

        Guid iidUnknown = new("00000000-0000-0000-C000-000000000046");
        hr = factory.CreateInstance(null, ref iidUnknown, out IntPtr pUnk);
        Assert.Equal(0, hr);

        IExplorerCommand command = (IExplorerCommand)Marshal.GetObjectForIUnknown(pUnk);
        Marshal.Release(pUnk);

        string testFolder = Path.Combine(Path.GetTempPath(), $"ornare_stress_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testFolder);

        string testFile = Path.Combine(testFolder, "test.txt");
        File.WriteAllText(testFile, "stress content");

        try
        {
            NativeShell.SHCreateItemFromParsingName(testFolder, IntPtr.Zero, IidIShellItem, out IShellItem dirItem);
            NativeShell.SHCreateShellItemArrayFromShellItem(dirItem, IidIShellItemArray, out IShellItemArray dirArray);

            NativeShell.SHCreateItemFromParsingName(testFile, IntPtr.Zero, IidIShellItem, out IShellItem fileItem);
            NativeShell.SHCreateShellItemArrayFromShellItem(fileItem, IidIShellItemArray, out IShellItemArray fileArray);

            // Baseline process metrics
            using var currentProc = Process.GetCurrentProcess();
            currentProc.Refresh();
            long initialPrivateBytes = currentProc.PrivateMemorySize64;
            int initialHandleCount = currentProc.HandleCount;

            _output.WriteLine($"Initial Private Bytes: {initialPrivateBytes / 1024} KB, Handles: {initialHandleCount}");

            // Execute 1,000 GetState cycles
            const int iterations = 1000;
            for (int i = 0; i < iterations; i++)
            {
                // Alternating directory, file, and null
                hr = command.GetState(dirArray, fOkToBeSlow: false, out int stateDir);
                Assert.Equal(0, hr);
                Assert.Equal(0, stateDir); // ECS_ENABLED

                hr = command.GetState(fileArray, fOkToBeSlow: false, out int stateFile);
                Assert.Equal(0, hr);
                Assert.Equal(1, stateFile); // ECS_DISABLED

                hr = command.GetState(null, fOkToBeSlow: false, out int stateNull);
                Assert.Equal(0, hr);
                Assert.Equal(2, stateNull); // ECS_HIDDEN
            }

            // Verify title repeatedly
            for (int i = 0; i < 100; i++)
            {
                hr = command.GetTitle(dirArray, out string? title);
                Assert.Equal(0, hr);
                Assert.False(string.IsNullOrEmpty(title));
            }

            // Collect GC and compare metrics
            GC.Collect();
            GC.WaitForPendingFinalizers();
            currentProc.Refresh();

            long finalPrivateBytes = currentProc.PrivateMemorySize64;
            int finalHandleCount = currentProc.HandleCount;

            _output.WriteLine($"Post 1,000 cycles Private Bytes: {finalPrivateBytes / 1024} KB, Handles: {finalHandleCount}");

            // Verify no runaway handle leak (handles should not grow substantially)
            int handleIncrease = finalHandleCount - initialHandleCount;
            Assert.True(handleIncrease < 100, $"Handle leak detected: handle increase is {handleIncrease}");

            Marshal.FinalReleaseComObject(dirArray);
            Marshal.FinalReleaseComObject(dirItem);
            Marshal.FinalReleaseComObject(fileArray);
            Marshal.FinalReleaseComObject(fileItem);
        }
        finally
        {
            if (Directory.Exists(testFolder))
            {
                Directory.Delete(testFolder, true);
            }
            if (command != null) Marshal.FinalReleaseComObject(command);
            if (factory != null) Marshal.FinalReleaseComObject(factory);
        }
    }
}
