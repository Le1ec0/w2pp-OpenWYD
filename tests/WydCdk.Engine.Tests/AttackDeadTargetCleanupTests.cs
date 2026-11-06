using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackDeadTargetCleanupTests
{
    private const int AttackerId = 1;
    private const int DeadNpcId = 20_101;
    private const int LiveNpcId = 20_102;
    private const short TargetX = 3_330;
    private const short TargetY = 1_538;

    public static void Run()
    {
        GateMatchesTheLegacyHpAndSkillExceptions();
        DeadPhysicalTargetClearsItsWireSlotAndTheBatchContinues();
        RevivalSkillsKeepTheDeadTargetException();
    }

    private static void GateMatchesTheLegacyHpAndSkillExceptions()
    {
        Check(LegacyAttackDeadTargetGate.ShouldClearTargetSlot(0, skillIndex: -1) &&
            LegacyAttackDeadTargetGate.ShouldClearTargetSlot(-1, skillIndex: 103),
            "The 7.69 dead-target gate must clear HP zero or negative for ordinary attacks.");
        Check(!LegacyAttackDeadTargetGate.ShouldClearTargetSlot(1, skillIndex: -1) &&
            !LegacyAttackDeadTargetGate.ShouldClearTargetSlot(0, skillIndex: 31) &&
            !LegacyAttackDeadTargetGate.ShouldClearTargetSlot(0, skillIndex: 99),
            "The 7.69 dead-target gate changed positive HP or the revive-skill exceptions.");
    }

    private static void DeadPhysicalTargetClearsItsWireSlotAndTheBatchContinues()
    {
        var world = CreateWorld();
        Check(world.ShouldClearDeadTargetInAttack(DeadNpcId, skillIndex: -1),
            "A zero-HP target was not selected for the 7.69 MSG_Attack cleanup.");

        var request = new LegacyAttackActionBatchRequest(
            AttackerId,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, DeadNpcId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, LiveNpcId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
            ],
            SupportsSecondaryDamageSlot: true);

        Check(world.TryApplyAttackActionBatch(request, out var batch) && batch is { CompletedNormally: true },
            "A dead target aborted the remaining MSG_Attack slots.");
        var result = batch!;
        var deadSlot = result.SlotOutcomes.Single(slot => slot.SlotIndex == 0);
        var liveSlot = result.SlotOutcomes.Single(slot => slot.SlotIndex == 1);
        Check(deadSlot.PhysicalResult == LegacyPhysicalAttackResult.TargetNotAlive && deadSlot.PhysicalOutcome is null,
            "A dead physical target was processed as a normal hit.");
        Check(result.WireSlotOverrides.Any(slot => slot.SlotIndex == 0 && slot.TargetConnectionId == 0 && slot.Damage == 0),
            "The dead target's relay slot did not clear TargetID and Damage to zero.");
        Check(liveSlot.PhysicalResult == LegacyPhysicalAttackResult.Accepted && liveSlot.PhysicalOutcome is { Damage: > 0 },
            "The slot after a dead target did not resolve normally.");
        Check(world.TryGetNpcSnapshot(LiveNpcId, out var liveNpc) && liveNpc is not null &&
            LegacyMobCombatState.Read(liveNpc.MobSnapshot).CurrentScore.Hp < 10_000,
            "The live target after a dead slot did not take damage.");
    }

    private static void RevivalSkillsKeepTheDeadTargetException()
    {
        var world = CreateWorld();
        Check(!world.ShouldClearDeadTargetInAttack(DeadNpcId, skillIndex: 31) &&
            !world.ShouldClearDeadTargetInAttack(DeadNpcId, skillIndex: 99),
            "The dead-target gate suppressed the 7.69 Renascimento or Ressurreição exception.");
    }

    private static WorldHub CreateWorld()
    {
        var attackerMob = CreateMob("DEAD_TARGET_ATTACKER", hp: 5_000, damage: 1_000);
        var world = new WorldHub();
        Check(world.Enter(AttackerId, "DEAD_TARGET_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The attack actor was not registered.");
        Check(world.SetCharacterState(AttackerId, 0, guildId: 0, clan: 1, guildLevel: 0, coin: 0,
                TargetX, TargetY, attackerMob),
            "The attack actor state was not installed.");

        AddNpc(world, DeadNpcId, "DEAD_TARGET", hp: 0);
        AddNpc(world, LiveNpcId, "LIVE_TARGET", hp: 10_000);
        return world;
    }

    private static void AddNpc(WorldHub world, int connectionId, string name, int hp)
    {
        var mob = CreateMob(name, hp, damage: 0);
        var registeredId = world.EnterNpc(mob, TargetX + 5, TargetY + 5,
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
