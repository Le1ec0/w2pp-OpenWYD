using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSkillSecondaryAffectTests
{
    private static readonly LegacyItemDataTable ItemData =
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static readonly LegacySkillDataTable SecondarySkills = LegacySkillDataTable.Load(new StringReader(
        "36,72,1,35,5,6,3,200,0,0,1,2,1,6.0.0.7.0.0.0.0,6.0.0.7.0.0.0.0,2,0,1,1,0,2,0,1,Nevasca\n" +
        "40,24,1,9,2,6,4,20,20,10,0,0,1,10.0.0.9.0.0.0.0,10.0.0.9.0.0.0.0,4,0,1,1,0,0,0,1,Neblina_Venenosa"));

    public static void Run()
    {
        FrostProcProjectsNpcType1OnPositiveDamage();
        FrostProcHonorsSetAffectNpcBoundAndRsvBlock();
        DrainProcUsesSetTickNpcBoundaryAndRejectsMerchant();
        SkillRsvProcsRequirePositiveDamageAndSuccessfulProcRolls();
        FrostProcProjectsPlayerType1Score();
        DrainProcPersistsOnSummon();
    }

    private static void FrostProcProjectsNpcType1OnPositiveDamage()
    {
        var fixture = CreateNpcFixture(1_000, attackerRsv: 0x01);
        var result = Apply(fixture.World, fixture.TargetId, DamageSkill(), frostRoll: 0, drainRoll: 1, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 } &&
              outcome.TargetAffectSnapshot is { } affected && HasAffectType(affected, 1),
            "A positive skill hit with RSV_FROST did not return its Type 1 affect snapshot.");
        Check(fixture.World.TryGetNpcSnapshot(fixture.TargetId, out var npc) && npc is not null &&
              HasAffectType(npc.AffectSnapshot, 1) && GetAffectValue(npc.AffectSnapshot, 1) == 2 &&
              GetAffectLevel(npc.AffectSnapshot, 1) == 40 && GetAffectTime(npc.AffectSnapshot, 1) == 1 &&
              LegacyScore.Read(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 } &&
              npc.HolyTouchBaseScore is { AttackRun: 0x96, Intelligence: 100 },
            "RSV_FROST did not apply SkillData 36 through SetAffect and project the NPC Type 1 score.");
    }

    private static void FrostProcHonorsSetAffectNpcBoundAndRsvBlock()
    {
        var beyondSetAffectBound = CreateNpcFixture(1_001, attackerRsv: 0x01);
        Check(Apply(beyondSetAffectBound.World, beyondSetAffectBound.TargetId, DamageSkill(), 0, 1, out _) ==
                  LegacySkillAttackResult.Accepted &&
              beyondSetAffectBound.World.TryGetNpcSnapshot(beyondSetAffectBound.TargetId, out var laterNpc) &&
              laterNpc is not null && !HasAffectType(laterNpc.AffectSnapshot, 1),
            "Skill-hit RSV_FROST crossed SetAffect's NPC 1000 bound.");

        var blockedByRsv = CreateNpcFixture(1_000, attackerRsv: 0x01, targetRsv: 0x80);
        Check(Apply(blockedByRsv.World, blockedByRsv.TargetId, DamageSkill(), 0, 1, out _) ==
                  LegacySkillAttackResult.Accepted &&
              blockedByRsv.World.TryGetNpcSnapshot(blockedByRsv.TargetId, out var blockedNpc) &&
              blockedNpc is not null && !HasAffectType(blockedNpc.AffectSnapshot, 1),
            "Skill-hit RSV_FROST ignored SetAffect's RSV_BLOCK gate.");
    }

    private static void DrainProcUsesSetTickNpcBoundaryAndRejectsMerchant()
    {
        var npc1001 = CreateNpcFixture(1_001, attackerRsv: 0x02);
        Check(Apply(npc1001.World, npc1001.TargetId, DamageSkill(), 1, 0, out var outcome) ==
                  LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 } &&
              npc1001.World.TryGetNpcSnapshot(npc1001.TargetId, out var affectedNpc) &&
              affectedNpc is not null && HasAffectType(affectedNpc.AffectSnapshot, 20) &&
              GetAffectValue(affectedNpc.AffectSnapshot, 20) == 10 &&
              GetAffectLevel(affectedNpc.AffectSnapshot, 20) == 40 &&
              outcome.TargetAffectSnapshot is { } affect && HasAffectType(affect, 20),
            "Skill-hit RSV_DRAIN did not apply SkillData 40 Type 20 through SetTick on NPC 1001.");

        var blockedByRsv = CreateNpcFixture(1_001, attackerRsv: 0x02, targetRsv: 0x80);
        Check(Apply(blockedByRsv.World, blockedByRsv.TargetId, DamageSkill(), 1, 0, out _) ==
                  LegacySkillAttackResult.Accepted &&
              blockedByRsv.World.TryGetNpcSnapshot(blockedByRsv.TargetId, out var rsvBlockedNpc) &&
              rsvBlockedNpc is not null && !HasAffectType(rsvBlockedNpc.AffectSnapshot, 20),
            "Skill-hit RSV_DRAIN ignored SetTick's RSV_BLOCK gate.");

        var merchantAt1001 = CreateNpcFixture(1_001, attackerRsv: 0x02, targetMerchant: 1);
        Check(Apply(merchantAt1001.World, merchantAt1001.TargetId, DamageSkill(), 1, 0, out _) ==
                  LegacySkillAttackResult.Accepted &&
              merchantAt1001.World.TryGetNpcSnapshot(merchantAt1001.TargetId, out var merchantNpc) &&
              merchantNpc is not null && !HasAffectType(merchantNpc.AffectSnapshot, 20),
            "Skill-hit RSV_DRAIN ignored SetTick's Merchant gate above NPC 1000.");
    }

    private static void SkillRsvProcsRequirePositiveDamageAndSuccessfulProcRolls()
    {
        var failedRolls = CreateNpcFixture(1_000, attackerRsv: 0x03);
        Check(Apply(failedRolls.World, failedRolls.TargetId, DamageSkill(), 1, 1, out var missedProc) ==
                  LegacySkillAttackResult.Accepted && missedProc is { Damage: > 0 } &&
              failedRolls.World.TryGetNpcSnapshot(failedRolls.TargetId, out var unaffectedNpc) &&
              unaffectedNpc is not null && !HasAffectType(unaffectedNpc.AffectSnapshot, 1) &&
              !HasAffectType(unaffectedNpc.AffectSnapshot, 20),
            "A failed per-proc roll still applied an RSV skill-hit affect.");

        var noDamage = CreateNpcFixture(1_000, attackerRsv: 0x03, targetClan: 6);
        Check(Apply(noDamage.World, noDamage.TargetId, DamageSkill(), 0, 0, out var zeroDamage) ==
                  LegacySkillAttackResult.Accepted && zeroDamage is { Damage: <= 0 } &&
              noDamage.World.TryGetNpcSnapshot(noDamage.TargetId, out var untouchedNpc) &&
              untouchedNpc is not null && !HasAffectType(untouchedNpc.AffectSnapshot, 1) &&
              !HasAffectType(untouchedNpc.AffectSnapshot, 20),
            "An RSV skill-hit proc ran after the source's non-positive-damage continue.");
    }

    private static void FrostProcProjectsPlayerType1Score()
    {
        var fixture = CreatePlayerFixture(attackerRsv: 0x01);
        var result = Apply(fixture.World, fixture.TargetId, DamageSkill(), 0, 1, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 } &&
              outcome.TargetAffectSnapshot is { } affected && HasAffectType(affected, 1) &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            "A skill-hit RSV_FROST did not project Type 1 on its player target.");
    }

    private static void DrainProcPersistsOnSummon()
    {
        var (world, summon) = CreateSummonFixture();
        Check(Apply(world, summon.ConnectionId, DamageSkill(), frostRoll: 1, drainRoll: 0, out var outcome) ==
                  LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 } &&
              outcome.TargetAffectSnapshot is { } affect && HasAffectType(affect, 20) &&
              GetAffectValue(affect, 20) == 10 && GetAffectLevel(affect, 20) == 40 &&
              GetAffectTime(affect, 20) == 1,
            "A positive skill hit with RSV_DRAIN did not persist SkillData 40 Type 20 on the summon.");
    }

    private static (WorldHub World, int TargetId) CreateNpcFixture(
        int targetId,
        ushort attackerRsv,
        ushort targetRsv = 0,
        byte targetMerchant = 0,
        byte targetClan = 0)
    {
        var world = new WorldHub(itemData: ItemData, skillData: SecondarySkills);
        var attackerMob = CreateMob(level: 1_500, damage: 300, attackRun: 0x96, intelligence: 100, special2: 40);
        BinaryPrimitives.WriteUInt16LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), attackerRsv);
        Check(world.Enter(1, "SKILL_RSV_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Skill-hit RSV attacker fixture was not installed.");

        var targetMob = CreateMob(level: 50, damage: 0, attackRun: 0x96, intelligence: 100, special2: 0);
        BinaryPrimitives.WriteUInt16LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), targetRsv);
        targetMob[LegacyAccountSnapshot.MobClanOffset] = targetClan;
        targetMob[LegacyAccountSnapshot.MobMerchantOffset] = targetMerchant;
        new LegacyItem(51, 0, 0, 0, 0, 0, 0)
            .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var createdNpcId = world.EnterNpc(
            targetMob,
            2_005,
            2_005,
            new byte[LegacyAccountSnapshot.AffectStride],
            requestedConnectionId: targetId);
        Check(createdNpcId == targetId, $"NPC fixture {targetId} could not be registered.");
        return (world, targetId);
    }

    private static (WorldHub World, int TargetId) CreatePlayerFixture(ushort attackerRsv)
    {
        var world = new WorldHub(itemData: ItemData, skillData: SecondarySkills);
        var attackerMob = CreateMob(level: 1_500, damage: 300, attackRun: 0x96, intelligence: 100, special2: 40);
        BinaryPrimitives.WriteUInt16LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), attackerRsv);
        Check(world.Enter(1, "SKILL_RSV_PLAYER_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Skill-hit RSV player attacker fixture was not installed.");

        var targetMob = CreateMob(level: 50, damage: 0, attackRun: 0x96, intelligence: 100, special2: 0);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        new LegacyItem(51, 0, 0, 0, 0, 0, 0)
            .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.Enter(2, "SKILL_RSV_PLAYER_TARGET", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(2, 0, 0, 2, 0, 0, 2_001, 2_000, targetMob),
            "Skill-hit RSV player target fixture was not installed.");
        return (world, 2);
    }

    private static (WorldHub World, LegacySummonedMob Summon) CreateSummonFixture()
    {
        var run = FindReference769TmsrvRun();
        var catalog = LegacySummonCatalog.Load(Path.Combine(run, "BaseSummon"));
        var world = new WorldHub(itemData: ItemData, skillData: SecondarySkills, summonCatalog: catalog);
        var attackerMob = CreateMob(level: 1_500, damage: 300, attackRun: 0x96, intelligence: 100, special2: 40);
        BinaryPrimitives.WriteUInt16LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x02);
        var ownerMob = CreateMob(level: 1_500, damage: 0, attackRun: 0x96, intelligence: 100, special2: 40);
        var ownerScore = LegacyScore.Read(ownerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { Special3 = 40 };
        ownerScore.Write(ownerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.Enter(1, "SKILL_RSV_SUMMON_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.Enter(2, "SKILL_RSV_SUMMON_OWNER", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, 1, 0, 0, 2_005, 2_005, ownerMob),
            "Skill-hit RSV summon participants could not be registered.");

        var summonSkill = new LegacySkillDefinition(
            50, 20, 0, 20, 0, 0, 11, 8, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "TEST_SUMMON");
        LegacySkillAttackOutcome? outcome = null;
        Check(world.TryConsumeSkillMana(2, summonSkill, out _, out _, out _) == LegacySkillManaResult.Accepted &&
              world.TryApplySkillAttack(2, 2, summonSkill, null, 0, 90, out outcome) == LegacySkillAttackResult.Accepted &&
              outcome?.SummonedMobs is { Count: 1 },
            "Skill-hit RSV fixture could not create its test summon.");
        return (world, outcome!.SummonedMobs![0]);
    }

    private static string FindReference769TmsrvRun()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("TMProject 7.69 TMSrv run directory was not found from the test output directory.");
    }

    private static LegacySkillAttackResult Apply(
        WorldHub world,
        int targetId,
        LegacySkillDefinition skill,
        int frostRoll,
        int drainRoll,
        out LegacySkillAttackOutcome? outcome) =>
        world.TryApplySkillAttack(
            1,
            targetId,
            skill,
            ItemData,
            weather: 0,
            randomFactor: 100,
            out outcome,
            attackMissRoll: 99,
            frostRoll: frostRoll,
            drainRoll: drainRoll);

    private static LegacySkillDefinition DamageSkill() =>
        new(2, 66, 1, 10, 3, 3, 1, 13, 0, 0, 0, 0, 0,
            [7, 0, 0, 7, 0, 0, 0, 0], [7, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 1, 2, 0, 0, 0, 1, "Golpe_Duplo");

    private static byte[] CreateMob(int level, int damage, byte attackRun, short intelligence, short special2, int armor = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, armor, damage, 0, attackRun, 0, 0,
            1_000_000, 1_000, 1_000_000, 1_000, 0, intelligence, 0, 0, 0, special2, 0, 0);
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

    private static byte GetAffectValue(ReadOnlySpan<byte> affect, byte type) =>
        affect[FindAffectOffset(affect, type) + 1];

    private static ushort GetAffectLevel(ReadOnlySpan<byte> affect, byte type) =>
        BinaryPrimitives.ReadUInt16LittleEndian(affect.Slice(FindAffectOffset(affect, type) + 2, sizeof(ushort)));

    private static uint GetAffectTime(ReadOnlySpan<byte> affect, byte type) =>
        BinaryPrimitives.ReadUInt32LittleEndian(affect.Slice(FindAffectOffset(affect, type) + 4, sizeof(uint)));

    private static int FindAffectOffset(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return offset;
        throw new InvalidOperationException($"Affect type {type} was not found.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
