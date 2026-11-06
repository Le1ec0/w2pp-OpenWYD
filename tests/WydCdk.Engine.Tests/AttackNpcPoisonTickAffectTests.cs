using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcPoisonTickAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        Type20TickSkillsApplyToNpc1001AndCarryTheirSourceFields();
        Type20TickHonorsSourceTargetGatesAndSetTickMerchantRule();
    }

    private static void Type20TickSkillsApplyToNpc1001AndCarryTheirSourceFields()
    {
        var skills = new[]
        {
            PoisonSkill(20),
            PoisonSkill(21),
            PoisonSkill(40),
        };

        foreach (var skill in skills)
        {
            var attempt = ApplyPoisonTickSkill(LegacyMobAiPlanner.MaximumPlayerId + 1, skill);
            Check(attempt.Result == LegacySkillAttackResult.Accepted && attempt.Outcome is { Damage: > 0 },
                $"The damaging skill {skill.Id} did not resolve against NPC 1001.");
            Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
                  npc.AffectSnapshot[0] == 20 && npc.AffectSnapshot[1] == skill.TickValue &&
                  BinaryPrimitives.ReadUInt16LittleEndian(npc.AffectSnapshot.AsSpan(2)) == 50 &&
                  BinaryPrimitives.ReadUInt32LittleEndian(npc.AffectSnapshot.AsSpan(4)) == 1,
                $"The damaging skill {skill.Id} did not apply its Type 20 tick to NPC 1001 with the source value, level, and one-tick duration.");
            Check(attempt.Outcome!.TargetAffectSnapshot is { } outcomeAffect && outcomeAffect[0] == 20,
                $"The damaging skill {skill.Id} outcome omitted its Type 20 tick snapshot.");
        }
    }

    private static void Type20TickHonorsSourceTargetGatesAndSetTickMerchantRule()
    {
        var sameGuildMob = CreateNpcMob(level: 50);
        BinaryPrimitives.WriteUInt16LittleEndian(sameGuildMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 12);
        AssertNotApplied(ApplyPoisonTickSkill(
            LegacyMobAiPlanner.MaximumPlayerId + 1,
            PoisonSkill(21),
            npcMob: sameGuildMob,
            attackerGuildId: 12), "same guild");

        AssertNotApplied(ApplyPoisonTickSkill(
            LegacyMobAiPlanner.MaximumPlayerId + 1,
            PoisonSkill(21),
            npcLeaderConnectionId: 1), "same leader");

        var clanSixMob = CreateNpcMob(level: 50);
        clanSixMob[LegacyAccountSnapshot.MobClanOffset] = 6;
        AssertNotApplied(ApplyPoisonTickSkill(
            LegacyMobAiPlanner.MaximumPlayerId + 1,
            PoisonSkill(21),
            npcMob: clanSixMob), "Clan 6");

        var blockedMob = CreateNpcMob(level: 50);
        BinaryPrimitives.WriteUInt16LittleEndian(blockedMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x80);
        AssertNotApplied(ApplyPoisonTickSkill(
            LegacyMobAiPlanner.MaximumPlayerId + 1,
            PoisonSkill(21),
            npcMob: blockedMob), "RSV_BLOCK");

        AssertNotApplied(ApplyPoisonTickSkill(
            LegacyMobAiPlanner.MaximumPlayerId + 1,
            PoisonSkill(21),
            attackerLevel: 1,
            npcLevel: 1_000,
            affectRandomFactor: 0), "AffectResist");

        var merchantAboveMaxUser = CreateNpcMob(level: 50);
        merchantAboveMaxUser[LegacyAccountSnapshot.MobMerchantOffset] = 1;
        AssertNotApplied(ApplyPoisonTickSkill(
            LegacyMobAiPlanner.MaximumPlayerId + 1,
            PoisonSkill(21),
            npcMob: merchantAboveMaxUser), "Merchant above MAX_USER");

        var firstIndexMerchant = CreateNpcMob(level: 50);
        firstIndexMerchant[LegacyAccountSnapshot.MobMerchantOffset] = 1;
        var firstIndex = ApplyPoisonTickSkill(
            LegacyMobAiPlanner.MaximumPlayerId,
            PoisonSkill(21),
            npcMob: firstIndexMerchant);
        Check(firstIndex.Result == LegacySkillAttackResult.Accepted &&
              firstIndex.World.TryGetNpcSnapshot(firstIndex.NpcId, out var firstIndexNpc) &&
              firstIndexNpc is not null && firstIndexNpc.AffectSnapshot[0] == 20,
            "SetTick's Merchant check incorrectly rejected the first NPC index at MAX_USER.");
    }

    private static (WorldHub World, int NpcId, LegacySkillAttackResult Result, LegacySkillAttackOutcome? Outcome) ApplyPoisonTickSkill(
        int npcId,
        LegacySkillDefinition skill,
        int affectRandomFactor = 0,
        int attackerLevel = 1_500,
        int npcLevel = 50,
        byte[]? npcMob = null,
        int attackerGuildId = 0,
        int npcLeaderConnectionId = 0)
    {
        var world = new WorldHub(itemData: ItemData);
        var attackerMob = CreateNpcMob(attackerLevel, hp: 100_000);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Special4 = 50,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        Check(world.Enter(1, "POISON_TICK_NPC_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, LegacyAccountSnapshot.ClassMasterMortal, attackerGuildId, 1, 0, 0,
                  2_000, 2_000, attackerMob),
            "Poison tick attacker fixture was not installed.");

        npcMob ??= CreateNpcMob(npcLevel);
        var createdNpcId = world.EnterNpc(
            npcMob,
            2_001,
            2_000,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: npcId,
            leaderConnectionId: npcLeaderConnectionId);
        Check(createdNpcId == npcId, $"Poison tick NPC fixture {npcId} could not be registered.");

        var result = world.TryApplySkillAttack(
            1,
            npcId,
            skill,
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
            $"The poison tick skill rejected its {scenario} fixture.");
        Check(attempt.World.TryGetNpcSnapshot(attempt.NpcId, out var npc) && npc is not null &&
              !HasAffectType(npc.AffectSnapshot, 20),
            $"The poison tick producer crossed the {scenario} source gate.");
    }

    private static byte[] CreateNpcMob(int level, int hp = 100_000)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, 83, 0, 0x96, 0, 0, hp, 10_000, hp, 10_000,
            0, 100, 40, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static LegacySkillDefinition PoisonSkill(int skillId) => skillId switch
    {
        20 => new(20, 66, 1, 30, 4, 5, 1, 65, 20, 2, 0, 0, 0,
            [9, 0, 0, 5, 0, 0, 0, 0], [9, 0, 0, 5, 0, 0, 0, 0],
            0, 0, 1, 1, 0, 0, 0, 1, "Ataque_da_Alma"),
        21 => new(21, 87, 1, 38, 4, 5, 1, 80, 20, 10, 0, 0, 0,
            [9, 0, 0, 5, 0, 0, 0, 0], [9, 0, 0, 5, 0, 0, 0, 0],
            4, 0, 1, 1, 0, 3, 0, 1, "Punhalada_Venenosa"),
        40 => new(40, 24, 1, 9, 2, 6, 4, 20, 20, 10, 0, 0, 0,
            [9, 0, 0, 5, 0, 0, 0, 0], [9, 0, 0, 5, 0, 0, 0, 0],
            4, 0, 1, 1, 0, 0, 0, 1, "Nevoa_Venenosa"),
        _ => throw new ArgumentOutOfRangeException(nameof(skillId)),
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
