using System.Runtime.InteropServices;

namespace Ornare.Windows;

/// <summary>
/// Keeps the desktop shortcut in step with the icon the user picked. The shortcut targets the
/// packaged application through <c>shell:AppsFolder</c>, so only its icon location changes;
/// everything else about an existing shortcut is preserved.
/// </summary>
public static class DesktopShortcutService
{
    private const string PackageAppId = @"shell:AppsFolder\Ornare_vs3ntctdctdza8!App";

    /// <summary>Full path of the desktop shortcut for the current user.</summary>
    public static string DefaultShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Ornare.lnk");

    /// <summary>True when a shortcut file exists at <paramref name="shortcutPath"/>.</summary>
    public static bool ShortcutExists(string shortcutPath) => File.Exists(shortcutPath);

    /// <summary>
    /// Creates the shortcut when it is missing, then points its icon at <paramref name="iconPath"/>.
    /// Returns false when the shell refused the write; a missing shortcut is never a reason to
    /// fail an icon change, so callers report nothing to the user.
    /// </summary>
    public static bool EnsureShortcut(string shortcutPath, string iconPath, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);

        if (!File.Exists(iconPath))
        {
            return false;
        }

        object? instance = null;
        try
        {
            instance = new ShellLinkCoClass();
            if (instance is not IShellLinkW link)
            {
                return false;
            }

            link.SetPath("explorer.exe");
            link.SetArguments(PackageAppId);
            link.SetIconLocation(iconPath, 0);
            if (!string.IsNullOrWhiteSpace(description))
            {
                link.SetDescription(description);
            }

            if (instance is not IPersistFile persist)
            {
                return false;
            }

            persist.Save(shortcutPath, true);
            return File.Exists(shortcutPath);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
        finally
        {
            if (instance is not null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }
    }

    /// <summary>The shell link coclass, activated in-process to write a .lnk file.</summary>
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ShellLinkCoClass
    {
    }

    /// <summary>
    /// <c>IShellLinkW</c>. Every member is declared in vtable order because a single missing
    /// member would shift the rest and corrupt the call.
    /// </summary>
    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        [PreserveSig]
        int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);

        [PreserveSig]
        int GetIDList(out IntPtr ppidl);

        [PreserveSig]
        int SetIDList(IntPtr pidl);

        [PreserveSig]
        int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);

        [PreserveSig]
        int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [PreserveSig]
        int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);

        [PreserveSig]
        int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        [PreserveSig]
        int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);

        [PreserveSig]
        int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        [PreserveSig]
        int GetHotkey(out ushort pwHotkey);

        [PreserveSig]
        int SetHotkey(ushort wHotkey);

        [PreserveSig]
        int GetShowCmd(out int piShowCmd);

        [PreserveSig]
        int SetShowCmd(int iShowCmd);

        [PreserveSig]
        int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);

        [PreserveSig]
        int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        [PreserveSig]
        int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

        [PreserveSig]
        int Resolve(IntPtr hwnd, uint fFlags);

        [PreserveSig]
        int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    /// <summary>
    /// <c>IPersistFile</c>, used only for <c>Save</c>. The earlier members exist to keep the
    /// vtable layout correct.
    /// </summary>
    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        [PreserveSig]
        int GetClassID(out Guid pClassID);

        [PreserveSig]
        int IsDirty();

        [PreserveSig]
        int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

        [PreserveSig]
        int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);

        [PreserveSig]
        int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        [PreserveSig]
        int GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
