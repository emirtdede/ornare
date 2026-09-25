using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Storage;
using Ornare.Windows;

namespace Ornare.CrashTestWorker;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Usage: --barrier <AssetGenerated|FilesystemStaged|FilesystemCommitted|MetadataPending> --folder <path> --db <path>
        string? barrier = null;
        string? targetFolder = null;
        string? dbPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--barrier" && i + 1 < args.Length) barrier = args[i + 1];
            if (args[i] == "--folder" && i + 1 < args.Length) targetFolder = args[i + 1];
            if (args[i] == "--db" && i + 1 < args.Length) dbPath = args[i + 1];
        }

        if (string.IsNullOrEmpty(barrier) || string.IsNullOrEmpty(targetFolder) || string.IsNullOrEmpty(dbPath))
        {
            Console.Error.WriteLine("Missing required arguments.");
            return 1;
        }

        var storage = new SqliteStorageService();
        await storage.InitializeAsync(dbPath);

        var pipeline = new ImagePipeline();
        var customizer = new FolderCustomizer();

        Guid opId = Guid.NewGuid();
        string tempIco = Path.Combine(Path.GetTempPath(), $"barrier_{opId:N}.ico");

        // 1. Prepare stage
        await storage.RecordJournalEntryAsync(new JournalEntry
        {
            OperationId = opId,
            Kind = OperationKind.Apply,
            TargetFolderPath = targetFolder,
            State = JournalState.Prepared,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // 2. AssetGenerated stage
        using (var img = new MagickImage(MagickColors.SteelBlue, 256, 256))
        {
            byte[] bytes = img.ToByteArray(MagickFormat.Png);
            pipeline.ProcessImageBytes(bytes, tempIco);
        }

        await storage.UpdateJournalStateAsync(opId, JournalState.AssetGenerated, tempIco);

        if (string.Equals(barrier, "AssetGenerated", StringComparison.OrdinalIgnoreCase))
        {
            SignalBarrierAndWait("AssetGenerated");
        }

        // 3. FilesystemStaged stage
        string ornareDir = Path.Combine(targetFolder, OrnareConstants.CanonicalFolderName);
        if (!Directory.Exists(ornareDir))
        {
            DirectoryInfo d = Directory.CreateDirectory(ornareDir);
            d.Attributes |= FileAttributes.Hidden | FileAttributes.System;
        }
        string targetIco = Path.Combine(ornareDir, "folder.ico");
        File.Copy(tempIco, targetIco, overwrite: true);
        File.SetAttributes(targetIco, FileAttributes.Hidden | FileAttributes.System);

        await storage.UpdateJournalStateAsync(opId, JournalState.FilesystemStaged);

        if (string.Equals(barrier, "FilesystemStaged", StringComparison.OrdinalIgnoreCase))
        {
            SignalBarrierAndWait("FilesystemStaged");
        }

        // 4. FilesystemCommitted stage
        string iniPath = Path.Combine(targetFolder, OrnareConstants.DesktopIniFileName);
        string currentIni = File.Exists(iniPath) ? DesktopIniParser.ReadFileWithEncodingFallback(iniPath) : string.Empty;
        string updatedIni = DesktopIniParser.MergeSemanticsForApply(currentIni, @".ornare\folder.ico");
        if (File.Exists(iniPath))
        {
            File.SetAttributes(iniPath, FileAttributes.Normal);
        }
        File.WriteAllText(iniPath, updatedIni, System.Text.Encoding.Unicode);
        File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

        DirectoryInfo dirInfo = new(targetFolder);
        dirInfo.Attributes |= FileAttributes.ReadOnly;

        await storage.UpdateJournalStateAsync(opId, JournalState.FilesystemCommitted);

        if (string.Equals(barrier, "FilesystemCommitted", StringComparison.OrdinalIgnoreCase))
        {
            SignalBarrierAndWait("FilesystemCommitted");
        }

        // 5. MetadataPending stage (after ShellRefresh, before MetadataCommitted)
        ShellRefreshService.NotifyFolderChanged(targetFolder);
        await storage.UpdateJournalStateAsync(opId, JournalState.ShellRefreshRequested);

        if (string.Equals(barrier, "MetadataPending", StringComparison.OrdinalIgnoreCase))
        {
            SignalBarrierAndWait("MetadataPending");
        }

        // 6. Complete
        await storage.UpdateJournalStateAsync(opId, JournalState.Completed);
        return 0;
    }

    private static void SignalBarrierAndWait(string phase)
    {
        Console.WriteLine($"BARRIER_REACHED:{phase}");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
    }
}
