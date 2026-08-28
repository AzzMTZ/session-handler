namespace SessionHandler.Utils;

/// <summary>
/// Per-key async mutual exclusion, registered as a singleton. Operations sharing a key
/// run serially; unrelated keys never block each other. Entries are dropped once
/// uncontended so <see cref="_entries"/> stays bounded.
///
/// One plain lock (<see cref="_gate"/>) guards both the dictionary lookup and the
/// ref-count change, so a count can't hit zero and be removed in the same instant
/// another caller increments it back — the race a <c>ConcurrentDictionary</c> +
/// <c>Interlocked</c> split would allow. The gate is never held across
/// <c>Semaphore.WaitAsync</c>.
/// </summary>
public class KeyedAsyncLock<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, Entry> _entries = new();
    private readonly object _gate = new();

    public async Task<IDisposable> LockAsync(TKey key, CancellationToken cancellationToken = default)
    {
        Entry entry;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                existing.RefCount++;
                entry = existing;
            }
            else
            {
                entry = new Entry();
                _entries[key] = entry;
            }
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            // Cancelled while queued: no permit was granted, so give back the RefCount
            // claim (else the entry leaks) but do NOT Release the semaphore — that
            // would hand a phantom permit to the real holder.
            DecrementAndRemoveIfUnused(key, entry);
            throw;
        }

        return new Releaser(this, key, entry);
    }

    private void Release(TKey key, Entry entry)
    {
        entry.Semaphore.Release();
        DecrementAndRemoveIfUnused(key, entry);
    }

    private void DecrementAndRemoveIfUnused(TKey key, Entry entry)
    {
        lock (_gate)
        {
            entry.RefCount--;
            if (entry.RefCount == 0)
            {
                _entries.Remove(key);
            }
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
