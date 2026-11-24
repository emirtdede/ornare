using System.Diagnostics;
using System.IO;
using System.Text;
using Ornare.Core;

namespace Ornare.Windows;

/// <summary>
/// The only place a <c>desktop.ini</c> is written.
/// <para>
/// Three invariants meet here, and every caller gets all three by construction instead of by
/// remembering to repeat them:
/// </para>
/// <list type="number">
/// <item><b>Atomic.</b> Content goes to a sibling temp file, is flushed to disk, then committed
/// with a same-volume write-through rename. A crash can never leave a truncated or zero-byte
/// <c>desktop.ini</c> (INV-FS-002).</item>
/// <item><b>Encoding preserved.</b> The merge reads the file with its original encoding and
/// encodes the result with the same one, promoting to UTF-8 only when a character cannot be
/// represented (INV-FS-002).</item>
/// <item><b>Trusted origin.</b> The freshly written file cannot legitimately carry a Mark of the
/// Web, so any inherited marker is removed before the shell is asked to render it
/// (INV-FS-007).</item>
/// </list>
/// </summary>
internal static class DesktopIniFileWriter
{
    private const int MergeAttempts = 3;

    /// <summary>
    /// Encoding used when the folder has no <c>desktop.ini</c> yet. Explorer has always read
    /// UTF-16 LE with a BOM, and it is the only encoding that round-trips every user name.
    /// </summary>
    private static readonly DesktopIniReadResult EmptySnapshot =
        new(string.Empty, Encoding.Unicode, EmitPreamble: true);

    /// <summary>
    /// Merges Ornare semantics into the existing file and commits it atomically, retrying when
    /// another program changed the file between the read and the commit.
    /// </summary>
    public static OperationResult WriteMerged(string iniPath, Func<string, string> merge)
        => MergeAndCommit(iniPath, merge, deleteWhenEmpty: false);

    /// <summary>
    /// Restores the prior semantics and deletes <c>desktop.ini</c> when nothing but Ornare's own
    /// icon semantics remained, which is what a folder looked like before Ornare first touched
    /// it. Uses the same optimistic retry as <see cref="WriteMerged"/>.
    /// </summary>
    public static OperationResult WriteMergedOrDelete(string iniPath, Func<string, string> merge)
        => MergeAndCommit(iniPath, merge, deleteWhenEmpty: true);

    private static OperationResult MergeAndCommit(
        string iniPath,
        Func<string, string> merge,
        bool deleteWhenEmpty)
    {
        for (int attempt = 0; attempt < MergeAttempts; attempt++)
        {
            bool exists = File.Exists(iniPath);
            DesktopIniReadResult snapshot;

            try
            {
                snapshot = exists ? DesktopIniParser.ReadFileWithEncoding(iniPath) : EmptySnapshot;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return OperationResult.Failure(
                    ErrorCode.FilesystemError,
                    "The folder configuration file could not be read.",
                    ex.ToString());
            }

            // Optimistic concurrency: refuse to overwrite a file that changed under us. The
            // re-read happens before the commit so a concurrent writer is never clobbered.
            if (exists)
            {
                try
                {
                    if (!string.Equals(
                            snapshot.Text,
                            DesktopIniParser.ReadFileWithEncoding(iniPath).Text,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return OperationResult.Failure(
                        ErrorCode.FilesystemError,
                        "The folder configuration file could not be read.",
                        ex.ToString());
                }
            }

            string merged = merge(snapshot.Text);

            if (deleteWhenEmpty && string.IsNullOrWhiteSpace(merged))
            {
                if (exists && !TryDelete(iniPath, out string? deleteError))
                {
                    return OperationResult.Failure(
                        ErrorCode.FilesystemError,
                        "The folder configuration file could not be removed.",
                        deleteError);
                }

                return OperationResult.Success();
            }

            if (!TryCommit(iniPath, DesktopIniParser.EncodeForWrite(merged, snapshot), out string? error))
            {
                return OperationResult.Failure(
                    ErrorCode.FilesystemError,
                    "The folder configuration file could not be updated.",
                    error);
            }

            return Finish(iniPath);
        }

        return OperationResult.Failure(
            ErrorCode.ConcurrencyConflict,
            "desktop.ini was changed by another program while it was being updated. Try again.");
    }

    private static OperationResult Finish(string iniPath)
    {
        if (!ShellTrustGuard.ClearMarkOfTheWeb(iniPath))
        {
            Trace.TraceWarning("desktop.ini still carries the Mark of the Web after writing.");
        }

        return OperationResult.Success();
    }

    private static bool TryDelete(string path, out string? error)
    {
        error = null;
        try
        {
            // desktop.ini is hidden+system while Ornare owns it; ReadOnly would block delete.
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.ToString();
            return false;
        }
    }

    private static bool TryCommit(string path, byte[] payload, out string? error)
    {
        error = null;
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        string tempPath = Path.Combine(directory, $".ornare-ini-{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Flush(flushToDisk: true);
            }

            if (!AtomicFileWriter.Commit(tempPath, fullPath, out int win32Error))
            {
                error = $"Atomic desktop.ini commit failed (Win32 error {win32Error}).";
                return false;
            }

            File.SetAttributes(fullPath, FileAttributes.Hidden | FileAttributes.System);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.ToString();
            return false;
        }
        finally
        {
            // The temp file only survives when the commit failed; leaving it behind would
            // pollute the user's folder with a hidden artefact.
            if (File.Exists(tempPath))
            {
                try
                {
                    File.SetAttributes(tempPath, FileAttributes.Normal);
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Trace.TraceWarning($"Stale desktop.ini temp cleanup failed: {ex.GetType().Name}");
                }
            }
        }
    }
}
