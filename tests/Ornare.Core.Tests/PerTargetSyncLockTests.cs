using Ornare.Core;
using Xunit;

namespace Ornare.Core.Tests;

public class PerTargetSyncLockTests
{
    [Fact]
    public async Task TryAcquireAsyncSamePathConcurrentlyBlocksSecondAttempt()
    {
        string path = @"C:\TestFolder\Sub";

        using var firstLock = await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromMilliseconds(500));
        Assert.NotNull(firstLock);

        using var secondLock = await PerTargetSyncLock.TryAcquireAsync(path, TimeSpan.FromMilliseconds(50));
        Assert.Null(secondLock);
    }

    [Fact]
    public async Task TryAcquireAsyncDifferentPathsAcquiresBoth()
    {
        string path1 = @"C:\TestFolder\A";
        string path2 = @"C:\TestFolder\B";

        using var lock1 = await PerTargetSyncLock.TryAcquireAsync(path1, TimeSpan.FromMilliseconds(500));
        using var lock2 = await PerTargetSyncLock.TryAcquireAsync(path2, TimeSpan.FromMilliseconds(500));

        Assert.NotNull(lock1);
        Assert.NotNull(lock2);
    }
}
