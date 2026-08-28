using System.Collections.Concurrent;

namespace SessionHandler.Utils;

/// <summary>
/// Per-key async mutual exclusion, registered as a singleton. Serializes operations
/// that share a key (e.g. the same session identity triple, passed as a value tuple —
/// no string concatenation, so there's no risk of two different keys colliding into
/// the same lock) without blocking operations on unrelated keys. Entries are removed
/// once uncontended so the backing dictionary doesn't grow unbounded across the
/// lifetime of the process.
/// </summary>
public class KeyedAsyncLock<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Entry> _entries = new();

    public async Task<IDisposable> LockAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var entry = _entries.AddOrUpdate(
            key,
            _ => new Entry(),
            (_, existing) =>
            {
                Interlocked.Increment(ref existing.RefCount);
                return existing;
            });

        await entry.Semaphore.WaitAsync(cancellationToken);
        return new Releaser(this, key, entry);
    }

    private void Release(TKey key, Entry entry)
    {
        entry.Semaphore.Release();

        // Interlocked.Decrement both mutates and returns the post-decrement value
        // atomically, so this check-then-remove can't race with the increment above
        // (which happens on a different code path, not covered by any shared lock).
        if (Interlocked.Decrement(ref entry.RefCount) == 0)
        {
            // Only removes if the dictionary still holds this exact instance, so a
            // concurrent LockAsync that already re-added this key isn't dropped.
            ((ICollection<KeyValuePair<TKey, Entry>>)_entries)
                .Remove(new KeyValuePair<TKey, Entry>(key, entry));
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount = 1;
    }

    private sealed class Releaser(KeyedAsyncLock<TKey> parent, TKey key, Entry entry) : IDisposable
    {
        public void Dispose() => parent.Release(key, entry);
    }
}
