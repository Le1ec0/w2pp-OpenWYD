using WydCdk.Protocol;

namespace WydCdk.World;

public enum LegacyBattleRoyaleEnvironmentalPacketKind
{
    PlayerDamage,
    AreaEffect,
}

public sealed record LegacyBattleRoyaleEnvironmentalPacket(
    int RecipientId,
    LegacyBattleRoyaleEnvironmentalPacketKind Kind,
    byte[] Frame);

public sealed record LegacyBattleRoyaleEnvironmentalRelayPlan(
    IReadOnlyList<LegacyBattleRoyaleEnvironmentalPacket> Packets);

/// <summary>Applies and relays the scheduled Battle Royale environmental damage and area effects.</summary>
public static class LegacyBattleRoyaleEnvironmentalRelay
{
    public static LegacyBattleRoyaleEnvironmentalRelayPlan? TryCreate(
        WorldHub world,
        LegacyBattleRoyaleEnvironmentalStep step,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(codec);

        var snapshot = world.TryApplyBattleRoyaleEnvironmentalDamage(step);
        if (snapshot is null)
            return null;

        var packets = new List<LegacyBattleRoyaleEnvironmentalPacket>();
        foreach (var target in snapshot.DamageTargets)
        {
            if (target.ConnectionId is <= 0 or > ushort.MaxValue ||
                target.RecipientIds.Any(static recipient => recipient is <= 0 or > ushort.MaxValue))
                return null;

            var frame = new SetHpDamConfirmation(target.CurrentHp, target.Damage)
                .ToFrame(codec, clientTick, keywordIndex, checked((ushort)target.ConnectionId));
            foreach (var recipientId in target.RecipientIds)
                packets.Add(new LegacyBattleRoyaleEnvironmentalPacket(
                    recipientId,
                    LegacyBattleRoyaleEnvironmentalPacketKind.PlayerDamage,
                    frame));
        }

        foreach (var effect in snapshot.Effects)
        {
            if (effect.RecipientIds.Any(static recipient => recipient is <= 0 or > ushort.MaxValue))
                return null;

            var area = effect.Area;
            var frame = new EnvironmentEffectConfirmation(
                    area.X1,
                    area.Y1,
                    area.X2,
                    area.Y2,
                    effect: 32,
                    effectParameter: 0)
                .ToFrame(codec, clientTick, keywordIndex);
            foreach (var recipientId in effect.RecipientIds)
                packets.Add(new LegacyBattleRoyaleEnvironmentalPacket(
                    recipientId,
                    LegacyBattleRoyaleEnvironmentalPacketKind.AreaEffect,
                    frame));
        }

        return new LegacyBattleRoyaleEnvironmentalRelayPlan(Array.AsReadOnly(packets.ToArray()));
    }

    public static async ValueTask SendAsync(
        WorldHub world,
        LegacyBattleRoyaleEnvironmentalRelayPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var packet in plan.Packets)
            await world.SendAsync(packet.RecipientId, packet.Frame, cancellationToken);
    }
}
