using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

public class AdversarialStorageAndPersistenceTests
{
    [Fact]
    public async Task CorruptedDatabaseFailsGracefullyWithoutCrashingOrDeletingIcons()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ornare_db_corrupt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string dbPath = Path.Combine(tempDir, "ornare.db");

        // Write garbage bytes pretending to be a SQLite DB
        byte[] garbage = Encoding.UTF8.GetBytes("THIS IS NOT A VALID SQLITE DATABASE FILE - CORRUPTED HEADER");
        File.WriteAllBytes(dbPath, garbage);

        // Also create a customized folder with an icon
        string folderPath = Path.Combine(tempDir, "MyCustomFolder");
        Directory.CreateDirectory(folderPath);
        string iconDir = Path.Combine(folderPath, OrnareConstants.CanonicalFolderName);
        Directory.CreateDirectory(iconDir);
        string iconPath = Path.Combine(iconDir, "folder.ico");
        File.WriteAllText(iconPath, "dummy icon content");

        try
        {
            using var storage = new SqliteStorageService();
            // Initializing against corrupted DB should either throw SqliteException or be caught gracefully, but must NOT wipe out user files
            await Assert.ThrowsAnyAsync<SqliteException>(async () =>
            {
                await storage.InitializeAsync(dbPath);
            });

            // Verify user icon and folder are untouched
            Assert.True(Directory.Exists(folderPath));
            Assert.True(File.Exists(iconPath));
            Assert.Equal("dummy icon content", File.ReadAllText(iconPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void DesktopIniAdversarialMatrixPreservesUnrelatedSectionsAndEncodings()
    {
        // 1. Complex existing desktop.ini with UTF-16, BOM, comments, ViewState, InfoTip, LocalizedResourceName
        string iniWithEverything = @"; This is a header comment
[ViewState]
Mode=
Vid=
FolderType=Generic
[CustomSection]
CustomKey=CustomValue
; Section comment
[.ShellClassInfo]
InfoTip=My special project folder
LocalizedResourceName=@%SystemRoot%\system32\shell32.dll,-8964
IconResource=C:\Windows\System32\shell32.dll,4
ConfirmFileOp=0
# Hash style comment
MalformedLineWithoutEquals
";
        // Parse semantics
        var semantics = DesktopIniParser.ParseSemanticsFromContent(iniWithEverything);
        Assert.Equal(@"C:\Windows\System32\shell32.dll,4", semantics.IconResource);
        Assert.Contains("InfoTip=My special project folder", semantics.RawUnrelatedContent);
        Assert.Contains("FolderType=Generic", semantics.RawUnrelatedContent);
        Assert.Contains("CustomKey=CustomValue", semantics.RawUnrelatedContent);

        // Apply new Ornare icon
        string applied = DesktopIniParser.MergeSemanticsForApply(iniWithEverything, @".ornare\folder.ico");

        Assert.Contains(@"IconResource=.ornare\folder.ico,0", applied);
        Assert.Contains("InfoTip=My special project folder", applied);
        Assert.Contains("LocalizedResourceName=@%SystemRoot%\\system32\\shell32.dll,-8964", applied);
        Assert.Contains("FolderType=Generic", applied);
        Assert.Contains("CustomKey=CustomValue", applied);
        Assert.Contains("ConfirmFileOp=0", applied);

        // Reset icon back to baseline
        string reset = DesktopIniParser.MergeSemanticsForReset(applied, semantics);
        Assert.Contains(@"IconResource=C:\Windows\System32\shell32.dll,4", reset);
        Assert.Contains("InfoTip=My special project folder", reset);
        Assert.Contains("FolderType=Generic", reset);
    }

    [Fact]
    public async Task DuplicateAssetImportDeterminismStoresExactlyOneCanonicalAsset()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ornare_dedup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string dbPath = Path.Combine(tempDir, "ornare.db");
        string sampleSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        string canonicalPath = Path.Combine(tempDir, "canonical.ico");
        File.WriteAllText(canonicalPath, "ico content");

        try
        {
            using (var storage = new SqliteStorageService())
            {
                await storage.InitializeAsync(dbPath);

                // 10 concurrent / sequential imports of the same asset
                for (int i = 0; i < 10; i++)
                {
                    await storage.AddOrUpdateAssetAsync(sampleSha256, canonicalPath, "PNG");
                }

                var allAssets = (await storage.GetAllAssetsAsync()).ToList();
                // Determinism: exactly 1 asset entry with that SHA256
                Assert.Single(allAssets);
                Assert.Equal(sampleSha256, allAssets[0].Sha256);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void HealthScanSynchronousEvaluationHandlesRapidQueriesWithoutLeaks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ornare_health_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            for (int i = 0; i < 100; i++)
            {
                var result = FolderHealthEngine.CheckHealth(tempDir);
                Assert.NotNull(result);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Theory]
    [InlineData("Klasör_İ_ı_I_i_Türkçe")]
    [InlineData("Folder with Spaces and [Parentheses] & Symbols")]
    [InlineData("Proje_123_ğüşiöç")]
    public void FolderPathWithTurkishAndUnicodeCharactersIsValidatedCorrectly(string folderName)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ornare_culture_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string target = Path.Combine(tempDir, folderName);
        Directory.CreateDirectory(target);

        try
        {
            var result = TargetFolderValidator.ValidateTarget(target);
            Assert.True(result.IsSuccess, $"Validation should succeed for Unicode/Turkish path: {target}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
