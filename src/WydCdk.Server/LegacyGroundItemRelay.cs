using System.Security.Cryptography;
using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Relays a newly created dynamic ground item to the viewers captured at placement time.</summary>
public static class LegacyGroundItemRelay
{
    public static async ValueTask RelaySpawnAsync(
        WorldHub world,
        LegacyGroundItemSpawn spawn,
        LegacyFrameCodec codec,
        CancellationToken cancellationToken,
        uint? clientTick = null,
        byte? keywordIndex = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(codec);

        var item = spawn.Item;
        var frame = new CreateItemConfirmation(
            checked((ushort)item.PositionX),
            checked((ushort)item.PositionY),
            checked((ushort)spawn.WireItemId),
            item.Item,
            item.Rotate)
            .ToFrame(
                codec,
                clientTick ?? unchecked((uint)Environment.TickCount64),
                keywordIndex ?? (byte)RandomNumberGenerator.GetInt32(256));

        foreach (var recipientId in spawn.RecipientConnectionIds)
            await LegacyMobAiRelay.TrySendToViewerAsync(world, recipientId, frame, cancellationToken).ConfigureAwait(false);
    }
}
