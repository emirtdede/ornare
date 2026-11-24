using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ornare.Core;

namespace Ornare.Storage.Cloud;

/// <summary>Outcome of a snapshot write. Failures are values, never exceptions to the user.</summary>
public sealed record CloudSyncWriteResult(bool Success, string? ErrorCode, int PrunedBackupFiles)
{
    public static CloudSyncWriteResult Ok(int pruned) => new(true, null, pruned);
    public static CloudSyncWriteResult Fail(string code) => new(false, code, 0);
}

/// <summary>
/// Serverless cloud backup over a locally synchronized folder (OneDrive, Google Drive,
/// Dropbox or a user-chosen directory).
/// <para>
/// The service never talks to a network: it only reads and writes files inside a folder
/// that the provider's own client already synchronizes. That keeps the feature free of
/// OAuth, secrets and server cost while still giving the user off-device recovery.
/// </para>
/// <para>
/// Writes are atomic (temp file + rename, with a copy fallback for locked files), the
/// previous snapshot is retained, and one dated backup per day is kept for
/// <see cref="CloudSyncFiles.RetainedBackupDays"/> days.
/// </para>
/// </summary>
public sealed class CloudSyncService
{
    private static readonly char[] PathSeparators = { '\\', '/' };

    /// <summary>Serialises snapshot writers inside this process.</summary>
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private readonly TimeProvider _time;
    private readonly string _fallbackRoot;

    public CloudSyncService(TimeProvider? timeProvider = null, string? fallbackRoot = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _fallbackRoot = fallbackRoot ?? DefaultFallbackRoot();
    }

    /// <summary>Local fallback used when no provider client is installed.</summary>
    public static string DefaultFallbackRoot()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, OrnareConstants.ApplicationName, "cloud-backup");
    }

    /// <summary>
    /// Detects every installed cloud client, in order: OneDrive (consumer, generic,
    /// commercial), Google Drive (Drive for desktop / My Drive folders) and Dropbox.
    /// Detection is purely local; missing clients are simply absent from the list.
    /// </summary>
    public static IReadOnlyList<CloudSyncLocation> DetectAll()
    {
        var found = new List<CloudSyncLocation>();
        foreach (CloudSyncProviderKind kind in new[]
                 {
                     CloudSyncProviderKind.OneDrive,
                     CloudSyncProviderKind.GoogleDrive,
                     CloudSyncProviderKind.Dropbox
                 })
        {
            CloudSyncLocation? location = Detect(kind);
            if (location is not null)
            {
                found.Add(location);
            }
        }

        return found;
    }

    /// <summary>Resolves the first existing root for a provider, or null when not installed.</summary>
    public static CloudSyncLocation? Detect(CloudSyncProviderKind kind)
    {
        foreach (string candidate in EnumerateCandidateRoots(kind))
        {
            if (IsUsableDirectory(candidate))
            {
                return new CloudSyncLocation(kind, Path.GetFullPath(candidate), false);
            }
        }

        return null;
    }

    /// <summary>
    /// Candidate roots for a provider, in the documented priority order. Exposed for tests
    /// and for diagnostics; callers should normally use <see cref="Detect"/>.
    /// </summary>
    public static IEnumerable<string> EnumerateCandidateRoots(CloudSyncProviderKind kind)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        switch (kind)
        {
            case CloudSyncProviderKind.OneDrive:
                foreach (string variable in new[] { "OneDriveConsumer", "OneDrive", "OneDriveCommercial" })
                {
                    string? value = Environment.GetEnvironmentVariable(variable);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
                }

                yield return Path.Combine(home, "OneDrive");
                break;

            case CloudSyncProviderKind.GoogleDrive:
                foreach (string variable in new[] { "GoogleDrive", "GOOGLE_DRIVE" })
                {
                    string? value = Environment.GetEnvironmentVariable(variable);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
                }

                yield return Path.Combine(home, "Google Drive");
                yield return Path.Combine(home, "GoogleDrive");
                yield return Path.Combine(home, "My Drive");
                foreach (string drive in SafeDriveRoots())
                {
                    yield return Path.Combine(drive, "My Drive");
                    yield return Path.Combine(drive, "Google Drive");
                }

                break;

            case CloudSyncProviderKind.Dropbox:
                string? dropboxEnv = Environment.GetEnvironmentVariable("DROPBOX_LOCAL_FOLDER");
                if (!string.IsNullOrWhiteSpace(dropboxEnv))
                {
                    yield return dropboxEnv;
                }

                yield return Path.Combine(home, "Dropbox");
                yield return Path.Combine(home, "Dropbox (Personal)");
                yield return Path.Combine(home, "Dropbox (Business)");
                break;

            case CloudSyncProviderKind.Custom:
            case CloudSyncProviderKind.None:
            default:
                break;
        }
    }

    private static IEnumerable<string> SafeDriveRoots()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (DriveInfo drive in drives)
        {
            bool ready;
            try
            {
                ready = drive.IsReady && drive.DriveType == DriveType.Fixed;
            }
            catch (IOException)
            {
                ready = false;
            }

            if (ready)
            {
                yield return drive.RootDirectory.FullName;
            }
        }
    }

    /// <summary>
    /// Resolves the folder that should hold the snapshot files for a provider. When the
    /// provider client is absent, a local fallback folder is returned and flagged.
    /// </summary>
    public CloudSyncLocation ResolveLocation(string? providerId, string? customRoot)
    {
        CloudSyncProviderKind kind = ParseProvider(providerId);

        if (kind == CloudSyncProviderKind.Custom)
        {
            if (IsSafeDirectoryPath(customRoot))
            {
                return new CloudSyncLocation(kind, Path.GetFullPath(customRoot!), false);
            }

            return new CloudSyncLocation(CloudSyncProviderKind.Custom, _fallbackRoot, true);
        }

        CloudSyncLocation? detected = Detect(kind);
        if (detected is not null)
        {
            return detected with { RootPath = Path.Combine(detected.RootPath, OrnareConstants.ApplicationName) };
        }

        if (!string.IsNullOrWhiteSpace(customRoot) && IsSafeDirectoryPath(customRoot))
        {
            return new CloudSyncLocation(kind, Path.GetFullPath(customRoot), false);
        }

        return new CloudSyncLocation(kind, _fallbackRoot, true);
    }

    /// <summary>Parses a provider identifier without throwing on unknown values.</summary>
    public static CloudSyncProviderKind ParseProvider(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return CloudSyncProviderKind.None;
        }

        return Enum.TryParse(providerId, ignoreCase: true, out CloudSyncProviderKind kind)
            ? kind
            : CloudSyncProviderKind.None;
    }

    /// <summary>
    /// True when a path is a fully qualified directory path with no parent traversal.
    /// This is the only validation gate before any file system call, so user-provided
    /// folders cannot escape into unintended locations.
    /// </summary>
    public static bool IsSafeDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 240)
        {
            return false;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        string normalized = Path.GetFullPath(path);
        foreach (string segment in path.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        foreach (string segment in normalized.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        try
        {
            return Directory.Exists(normalized);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsUsableDirectory(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Builds a snapshot from live storage. Collections and folders are metadata only.</summary>
    /// <param name="storage">Storage that owns folders, collections and settings.</param>
    /// <param name="provider">Provider the snapshot is written for.</param>
    /// <param name="appVersion">Version recorded in the snapshot header.</param>
    /// <param name="now">Export timestamp.</param>
    /// <param name="settingsOverride">
    /// Settings to export instead of the stored row. Callers pass the in-memory settings when
    /// they changed a value that has not been persisted yet, so the snapshot never records a
    /// stale sync state.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<CloudSyncSnapshot> BuildSnapshotAsync(
        IStorageService storage,
        CloudSyncProviderKind provider,
        string appVersion,
        DateTimeOffset now,
        UserSettings? settingsOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);

        UserSettings settings = settingsOverride
            ?? await storage.GetUserSettingsAsync().ConfigureAwait(false);
        IReadOnlyList<FolderAssignment> assignments = await storage.GetAllFolderAssignmentsAsync().ConfigureAwait(false);
        IReadOnlyList<LibraryCollection> collections = await storage.GetAllCollectionsAsync().ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return new CloudSyncSnapshot
        {
            FormatVersion = CloudSyncSnapshot.CurrentFormatVersion,
            App = "Ornare",
            Version = appVersion,
            Provider = provider.ToString(),
            ExportedAt = now,
            Settings = settings,
            Folders = assignments
                .Select(a => new CloudFolderSnapshot(
                    RedactFolderPath(a.FolderPath),
                    a.IconRelativePath,
                    a.Status.ToString(),
                    a.UpdatedAt))
                .ToArray(),
            Collections = collections.Select(c => c.DisplayName).ToArray()
        };
    }

    /// <summary>
    /// Records only the tail of a folder path in the snapshot. The cloud folder is read by
    /// other machines and by the provider, so the user name and the full directory structure
    /// never leave the device; the stable hash still lets two snapshots be compared.
    /// </summary>
    public static string RedactFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        string trimmed = folderPath.TrimEnd(PathSeparators);
        string leaf = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(leaf))
        {
            leaf = trimmed; // volume root, e.g. "D:\"
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(trimmed.ToUpperInvariant()));
        string fingerprint = Convert.ToHexString(hash, 0, 8);
        return $"{leaf}·{fingerprint}";
    }

    /// <summary>
    /// Writes a snapshot atomically. Returns a failure code instead of throwing so a
    /// background sync can never block or crash the UI.
    /// </summary>
    public async Task<CloudSyncWriteResult> WriteSnapshotAsync(
        CloudSyncLocation location,
        CloudSyncSnapshot snapshot,
        bool allowEmptyOverwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(snapshot);

        // Serialise writers inside this process: the debounced background sync and the
        // close-time flush could otherwise race on the same current/previous files and
        // destroy the retained previous snapshot.
        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await WriteSnapshotCoreAsync(location, snapshot, allowEmptyOverwrite, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            WriteGate.Release();
        }
    }

    private async Task<CloudSyncWriteResult> WriteSnapshotCoreAsync(
        CloudSyncLocation location,
        CloudSyncSnapshot snapshot,
        bool allowEmptyOverwrite,
        CancellationToken cancellationToken)
    {
        if (!IsSafeDirectoryPath(location.RootPath) && !TryCreateDirectory(location.RootPath))
        {
            return CloudSyncWriteResult.Fail("FolderUnavailable");
        }

        string currentPath = Path.Combine(location.RootPath, CloudSyncFiles.CurrentName);
        string previousPath = Path.Combine(location.RootPath, CloudSyncFiles.PreviousName);

        if (!allowEmptyOverwrite && snapshot.IsEmpty)
        {
            CloudSyncReadResult existing = await ReadSnapshotAsync(location, cancellationToken).ConfigureAwait(false);
            if (existing.HasSnapshot && !existing.Snapshot!.IsEmpty)
            {
                return CloudSyncWriteResult.Fail("EmptySnapshotGuard");
            }
        }

        string json = JsonSerializer.Serialize(snapshot, FolderJsonContext.Default.CloudSyncSnapshot);
        // Unique temp name: a second process must never reuse (and truncate) this writer's temp.
        string tempPath = currentPath + ".tmp-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N");

        try
        {
            await File.WriteAllTextAsync(tempPath, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            return CloudSyncWriteResult.Fail("WriteFailed");
        }

        if (File.Exists(currentPath))
        {
            try
            {
                File.Move(currentPath, previousPath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A locked provider handle must not lose the incoming snapshot; continue
                // and let the atomic replace below deal with the current file.
            }
        }

        try
        {
            File.Move(tempPath, currentPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Copy(tempPath, currentPath, overwrite: true);
                TryDelete(tempPath);
            }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
            {
                TryDelete(tempPath);
                return CloudSyncWriteResult.Fail("Locked");
            }
        }

        WriteDailyBackup(location, currentPath);
        int pruned = PruneBackups(location);
        return CloudSyncWriteResult.Ok(pruned);
    }

    /// <summary>
    /// Reads the recovery chain: current, then previous, then the newest daily backup.
    /// Corrupt or truncated files are skipped silently; an empty valid snapshot is only
    /// used when nothing richer exists.
    /// </summary>
    public static async Task<CloudSyncReadResult> ReadSnapshotAsync(
        CloudSyncLocation location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        var candidates = new List<(CloudSyncSource Source, string Path)>
        {
            (CloudSyncSource.Current, Path.Combine(location.RootPath, CloudSyncFiles.CurrentName)),
            (CloudSyncSource.Previous, Path.Combine(location.RootPath, CloudSyncFiles.PreviousName))
        };

        foreach (string backup in ListBackupFiles(location.RootPath))
        {
            candidates.Add((CloudSyncSource.DailyBackup, backup));
        }

        CloudSyncReadResult? firstValid = null;
        foreach ((CloudSyncSource source, string path) in candidates)
        {
            CloudSyncSnapshot? snapshot = await TryReadFileAsync(path, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                continue;
            }

            var result = new CloudSyncReadResult(source, snapshot, path);
            if (!snapshot.IsEmpty)
            {
                return result;
            }

            firstValid ??= result;
        }

        return firstValid ?? CloudSyncReadResult.None;
    }

    private static async Task<CloudSyncSnapshot?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            CloudSyncSnapshot? snapshot = JsonSerializer.Deserialize(json, FolderJsonContext.Default.CloudSyncSnapshot);
            if (snapshot is null || snapshot.FormatVersion <= 0)
            {
                return null;
            }

            return string.Equals(snapshot.App, "Ornare", StringComparison.Ordinal) ? snapshot : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void WriteDailyBackup(CloudSyncLocation location, string currentPath)
    {
        try
        {
            string backupPath = Path.Combine(
                location.RootPath,
                CloudSyncFiles.BackupFileName(_time.GetLocalNow()));
            File.Copy(currentPath, backupPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A missing daily backup never invalidates a successful snapshot write.
        }
    }

    /// <summary>
    /// Removes dated backups older than the retention window and caps the set at
    /// <see cref="CloudSyncFiles.RetainedBackupDays"/> files. Returns the number removed.
    /// </summary>
    public int PruneBackups(CloudSyncLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (!IsUsableDirectory(location.RootPath))
        {
            return 0;
        }

        DateTimeOffset cutoff = _time.GetLocalNow().Date.AddDays(-(CloudSyncFiles.RetainedBackupDays - 1));
        int removed = 0;
        var kept = new List<string>();

        foreach (string file in ListBackupFiles(location.RootPath))
        {
            DateTimeOffset? date = ParseBackupDate(file);
            if (date is null || date.Value.Date < cutoff)
            {
                if (TryDelete(file))
                {
                    removed++;
                }

                continue;
            }

            kept.Add(file);
        }

        // Defensive cap in case several files share a date or the clock moved backwards.
        for (int i = CloudSyncFiles.RetainedBackupDays; i < kept.Count; i++)
        {
            if (TryDelete(kept[i]))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Dated backups, newest first. Unparsable names are ignored.</summary>
    public static IReadOnlyList<string> ListBackupFiles(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            return Directory
                .EnumerateFiles(rootPath, CloudSyncFiles.BackupPrefix + "*" + CloudSyncFiles.BackupExtension)
                .Where(f => ParseBackupDate(f) is not null)
                .OrderByDescending(f => ParseBackupDate(f)!.Value)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static DateTimeOffset? ParseBackupDate(string filePath)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);
        if (!name.StartsWith(CloudSyncFiles.BackupPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string raw = name[CloudSyncFiles.BackupPrefix.Length..];
        if (DateTimeOffset.TryParseExact(
                raw,
                CloudSyncFiles.DateFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal,
                out DateTimeOffset parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>Reads the status summary shown by the Settings card.</summary>
    public static async Task<CloudSyncStatus> GetStatusAsync(
        CloudSyncLocation location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        CloudSyncReadResult read = await ReadSnapshotAsync(location, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> backups = ListBackupFiles(location.RootPath);

        DateTimeOffset? lastSync = read.Snapshot?.ExportedAt;
        if (lastSync is null && backups.Count > 0)
        {
            lastSync = ParseBackupDate(backups[0]);
        }

        return new CloudSyncStatus(
            location.Provider,
            location.RootPath,
            location.IsFallback,
            lastSync,
            backups.Count,
            read.Snapshot?.Folders.Count ?? 0,
            read.Snapshot?.Collections.Count ?? 0,
            read.Source);
    }

    private static bool TryCreateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }
}
