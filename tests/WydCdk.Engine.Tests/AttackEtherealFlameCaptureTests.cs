using System.Text;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

/// <summary>
/// COMBAT-ATTACK-SKILL-ETHEREAL-FLAME-NPC-CAPTURE-001: TMProject 7.69 InstanceType 12 against a
/// server mob (_MSG_Attack.cpp:1115-1316), with the capture chance corrected by project decision.
/// </summary>
internal static class AttackEtherealFlameCaptureTests
{
    private const int CasterId = 1;
    private const int NpcId = 20_001;
    private const short NpcX = 2_005;
    private const short NpcY = 2_000;

    private static readonly LegacySkillDefinition EtherealFlame =
        new(49, 36, 1, 35, 30, 6, 12, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Chamas_Etereas");

    public static void CaptureThresholdFallsAsTheMobGetsStronger()
    {
        // Project decision (14/10/2026): 100 - 4d instead of the reconstruction's (d << 2) - 100.
        Check(LegacyCombatThreshold(50, 100) == 100, "A much weaker mob was not a guaranteed capture.");
        Check(LegacyCombatThreshold(90, 100) == 100, "d = 0 did not keep the full threshold.");
        Check(LegacyCombatThreshold(91, 100) == 96, "d = 1 did not lower the threshold by four.");
        Check(LegacyCombatThreshold(114, 100) == 4, "d = 24 did not keep a 5% window.");
        Check(LegacyCombatThreshold(115, 100) == 0, "d = 25 did not leave only roll 0.");
        Check(LegacyCombatThreshold(140, 100) < 0, "A mob 40 levels above the caster stayed capturable.");

        static int LegacyCombatThreshold(int targetLevel, int casterLevel) =>
            WorldHub.GetEtherealFlameCaptureThreshold(targetLevel, casterLevel);
    }

    public static void SoloCasterCapturesMobIntoOwnParty()
    {
        var hub = CreateWorld(npcName: "Lobo_Negro", npcLevel: 50);
        var result = hub.TryApplySkillAttack(CasterId, NpcId, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var outcome, instanceRandomFactor: 99);

        Check(result == LegacySkillAttackResult.Accepted && outcome?.CapturedSummon is not null,
            $"A capturable mob was not captured: result={result}.");
        var captured = outcome!.CapturedSummon!;
        Check(outcome!.Damage == 0 && !outcome.PreserveSkillMarkerOnWire,
            "A successful capture did not write Dam[i] = 0 like the 7.69 fall-through.");
        Check(!hub.TryGetNpcSnapshot(NpcId, out _), "The captured mob is still registered as a world NPC.");
        Check(hub.TryGetSummonTarget(NpcId, out var summonTarget) && summonTarget == 0, "The captured mob was not registered as a peaceful summon.");
        Check(captured.LeaderConnectionId == CasterId && captured.SummonerConnectionId == CasterId && captured.ConnectionId == NpcId,
            "The captured mob did not keep its index with the caster as leader and summoner.");

        var mob = captured.MobSnapshot;
        Check(ReadName(mob) == "Lobo Negro^", $"The captured name did not follow strcat('^') and '_' -> ' ': '{ReadName(mob)}'.");
        Check(mob[LegacyAccountSnapshot.MobClanOffset] == 4, "The captured mob was not moved to Clan 4.");
        var baseScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(baseScore.Level == 100 && currentScore.Level == 100, "The captured mob did not take the caster's BaseScore level.");
        Check(currentScore.Hp == currentScore.MaxHp, "The captured mob was not restored to full HP.");
        Check(captured.AffectSnapshot.All(static value => value == 0), "The captured mob kept its affects.");

        Check(outcome.CaptureViewRecipients?.Contains(CasterId) == true, "The capture CreateMob was not addressed to players viewing the mob.");
        var adds = outcome.PartyAddNotices!;
        Check(adds.Count == 2, $"A first party member must produce the leader-self and the member SendAddParty: {adds.Count}.");
        Check(adds[0].RecipientConnectionId == CasterId && adds[0].MemberConnectionId == CasterId && adds[0].LeaderField == CasterId,
            "The first SendAddParty(Leader, Leader, 0) was not sent to the new party leader.");
        Check(adds[1].RecipientConnectionId == CasterId && adds[1].MemberConnectionId == NpcId && adds[1].LeaderField == 30000 &&
            adds[1].MobName == "Lobo Negro^" && adds[1].Level == 100,
            "SendAddParty(Leader, idx, j + 1) did not describe the captured mob.");
    }

    public static void CaptureGateFailuresKeepTheClientMarker()
    {
        AssertSkipped(CreateWorld(npcLevel: 140), roll: 0, "A mob 40 levels above the caster was captured.");
        AssertSkipped(CreateWorld(npcLevel: 115), roll: 1, "Roll 1 captured with threshold 0.");
        AssertSkipped(CreateWorld(npcHp: 500), roll: 0, "A damaged mob was captured.");
        AssertSkipped(CreateWorld(npcClan: 6), roll: 0, "A Clan 6 mob was captured.");
        AssertSkipped(CreateWorld(npcName: "Torre"), roll: 0, "An excluded boss name was captured.");
        AssertSkipped(CreateWorld(npcX: 3_730, npcY: 2_840, casterX: 3_725, casterY: 2_840), roll: 0, "A mob inside an excluded area was captured.");

        var merchantWorld = CreateWorld(merchant: 72);
        var result = merchantWorld.TryApplySkillAttack(CasterId, NpcId, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var outcome, instanceRandomFactor: 0);
        Check(result == LegacySkillAttackResult.Accepted && outcome is not null && outcome.CapturedSummon is null &&
            !outcome.PreserveSkillMarkerOnWire && outcome.Damage == 0,
            "A merchant mob did not fall through both branches with Dam[i] = 0.");
        Check(merchantWorld.TryGetNpcSnapshot(NpcId, out _), "A merchant mob left the NPC table.");

        var captureWorld = CreateWorld(npcLevel: 115);
        Check(captureWorld.TryApplySkillAttack(CasterId, NpcId, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var edgeOutcome, instanceRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
            edgeOutcome?.CapturedSummon is not null,
            "Roll 0 did not capture at threshold 0.");
    }

    public static void PartyMemberCaptureJoinsTheLeaderParty()
    {
        const int LeaderId = 3;
        var hub = CreateWorld();
        AddPlayer(hub, LeaderId, "PARTY_LEADER", 2_003, 2_000);
        Check(hub.TrySetParty(LeaderId, [LeaderId, CasterId]), "The two-player party was not installed.");

        var result = hub.TryApplySkillAttack(CasterId, NpcId, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var outcome, instanceRandomFactor: 0);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.CapturedSummon is { } captured &&
            captured.LeaderConnectionId == LeaderId && captured.SummonerConnectionId == CasterId,
            "A party member capture did not attach the mob to the party leader with the caster as summoner.");
        var adds = outcome!.PartyAddNotices!;
        Check(adds.Count == 2 &&
            adds[0].RecipientConnectionId == LeaderId && adds[0].MemberConnectionId == NpcId && adds[0].LeaderField == 30000 &&
            adds[1].RecipientConnectionId == CasterId && adds[1].MemberConnectionId == NpcId,
            "An existing party did not receive only the new member SendAddParty frames.");
    }

    public static void FullPartyAbortsTheHandlerWithNotice()
    {
        var hub = CreateWorld();
        var members = new List<int> { CasterId };
        for (var id = 2; id <= 12; id++)
        {
            AddPlayer(hub, id, $"MEMBER{id}", 2_000, (short)(2_000 + id));
            members.Add(id);
        }
        Check(hub.TrySetParty(CasterId, members), "The eleven-member party was not installed.");
        // A first capture fills the twelfth PartyList slot; summons count toward MAX_PARTY.
        const int FirstNpcId = 20_002;
        EnterCaptureNpc(hub, FirstNpcId, "Lobo", 50, 1_000, 0, 0, 2_006, 2_001);
        Check(hub.TryApplySkillAttack(CasterId, FirstNpcId, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var firstCapture, instanceRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
            firstCapture?.CapturedSummon is not null && firstCapture.PartyAddNotices!.Count == 12,
            "The capture filling the last party slot did not notify the leader and the eleven members.");

        var direct = hub.TryApplySkillAttack(CasterId, NpcId, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var outcome, instanceRandomFactor: 0);
        Check(direct == LegacySkillAttackResult.PartyFullCantSummon && outcome is null, $"A full party did not stop the capture: {direct}.");
        Check(hub.TryGetNpcSnapshot(NpcId, out _), "A refused capture removed the NPC.");
        Check(LegacySkillSummonNoticeMapper.GetMessage(12, LegacySkillAttackResult.PartyFullCantSummon) == "Grupo está cheio. Não é possível convocar mais membros.",
            "The party-full refusal did not map to Language.txt:33.");
        Check(LegacySkillSummonNoticeMapper.GetMessage(12, LegacySkillAttackResult.TargetNotAlive) is null,
            "InstanceType 12 mapped an unrelated result to a notice.");

        var request = new LegacyAttackActionBatchRequest(
            CasterId,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, NpcId, SkillRolls: new LegacySkillAttackBatchSlotRolls(InstanceRandomFactor: 0), SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 1),
            ],
            EtherealFlame);
        Check(hub.TryApplyAttackActionBatch(request, out var batch) && batch is not null, "The InstanceType 12 batch was not processed.");
        Check(!batch!.CompletedNormally && batch.AbortSlotIndex == 0 && batch.SlotOutcomes.Count == 1,
            $"The 7.69 `return` after the party-full notice did not abort the handler: completed={batch.CompletedNormally}, abort={batch.AbortSlotIndex}, slots={batch.SlotOutcomes.Count}.");
    }

    public static void PlayerBranchKeepsTheClientMarker()
    {
        var hub = CreateWorld();
        AddPlayer(hub, 2, "FLAME_TARGET", 2_003, 2_000);
        var burn = hub.TryApplySkillAttack(CasterId, 2, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var outcome, instanceRandomFactor: 99, burnRandomFactor: 0);
        Check(burn == LegacySkillAttackResult.Accepted && outcome?.PreserveSkillMarkerOnWire == true,
            "The player burn branch `continue` did not keep the client's -1 marker.");
    }

    private static void AssertSkipped(WorldHub hub, int roll, string message)
    {
        var result = hub.TryApplySkillAttack(CasterId, NpcId, EtherealFlame, itemData: null, weather: 0, randomFactor: 90, out var outcome, instanceRandomFactor: roll);
        Check(result == LegacySkillAttackResult.Accepted && outcome is not null && outcome.CapturedSummon is null && outcome.PreserveSkillMarkerOnWire,
            message);
        Check(hub.TryGetNpcSnapshot(NpcId, out var npc) && npc!.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] != 4,
            message + " (the NPC was mutated or removed)");
    }

    private static WorldHub CreateWorld(
        string npcName = "Lobo_Negro",
        int npcLevel = 50,
        int npcHp = 1_000,
        byte npcClan = 0,
        byte merchant = 0,
        short npcX = NpcX,
        short npcY = NpcY,
        short casterX = 2_000,
        short casterY = 2_000)
    {
        var hub = new WorldHub();
        AddPlayer(hub, CasterId, "FLAME_CASTER", casterX, casterY);
        EnterCaptureNpc(hub, NpcId, npcName, npcLevel, npcHp, npcClan, merchant, npcX, npcY);
        return hub;
    }

    private static void EnterCaptureNpc(WorldHub hub, int npcId, string npcName, int npcLevel, int npcHp, byte npcClan, byte merchant, short npcX, short npcY)
    {
        var npcMob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(npcName).CopyTo(npcMob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        npcMob[LegacyAccountSnapshot.MobClanOffset] = npcClan;
        npcMob[LegacyAccountSnapshot.MobMerchantOffset] = merchant;
        new LegacyScore(npcLevel, 0, 0, 0, 0, 0, 0, 1_000, 0, 1_000, 0, 0, 0, 0, 0, 0, 0, 0, 0).Write(npcMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(npcLevel, 0, 0, 0, 0, 0, 0, 1_000, 0, npcHp, 0, 0, 0, 0, 0, 0, 0, 0, 0).Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 18;
        Check(hub.EnterNpc(npcMob, npcX, npcY, affect, requestedConnectionId: npcId, generateIndex: 1) == npcId, "The capture NPC was not registered.");
    }

    private static void AddPlayer(WorldHub hub, int connectionId, string name, short x, short y)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000, 0, 0, 0, 0, 0, 70, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 1_000, 0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(hub.Enter(connectionId, name, (_, _) => ValueTask.CompletedTask) &&
            hub.SetCharacterState(connectionId, 0, 0, 1, 0, 0, x, y, mob),
            $"Player {name} was not registered.");
    }

    private static string ReadName(byte[] mob)
    {
        var name = mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16);
        var length = name.IndexOf((byte)0);
        return Encoding.ASCII.GetString(length < 0 ? name : name[..length]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
