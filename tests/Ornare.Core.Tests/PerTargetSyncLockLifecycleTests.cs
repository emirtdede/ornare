using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class PerTargetSyncLockLifecycleTests
{
    [Fact]
    public async Task FailedAcquisitionDoesNotPoisonLaterAcquisitions()
    {
        string path = @"C:\TestFolder\Contended";

        using var held = await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(held);

        using var timedOut = await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromMilliseconds(50));
        Assert.Null(timedOut);

        held!.Dispose();

        using var reacquired = await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(reacquired);
    }

    [Fact]
    public async Task PathComparisonIgnoresCaseAndTrailingSeparator()
    {
        using var first = await PerTargetSyncLock.TryAcquireAsync(@"C:\TestFolder\CaseSensitive", TimeSpan.FromMilliseconds(500));
        Assert.NotNull(first);

        using var second = await PerTargetSyncLock.TryAcquireAsync(@"c:\testfolder\casesensitive\", TimeSpan.FromMilliseconds(50));
        Assert.Null(second);
    }

    [Fact]
    public async Task CancelledWaitDoesNotPoisonLaterAcquisitions()
    {
        string path = @"C:\TestFolder\Cancelled";
        using var held = await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(held);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromSeconds(5), cts.Token));

        held!.Dispose();

        using var reacquired = await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(reacquired);
    }
}
