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
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlushNoWait = 0x3000; // SHCNF_FLUSH (0x1000) | SHCNF_FLUSHNOWAIT (0x2000)

    public static void NotifyFolderChanged(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        try
        {
            uint pathFlags = ShcnfPathW | ShcnfFlushNoWait;

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

                // 2. Notify target folder attributes and item changed
                fixed (char* pPath = folderPath)
                {
                    PInvoke.SHChangeNotify((SHCNE_ID)ShcneAttributes, (SHCNF_FLAGS)pathFlags, pPath, null);
                    PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateItem, (SHCNF_FLAGS)pathFlags, pPath, null);
                }

                // 3. Notify parent directory (e.g. Desktop or Explorer folder) updated so child item refreshes
                string? parentDir = Path.GetDirectoryName(folderPath.TrimEnd('\\', '/'));
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    fixed (char* pParent = parentDir)
                    {
                        PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateDir, (SHCNF_FLAGS)pathFlags, pParent, null);
                        PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateItem, (SHCNF_FLAGS)pathFlags, pParent, null);
                    }
                }
            }
        }
        catch
        {
            // Shell notification failure must not crash the app
        }
    }
}

