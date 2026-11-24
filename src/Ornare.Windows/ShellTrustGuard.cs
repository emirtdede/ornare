using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// Detects and removes the two conditions that make Windows ignore an otherwise valid folder
/// customization, and reports them instead of claiming a success the user cannot see.
/// <para>
/// <b>Mark of the Web.</b> A file that arrived from the internet carries a
/// <c>Zone.Identifier</c> alternate data stream. Since the June 2026 security update Explorer
/// ignores <c>desktop.ini</c> (and can refuse to extract an icon) from such a source. Ornare
/// writes these files itself, so the mark can never be legitimate on a file it owns and is
/// removed with the documented <c>Unblock-File</c> equivalent.
/// </para>
/// <para>
/// <b>Remote location.</b> Explorer honours <c>desktop.ini</c> on a network path only when the
/// path is in a trusted zone. The product rejects UNC targets for writes, but a mapped network
/// drive or a folder that became remote after the fact still reaches this code through repair
/// and inspection, so it is reported rather than silently ignored.
/// </para>
/// </summary>
public static class ShellTrustGuard
{
    private const string ZoneIdentifierStream = ":Zone.Identifier";

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "DeleteFileW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteFileW(string lpFileName);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "PathIsNetworkPathW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PathIsNetworkPathW(string pszPath);

    /// <summary>
    /// True when the file carries a <c>Zone.Identifier</c> stream, which Explorer treats as an
    /// untrusted origin.
    /// </summary>
    public static bool HasMarkOfTheWeb(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            // The stream is readable only if it exists; an empty stream still means "blocked".
            using var stream = new FileStream(
                LongPath.ToExtended(filePath) + ZoneIdentifierStream,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable is not the same as absent. Treat an unreadable marker as present so
            // the caller reports a possible risk instead of a false "will render" answer.
            return true;
        }
    }

    /// <summary>
    /// Deletes the <c>Zone.Identifier</c> stream, the programmatic equivalent of
    /// <c>Unblock-File</c>. Returns true when the file is free of the mark afterwards.
    /// </summary>
    public static bool ClearMarkOfTheWeb(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return true;
        }

        if (!HasMarkOfTheWeb(filePath))
        {
            return true;
        }

        // Removing a stream from a read-only or hidden file fails with access denied. The
        // attribute is restored immediately afterwards so the caller's invariants still hold.
        bool removed = DeleteFileW(LongPath.ToExtended(filePath) + ZoneIdentifierStream);
        if (removed)
        {
            return true;
        }

        FileAttributes original = File.GetAttributes(filePath);
        try
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
            return DeleteFileW(LongPath.ToExtended(filePath) + ZoneIdentifierStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Clearing Mark of the Web failed for a file: {ex.GetType().Name}");
            return false;
        }
        finally
        {
            try
            {
                File.SetAttributes(filePath, original);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Restoring attributes after unblock failed: {ex.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// True when the folder is reached through a network path (UNC or a mapped network drive).
    /// </summary>
    public static bool IsRemoteLocation(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        try
        {
            return PathIsNetworkPathW(LongPath.ToExtended(folderPath));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // shlwapi is present on every supported Windows build; a probe failure must not
            // turn into a false "remote" verdict that scares the user.
            Trace.TraceWarning("PathIsNetworkPathW unavailable.");
            return false;
        }
    }

    /// <summary>
    /// Classifies the presentation risk of a folder whose <c>desktop.ini</c> Ornare manages.
    /// The returned enum is user-visible only through localized copy; technical detail stays in
    /// the diagnostic log.
    /// </summary>
    public static FolderPresentationRisk Diagnose(string folderPath, string iniPath)
    {
        if (HasMarkOfTheWeb(iniPath))
        {
            return FolderPresentationRisk.MarkOfTheWeb;
        }

        return IsRemoteLocation(folderPath)
            ? FolderPresentationRisk.RemoteLocation
            : FolderPresentationRisk.None;
    }

    /// <summary>
    /// Removes the Mark of the Web from files Ornare just wrote or copied into the target
    /// folder, and reports a remaining risk. Called at the single point where a customization
    /// becomes durable, so no write path can forget it.
    /// </summary>
    public static FolderPresentationRisk NormalizeOwnedFiles(string folderPath, params string[] ownedFiles)
    {
        foreach (string file in ownedFiles)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            if (!ClearMarkOfTheWeb(file))
            {
                Trace.TraceWarning("An Ornare-owned file still carries the Mark of the Web.");
            }
        }

        string iniPath = Path.Combine(folderPath, OrnareConstants.DesktopIniFileName);
        return Diagnose(folderPath, iniPath);
    }
}
