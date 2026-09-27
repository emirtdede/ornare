using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Ornare.Core;

namespace Ornare.Imaging.Emblems;

/// <summary>
/// Authoritative curated local vector icon catalog (Section 18 &amp; 19).
/// Provides lazy initialization, zero network calls, instant local search, and deterministic ID resolution.
/// </summary>
public static class VectorEmblemCatalog
{
    private static readonly Lazy<VectorEmblemManifest> ManifestLazy = new(LoadManifestInternal, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<Dictionary<string, VectorEmblemEntry>> IndexByIdLazy = new(() =>
    {
        var manifest = ManifestLazy.Value;
        var dict = new Dictionary<string, VectorEmblemEntry>(manifest.Emblems.Count * 2, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Emblems)
        {
            dict[entry.Id] = entry;
            if (!string.IsNullOrWhiteSpace(entry.ShortId) && !dict.ContainsKey(entry.ShortId))
            {
                dict[entry.ShortId] = entry;
            }
        }
        return dict;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Indicates whether the catalog manifest has been loaded into memory.
    /// </summary>
    public static bool IsLoaded => ManifestLazy.IsValueCreated;

    /// <summary>
    /// Total count of curated vector icons in the catalog (exactly 320).
    /// </summary>
    public static int TotalCount => ManifestLazy.Value.TotalCount;

    /// <summary>
    /// Gets all 320 curated vector icons.
    /// </summary>
    public static IReadOnlyList<VectorEmblemEntry> All => ManifestLazy.Value.Emblems;

    /// <summary>
    /// Gets the 12 curated functional categories.
    /// </summary>
    public static IReadOnlyList<string> Categories => ManifestLazy.Value.Categories;

    /// <summary>
    /// Resolves an emblem entry by full ID (e.g. "ic_fluent_folder_24_regular") or short ID (e.g. "folder").
    /// </summary>
    public static VectorEmblemEntry? GetById(string? idOrShortId)
    {
        if (string.IsNullOrWhiteSpace(idOrShortId))
        {
            return null;
        }

        IndexByIdLazy.Value.TryGetValue(idOrShortId.Trim(), out var entry);
        return entry;
    }

    /// <summary>
    /// Performs immediate, in-memory search and category filtering (P95 &lt;= 50 ms).
    /// </summary>
    public static IEnumerable<VectorEmblemEntry> Search(string? query, string? category = null)
    {
        var all = All;
        bool hasCategory = !string.IsNullOrWhiteSpace(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase);
        bool hasQuery = !string.IsNullOrWhiteSpace(query);

        if (!hasCategory && !hasQuery)
        {
            return all;
        }

        string trimmedQuery = query?.Trim() ?? string.Empty;

        return all.Where(e =>
        {
            if (hasCategory && !string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!hasQuery)
            {
                return true;
            }

            if (e.DisplayName.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)) return true;
            if (e.ShortId.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)) return true;
            if (e.Category.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)) return true;

            foreach (var tag in e.Tags)
            {
                if (tag.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        });
    }

    private static VectorEmblemManifest LoadManifestInternal()
    {
        string? manifestPath = ResolveManifestPath();
        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Curated vector emblem manifest could not be found at '{manifestPath}'.");
        }

        string json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize(json, FolderJsonContext.Default.VectorEmblemManifest);
        if (manifest == null || manifest.Emblems.Count == 0)
        {
            throw new InvalidDataException($"Failed to deserialize vector emblem manifest from '{manifestPath}'.");
        }

        return manifest;
    }

    private static string? ResolveManifestPath()
    {
        // 1. Direct path relative to AppContext.BaseDirectory
        string candidate1 = Path.Combine(AppContext.BaseDirectory, "assets", "emblems", "manifest.json");
        if (File.Exists(candidate1))
        {
            return candidate1;
        }

        // 2. Relative to working directory
        string candidate2 = Path.Combine(Directory.GetCurrentDirectory(), "assets", "emblems", "manifest.json");
        if (File.Exists(candidate2))
        {
            return candidate2;
        }

        // 3. Search up parent directories from AppContext.BaseDirectory (for test runners / build outputs)
        string? currentDir = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(currentDir); i++)
        {
            string candidate = Path.Combine(currentDir, "assets", "emblems", "manifest.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        // 4. Search up parent directories from CurrentDirectory
        currentDir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(currentDir); i++)
        {
            string candidate = Path.Combine(currentDir, "assets", "emblems", "manifest.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        return null;
    }
}
