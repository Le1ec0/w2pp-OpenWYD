using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Relays each generated NPC in a group to its legacy GridMulticast view.</summary>
public static class LegacyNpcGenerationRelay
{
    public static async Task RelayGroupAsync(
        WorldHub world,
        IReadOnlyList<LegacyWorldNpc> group,
        Func<LegacyWorldNpc, byte[]> createFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(createFrame);

        foreach (var npc in group)
        {
            var frame = createFrame(npc);
            foreach (var recipientId in world.GetParticipantIdsInNpcView(npc.ConnectionId))
                await LegacyMobAiRelay.TrySendToViewerAsync(world, recipientId, frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
