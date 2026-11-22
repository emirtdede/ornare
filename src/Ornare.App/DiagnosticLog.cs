using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ornare.App;

/// <summary>
/// Local-first, privacy-minimized diagnostic sink as required by
/// <c>docs/LOGGING_DIAGNOSTICS.md</c>.
/// <para>
/// Keeps a bounded in-memory ring buffer of recent events and redacts user folder
/// paths before they are stored. Verbose persistent logging is opt-in only
/// (temporary diagnostic session), is size-capped, and never runs on the UI thread.
/// </para>
/// </summary>
internal static class DiagnosticLog
{
    private const int RingCapacity = 256;
    private const int MaxPendingFileLines = 4096;
    private const long MaxLogFileBytes = 512 * 1024;
    private const string SessionVariableName = "ORNARE_DIAGNOSTIC_SESSION";

    /// <summary>
    /// Matches drive-rooted or UNC absolute paths so they can be reduced to a
    /// non-identifying placeholder before the entry is retained.
    /// </summary>
    private static readonly Regex AbsolutePathRegex = new(
        @"(?:[A-Za-z]:[\\/]|\\\\)[^\s""'<>|:*?\r\n]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    /// <summary>
    /// Matches the account segment of well-known user profile roots.
    /// </summary>
    private static readonly Regex UserSegmentRegex = new(
        @"(?<=(?:Users|home)[\\/])[^\\/\s""'<>|]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(50));

    private static readonly ConcurrentQueue<string> Ring = new();
    private static readonly ConcurrentQueue<string> PendingFileLines = new();
    private static readonly object FileGate = new();

    private static int _writerRunning;

    /// <summary>
    /// True only when a temporary diagnostic session was explicitly requested.
    /// Release builds default to off so no verbose path-bearing log is produced.
    /// </summary>
    public static bool PersistentSessionEnabled { get; } = ResolvePersistentSession();

    public static void Write(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string sanitized = Redact(message);

        Ring.Enqueue(sanitized);
        while (Ring.Count > RingCapacity && Ring.TryDequeue(out _))
        {
            // Trim oldest entries; the buffer is intentionally bounded.
        }

        if (!PersistentSessionEnabled)
        {
            return;
        }

        PendingFileLines.Enqueue($"[{DateTime.UtcNow:O}] [PID:{Environment.ProcessId}] {sanitized}");

        // The disk queue is bounded too: a storage stall or a pathological log storm must
        // not grow memory without limit while the writer thread is busy. Oldest entries
        // are dropped first; the ring buffer already retains the most recent context.
        while (PendingFileLines.Count > MaxPendingFileLines && PendingFileLines.TryDequeue(out _))
        {
            // Drop the oldest queued line; recent context is what matters for diagnosis.
        }

        ScheduleDiskFlush();
    }

    /// <summary>
    /// Returns a sanitized snapshot of the in-memory ring buffer for diagnostic export.
    /// </summary>
    public static IReadOnlyList<string> Snapshot() => [.. Ring];

    /// <summary>
    /// Replaces absolute paths and user-profile segments with stable placeholders.
    /// </summary>
    public static string Redact(string message)
    {
        string redacted;
        try
        {
            redacted = AbsolutePathRegex.Replace(message, "<path>");
            redacted = UserSegmentRegex.Replace(redacted, "<REDACTED_USER>");
        }
        catch (RegexMatchTimeoutException)
        {
            return "<unredactable diagnostic message>";
        }

        return redacted;
    }

    /// <summary>
    /// Attempts to flush queued entries. Called on shutdown; never throws.
    /// </summary>
    public static void Flush()
    {
        if (!PersistentSessionEnabled)
        {
            return;
        }

        try
        {
            DrainToDisk();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never take the process down.
        }
    }

    private static void ScheduleDiskFlush()
    {
        if (Interlocked.CompareExchange(ref _writerRunning, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                DrainToDisk();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The writer must keep the process alive; the queue is bounded.
            }
            finally
            {
                Interlocked.Exchange(ref _writerRunning, 0);

                // Entries queued while the writer was busy need one more pass.
                if (!PendingFileLines.IsEmpty)
                {
                    ScheduleDiskFlush();
                }
            }
        });
    }

    private static void DrainToDisk()
    {
        if (PendingFileLines.IsEmpty)
        {
            return;
        }

        string directory = Ornare.Core.OrnareConstants.DiagnosticsDirectory;
        Directory.CreateDirectory(directory);

        string logPath = Path.Combine(directory, "diagnostic-session.log");
        RotateIfNeeded(logPath);

        var builder = new StringBuilder();
        while (PendingFileLines.TryDequeue(out string? line))
        {
            builder.AppendLine(line);
        }

        if (builder.Length == 0)
        {
            return;
        }

        lock (FileGate)
        {
            File.AppendAllText(logPath, builder.ToString(), Encoding.UTF8);
        }
    }

    private static void RotateIfNeeded(string logPath)
    {
        var info = new FileInfo(logPath);
        if (!info.Exists || info.Length < MaxLogFileBytes)
        {
            return;
        }

        lock (FileGate)
        {
            string previousPath = Path.Combine(
                info.DirectoryName ?? string.Empty,
                "diagnostic-session.previous.log");

            try
            {
                File.Move(logPath, previousPath, overwrite: true);
            }
            catch (IOException)
            {
                // If the previous file is locked we keep appending to the current one.
            }
        }
    }

    private static bool ResolvePersistentSession()
    {
        string? flag = Environment.GetEnvironmentVariable(SessionVariableName);
        return string.Equals(flag, "1", StringComparison.Ordinal)
            || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }
}
