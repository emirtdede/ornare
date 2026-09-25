using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Ornare.Windows;

public static class ShellRefreshService
{
    private const int ShcneUpdateItem = 0x00002000;
    private const int ShcneAttributes = 0x00000800;
    private const uint ShcnfPathW = 0x0005;

    public static void NotifyFolderChanged(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        try
        {
            unsafe
            {
                fixed (char* pPath = folderPath)
                {
                    PInvoke.SHChangeNotify((SHCNE_ID)ShcneUpdateItem, (SHCNF_FLAGS)ShcnfPathW, pPath, null);
                    PInvoke.SHChangeNotify((SHCNE_ID)ShcneAttributes, (SHCNF_FLAGS)ShcnfPathW, pPath, null);
                }
            }
        }
        catch
        {
            // Shell notification failure must not crash the app
        }
    }
}
