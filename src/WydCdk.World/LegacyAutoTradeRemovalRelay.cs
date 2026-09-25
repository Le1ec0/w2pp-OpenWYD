using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds the client-facing MSG_RemoveMob frame for an autotrade visual that
/// is no longer active. It does not access sockets or choose recipients.
/// </summary>
public static class LegacyAutoTradeRemovalRelay
{
    public static bool TryBuild(
        LegacyAutoTradeSnapshot snapshot,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        out LegacyAutoTradeRemovalRelayPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(codec);
        plan = null;

        if (snapshot.ConnectionId <= 0 || snapshot.ConnectionId > ushort.MaxValue)
            return false;

        var frame = new RemoveMobConfirmation(checked((ushort)snapshot.ConnectionId))
            .ToFrame(codec, clientTick, keywordIndex);
        plan = new LegacyAutoTradeRemovalRelayPlan(
            snapshot.ConnectionId,
            snapshot.PositionX,
            snapshot.PositionY,
            frame);
        return true;
    }
}

public sealed record LegacyAutoTradeRemovalRelayPlan(
    int ShopConnectionId,
    short PositionX,
    short PositionY,
    byte[] Frame);
