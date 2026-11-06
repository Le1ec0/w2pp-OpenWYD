using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSameClanAreaGateTests
{
    private const int AttackerId = 1;
    private const int TargetPlayerId = 2;
    private const int TargetNpcId = 20_201;
    private const int TargetHp = 10_000;
    private const short AreaX = 1_020;
    private const short AreaY = 1_920;

    public static void PhysicalDamageIsSuppressedForSameClanInsideArea()
    {
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true);
        var result = world.TryApplyPhysicalAttack(
            AttackerId, TargetPlayerId, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: 0, RemainingHp: TargetHp, ClearTargetIdOnWire: true },
            $"Same-clan physical damage inside the legacy area was not suppressed: result={result}, outcome={outcome}.");
    }

    public static void PhysicalAreaGateSkipsLaterManaControlEffects()
    {
        var targetAffect = new byte[LegacyAccountSnapshot.AffectStride];
        targetAffect[0] = 18;
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true, targetAffect);
        var result = world.TryApplyPhysicalAttack(
            AttackerId, TargetPlayerId, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is
            { Damage: 0, RemainingHp: TargetHp, TargetResourceChanged: false, ClearTargetIdOnWire: true },
            $"The same-clan area gate did not continue before physical ManaControl: result={result}, outcome={outcome}.");
        Check(LegacyMobCombatState.Read(outcome!.TargetMobSnapshot).CurrentMana == 1_000,
            "The same-clan area gate changed target MP through the later ManaControl stage.");
    }

    public static void PlayerSkillDamageIsSuppressedForSameClanInsideArea()
    {
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true);
        var result = world.TryApplySkillAttack(
            AttackerId,
            TargetPlayerId,
            CreateDamageSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 0, RemainingHp: TargetHp, ClearTargetIdOnWire: true },
            $"Same-clan player skill damage inside the legacy area was not suppressed: result={result}, outcome={outcome}.");
    }

    public static void NpcSkillDamageIsSuppressedForSameClanInsideArea()
    {
        var world = CreateWorld(targetClan: 5, targetIsPlayer: false);
        var result = world.TryApplySkillAttack(
            AttackerId,
            TargetNpcId,
            CreateDamageSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 0, RemainingHp: TargetHp, ClearTargetIdOnWire: true },
            $"Same-clan NPC skill damage inside the legacy area was not suppressed: result={result}, outcome={outcome}.");
    }

    public static void GateUsesInclusiveBoundsAndRequiresPositiveDamage()
    {
        Check(LegacyAttackSameClanAreaGate.ShouldClearTargetId(1_017, 1_911, 5, 5, 1) &&
            LegacyAttackSameClanAreaGate.ShouldClearTargetId(1_290, 2_183, 5, 5, int.MaxValue),
            "The same-clan gate did not include both opposite area boundaries.");
        Check(!LegacyAttackSameClanAreaGate.ShouldClearTargetId(1_016, 1_911, 5, 5, 1) &&
            !LegacyAttackSameClanAreaGate.ShouldClearTargetId(1_290, 2_184, 5, 5, 1) &&
            !LegacyAttackSameClanAreaGate.ShouldClearTargetId(1_020, 1_920, 5, 4, 1) &&
            !LegacyAttackSameClanAreaGate.ShouldClearTargetId(1_020, 1_920, 5, 5, 0),
            "The same-clan gate expanded beyond its area, clan, or positive-damage conditions.");
    }

    public static void MixedBatchClearsBlockedTargetIdsOnWire()
    {
        var world = CreateWorld(targetClan: 5, targetIsPlayer: true);
        var request = new LegacyAttackActionBatchRequest(
            AttackerId,
            [
                new LegacyAttackActionBatchSlotRequest(
                    0,
                    LegacyAttackActionSlotKind.Physical,
                    TargetPlayerId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, TargetPlayerId),
            ],
            Skill: CreateDamageSkill(),
            ItemData: CreateItemData(),
            SupportsSecondaryDamageSlot: true,
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var batch) && batch is { CompletedNormally: true },
            "The same-clan area batch did not complete normally.");
        var result = batch!;
        Check(result.SlotOutcomes.Single(slot => slot.SlotIndex == 0).PhysicalOutcome is { Damage: 0, RemainingHp: TargetHp } &&
            result.SlotOutcomes.Single(slot => slot.SlotIndex == 1).SkillOutcome is { Damage: 0, RemainingHp: TargetHp },
            "A mixed batch changed HP for a same-clan target inside the legacy area.");
        Check(result.WireSlotOverrides.Count(slot => slot.TargetConnectionId == 0 && slot.Damage == 0) == 2 &&
            result.WireSlotOverrides.Any(slot => slot.SlotIndex == 0) &&
            result.WireSlotOverrides.Any(slot => slot.SlotIndex == 1),
            "The mixed batch did not clear TargetID and Damage for both blocked slots.");
    }

    private static WorldHub CreateWorld(byte targetClan, bool targetIsPlayer, byte[]? targetAffect = null)
    {
        var world = new WorldHub();
        var attackerMob = CreateMob("SAME_CLAN_AREA_ATTACKER", clan: 5, guild: 7, damage: 2_000, magic: 2_000);
        Check(world.Enter(AttackerId, "SAME_CLAN_AREA_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The same-clan area attacker was not registered.");
        Check(world.SetCharacterState(AttackerId, 0, guildId: 7, clan: 5, guildLevel: 0, coin: 0,
                AreaX, AreaY, attackerMob),
            "The same-clan area attacker state was not installed.");

        var targetGuild = targetIsPlayer ? 8 : 0;
        var targetMob = CreateMob("SAME_CLAN_AREA_TARGET", targetClan, targetGuild, damage: 0, magic: 0);
        if (targetIsPlayer)
        {
            Check(world.Enter(TargetPlayerId, "SAME_CLAN_AREA_TARGET", (_, _) => ValueTask.CompletedTask),
                "The same-clan area player target was not registered.");
            Check(world.SetCharacterState(TargetPlayerId, 0, guildId: targetGuild, clan: targetClan,
                    guildLevel: 0, coin: 0, AreaX + 5, AreaY, targetMob,
                    LegacyAccountSnapshot.ClassMasterMortal, targetAffect ?? new byte[LegacyAccountSnapshot.AffectStride]),
                "The same-clan area player target state was not installed.");
        }
        else
        {
            var registeredNpc = world.EnterNpc(targetMob, AreaX + 5, AreaY,
                requestedConnectionId: TargetNpcId, generateIndex: 1);
            Check(registeredNpc == TargetNpcId, "The same-clan area NPC was not registered at the requested ID.");
        }

        return world;
    }

    private static byte[] CreateMob(string name, byte clan, int guild, int damage, int magic)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guild));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), magic);
        var score = new LegacyScore(50, 0, damage, 0, 0, 0, 0, TargetHp, 1_000, TargetHp, 1_000, 100, 100, 0, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static LegacySkillDefinition CreateDamageSkill() => new(
        10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0,
        "SAME_CLAN_AREA_DAMAGE");

    private static LegacyItemDataTable CreateItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
