using System.Collections;
using System.Reflection;
using SessionHandler.Utils;

namespace SessionHandler.Tests.Unit;

/// <summary>
/// <see cref="KeyedAsyncLock{TKey}"/> in isolation: per-key mutual exclusion, no
/// cross-key blocking, and the entry bookkeeping that the concurrency-safety fixes
/// were about — a cancelled waiter must give its claim back without releasing the
/// lock, and uncontended keys must not linger in the backing dictionary.
/// </summary>
public class KeyedAsyncLockTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Second_acquire_on_the_same_key_waits_for_the_first_release()
    {
        var locks = new KeyedAsyncLock<string>();
        var first = await locks.LockAsync("k");

        var second = locks.LockAsync("k");
        Assert.False(second.IsCompleted);

        first.Dispose();

        var releaser = await second.WaitAsync(Timeout);
        releaser.Dispose();
    }

    [Fact]
    public async Task Different_keys_do_not_block_each_other()
    {
        var locks = new KeyedAsyncLock<string>();
        using var heldA = await locks.LockAsync("a");

        var acquireB = locks.LockAsync("b");

        using var heldB = await acquireB.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Concurrent_workers_on_one_key_never_interleave()
    {
        var locks = new KeyedAsyncLock<string>();
        var counter = 0;

        async Task Bump()
        {
            using var _ = await locks.LockAsync("k");
            var seen = counter;
            await Task.Yield();      // invite interleaving; the lock must prevent it
            counter = seen + 1;      // non-atomic, so a broken lock loses updates
        }

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Bump()));

        Assert.Equal(50, counter);
    }

    [Fact]
    public async Task Cancelling_a_queued_waiter_throws_and_does_not_release_the_lock()
    {
        var locks = new KeyedAsyncLock<string>();
        var holder = await locks.LockAsync("k");

        using var cts = new CancellationTokenSource();
        var queued = locks.LockAsync("k", cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        // The cancelled attempt must not have handed its permit to the next waiter.
        var next = locks.LockAsync("k");
        Assert.False(next.IsCompleted);

        holder.Dispose();
        using var _ = await next.WaitAsync(Timeout);
    }

    [Fact]
    public async Task An_already_cancelled_token_throws_without_taking_the_lock()
    {
        var locks = new KeyedAsyncLock<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => locks.LockAsync("k", new CancellationToken(canceled: true)));

        using var _ = await locks.LockAsync("k").WaitAsync(Timeout);
    }

    [Fact]
    public async Task Uncontended_keys_are_removed_from_the_backing_dictionary()
    {
        var locks = new KeyedAsyncLock<string>();

        (await locks.LockAsync("k")).Dispose();
        Assert.Equal(0, EntryCount(locks));

        // Also after a cancelled queued waiter, once the holder lets go.
        var holder = await locks.LockAsync("k");
        using (var cts = new CancellationTokenSource())
        {
            var queued = locks.LockAsync("k", cts.Token);
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        }
        holder.Dispose();
        Assert.Equal(0, EntryCount(locks));
    }

    private static int EntryCount<TKey>(KeyedAsyncLock<TKey> locks) where TKey : notnull
    {
        var entries = typeof(KeyedAsyncLock<TKey>)
            .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(locks);
        return ((ICollection)entries!).Count;
    }
}
