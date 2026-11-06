using WydCdk.Protocol;

namespace WydCdk.World;

public sealed record LegacyBattleRoyaleFramePlan(byte[] Frame, IReadOnlyList<int> RecipientIds);

public sealed record LegacyBattleRoyaleRewardRelayPlan(
    LegacyWorldItem GroundItem,
    int WireItemId,
    IReadOnlyList<LegacyBattleRoyaleFramePlan> Frames);

/// <summary>Creates the scheduled Battle Royale prize and captures its legacy item/notice relay order.</summary>
public static class LegacyBattleRoyaleRewardRelay
{
    public const short AltarX = 2621;
    public const short AltarY = 1726;

    public static LegacyBattleRoyaleRewardRelayPlan? TryCreate(
        WorldHub world,
        LegacyBattleRoyaleTransition transition,
        int rotate,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(codec);

        if (transition.PreviousState != 2 || transition.State != 3 ||
            transition.BRItem <= 0 || transition.BRItem >= LegacyItemDataTable.MaxItemIndex ||
            rotate is < 0 or > 3)
            return null;

        var item = new LegacyItem(checked((short)transition.BRItem), 0, 0, 0, 0, 0, 0);
        var spawn = world.TryEnterGroundItemAndCaptureView(item, AltarX, AltarY, rotate);
        if (spawn is null)
            return null;

        var frames = new List<LegacyBattleRoyaleFramePlan>
        {
            new(
                new CreateItemConfirmation(
                    checked((ushort)AltarX),
                    checked((ushort)AltarY),
                    checked((ushort)spawn.WireItemId),
                    item,
                    rotate)
                    .ToFrame(codec, clientTick, keywordIndex),
                spawn.RecipientConnectionIds),
        };

        if (transition.AreaNotice is { } notice)
        {
            var frame = new MessagePanelConfirmation(notice.Message)
                .ToFrame(codec, clientTick, keywordIndex);
            var recipients = world.GetParticipantIdsInArea(notice.X1, notice.Y1, notice.X2, notice.Y2);
            frames.Add(new LegacyBattleRoyaleFramePlan(frame, recipients));
        }

        return new LegacyBattleRoyaleRewardRelayPlan(
            spawn.Item,
            spawn.WireItemId,
            Array.AsReadOnly(frames.ToArray()));
    }

    public static async ValueTask SendAsync(
        WorldHub world,
        LegacyBattleRoyaleRewardRelayPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var framePlan in plan.Frames)
            foreach (var connectionId in framePlan.RecipientIds)
                await world.SendAsync(connectionId, framePlan.Frame, cancellationToken);
    }
}
