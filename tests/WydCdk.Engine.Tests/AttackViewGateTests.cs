using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackViewGateTests
{
    private const int AttackerId = 1;
    private const int OutOfViewTargetId = 20_301;
    private const int InViewTargetId = 20_302;
    private const short AttackerX = 1_000;
    private const short AttackerY = 1_000;

    public static void Run()
    {
        ViewGateUsesTheLegacyInclusive33TileBoxAndSkill42Exception();
        OutOfViewPhysicalSlotQueuesRemoveMobAndContinuesTheBatch();
        MissingTargetSlotsDistinguishEmptyMobFromInvalidIndex();
    }

    private static void ViewGateUsesTheLegacyInclusive33TileBoxAndSkill42Exception()
    {
        Check(!LegacyAttackViewGate.ShouldSendRemoveMob(AttackerX, AttackerY, AttackerX + 33, AttackerY + 33, 1),
            "The view gate rejected a target on the inclusive 33-tile boundary.");
        Check(LegacyAttackViewGate.ShouldSendRemoveMob(AttackerX, AttackerY, AttackerX + 34, AttackerY, 1),
            "The view gate did not reject a target 34 tiles away on X.");
        Check(LegacyAttackViewGate.ShouldSendRemoveMob(AttackerX, AttackerY, AttackerX, AttackerY - 34, 1),
            "The view gate did not reject a target 34 tiles away on Y.");
        Check(!LegacyAttackViewGate.ShouldSendRemoveMob(AttackerX, AttackerY, AttackerX + 100, AttackerY, 42),
            "Skill 42 did not bypass the MSG_Attack view gate.");
    }

    private static void OutOfViewPhysicalSlotQueuesRemoveMobAndContinuesTheBatch()
    {
        var world = CreateWorld();
        Check(world.ShouldRemoveOutOfViewTargetInAttack(AttackerId, OutOfViewTargetId, skillIndex: 1),
            "The Host precheck did not identify an out-of-view target.");
        Check(!world.ShouldRemoveOutOfViewTargetInAttack(AttackerId, OutOfViewTargetId, skillIndex: 42),
            "The Host precheck ignored skill 42's view-gate exception.");

        var request = new LegacyAttackActionBatchRequest(
            AttackerId,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, OutOfViewTargetId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, InViewTargetId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
            ],
            SupportsSecondaryDamageSlot: true)
        {
            SkillIndex = 1,
        };

        Check(world.TryApplyAttackActionBatch(request, out var batch) && batch is { CompletedNormally: true },
            "An out-of-view target aborted the remaining MSG_Attack slots.");
        var result = batch!;
        var outOfViewSlot = result.SlotOutcomes.Single(slot => slot.SlotIndex == 0);
        var inViewSlot = result.SlotOutcomes.Single(slot => slot.SlotIndex == 1);
        Check(outOfViewSlot.PhysicalResult == LegacyPhysicalAttackResult.TargetOutOfView && outOfViewSlot.PhysicalOutcome is null,
            "The out-of-view target was processed as a normal hit or range abort.");
        Check(result.ViewRemovalEffects.Count == 1 &&
            result.ViewRemovalEffects[0].SlotIndex == 0 &&
            result.ViewRemovalEffects[0].TargetConnectionId == OutOfViewTargetId,
            "The batch did not retain a RemoveMob effect for the out-of-view slot.");
        Check(result.WireSlotOverrides.All(slot => slot.SlotIndex != 0),
            "The view gate rewrote the incoming Damage[] slot instead of only queuing RemoveMob.");
        Check(inViewSlot.PhysicalResult == LegacyPhysicalAttackResult.Accepted && inViewSlot.PhysicalOutcome is { Damage: > 0 },
            "The slot after an out-of-view target did not resolve normally.");
        Check(!world.ShouldRemoveOutOfViewTargetInAttack(AttackerId, InViewTargetId, skillIndex: 1),
            "A target inside the view box was incorrectly removed.");
    }

    private static void MissingTargetSlotsDistinguishEmptyMobFromInvalidIndex()
    {
        var world = CreateWorld();
        Check(world.GetAttackTargetSlotState(0) == LegacyAttackTargetSlotState.InvalidIndex &&
            world.GetAttackTargetSlotState(24_999) == LegacyAttackTargetSlotState.Empty &&
            world.GetAttackTargetSlotState(25_000) == LegacyAttackTargetSlotState.InvalidIndex &&
            world.GetAttackTargetSlotState(InViewTargetId) == LegacyAttackTargetSlotState.Active,
            "The Host precheck did not distinguish invalid, empty, and active MOB slots.");
    }

    private static WorldHub CreateWorld()
    {
        var world = new WorldHub();
        var attackerMob = CreateMob("VIEW_ATTACKER", hp: 5_000, damage: 1_000);
        Check(world.Enter(AttackerId, "VIEW_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The attack actor was not registered.");
        Check(world.SetCharacterState(AttackerId, 0, guildId: 0, clan: 1, guildLevel: 0, coin: 0,
                AttackerX, AttackerY, attackerMob),
            "The attack actor state was not installed.");

        AddNpc(world, OutOfViewTargetId, "OUT_OF_VIEW", (short)(AttackerX + 34), AttackerY);
        AddNpc(world, InViewTargetId, "IN_VIEW", (short)(AttackerX + 5), (short)(AttackerY + 5));
        return world;
    }

    private static void AddNpc(WorldHub world, int connectionId, string name, short x, short y)
    {
        var registeredId = world.EnterNpc(CreateMob(name, hp: 10_000, damage: 0), x, y,
            requestedConnectionId: connectionId, generateIndex: 1);
        Check(registeredId == connectionId, $"NPC {name} was not registered with its expected connection ID.");
    }

    private static byte[] CreateMob(string name, int hp, int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        new LegacyScore(100, 0, damage, 0, 0, 0, 0, hp, 10_000, hp, 10_000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
