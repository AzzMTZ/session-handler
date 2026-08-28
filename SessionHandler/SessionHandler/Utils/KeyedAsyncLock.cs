namespace SessionHandler.Utils;

/// <summary>
/// Per-key async mutual exclusion, registered as a singleton. Serializes operations
/// that share a key (e.g. the same session identity triple, passed as a value tuple —
/// no string concatenation, so there's no risk of two different keys colliding into
/// the same lock) without blocking operations on unrelated keys. Entries are removed
/// once uncontended so the backing dictionary doesn't grow unbounded across the
/// lifetime of the process.
///
/// The dictionary lookup and the ref-count increment/decrement are guarded by one
/// plain lock (<see cref="_gate"/>) rather than a lock-free <c>ConcurrentDictionary</c>
/// with <c>Interlocked</c> counters: those are two different atomicity mechanisms that
/// don't coordinate with each other, which is exactly what let a ref count reach zero
/// and get removed from the dictionary in the same instant a new caller had just
/// incremented it back to one — silently splitting one logical lock into two. A single
/// lock around the whole "check refcount, decide" sequence on both the acquire and
/// release side closes that gap entirely. It's only ever held for O(1) dictionary
/// bookkeeping, never across the actual <c>await Semaphore.WaitAsync</c>, so unrelated
/// keys still never block on each other for any meaningful duration.
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
            // WaitAsync never granted a permit here (cancellation is the realistic
            // case - RequestAborted firing while queued behind another operation on
            // the same identity), so this claim on the entry must be given back the
            // same way a real release would, or a cancelled acquire attempt would
            // leak RefCount forever and the entry could never be removed again. Must
            // not call entry.Semaphore.Release() here: no permit was taken, so
            // releasing one would hand out a phantom permit to whoever is genuinely
            // holding it, breaking mutual exclusion.
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
