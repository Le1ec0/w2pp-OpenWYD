using WydCdk.Protocol;
using WydCdk.World;

// Short-lived leases for existing synchronous unit fixtures only. These helpers
// are not in the product assembly and cannot bypass its protected API.
internal static class AutoTradeBookTestExtensions
{
    internal static LegacyAutoTradeStartResult TryStart(this LegacyAutoTradeBook book, int id,
        AutoTradeStartRequest request, LegacyAutoTradeStartContext context, out LegacyAutoTradeSnapshot? snapshot)
    {
        var lease = book.AcquireAsync([id]).GetAwaiter().GetResult();
        try
        {
            var result = book.TryStart(lease, id, new("SELLER", 1), request, context, out var handle);
            snapshot = handle?.Snapshot;
            return result;
        }
        finally { lease.DisposeAsync().GetAwaiter().GetResult(); }
    }

    internal static bool TryGet(this LegacyAutoTradeBook book, int id, out LegacyAutoTradeSnapshot? snapshot)
    {
        var lease = book.AcquireAsync([id]).GetAwaiter().GetResult();
        try
        {
            var result = book.TryGet(lease, id, out var handle);
            snapshot = handle?.Snapshot;
            return result;
        }
        finally { lease.DisposeAsync().GetAwaiter().GetResult(); }
    }

    internal static bool TryStop(this LegacyAutoTradeBook book, int id, out LegacyAutoTradeSnapshot? snapshot)
    {
        var lease = book.AcquireAsync([id]).GetAwaiter().GetResult();
        try
        {
            snapshot = null;
            if (!book.TryGet(lease, id, out var handle) || handle is null || !book.TryStop(lease, handle)) return false;
            snapshot = handle.Snapshot;
            return true;
        }
        finally { lease.DisposeAsync().GetAwaiter().GetResult(); }
    }

    internal static bool TryRestore(this LegacyAutoTradeBook book, int id, LegacyAutoTradeSnapshot snapshot)
    {
        var lease = book.AcquireAsync([id]).GetAwaiter().GetResult();
        try { return book.TryRestore(lease, id, new("OFFLINE", 1), snapshot, out _); }
        finally { lease.DisposeAsync().GetAwaiter().GetResult(); }
    }

    internal static LegacyAutoTradePurchaseResult TryApplyPurchase(this LegacyAutoTradeBook book, int id,
        LegacyAutoTradePurchasePlan plan, out LegacyAutoTradeSnapshot? snapshot)
    {
        var lease = book.AcquireAsync([id]).GetAwaiter().GetResult();
        try
        {
            snapshot = null;
            if (!book.TryGet(lease, id, out var handle) || handle is null)
                return LegacyAutoTradePurchaseResult.TargetNotInAutoTrade;
            var result = book.TryApplyPurchase(lease, handle, plan, out var updated);
            snapshot = updated?.Snapshot;
            return result;
        }
        finally { lease.DisposeAsync().GetAwaiter().GetResult(); }
    }
}
