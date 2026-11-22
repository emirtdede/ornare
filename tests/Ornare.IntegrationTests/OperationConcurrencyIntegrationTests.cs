using System;
using System.IO;
using System.Threading.Tasks;
using Ornare.Core;
using Ornare.Storage;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Verifies that Apply, Reset, Undo and ApplyLast all serialize on the same per-target
/// lock. Regression guard for the previous behaviour where each service owned a private
/// semaphore dictionary, so two operations could mutate one folder at the same time
/// (INV-CON-001).
/// </summary>
public class OperationConcurrencyIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SqliteStorageService _storage;
    private readonly FolderCustomizer _customizer;

    public OperationConcurrencyIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Ornare_Concurrency_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        _storage = new SqliteStorageService();
        _storage.InitializeAsync(Path.Combine(_testRoot, "ornare_test.db")).GetAwaiter().GetResult();
        _customizer = new FolderCustomizer();
    }

    public void Dispose()
    {
        _storage.Dispose();
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Temp cleanup must never fail a test.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ResetIsRejectedWhileAnotherOperationHoldsTheTargetLock()
    {
        string folder = CreateFolder("ContendedReset");

        using var held = await PerTargetSyncLock.TryAcquireAsync(folder, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(held);

        var result = await ResetService.ResetAsync(folder, _storage, _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.OperationBusy, result.ErrorCode);
    }

    [Fact]
    public async Task UndoIsRejectedWhileAnotherOperationHoldsTheTargetLock()
    {
        string folder = CreateFolder("ContendedUndo");

        using var held = await PerTargetSyncLock.TryAcquireAsync(folder, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(held);

        var result = await UndoService.UndoAsync(folder, _storage, _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.OperationBusy, result.ErrorCode);
    }

    [Fact]
    public async Task ApplyIsRejectedWhileAnotherOperationHoldsTheTargetLock()
    {
        string folder = CreateFolder("ContendedApply");
        string stagedIco = Path.Combine(_testRoot, "staged.ico");
        await File.WriteAllBytesAsync(stagedIco, [0, 0, 1, 0]);

        using var held = await PerTargetSyncLock.TryAcquireAsync(folder, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(held);

        var result = await ApplyService.ApplyAsync(
            folder,
            stagedIco,
            "test-hash",
            renderKey: null,
            recipe: null,
            _storage,
            _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.OperationBusy, result.ErrorCode);
    }

    [Fact]
    public async Task ApplyLastIsRejectedWhileAnotherOperationHoldsTheTargetLock()
    {
        string folder = CreateFolder("ContendedApplyLast");

        using var held = await PerTargetSyncLock.TryAcquireAsync(folder, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(held);

        var result = await ApplyLastService.ApplyLastAsync(folder, _storage, _customizer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.OperationBusy, result.ErrorCode);
    }

    [Fact]
    public async Task ReleasingTheLockAllowsResetToProceed()
    {
        string folder = CreateFolder("ReleasedReset");

        var held = await PerTargetSyncLock.TryAcquireAsync(folder, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(held);
        held!.Dispose();

        var result = await ResetService.ResetAsync(folder, _storage, _customizer);

        Assert.NotEqual(ErrorCode.OperationBusy, result.ErrorCode);
    }

    private string CreateFolder(string name)
    {
        string folder = Path.Combine(_testRoot, name);
        Directory.CreateDirectory(folder);
        return folder;
    }
}
