using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPvpJewelAbsorptionTests
{
    public static void Run()
    {
        PhysicalHitAppliesType8AbsorbFromFinalDamage();
        SkillHitAppliesType8AbsorbFromFinalDamage();
        SkillHitAgainstPlayerAppliesTargetReflectionInLegacyOrder();
        SkillHitAgainstNpcAppliesType8Absorb();
        ActiveAffectsApplyTheCapInSlotOrder();
        ZeroDamageAndFullHpDoNotAbsorb();
        LegacyOverflowAdjustmentMatches769();
    }

    public static void PhysicalHitAppliesType8AbsorbFromFinalDamage()
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(2), 1 << 3);

        var world = new WorldHub();
        Check(world.Enter(1, "ABSORB_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "ABSORB_TARGET", (_, _) => ValueTask.CompletedTask),
            "The Type 8 absorb participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000,
                CreateMob("ABSORB_ATTACKER", damage: 1_000, currentHp: 50, maximumHp: 100),
                LegacyAccountSnapshot.ClassMasterMortal, affects) &&
            world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_005, 2_005,
                CreateMob("ABSORB_TARGET", damage: 1_000, currentHp: 1_000, maximumHp: 10_000)),
            "The Type 8 absorb character snapshots were not installed.");

        var result = world.TryApplyPhysicalAttack(
            1,
            2,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            absorptionRoll: 1);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0 },
            $"The deterministic physical hit did not land: result={result}, damage={outcome?.Damage}.");
        var expectedAbsorption = outcome!.Damage * 2 / 100;
        Check(outcome.AttackerHpAbsorbed == expectedAbsorption,
            $"Type 8 bit 3 did not restore 2% of finalized damage regardless of the legacy roll: damage={outcome.Damage}, absorbed={outcome.AttackerHpAbsorbed}, expected={expectedAbsorption}.");
        Check(world.TryGetResourceState(1, out var attackerState, out var requestedHp, out _) &&
            attackerState!.CurrentScore.Hp == 50 + expectedAbsorption && requestedHp == 50 + expectedAbsorption,
            $"Type 8 bit 3 did not update authoritative/current requested HP: hp={attackerState?.CurrentScore.Hp}, requested={requestedHp}, expected={50 + expectedAbsorption}.");
    }

    private static void SkillHitAppliesType8AbsorbFromFinalDamage()
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(2), 1 << 3);
        var world = CreateWorld(affects, attackerHp: 50, attackerMaxHp: 1_000, targetHp: 20_000, targetMaxHp: 30_000);
        var skill = new LegacySkillDefinition(79, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 0, 0, 0, 0, 0, "ABSORB_SKILL_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

        var result = world.TryApplySkillAttack(1, 2, skill, itemData, weather: 0, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 10_000 },
            $"The deterministic skill hit did not resolve: result={result}, damage={outcome?.Damage}.");
        var expectedHp = 50 + outcome!.Damage * 2 / 100;
        Check(world.TryGetCombatState(1, out var attackerState) && attackerState!.CurrentScore.Hp == expectedHp,
            $"Type 8 bit 3 did not run on the player-target skill branch: hp={attackerState?.CurrentScore.Hp}, expected={expectedHp}.");
    }

    private static void SkillHitAgainstNpcAppliesType8Absorb()
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(2), 1 << 3);
        var attackerMob = CreateMob("ABSORB_ATTACKER", damage: 100, currentHp: 50, maximumHp: 1_000);
        var npcMob = CreateMob("ABSORB_NPC", damage: 100, currentHp: 1_000, maximumHp: 1_000);
        npcMob[LegacyAccountSnapshot.MobClassOffset] = 0;
        var world = new WorldHub();
        Check(world.Enter(1, "ABSORB_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, attackerMob,
                LegacyAccountSnapshot.ClassMasterMortal, affects),
            "The Type 8 NPC-skill attacker was not registered or initialized.");
        var npcId = world.EnterNpc(npcMob, 2_005, 2_005, new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 20_010);
        Check(npcId == 20_010, $"The Type 8 NPC-skill target could not be registered: {npcId}.");

        var skill = new LegacySkillDefinition(79, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 0, 0, 0, 0, 0, "ABSORB_NPC_SKILL_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var result = world.TryApplySkillAttack(1, npcId, skill, itemData, weather: 0,
            randomFactor: 99, out var outcome);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 },
            $"The deterministic NPC-target skill did not resolve: result={result}, damage={outcome?.Damage}.");
        var expectedHp = 50 + outcome!.Damage * 2 / 100;
        Check(world.TryGetCombatState(1, out var attackerState) && attackerState!.CurrentScore.Hp == expectedHp,
            $"Type 8 bit 3 did not run on the NPC-target skill branch: hp={attackerState?.CurrentScore.Hp}, expected={expectedHp}.");
    }

    private static void SkillHitAgainstPlayerAppliesTargetReflectionInLegacyOrder()
    {
        var itemData = CreateReflectionItemData();
        var targetMob = CreateMob("REFLECTING_TARGET", damage: 10_000, currentHp: 20_000, maximumHp: 30_000);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(
            targetMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset),
            1u << 17);
        (LegacyMobCombatState.Read(targetMob).CurrentScore with { Special4 = 59 })
            .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(16, 0, 0, 0, 0, 0, 0).Write(
            targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));

        var world = new WorldHub();
        Check(world.Enter(1, "REFLECTION_SKILL_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.Enter(2, "REFLECTION_SKILL_TARGET", (_, _) => ValueTask.CompletedTask),
            "The skill-reflection participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000,
                  CreateMob("REFLECTION_SKILL_ATTACKER", damage: 1_000, currentHp: 1_000, maximumHp: 1_000)) &&
              world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_005, 2_005, targetMob),
            "The skill-reflection character snapshots were not installed.");

        var skill = new LegacySkillDefinition(79, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0,
            [], [], 0, 0, 1, 0, 0, 0, 0, 0, "REFLECTION_SKILL_TEST");
        var result = world.TryApplySkillAttack(
            1, 2, skill, itemData, weather: 0, randomFactor: 99, out var outcome, attackMissRoll: 99);

        // 7.69/W2PP truncate damage / 100 before multiplying ReflectPvP: 10,000 - 30 - (9,970 / 100 * 5).
        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 9_475, RemainingHp: 10_525 },
            $"The skill PvP path skipped or reordered target ReflectDamage/ReflectPvP: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
    }

    private static LegacyItemDataTable CreateReflectionItemData()
    {
        const short reflectingItemIndex = 16;
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        var decoded = new byte[LegacyItemDataTable.RecordSizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(decoded.AsSpan(80), LegacyItemEffect.PvpDefense);
        BinaryPrimitives.WriteInt16LittleEndian(decoded.AsSpan(82), 50);
        BinaryPrimitives.WriteInt16LittleEndian(decoded.AsSpan(142), 8);
        var record = encoded.AsSpan(reflectingItemIndex * LegacyItemDataTable.RecordSizeInBytes, LegacyItemDataTable.RecordSizeInBytes);
        for (var index = 0; index < decoded.Length; index++)
            record[index] = (byte)(decoded[index] ^ 0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static void ActiveAffectsApplyTheCapInSlotOrder()
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(2), 1 << 3);
        affects[8] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(10), 1 << 3);

        var actual = LegacyPvpJewelAttackMath.ApplyAbsorption(affects, damage: 20_000, startingHp: 0, maximumHp: 10_000);

        Check(actual == new LegacyPvpJewelAbsorptionStage(CurrentHp: 700, AbsorbedHp: 700),
            $"Type 8 bit 3 did not apply a per-affect 350 HP cap in slot order: {actual}.");
    }

    private static void ZeroDamageAndFullHpDoNotAbsorb()
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(2), 1 << 3);

        Check(LegacyPvpJewelAttackMath.ApplyAbsorption(affects, damage: 100, startingHp: 100, maximumHp: 100)
                == new LegacyPvpJewelAbsorptionStage(100, 0),
            "A full-HP attacker received Type 8 absorb.");
        Check(LegacyPvpJewelAttackMath.ApplyAbsorption(affects, damage: 0, startingHp: 50, maximumHp: 100)
                == new LegacyPvpJewelAbsorptionStage(50, 0),
            "A zero-damage action received Type 8 absorb.");
        Check(LegacyPvpJewelAttackMath.ApplyAbsorption(affects, damage: -3, startingHp: 50, maximumHp: 100)
                == new LegacyPvpJewelAbsorptionStage(50, 0),
            "A legacy miss received Type 8 absorb.");
    }

    private static void LegacyOverflowAdjustmentMatches769()
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(2), 1 << 3);

        var actual = LegacyPvpJewelAttackMath.ApplyAbsorption(affects, damage: 20_000, startingHp: 999, maximumHp: 1_000);

        Check(actual == new LegacyPvpJewelAbsorptionStage(CurrentHp: 1_348, AbsorbedHp: 349),
            $"The Type 8 legacy MaxHP overflow adjustment changed: {actual}.");

        var alreadyOverMaximum = LegacyPvpJewelAttackMath.ApplyAbsorption(
            affects, damage: 20_000, startingHp: 1_348, maximumHp: 1_000);
        Check(alreadyOverMaximum == new LegacyPvpJewelAbsorptionStage(CurrentHp: 1_696, AbsorbedHp: 348),
            $"The Type 8 legacy overflow adjustment for already-over-maximum HP changed: {alreadyOverMaximum}.");
    }

    private static WorldHub CreateWorld(byte[] affects, int attackerHp, int attackerMaxHp, int targetHp, int targetMaxHp)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "ABSORB_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "ABSORB_TARGET", (_, _) => ValueTask.CompletedTask),
            "The Type 8 absorb participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000,
                CreateMob("ABSORB_ATTACKER", damage: 1_000, currentHp: attackerHp, maximumHp: attackerMaxHp),
                LegacyAccountSnapshot.ClassMasterMortal, affects) &&
            world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_005, 2_005,
                CreateMob("ABSORB_TARGET", damage: 10_000, currentHp: targetHp, maximumHp: targetMaxHp)),
            "The Type 8 absorb character snapshots were not installed.");
        return world;
    }

    private static byte[] CreateMob(string name, int damage, int currentHp, int maximumHp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, 0);
        new LegacyScore(50, 0, damage, 0, 0, 0, 0, maximumHp, 1_000, currentHp, 1_000,
            0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
