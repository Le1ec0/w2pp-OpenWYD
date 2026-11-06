using System.Collections.Concurrent;

namespace WydCdk.World;

/// <summary>
/// Serializes in-process readers and writers that replace account blobs under one
/// configured account root. It protects the file adapter only; it is not a
/// cross-process or distributed lock.
/// </summary>
internal static class LegacyAccountFileStateGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static SemaphoreSlim ForRoot(string accountRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountRoot);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(accountRoot));
        return Gates.GetOrAdd(normalizedRoot, static _ => new SemaphoreSlim(1, 1));
    }
}
