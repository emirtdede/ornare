using System.Collections.Concurrent;
using System.IO;

namespace Ornare.Core;

public sealed class PerTargetSyncLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IDisposable?> TryAcquireAsync(string folderPath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        string canonicalPath = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
        SemaphoreSlim semaphore = Locks.GetOrAdd(canonicalPath, _ => new SemaphoreSlim(1, 1));

        bool acquired = await semaphore.WaitAsync(timeout, cancellationToken);
        if (!acquired)
        {
            return null;
        }

        return new Releaser(semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _semaphore.Release();
            }
        }
    }
}
