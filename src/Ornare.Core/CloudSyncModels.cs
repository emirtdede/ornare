using System;
using System.Collections.Generic;

namespace Ornare.Core;

/// <summary>Cloud folder providers that can be detected locally without any account or server.</summary>
public enum CloudSyncProviderKind
{
    None = 0,
    OneDrive = 1,
    GoogleDrive = 2,
    Dropbox = 3,
    Custom = 4
}

/// <summary>A resolved, writable synchronization root plus how it was found.</summary>
/// <param name="Provider">Provider that produced this root.</param>
/// <param name="RootPath">Absolute path of the folder that holds the snapshot files.</param>
/// <param name="IsFallback">True when the provider client was not installed and a local fallback folder is used.</param>
public sealed record CloudSyncLocation(CloudSyncProviderKind Provider, string RootPath, bool IsFallback)
{
    /// <summary>Stable provider identifier used in settings and snapshots.</summary>
    public string ProviderId => Provider.ToString();
}

/// <summary>Minimal, portable record of a customized folder inside a snapshot.</summary>
public sealed record CloudFolderSnapshot(string FolderPath, string IconRelativePath, string Status, DateTimeOffset UpdatedAt);

/// <summary>
/// The payload written to the cloud folder. Deliberately limited to portable user intent:
/// preferences, customized folder records and collection names. Binary icon caches are not
/// synchronized because they are reproducible from the recipe stored in the local database.
/// </summary>
public sealed record CloudSyncSnapshot
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public string App { get; init; } = "Ornare";
    public string Version { get; init; } = "1.0.0";
    public string Provider { get; init; } = nameof(CloudSyncProviderKind.None);
    public DateTimeOffset ExportedAt { get; init; }
    public UserSettings Settings { get; init; } = new();
    public IReadOnlyList<CloudFolderSnapshot> Folders { get; init; } = Array.Empty<CloudFolderSnapshot>();
    public IReadOnlyList<string> Collections { get; init; } = Array.Empty<string>();

    /// <summary>
    /// True when the snapshot carries no real user intent. Overwriting a non-empty backup
    /// with an empty snapshot is the data-loss scenario this flag exists to prevent.
    /// </summary>
    public bool IsEmpty => Folders.Count == 0 && Collections.Count == 0 && Settings.IsDefault();
}

/// <summary>Where a snapshot was read from, in recovery-chain order.</summary>
public enum CloudSyncSource
{
    None = 0,
    Current = 1,
    Previous = 2,
    DailyBackup = 3
}

/// <summary>Result of reading the recovery chain. Never throws for corrupt files.</summary>
public sealed record CloudSyncReadResult(CloudSyncSource Source, CloudSyncSnapshot? Snapshot, string? FilePath)
{
    public bool HasSnapshot => Snapshot is not null && Source != CloudSyncSource.None;

    public static CloudSyncReadResult None { get; } = new(CloudSyncSource.None, null, null);
}

/// <summary>Summary shown in the Settings synchronization card.</summary>
public sealed record CloudSyncStatus(
    CloudSyncProviderKind Provider,
    string RootPath,
    bool IsFallback,
    DateTimeOffset? LastSyncUtc,
    int BackupFileCount,
    int FolderCount,
    int CollectionCount,
    CloudSyncSource LastReadSource);

/// <summary>Stable file names used inside a synchronization root.</summary>
public static class CloudSyncFiles
{
    public const string CurrentName = "ornare-cloud-sync.json";
    public const string PreviousName = "ornare-cloud-previous.json";
    public const string BackupPrefix = "ornare-backup-";
    public const string BackupExtension = ".json";
    public const string DateFormat = "yyyy-MM-dd";
    public const int RetainedBackupDays = 15;

    /// <summary>Builds the deterministic daily backup file name for a local date.</summary>
    public static string BackupFileName(DateTimeOffset localDate) =>
        BackupPrefix + localDate.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture) + BackupExtension;
}

