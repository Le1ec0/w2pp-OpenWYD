using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackNpcHeadAreaGateTests
{
    private const int AttackerId = 1;
    private const int ProtectedPhysicalNpcId = 20_001;
    private const int ProtectedSkillNpcId = 20_002;
    private const int OrdinaryNpcId = 20_003;
    private const short AreaX = 26 * 128;
    private const short AreaY = 12 * 128;

    public static Task Run()
    {
        GateMatchesTheLegacyNpcHeadAndTileCondition();
        BatchClearsProtectedPhysicalAndSkillSlotsAndContinues();
        return Task.CompletedTask;
    }

    private static void GateMatchesTheLegacyNpcHeadAndTileCondition()
    {
        Check(LegacyAttackNpcHeadAreaGate.ShouldBlock(true, 219, AreaX, AreaY),
            "A non-player with head 219 in tile 26,12 was not blocked.");
        Check(LegacyAttackNpcHeadAreaGate.ShouldBlock(true, 220, AreaX + 127, AreaY + 127),
            "A non-player with head 220 at the last cell of tile 26,12 was not blocked.");
        Check(!LegacyAttackNpcHeadAreaGate.ShouldBlock(false, 219, AreaX, AreaY) &&
            !LegacyAttackNpcHeadAreaGate.ShouldBlock(true, 218, AreaX, AreaY) &&
            !LegacyAttackNpcHeadAreaGate.ShouldBlock(true, 219, AreaX - 1, AreaY) &&
            !LegacyAttackNpcHeadAreaGate.ShouldBlock(true, 220, AreaX, AreaY + 128),
            "The legacy NPC head-area gate blocked a player, another head, or an adjacent tile.");
    }

    private static void BatchClearsProtectedPhysicalAndSkillSlotsAndContinues()
    {
        var hub = CreateWorld();
        Check(hub.IsAttackTargetBlockedByNpcHeadArea(ProtectedPhysicalNpcId) &&
            hub.IsAttackTargetBlockedByNpcHeadArea(ProtectedSkillNpcId) &&
            !hub.IsAttackTargetBlockedByNpcHeadArea(OrdinaryNpcId) &&
            !hub.IsAttackTargetBlockedByNpcHeadArea(AttackerId),
            "The Host pre-marker gate misclassified a protected NPC, ordinary NPC, or player target.");
        var skill = new LegacySkillDefinition(10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 12, 0, 0, 0, 0, "HEAD_AREA_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var request = new LegacyAttackActionBatchRequest(
            AttackerId,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, ProtectedPhysicalNpcId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, ProtectedSkillNpcId,
                    SkillRolls: new LegacySkillAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(2, LegacyAttackActionSlotKind.Physical, OrdinaryNpcId,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
            ],
            Skill: skill,
            ItemData: itemData,
            ClientTick: 100);

        Check(hub.TryApplyAttackActionBatch(request, out var batch) && batch is { CompletedNormally: true },
            "A protected NPC slot aborted the remaining MSG_Attack targets.");
        var physicalGate = batch!.SlotOutcomes.Single(slot => slot.SlotIndex == 0);
        var skillGate = batch.SlotOutcomes.Single(slot => slot.SlotIndex == 1);
        var ordinaryHit = batch.SlotOutcomes.Single(slot => slot.SlotIndex == 2);
        Check(physicalGate.PhysicalResult == LegacyPhysicalAttackResult.NpcHeadAreaBlocked && physicalGate.PhysicalOutcome is null,
            "A protected physical NPC slot produced an outcome instead of being cleared.");
        Check(skillGate.SkillResult == LegacySkillAttackResult.NpcHeadAreaBlocked && skillGate.SkillOutcome is null,
            "A protected skill NPC slot produced an outcome instead of being cleared.");
        Check(ordinaryHit.PhysicalResult == LegacyPhysicalAttackResult.Accepted && ordinaryHit.PhysicalOutcome is { Damage: > 0 },
            "The slot after protected NPC targets did not resolve normally.");
        AssertNpcHp(hub, ProtectedPhysicalNpcId, 10_000,
            "A blocked physical target's HP changed.");
        AssertNpcHp(hub, ProtectedSkillNpcId, 10_000,
            "A blocked skill target's HP changed.");
        Check(hub.TryGetNpcSnapshot(OrdinaryNpcId, out var ordinaryNpc) && ordinaryNpc is not null &&
            LegacyMobCombatState.Read(ordinaryNpc.MobSnapshot).CurrentScore.Hp < 10_000,
            "The ordinary NPC slot did not apply its physical damage after the protected targets.");
    }

    private static WorldHub CreateWorld()
    {
        var attackerMob = CreateMob("HEAD_AREA_ATTACKER", headIndex: 0, hp: 5_000, damage: 1_000);
        var hub = new WorldHub();
        Check(hub.Enter(AttackerId, "HEAD_AREA_ATTACKER", (_, _) => ValueTask.CompletedTask),
            "The head-area attacker was not registered.");
        Check(hub.SetCharacterState(AttackerId, 0, guildId: 0, clan: 1, guildLevel: 0, coin: 0,
                3_330, 1_538, attackerMob),
            "The head-area attacker state was not installed.");

        AddNpc(hub, ProtectedPhysicalNpcId, "HEAD_219", 219, hp: 10_000);
        AddNpc(hub, ProtectedSkillNpcId, "HEAD_220", 220, hp: 10_000);
        AddNpc(hub, OrdinaryNpcId, "HEAD_AREA_ORDINARY", 0, hp: 10_000);
        return hub;
    }

    private static void AddNpc(WorldHub hub, int connectionId, string name, short headIndex, int hp)
    {
        var mob = CreateMob(name, headIndex, hp, damage: 0);
        var registeredId = hub.EnterNpc(mob, AreaX + 5, AreaY + 5,
            requestedConnectionId: connectionId, generateIndex: 1);
        Check(registeredId == connectionId, $"NPC {name} was not registered with its expected connection ID.");
    }

    private static byte[] CreateMob(string name, short headIndex, int hp, int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var encodedName = Encoding.ASCII.GetBytes(name);
        encodedName.AsSpan(0, Math.Min(encodedName.Length, 15)).CopyTo(mob);
        new LegacyScore(100, 0, damage, 0, 0, 0, 0, hp, 1_000, hp, 1_000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(headIndex, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        return mob;
    }

    private static void AssertNpcHp(WorldHub hub, int connectionId, int expectedHp, string message)
    {
        Check(hub.TryGetNpcSnapshot(connectionId, out var npc) && npc is not null,
            message);
        Check(LegacyMobCombatState.Read(npc!.MobSnapshot).CurrentScore.Hp == expectedHp, message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
