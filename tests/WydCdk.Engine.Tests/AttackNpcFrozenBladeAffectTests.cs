using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcFrozenBladeAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        LaminaCongelanteProjectsType7OnTheDamagingNpcSkillPath();
        LaminaCongelanteHonorsNpcAffectResistanceAndSetAffectBound();
        LaminaCongelanteType7ExpiryRestoresGeneratedAndNonGeneratedNpcScore();
    }

    private static void LaminaCongelanteProjectsType7OnTheDamagingNpcSkillPath()
    {
        var first = ApplyLaminaCongelante(LegacyMobAiPlanner.MaximumPlayerId);
        Check(first.Result == LegacySkillAttackResult.Accepted && first.Outcome is { Damage: > 0 },
            "Lamina Congelante did not resolve a damaging InstanceType 3 NPC attack.");
        Check(first.World.TryGetNpcSnapshot(first.NpcId, out var firstNpc) && firstNpc is not null &&
              HasAffectType(firstNpc.AffectSnapshot, 7) && firstNpc.AffectSnapshot[1] == 1 &&
              BinaryPrimitives.ReadUInt16LittleEndian(firstNpc.AffectSnapshot.AsSpan(2)) == 50 &&
              GetAffectTime(firstNpc.AffectSnapshot, 7) == 1,
            "Lamina Congelante did not apply the Level 50 Type 7 affect with its one-tick source duration at NPC 1000.");

        var firstScore = LegacyScore.Read(firstNpc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(firstScore.AttackRun == 0x96 && firstScore.Intelligence == 75,
            $"Lamina Congelante did not subtract Level/10+10 from raw AttackSpeed before packing and Int by tmp+10 for head index >50; AttackRun=0x{firstScore.AttackRun:X2}, Int={firstScore.Intelligence}.");
        Check(first.Outcome!.TargetAffectSnapshot is { } outcomeAffect && HasAffectType(outcomeAffect, 7),
            "The damaging NPC skill outcome omitted the applied Type 7 affect snapshot.");
    }

    private static void LaminaCongelanteHonorsNpcAffectResistanceAndSetAffectBound()
    {
        var resisted = ApplyLaminaCongelante(
            LegacyMobAiPlanner.MaximumPlayerId,
            attackerLevel: 1_000,
            npcLevel: 900,
            affectRandomFactor: 53);
        AssertNotApplied(resisted, "AffectResist roll above the source threshold");

        var sameGuildMob = CreateNpcMob(level: 50, attackRun: 0xA6, intelligence: 100, dexterity: 40, headIndex: 51);
        BinaryPrimitives.WriteUInt16LittleEndian(sameGuildMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        AssertNotApplied(ApplyLaminaCongelante(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: sameGuildMob,
            attackerGuildId: 12), "same guild");

        AssertNotApplied(ApplyLaminaCongelante(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcLeaderConnectionId: 1), "same leader");

        var clanSixMob = CreateNpcMob(level: 50, attackRun: 0xA6, intelligence: 100, dexterity: 40, headIndex: 51);
        clanSixMob[LegacyAccountSnapshot.MobClanOffset] = 6;
        AssertNotApplied(ApplyLaminaCongelante(
            LegacyMobAiPlanner.MaximumPlayerId,
            npcMob: clanSixMob), "Clan 6");

        var laterNpc = ApplyLaminaCongelante(LegacyMobAiPlanner.MaximumPlayerId + 1);
        Check(laterNpc.Result == LegacySkillAttackResult.Accepted &&
              laterNpc.World.TryGetNpcSnapshot(laterNpc.NpcId, out var laterSnapshot) && laterSnapshot is not null &&
              !HasAffectType(laterSnapshot.AffectSnapshot, 7) &&
              LegacyScore.Read(laterSnapshot.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0xA6, Intelligence: 100 },
            "Lamina Congelante applied Type 7 beyond the legacy first-NPC SetAffect bound.");

        var blockedMob = CreateNpcMob(level: 50, attackRun: 0xA6, intelligence: 100, dexterity: 40, headIndex: 51);
        BinaryPrimitives.WriteUInt16LittleEndian(blockedMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x80);
        AssertNotApplied(ApplyLaminaCongelante(LegacyMobAiPlanner.MaximumPlayerId, npcMob: blockedMob), "RSV_BLOCK");

        var merchantMob = CreateNpcMob(level: 50, attackRun: 0xA6, intelligence: 100, dexterity: 40, headIndex: 51);
        merchantMob[LegacyAccountSnapshot.MobMerchantOffset] = 1;
        AssertNotApplied(ApplyLaminaCongelante(LegacyMobAiPlanner.MaximumPlayerId, npcMob: merchantMob), "merchant");
    }

    private static void LaminaCongelanteType7ExpiryRestoresGeneratedAndNonGeneratedNpcScore()
    {
        AssertType7Expires(generated: false);
        AssertType7Expires(generated: true);
    }

    private static void AssertType7Expires(bool generated)
    {
        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var attempt = ApplyLaminaCongelante(
            npcId,
            npcMob: CreateNpcMob(level: 50, attackRun: 0xA6, intelligence: 100, dexterity: 40, headIndex: 51),
            generateIndex: generated ? 0 : -1);
        Check(attempt.Result == LegacySkillAttackResult.Accepted &&
              attempt.World.TryGetNpcSnapshot(npcId, out var affectedNpc) && affectedNpc is not null &&
              LegacyScore.Read(affectedNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 75 },
            $"The {(generated ? "generated" : "non-generated")} Lamina Congelante Type 7 fixture did not project the raw speed penalty.");

        var processed = false;
        if (generated)
        {
            for (var visit = 0; visit < 4; visit++)
                processed = attempt.World.TryProcessGeneratedNpcCombatTimerAffects(npcId, out _);
        }
        else
        {
            processed = attempt.World.TryProcessNonGeneratedNpcTimerAffects(npcId, out _);
        }
        Check(processed,
            $"The {(generated ? "generated" : "non-generated")} NPC Type 7 timer did not report its score-refresh expiry.");

        Check(attempt.World.TryGetNpcSnapshot(npcId, out var expiredNpc) && expiredNpc is not null &&
              expiredNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              LegacyScore.Read(expiredNpc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0xA6, Intelligence: 100 },
            $"The {(generated ? "generated" : "non-generated")} NPC Type 7 expiry did not clear the record and restore its baseline.");
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome) ApplyLaminaCongelante(
        int npcId,
        int affectRandomFactor = 0,
        int attackerLevel = 1_500,
        int npcLevel = 50,
        byte[]? npcMob = null,
        int generateIndex = -1,
        int attackerGuildId = 0,
        int npcLeaderConnectionId = 0)
    {
        var world = new WorldHub(itemData: ItemData);
        var attackerMob = CreateNpcMob(level: attackerLevel, attackRun: 0x96, intelligence: 100, dexterity: 0, headIndex: 0);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { Special4 = 50 };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        Check(world.Enter(1, "FROZEN_BLADE_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, LegacyAccountSnapshot.ClassMasterMortal, attackerGuildId, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Lamina Congelante attacker fixture was not installed.");

        npcMob ??= CreateNpcMob(level: npcLevel, attackRun: 0xA6, intelligence: 100, dexterity: 40, headIndex: 51);
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_001,
            2_000,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId,
            generateIndex: generateIndex,
            leaderConnectionId: npcLeaderConnectionId);
        Check(createdNpcId == npcId, $"Lamina Congelante NPC fixture {npcId} could not be registered.");

        var result = world.TryApplySkillAttack(
            1,
            npcId,
            LaminaCongelante(),
            ItemData,
            weather: 0,
            randomFactor: 100,
            out var outcome,
            affectRandomFactor: affectRandomFactor);
        return (world, npcId, result, outcome);
    }

    private static void AssertNotApplied(
        (WorldHub World, int NpcId, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome) attempt,
        string scenario)
    {
        Check(attempt.Result == LegacySkillAttackResult.Accepted,
            $"Lamina Congelante rejected its {scenario} NPC fixture.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 7) &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0xA6, Intelligence: 100 },
            $"Lamina Congelante changed an NPC behind the {scenario} gate.");
    }

    private static byte[] CreateNpcMob(int level, byte attackRun, short intelligence, short dexterity, int headIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, 83, 0, attackRun, 0, 0, 10_000, 1_000, 10_000, 1_000,
            0, intelligence, dexterity, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(checked((short)headIndex), 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset < affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static uint GetAffectTime(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return BinaryPrimitives.ReadUInt32LittleEndian(affect.Slice(offset + 4, sizeof(uint)));
        return 0;
    }

    private static LegacySkillDefinition LaminaCongelante() =>
        new(19, 78, 1, 22, 4, 5, 3, 50, 0, 0, 7, 1, 0,
            [8, 0, 0, 7, 0, 0, 0, 0], [8, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Lamina_Congelante");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
