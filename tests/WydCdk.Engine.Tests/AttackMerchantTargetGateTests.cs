using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackMerchantTargetGateTests
{
    private const int AttackerId = 1;
    private const int ProtectedMerchantId = 20_201;
    private const int OrdinaryMerchantId = 20_202;
    private const int LiveTargetId = 20_203;
    private const short TargetX = 3_330;
    private const short TargetY = 1_538;

    public static void Run()
    {
        GateMatchesTheLegacyMerchantValues();
        ProtectedMerchantClearsTheWireSlotAndTheBatchContinues();
    }

    private static void GateMatchesTheLegacyMerchantValues()
    {
        foreach (var merchant in new byte[] { 1, 4, 43, 100 })
            Check(LegacyAttackMerchantGate.ShouldClearTargetSlot(merchant),
                $"The 7.69 Merchant gate did not block Merchant={merchant}.");

        Check(!LegacyAttackMerchantGate.ShouldClearTargetSlot(0) &&
            !LegacyAttackMerchantGate.ShouldClearTargetSlot(72),
            "The 7.69 Merchant gate blocked an ordinary target or Pista Merchant=72.");
    }

    private static void ProtectedMerchantClearsTheWireSlotAndTheBatchContinues()
    {
        var world = CreateWorld();
        Check(world.ShouldClearMerchantTargetInAttack(ProtectedMerchantId),
            "A protected Merchant target was not selected for MSG_Attack cleanup.");
        Check(!world.ShouldClearMerchantTargetInAttack(OrdinaryMerchantId),
            "Pista Merchant=72 was incorrectly selected for MSG_Attack cleanup.");

        var request = new LegacyAttackActionBatchRequest(
            AttackerId,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, ProtectedMerchantId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, LiveTargetId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
            ],
            SupportsSecondaryDamageSlot: true);

        Check(world.TryApplyAttackActionBatch(request, out var batch) && batch is { CompletedNormally: true },
            "A protected Merchant target aborted the remaining MSG_Attack slots.");
        var result = batch!;
        var merchantSlot = result.SlotOutcomes.Single(slot => slot.SlotIndex == 0);
        var liveSlot = result.SlotOutcomes.Single(slot => slot.SlotIndex == 1);
        Check(merchantSlot.PhysicalResult == LegacyPhysicalAttackResult.TargetMerchantBlocked && merchantSlot.PhysicalOutcome is null,
            "A protected Merchant target was processed as a normal hit.");
        Check(result.WireSlotOverrides.Any(slot => slot.SlotIndex == 0 && slot.TargetConnectionId == 0 && slot.Damage == 0),
            "The Merchant target's relay slot did not clear TargetID and Damage to zero.");
        Check(liveSlot.PhysicalResult == LegacyPhysicalAttackResult.Accepted && liveSlot.PhysicalOutcome is { Damage: > 0 },
            "The slot after a protected Merchant target did not resolve normally.");
    }

    private static WorldHub CreateWorld()
    {
        var world = new WorldHub();
        var attackerMob = CreateMob("MERCHANT_ATTACKER", hp: 5_000, damage: 1_000, merchant: 0);
        Check(world.Enter(AttackerId, "MERCHANT_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The attack actor was not registered.");
        Check(world.SetCharacterState(AttackerId, 0, guildId: 0, clan: 1, guildLevel: 0, coin: 0,
                TargetX, TargetY, attackerMob),
            "The attack actor state was not installed.");

        AddNpc(world, ProtectedMerchantId, "PROTECTED_MERCHANT", hp: 10_000, merchant: 43);
        AddNpc(world, OrdinaryMerchantId, "PISTA_MERCHANT", hp: 10_000, merchant: 72);
        AddNpc(world, LiveTargetId, "LIVE_TARGET", hp: 10_000, merchant: 0);
        return world;
    }

    private static void AddNpc(WorldHub world, int connectionId, string name, int hp, byte merchant)
    {
        var mob = CreateMob(name, hp, damage: 0, merchant);
        var registeredId = world.EnterNpc(mob, TargetX + 5, TargetY + 5,
            requestedConnectionId: connectionId, generateIndex: 1);
        Check(registeredId == connectionId, $"NPC {name} was not registered with its expected connection ID.");
    }

    private static byte[] CreateMob(string name, int hp, int damage, byte merchant)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        new LegacyScore(100, 0, damage, merchant, 0, 0, 0, hp, 10_000, hp, 10_000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
