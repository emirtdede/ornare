using System;
using System.IO;
using System.Runtime.InteropServices;
using Ornare.Core;
using Ornare.Storage;

namespace Ornare.Cli;

/// <summary>
/// Resolves the data root the CLI must use so it always shares state with the installed app.
/// <para>
/// A packaged process is redirected by Windows to
/// <c>%LOCALAPPDATA%\Packages\&lt;family&gt;\LocalCache\Local\Ornare</c>. A raw executable is
/// not redirected, so without this step the two processes write two databases for the same
/// user and each one is invisible to the other. The CLI therefore adopts the installed
/// package's root before the first storage object is constructed, and preserves anything the
/// old root already held.
/// </para>
/// </summary>
internal static class PackageDataRoot
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    /// <summary>
    /// Sets <see cref="OrnareConstants.DataRootOverrideVariable"/> when the CLI is running
    /// outside its package but the package is installed. A no-op when already packaged, when
    /// the caller supplied an explicit override, or when no installed package is found.
    /// </summary>
    public static void AdoptInstalledPackageRoot()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OrnareConstants.DataRootOverrideVariable)))
        {
            return;
        }

        if (IsPackaged())
        {
            return;
        }

        string? root = FindInstalledPackageRoot();
        if (root is null)
        {
            return;
        }

        Environment.SetEnvironmentVariable(OrnareConstants.DataRootOverrideVariable, root);
        MigrateLegacyDatabase(root);
    }

    private static bool IsPackaged()
    {
        int length = 0;
        int result = GetCurrentPackageFullName(ref length, null);
        return result != APPMODEL_ERROR_NO_PACKAGE;
    }

    /// <summary>
    /// Merges <c>%LOCALAPPDATA%\Ornare\data\ornare.db</c> into the packaged database and retires
    /// it, so folders customized through the old CLI stay visible, repairable and undoable in
    /// the app. Failures are swallowed on purpose: the packaged root is still the correct place
    /// to work, and a legacy file that cannot be merged is left untouched for a manual rescue.
    /// </summary>
    private static void MigrateLegacyDatabase(string packageRoot)
    {
        try
        {
            string legacyRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                OrnareConstants.ApplicationName);

            if (string.Equals(
                    Path.GetFullPath(legacyRoot).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string legacyDatabase = Path.Combine(legacyRoot, "data", "ornare.db");
            if (!File.Exists(legacyDatabase))
            {
                return;
            }

            string packageDatabase = Path.Combine(packageRoot, "data", "ornare.db");
            int merged = LegacyDataRootReconciliation.Reconcile(legacyDatabase, packageDatabase);
            LegacyDataRootReconciliation.Retire(legacyDatabase);

            // A one-line notice belongs on the console only when a person is watching it.
            // Redirected stderr is usually a script, and a script parsing JSON must not have a
            // migration note mixed into its error stream.
            System.Diagnostics.Trace.TraceInformation(
                "Legacy data root merged: {0} row(s) into {1}.", merged, packageDatabase);

            if (!Console.IsErrorRedirected)
            {
                Console.Error.WriteLine(
                    $"ornare: merged {merged} row(s) from the legacy data root into the installed app's database.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Best effort. The CLI still works against the packaged root.
        }
    }

    /// <summary>
    /// Scans <c>%LOCALAPPDATA%\Packages</c> for the Ornare package's redirected data root.
    /// The published family name is tried first; a scan is the fallback for a differently
    /// signed build. More than one match means there is no correct single answer, so the CLI
    /// leaves the default root alone rather than guess.
    /// </summary>
    private static string? FindInstalledPackageRoot()
    {
        try
        {
            string packagesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages");

            if (!Directory.Exists(packagesRoot))
            {
                return null;
            }

            string preferred = Path.Combine(
                packagesRoot,
                OrnareConstants.PackageFamilyName,
                "LocalCache",
                "Local",
                OrnareConstants.ApplicationName);

            if (Directory.Exists(preferred))
            {
                return preferred;
            }

            string? match = null;
            foreach (string familyDir in Directory.EnumerateDirectories(packagesRoot, "Ornare_*"))
            {
                string candidate = Path.Combine(
                    familyDir,
                    "LocalCache",
                    "Local",
                    OrnareConstants.ApplicationName);

                if (!Directory.Exists(candidate))
                {
                    continue;
                }

                if (match is not null)
                {
                    // Two installs: do not silently pick one.
                    return null;
                }

                match = candidate;
            }

            return match;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
