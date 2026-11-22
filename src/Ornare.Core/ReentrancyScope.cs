namespace Ornare.Core;

/// <summary>
/// Marks a region of code whose changes to control state must not be interpreted as user
/// intent.
/// <para>
/// Controls report a programmatic state change through the same notification they use for
/// a real user action. A picker that is repopulated or re-selected by code therefore looks
/// exactly like a pick: a handler that persists the new selection writes a settings
/// snapshot taken before the caller finished its own update, which silently reverts values
/// the caller had just applied.
/// </para>
/// <para>
/// The scope is reference counted rather than a simple flag so nested uses cannot clear the
/// suppression early, and it restores the previous state even when the guarded action
/// throws.
/// </para>
/// </summary>
public sealed class ReentrancyScope
{
    private int _depth;

    /// <summary>True while a guarded region is executing on the current call stack.</summary>
    public bool IsActive => Volatile.Read(ref _depth) > 0;

    /// <summary>Runs <paramref name="action"/> with change notifications suppressed.</summary>
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Interlocked.Increment(ref _depth);
        try
        {
            action();
        }
        finally
        {
            Interlocked.Decrement(ref _depth);
        }
    }

    /// <summary>Runs <paramref name="func"/> with change notifications suppressed.</summary>
    public T Run<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        Interlocked.Increment(ref _depth);
        try
        {
            return func();
        }
        finally
        {
            Interlocked.Decrement(ref _depth);
        }
    }
}
