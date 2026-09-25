using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds the two post-commit carry refreshes used by legacy trade completion.
/// This is a relay plan only: it does not write to a socket or persist a MOB.
/// </summary>
public static class LegacyTradeCompletionRelay
{
    public static bool TryBuild(
        LegacyTradeCompletionOutcome outcome,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        out LegacyTradeCompletionRelayPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);
        plan = null;

        if (!outcome.RequiresPersistence ||
            outcome.FirstConnectionId <= 0 || outcome.FirstConnectionId > ushort.MaxValue ||
            outcome.SecondConnectionId <= 0 || outcome.SecondConnectionId > ushort.MaxValue ||
            !TryReadCarry(outcome.FirstMobSnapshot, out var firstCarry) ||
            !TryReadCarry(outcome.SecondMobSnapshot, out var secondCarry))
            return false;

        var firstFrame = new UpdateCarryConfirmation(firstCarry, outcome.FirstCoin)
            .ToFrame(codec, clientTick, keywordIndex, checked((ushort)outcome.FirstConnectionId));
        var secondFrame = new UpdateCarryConfirmation(secondCarry, outcome.SecondCoin)
            .ToFrame(codec, clientTick, keywordIndex, checked((ushort)outcome.SecondConnectionId));

        plan = new LegacyTradeCompletionRelayPlan(
            outcome.FirstConnectionId,
            outcome.SecondConnectionId,
            outcome.FirstCoin,
            outcome.SecondCoin,
            firstFrame,
            secondFrame);
        return true;
    }

    private static bool TryReadCarry(ReadOnlySpan<byte> mob, out LegacyItem[] carry)
    {
        carry = [];
        var requiredLength = LegacyAccountSnapshot.MobCarryOffset +
            (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes);
        if (mob.Length < requiredLength)
            return false;

        carry = new LegacyItem[LegacyAccountSnapshot.MobCarryCount];
        for (var index = 0; index < carry.Length; index++)
            carry[index] = LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobCarryOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        return true;
    }
}

public sealed record LegacyTradeCompletionRelayPlan(
    int FirstConnectionId,
    int SecondConnectionId,
    int FirstCoin,
    int SecondCoin,
    byte[] FirstCarryFrame,
    byte[] SecondCarryFrame);
