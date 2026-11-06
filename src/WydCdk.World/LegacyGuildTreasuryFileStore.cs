using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;

namespace WydCdk.World;

public enum LegacyGuildTreasurySaveResult
{
    Saved,
    NpcMissing,
    NpcDead,
}

/// <summary>Persists a guild treasury using TMSrv's raw MOB file format.</summary>
public sealed class LegacyGuildTreasuryFileStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string npcRoot;

    public LegacyGuildTreasuryFileStore(string npcRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(npcRoot);
        this.npcRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(npcRoot));
    }

    public async ValueTask<IReadOnlyList<(int ConnectionId, LegacyGuildTreasurySaveResult Result)>> SaveRegisteredAsync(
        WorldHub world,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        var results = new List<(int ConnectionId, LegacyGuildTreasurySaveResult Result)>();
        foreach (var connectionId in world.GetGuildTreasuryNpcConnectionIds())
            results.Add((connectionId, await SaveAsync(world, connectionId, cancellationToken)));
        return results.AsReadOnly();
    }

    public async ValueTask<LegacyGuildTreasurySaveResult> SaveAsync(
        WorldHub world,
        int npcConnectionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGetRegisteredGuildTreasuryNpcSnapshot(npcConnectionId, out var discoveredNpc) || discoveredNpc is null)
            return LegacyGuildTreasurySaveResult.NpcMissing;

        var path = GetPath(discoveredNpc.MobSnapshot);
        var fileLock = FileLocks.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            if (!world.TryGetRegisteredGuildTreasuryNpcSnapshot(npcConnectionId, out var npc) || npc is null)
                return LegacyGuildTreasurySaveResult.NpcMissing;

            var currentPath = GetPath(npc.MobSnapshot);
            if (!string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
                return LegacyGuildTreasurySaveResult.NpcMissing;
            if (npc.MobSnapshot.Length != LegacyAccountSnapshot.CharacterStride)
                throw new InvalidDataException("The guild treasury snapshot is not a complete legacy MOB.");
            if (BinaryPrimitives.ReadInt32LittleEndian(
                    npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) <= 0)
                return LegacyGuildTreasurySaveResult.NpcDead;

            temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(temporaryPath, npc.MobSnapshot, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
            temporaryPath = null;
            return LegacyGuildTreasurySaveResult.Saved;
        }
        finally
        {
            try
            {
                if (temporaryPath is not null && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            finally
            {
                fileLock.Release();
            }
        }
    }

    private string GetPath(ReadOnlySpan<byte> mob)
    {
        if (mob.Length < LegacyAccountSnapshot.MobNameOffset + 16)
            throw new InvalidDataException("The guild treasury snapshot has no complete MOB name field.");

        var nameBytes = mob.Slice(LegacyAccountSnapshot.MobNameOffset, 16);
        var terminator = nameBytes.IndexOf((byte)0);
        if (terminator >= 0)
            nameBytes = nameBytes[..terminator];
        var fileName = Encoding.Latin1.GetString(nameBytes).Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or ".." ||
            Path.GetFileName(fileName) != fileName || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("The guild treasury MOB name is not a safe legacy NPC filename.");

        var path = Path.GetFullPath(Path.Combine(npcRoot, fileName));
        var rootPrefix = npcRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The guild treasury path escaped the configured NPC root.");
        return path;
    }
}
