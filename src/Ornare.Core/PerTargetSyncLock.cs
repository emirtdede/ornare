using System.Collections.Concurrent;
using System.IO;

namespace Ornare.Core;

/// <summary>
/// Process-wide in-memory mutual exclusion per canonical target folder.
/// <para>
/// Entries are reference counted and removed once no caller holds or waits on them, so a
/// long session that touches thousands of folders cannot grow an unbounded dictionary.
/// This lock does not provide cross-process exclusion; see the operation journal for
/// crash recovery across application/CLI boundaries.
/// </para>
/// </summary>
public sealed class PerTargetSyncLock
{
    private static readonly ConcurrentDictionary<string, LockEntry> Locks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static async Task<IDisposable?> TryAcquireAsync(string folderPath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        string canonicalPath = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
        LockEntry entry;

        lock (Gate)
        {
            entry = Locks.GetOrAdd(canonicalPath, static _ => new LockEntry());
            entry.ReferenceCount++;
        }

        bool acquired;
        try
        {
            acquired = await entry.Semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // WaitAsync guarantees no acquisition when it throws, so the reference can be
            // dropped without releasing the semaphore.
            ReleaseReference(canonicalPath, entry);
            throw;
        }

        if (!acquired)
        {
            ReleaseReference(canonicalPath, entry);
            return null;
        }

        return new Releaser(canonicalPath, entry);
    }

    private static void ReleaseReference(string canonicalPath, LockEntry entry)
    {
        bool removed = false;
        lock (Gate)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount <= 0)
            {
                removed = Locks.TryRemove(new KeyValuePair<string, LockEntry>(canonicalPath, entry));
            }
        }

        // Dispose outside the gate: disposal blocks while waiters drain and must never
        // hold the dictionary lock. Only dispose when this call actually removed the
        // entry, which proves no other caller still references the semaphore.
        if (removed)
        {
            entry.Semaphore.Dispose();
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly string _canonicalPath;
        private readonly LockEntry _entry;
        private bool _disposed;

        public Releaser(string canonicalPath, LockEntry entry)
        {
            _canonicalPath = canonicalPath;
            _entry = entry;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Release before decrementing so a waiter that acquires the semaphore keeps
            // a positive reference count and the entry is not removed from under it.
            _entry.Semaphore.Release();
            ReleaseReference(_canonicalPath, _entry);
        }
    }
}
