using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

/// <summary>
/// COMBAT-ATTACK-BR-ARENA-GATES-001: TMProject 7.69 Battle Royale gates outside combat
/// (_MSG_SendReqParty.cpp:51, _MSG_AcceptParty.cpp:68, _MSG_Action.cpp:308-322, _MSG_MessageChat.cpp:221).
/// </summary>
internal static class BattleRoyaleArenaGateTests
{
    private const short ArenaX = 2_610;
    private const short ArenaY = 1_720;

    public static void RulesFollowTheSourceBounds()
    {
        var active = new LegacyCombatWorldState(BattleRoyaleState: 1, BattleRoyaleItem: 5);
        Check(LegacyBattleRoyaleArenaRules.BlocksParty(active, 2_604, 1_708) && LegacyBattleRoyaleArenaRules.BlocksParty(active, 2_648, 1_744),
            "The inclusive arena corners did not block party requests.");
        Check(!LegacyBattleRoyaleArenaRules.BlocksParty(active, 2_603, 1_720) && !LegacyBattleRoyaleArenaRules.BlocksParty(active, 2_610, 1_745),
            "Tiles outside the arena blocked party requests.");
        Check(!LegacyBattleRoyaleArenaRules.BlocksParty(active with { BattleRoyaleState = 0 }, ArenaX, ArenaY) &&
            !LegacyBattleRoyaleArenaRules.BlocksParty(active with { BattleRoyaleItem = 0 }, ArenaX, ArenaY),
            "The arena gates ran without BrState and BRItem > 0.");
        Check(LegacyBattleRoyaleArenaRules.MasksChat(active, 896, 1_405) && LegacyBattleRoyaleArenaRules.MasksChat(active, 1_150, 1_538) &&
            !LegacyBattleRoyaleArenaRules.MasksChat(active, 1_151, 1_538),
            "The second chat-mask rectangle 896..1150/1405..1538 was not applied inclusively.");

        foreach (var (grid, level, expected) in new[]
        {
            (0, 99, false), (0, 100, true), (0, 999, true), (0, 1000, false),
            (1, 199, false), (1, 200, true), (1, 999, true), (1, 1000, false),
            (2, 500, false),
        })
        {
            Check(LegacyBattleRoyaleArenaRules.RecallsOnAction(active with { BattleRoyaleGrid = grid }, ArenaX, ArenaY, level) == expected,
                $"BrGrid {grid} level {level} recall decision differs from _MSG_Action.cpp:317-321.");
        }

        Check(LegacyBattleRoyaleArenaRules.ApplyChatMask("hello world") == "??????world", "The first six chat bytes were not replaced.");
        Check(LegacyBattleRoyaleArenaRules.ApplyChatMask("hi") == "??????", "A short chat message did not become the bare mask.");
    }

    public static void PartyInviteAndAcceptAreDroppedInsideTheArena()
    {
        var hub = new WorldHub();
        AddPlayer(hub, 1, "BR_LEADER", ArenaX, ArenaY, level: 300);
        AddPlayer(hub, 2, "BR_TARGET", 2_500, 1_700, level: 300);
        hub.SetBattleRoyaleState(1, 5);

        Check(hub.TryPreparePartyInvite(1, 2, out var blockedPlan) == PartyInviteCheckResult.BattleRoyaleArenaBlocked && blockedPlan is null,
            "A party request from inside the active arena was not dropped.");
        hub.SetBattleRoyaleState(0, 5);
        Check(hub.TryPreparePartyInvite(1, 2, out var plan) == PartyInviteCheckResult.Accepted && plan is not null,
            "A party request was blocked with BrState 0.");

        hub.SetBattleRoyaleState(1, 5);
        Check(hub.UpdatePosition(2, ArenaX, (short)(ArenaY + 2)), "The accepting player could not enter the arena.");
        Check(hub.TryAcceptParty(2, 1, "BR_LEADER", out var formation) == PartyAcceptResult.BattleRoyaleArenaBlocked && formation is null,
            "An acceptance from inside the active arena was not ignored.");
    }

    public static void ActionInsideTheArenaRecallsByLevelBand()
    {
        var hub = new WorldHub();
        AddPlayer(hub, 1, "BR_LOW", ArenaX, ArenaY, level: 150);
        hub.SetBattleRoyaleState(2, 5);

        hub.SetBattleRoyaleGrid(1);
        Check(!hub.TryRecallForBattleRoyaleArenaWithViews(1, out _), "BrGrid 1 recalled a level 150 player.");

        hub.SetBattleRoyaleGrid(0);
        Check(hub.TryRecallForBattleRoyaleArenaWithViews(1, out var transition) && transition is not null &&
            transition.Recall.FromX == ArenaX && transition.Recall.FromY == ArenaY &&
            !LegacyBattleRoyaleArenaRules.IsInsideArena(transition.Recall.ToX, transition.Recall.ToY),
            "BrGrid 0 did not DoRecall a level 150 player out of the arena.");
        Check(hub.TryGetCharacterSnapshot(1, out _, out var x, out var y) && x == transition!.Recall.ToX && y == transition.Recall.ToY,
            "The authoritative position did not follow the recall.");

        hub.SetBattleRoyaleState(0, 5);
        Check(hub.UpdatePosition(1, ArenaX, ArenaY) && !hub.TryRecallForBattleRoyaleArenaWithViews(1, out _),
            "The arena recall ran with BrState 0.");
    }

    public static void ChatMaskFollowsTheSenderPosition()
    {
        var hub = new WorldHub();
        AddPlayer(hub, 1, "BR_CHAT", ArenaX, ArenaY, level: 150);
        Check(!hub.ShouldMaskBattleRoyaleChat(1), "Chat was masked without an active Battle Royale.");
        hub.SetBattleRoyaleState(3, 5);
        Check(hub.ShouldMaskBattleRoyaleChat(1), "Chat inside the active arena was not masked.");
        Check(hub.UpdatePosition(1, 2_000, 2_000) && !hub.ShouldMaskBattleRoyaleChat(1), "Chat outside both areas was masked.");
    }

    public static void ControllerPublishesBrGridForEligibleServers()
    {
        var hub = new WorldHub();
        var eligible = new LegacyBattleRoyaleScheduleController(new LegacyBattleRoyaleConfiguration(5, 21, 0, 1, TestServer: false, LocalServer: false), hub);
        eligible.Tick(new DateTime(2026, 10, 15, 3, 47, 0));
        Check(hub.GetCombatWorldState().BattleRoyaleGrid == 2, "An eligible server did not publish BrGrid = tm_min / 20 outside BRHour.");

        var otherHub = new WorldHub();
        var notSelected = new LegacyBattleRoyaleScheduleController(new LegacyBattleRoyaleConfiguration(5, 21, 1, 2, TestServer: false, LocalServer: false), otherHub);
        notSelected.Tick(new DateTime(2026, 10, 15, 3, 47, 0));
        Check(otherHub.GetCombatWorldState().BattleRoyaleGrid == 0, "A server not selected for the day changed BrGrid.");
    }

    private static void AddPlayer(WorldHub hub, int connectionId, string name, short x, short y, int level)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        new LegacyScore(level, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(level, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(hub.Enter(connectionId, name, (_, _) => ValueTask.CompletedTask) &&
            hub.SetCharacterState(connectionId, 0, 0, 1, 0, 0, x, y, mob),
            $"Player {name} was not registered.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
