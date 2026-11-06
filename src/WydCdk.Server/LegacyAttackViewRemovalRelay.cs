using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Relays view-captured removals produced inside an attack action batch.</summary>
public static class LegacyAttackViewRemovalRelay
{
    public static async ValueTask RelayCapturedViewAsync(
        WorldHub world,
        int senderConnectionId,
        LegacyAttackViewRemovalEffect effect,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.ViewRecipientConnectionIds is not { } recipients)
            return;

        foreach (var recipientConnectionId in recipients)
        {
            if (recipientConnectionId == senderConnectionId)
                continue;

            _ = await LegacyMobAiRelay.TrySendToViewerAsync(
                world,
                recipientConnectionId,
                frame,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
