using System.Collections.Concurrent;
using WydCdk.World;

internal static class LegacyConnectionSlotAllocatorTests
{
    public static void StartsAtPlayerSlotOne()
    {
        var allocator = new LegacyConnectionSlotAllocator();

        if (!allocator.TryAllocate(out var connectionId) || connectionId != 1)
            throw new InvalidOperationException($"The first player slot must be 1, got {connectionId}.");
    }

    public static void AllocatesEveryPlayerSlotOnceThenReportsExhaustion()
    {
        var allocator = new LegacyConnectionSlotAllocator();
        var seen = new HashSet<int>();

        for (var expectedId = 1; expectedId <= 999; expectedId++)
        {
            if (!allocator.TryAllocate(out var connectionId))
                throw new InvalidOperationException($"Allocation stopped before player slot {expectedId}.");
            if (connectionId != expectedId || !seen.Add(connectionId))
                throw new InvalidOperationException($"Expected unique player slot {expectedId}, got {connectionId}.");
        }

        if (allocator.TryAllocate(out _))
            throw new InvalidOperationException("An exhausted allocator issued a slot outside the 1..999 player range.");
    }

    public static void ReusesReleasedSlotAfterCircularWrap()
    {
        var allocator = new LegacyConnectionSlotAllocator();
        for (var expectedId = 1; expectedId <= 999; expectedId++)
        {
            if (!allocator.TryAllocate(out var connectionId) || connectionId != expectedId)
                throw new InvalidOperationException($"Could not fill the ring at slot {expectedId}; got {connectionId}.");
        }

        if (!allocator.Release(1))
            throw new InvalidOperationException("An active slot could not be released.");
        if (!allocator.TryAllocate(out var reusedId) || reusedId != 1)
            throw new InvalidOperationException($"The first released slot after wrap must be 1, got {reusedId}.");
    }

    public static void RejectsInvalidAndDuplicateRelease()
    {
        var allocator = new LegacyConnectionSlotAllocator();
        if (allocator.Release(0) || allocator.Release(-1) || allocator.Release(1000))
            throw new InvalidOperationException("A slot outside 1..999 was released.");
        if (!allocator.TryAllocate(out var connectionId) || connectionId != 1)
            throw new InvalidOperationException($"Expected initial slot 1, got {connectionId}.");
        if (!allocator.Release(connectionId))
            throw new InvalidOperationException("An active slot was not released.");
        if (allocator.Release(connectionId))
            throw new InvalidOperationException("A duplicate release was reported as successful.");
    }

    public static void ConcurrentAllocationNeverDuplicatesPlayerSlots()
    {
        var allocator = new LegacyConnectionSlotAllocator();
        var allocated = new ConcurrentBag<int>();
        Parallel.For(0, 1_000, _ =>
        {
            if (allocator.TryAllocate(out var connectionId))
                allocated.Add(connectionId);
        });

        var ids = allocated.ToArray();
        if (ids.Length != 999 || ids.Distinct().Count() != 999 || ids.Any(static id => id is < 1 or > 999))
            throw new InvalidOperationException("Concurrent allocation did not issue exactly 999 distinct player slots in range.");
        if (allocator.TryAllocate(out _))
            throw new InvalidOperationException("Concurrent exhaustion issued an extra player slot.");
    }
}
