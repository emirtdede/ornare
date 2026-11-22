using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Ornare.Windows;

public static class ShellRefreshService
{
    private const int ShcneUpdateItem = 0x00002000;
    private const int ShcneAttributes = 0x00000800;
    private const int ShcneUpdateDir = 0x00001000;
    private const int ShcneAssocChanged = 0x08000000;

    private const uint ShcnfIdList = 0x0000;
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlush = 0x1000; // SHCNF_FLUSH synchronous delivery

    private const int CSIDL_DESKTOP = 0x0000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFOLDERCUSTOMSETTINGS
    {
        public uint dwSize;
        public uint dwMask;
        public IntPtr pvid;
        public string? pszWebViewTemplate;
        public uint cchWebViewTemplate;
        public string? pszWebViewTemplateVersion;
        public string? pszInfoTip;
        public uint cchInfoTip;
        public IntPtr pclsid;
        public uint dwFlags;
        public string? pszIconFile;
        public uint cchIconFile;
        public int iIconIndex;
        public string? pszLogo;
        public uint cchLogo;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int SHGetSetFolderCustomSettings(ref SHFOLDERCUSTOMSETTINGS pfcs, string pszPath, uint dwReadWrite);

    [DllImport("shell32.dll")]
    private static extern int SHGetSpecialFolderLocation(IntPtr hwnd, int csidl, out IntPtr ppidl);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    internal const uint FcsForceWrite = 0x00000002;
    internal const uint FcsmIconFile = 0x00000010;

    public static void NotifyFolderChanged(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        try
        {
            // Every notification in this method is a cache-invalidation hint, not a
            // state transition: the durable state (desktop.ini, ICO file, journal row)
            // is already committed before this runs. SHCNF_FLUSH makes SHChangeNotify
            // block until each top-level window has synchronously processed the event;
            // measured on the reference machine that cost 83 ms (item update), 14 ms
            // (attributes), 19 ms (parent dir) and 176-429 ms (global invalidation) of
            // the apply path. Delivering them asynchronously keeps Explorer responsive
            // and does not change the final visible result, because Explorer re-reads
            // the folder when it processes the queued event.
            uint pathFlags = ShcnfPathW;
            uint idFlags = ShcnfIdList;

            unsafe
            {
                // 1. Notify desktop.ini updated to force ShellClassInfo parser to reload icon resource
                string iniPath = Path.Combine(folderPath, "desktop.ini");
                if (File.Exists(iniPath))
                {
                    fixed (char* pIni = iniPath)
                    {
                        PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateItem, (SHCNF_FLAGS)pathFlags, pIni, null);
                    }
                }

                // 2. Notify target folder item and attributes changed
                fixed (char* pPath = folderPath)
                {
                    PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateItem, (SHCNF_FLAGS)pathFlags, pPath, null);
                    PInvoke.SHChangeNotify((SHCNE_ID)ShcneAttributes, (SHCNF_FLAGS)pathFlags, pPath, null);
                }

                // 3. Notify parent directory updated so child item views refresh
                string? parentDir = Path.GetDirectoryName(folderPath.TrimEnd('\\', '/'));
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    fixed (char* pParent = parentDir)
                    {
                        PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateDir, (SHCNF_FLAGS)pathFlags, pParent, null);
                    }
                }

                // 4. Invalidate desktop view directly if folder resides on desktop
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(parentDir) &&
                    (string.Equals(parentDir, desktopDir, StringComparison.OrdinalIgnoreCase) ||
                     parentDir.EndsWith(@"\Desktop", StringComparison.OrdinalIgnoreCase)))
                {
                    if (SHGetSpecialFolderLocation(IntPtr.Zero, CSIDL_DESKTOP, out IntPtr desktopPidl) == 0 && desktopPidl != IntPtr.Zero)
                    {
                        try
                        {
                            PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateDir, (SHCNF_FLAGS)idFlags, (void*)desktopPidl, null);
                        }
                        finally
                        {
                            CoTaskMemFree(desktopPidl);
                        }
                    }
                }

                // 5. Invalidate Shell icon and thumbnail cache so Explorer views (including Desktop) redraw immediately
                PInvoke.SHChangeNotify((SHCNE_ID)ShcneAssocChanged, (SHCNF_FLAGS)idFlags, null, null);
            }
        }
        catch
        {
            // Shell notification failure must not crash the app
        }
    }
}

