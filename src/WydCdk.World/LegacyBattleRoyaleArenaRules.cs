namespace WydCdk.World;

/// <summary>
/// TMProject 7.69 Battle Royale gates outside combat. All of them require <c>BrState != 0</c> and
/// <c>BRItem &gt; 0</c> and test the player's own <c>TargetX/TargetY</c>:
/// party invite/accept are dropped silently inside the arena (<c>_MSG_SendReqParty.cpp:51</c>,
/// <c>_MSG_AcceptParty.cpp:68</c>), an accepted Action inside the arena recalls by level band
/// (<c>_MSG_Action.cpp:308-322</c>) and chat from the arena or the 896..1150/1405..1538 area has its
/// first six bytes replaced by <c>??????</c> (<c>_MSG_MessageChat.cpp:221-226</c>).
/// </summary>
public static class LegacyBattleRoyaleArenaRules
{
    private const string ChatMask = "??????";

    public static bool IsActive(LegacyCombatWorldState state) =>
        state.BattleRoyaleState != 0 && state.BattleRoyaleItem > 0;

    public static bool IsInsideArena(int x, int y) =>
        x >= LegacyBattleRoyaleAreaClearRelay.MinX && y >= LegacyBattleRoyaleAreaClearRelay.MinY &&
        x <= LegacyBattleRoyaleAreaClearRelay.MaxX && y <= LegacyBattleRoyaleAreaClearRelay.MaxY;

    public static bool BlocksParty(LegacyCombatWorldState state, int x, int y) =>
        IsActive(state) && IsInsideArena(x, y);

    public static bool MasksChat(LegacyCombatWorldState state, int x, int y) =>
        IsActive(state) && (IsInsideArena(x, y) || (x >= 896 && y >= 1405 && x <= 1150 && y <= 1538));

    /// <summary><c>BrGrid = tm_min / 20</c>: 0 recalls levels 100..999, 1 recalls 200..999, 2 none.</summary>
    public static bool RecallsOnAction(LegacyCombatWorldState state, int x, int y, int level) =>
        IsActive(state) && IsInsideArena(x, y) &&
        ((state.BattleRoyaleGrid == 0 && level >= 100 && level < 1000) ||
         (state.BattleRoyaleGrid == 1 && level >= 200 && level < 1000));

    /// <summary><c>strncpy(String, "??????", 6)</c> over the fixed chat buffer.</summary>
    public static string ApplyChatMask(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        // A shorter message loses its terminator; the client buffer after it is zero-filled,
        // so the visible text becomes exactly the mask.
        return message.Length > ChatMask.Length ? ChatMask + message[ChatMask.Length..] : ChatMask;
    }
}
