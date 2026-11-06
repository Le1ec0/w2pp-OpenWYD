using System.Reflection;
using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackMixedSlotTests
{
    private static readonly Lazy<LegacyItemDataTable> EmptyItemData = new(() =>
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray()));

    public static void ContractSurfaceExists()
    {
        var assembly = typeof(WorldHub).Assembly;
        Check(assembly.GetType("WydCdk.World.LegacyAttackActionSlotKind", throwOnError: false) is not null,
            "The typed physical/skill attack slot discriminator is missing.");
        Check(assembly.GetType("WydCdk.World.LegacyAttackActionBatchRequest", throwOnError: false) is not null,
            "The typed attack action batch request is missing.");
        Check(assembly.GetType("WydCdk.World.LegacyAttackActionBatchOutcome", throwOnError: false) is not null,
            "The aggregate attack action batch outcome is missing.");
        Check(typeof(WorldHub).GetMethod("TryApplyAttackActionBatch", BindingFlags.Instance | BindingFlags.Public) is not null,
            "WorldHub does not expose TryApplyAttackActionBatch.");
    }

    public static void RejectsInvalidShapeWithoutMutation()
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 40, 0, 0, 0, 0, 0, 1000, 1000, 1000, 500, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var world = new WorldHub();
        Check(world.Enter(1, "MIXED_SHAPE", (_, _) => ValueTask.CompletedTask), "Mixed-slot fixture participant did not enter the world.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2000, 2000, mob), "Mixed-slot fixture state was not installed.");
        Check(world.TryGetCombatState(1, out var before) && before is not null, "Mixed-slot fixture combat state is unavailable.");

        var duplicate = new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, 1);
        var invalidRequests = new (string Name, LegacyAttackActionBatchRequest Request)[]
        {
            ("empty", new LegacyAttackActionBatchRequest(1, [])),
            ("over 13", new LegacyAttackActionBatchRequest(1, Enumerable.Range(0, 14)
                .Select(index => new LegacyAttackActionBatchSlotRequest(index, LegacyAttackActionSlotKind.Physical, 1)).ToArray())),
            ("duplicate", new LegacyAttackActionBatchRequest(1, [duplicate, duplicate])),
            ("descending", new LegacyAttackActionBatchRequest(1, [
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, 1), duplicate])),
            ("out of range", new LegacyAttackActionBatchRequest(1, [new LegacyAttackActionBatchSlotRequest(13, LegacyAttackActionSlotKind.Physical, 1)])),
            ("skill without definition", new LegacyAttackActionBatchRequest(1, [new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 1)])),
        };

        foreach (var (name, request) in invalidRequests)
        {
            LegacyAttackActionBatchOutcome? outcome = null;
            var rejected = false;
            try
            {
                world.TryApplyAttackActionBatch(request, out outcome);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Check(rejected && outcome is null, $"The invalid {name} batch shape was not rejected before producing an outcome.");
            Check(world.TryGetCombatState(1, out var after) && after is not null &&
                after.CurrentScore.Hp == before!.CurrentScore.Hp && after.CurrentMana == before.CurrentMana,
                $"Rejecting the invalid {name} batch changed the participant's authoritative resources.");
        }
    }

    public static void SkillTargetIndexCanOverrideWireIndex()
    {
        var property = typeof(LegacyAttackActionBatchSlotRequest).GetProperty("SkillTargetIndex", BindingFlags.Instance | BindingFlags.Public);
        Check(property is not null && property.PropertyType == typeof(int?),
            "An attack action slot cannot preserve the legacy skill target index independently of its wire slot.");
    }

    public static void GenericSingleSlotSkillBatchMatchesPublicWrapper()
    {
        var cases = new (string Name, LegacySkillDefinition Skill, int TargetIndex, bool JoinParty, int TargetHp, LegacySkillAttackResult Expected)[]
        {
            ("accepted heal", AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 1), 0, true, 1000, LegacySkillAttackResult.Accepted),
            ("unsupported effect", AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0) with { Id = 30, InstanceType = 7, Aggressive = 1 }, 0, false, 1000, LegacySkillAttackResult.UnsupportedSkillType),
            ("MaxTarget/type28", AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 1), 2, false, 1000, LegacySkillAttackResult.TargetLimitExceeded),
            ("party/type27", AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 1), 0, false, 1000, LegacySkillAttackResult.PartySkillBlocked),
            ("dead target", AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0), 0, false, 0, LegacySkillAttackResult.TargetNotAlive),
        };

        foreach (var testCase in cases)
        {
            var genericHub = AttackPartySkillGateTests.CreateWorld(attackerGuild: 7, targetGuild: 8, targetHp: testCase.TargetHp);
            var wrapperHub = AttackPartySkillGateTests.CreateWorld(attackerGuild: 7, targetGuild: 8, targetHp: testCase.TargetHp);
            if (testCase.JoinParty)
            {
                Check(genericHub.TrySetParty(1, [1, 2]) && wrapperHub.TrySetParty(1, [1, 2]),
                    $"{testCase.Name}: matching party fixtures could not be formed.");
            }

            const int randomFactor = 90;
            const uint clientTick = 100;
            var request = new LegacyAttackActionBatchRequest(
                1,
                [new LegacyAttackActionBatchSlotRequest(
                    0,
                    LegacyAttackActionSlotKind.Skill,
                    2,
                    SkillRolls: new LegacySkillAttackBatchSlotRolls(RandomFactor: randomFactor),
                    SkillTargetIndex: testCase.TargetIndex)],
                Skill: testCase.Skill,
                ClientTick: clientTick);

            var batchCompleted = genericHub.TryApplyAttackActionBatch(request, out var batch);
            var wrapperResult = wrapperHub.TryApplySkillAttack(
                1, 2, testCase.Skill, null, 0, randomFactor, out var wrapperOutcome,
                targetIndex: testCase.TargetIndex,
                clientTick: clientTick);

            Check(batchCompleted && batch is { CompletedNormally: true, SlotOutcomes.Count: 1 },
                $"{testCase.Name}: a one-slot skill batch did not complete.");
            var slot = batch!.SlotOutcomes[0];
            Check(slot.Kind == LegacyAttackActionSlotKind.Skill && slot.SkillResult == testCase.Expected,
                $"{testCase.Name}: generic result was {slot.SkillResult}; expected {testCase.Expected}.");
            Check(wrapperResult == testCase.Expected,
                $"{testCase.Name}: compatibility wrapper result was {wrapperResult}; expected {testCase.Expected}.");
            Check(SkillOutcomesMatch(slot.SkillOutcome, wrapperOutcome),
                $"{testCase.Name}: generic and compatibility wrapper outcomes differ.");
            Check(genericHub.TryGetCombatState(2, out var genericTarget) && wrapperHub.TryGetCombatState(2, out var wrapperTarget) &&
                genericTarget!.CurrentScore.Hp == wrapperTarget!.CurrentScore.Hp && genericTarget.CurrentMana == wrapperTarget.CurrentMana,
                $"{testCase.Name}: generic and compatibility wrapper state differs.");
        }
    }

    public static void SkillThenPhysicalDoesNotResolveSlotZeroCritical()
    {
        var world = CreateMixedWorld(attackRun: 0xF0);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, 3,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
            ],
            Skill: AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0),
            ClientProgress: 4,
            SupportsSecondaryDamageSlot: true,
            PhysicalActionRolls: new LegacyPhysicalAttackBatchActionRolls(
                DoubleCriticalRoll: 0,
                SecondaryDamageRandomFactor: 99,
                SecondaryDamageFloorRoll: 0,
                DoubleCriticalChanceRoll: 0,
                DoubleCriticalDamageRoll: 0),
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true },
            "A skill slot followed by a physical slot did not complete as a mixed action.");
        var physical = action!.SlotOutcomes.Single(slot => slot.Kind == LegacyAttackActionSlotKind.Physical);
        Check(action.DoubleCritical == 0 && action.NextProgress == 4 && physical.PhysicalOutcome is { DoubleCritical: 0, NextProgress: 4 },
            "A physical slot after a skill incorrectly resolved slot-0 DoubleCritical/Progress.");
    }

    public static void PhysicalThenSkillSharesSlotZeroActionState()
    {
        var world = CreateMixedWorld(attackRun: 0xF0);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, 2,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3),
            ],
            Skill: AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0),
            ClientProgress: 4,
            SupportsSecondaryDamageSlot: true,
            PhysicalActionRolls: new LegacyPhysicalAttackBatchActionRolls(
                DoubleCriticalRoll: 0,
                SecondaryDamageRandomFactor: 99,
                SecondaryDamageFloorRoll: 0,
                DoubleCriticalChanceRoll: 0,
                DoubleCriticalDamageRoll: 0),
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true },
            "A physical slot followed by a skill slot did not complete as a mixed action.");
        var physical = action!.SlotOutcomes.Single(slot => slot.Kind == LegacyAttackActionSlotKind.Physical);
        Check(action.DoubleCritical == 1 && action.NextProgress == 5 && physical.PhysicalOutcome is { DoubleCritical: 1, NextProgress: 5 },
            "The physical slot at wire index 0 did not resolve and share DoubleCritical/Progress with the later skill slot.");
    }

    public static void PassiveReservationSkipsOriginalSkillSlotOne()
    {
        var world = CreateMixedWorld(attackerClass: 3, learnedSkill: 0x200000, strength: 1000, special4: 1000);
        Check(world.TryGetCombatState(3, out var before) && before is not null, "Passive reservation target was unavailable.");
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, 2,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3),
            ],
            Skill: AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0),
            SupportsSecondaryDamageSlot: true,
            PhysicalActionRolls: new LegacyPhysicalAttackBatchActionRolls(
                DoubleCriticalRoll: 0,
                SecondaryDamageRandomFactor: 99,
                SecondaryDamageFloorRoll: 0,
                DoubleCriticalChanceRoll: 0,
                DoubleCriticalDamageRoll: 0),
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true },
            "The passive-reservation mixed action did not complete.");
        Check(action!.DoubleCritical == 4 && action.WireSlotOverrides is [{ SlotIndex: 1, TargetConnectionId: 0, Damage: > 0 }],
            "The class-3 DoubleCritical passive did not replace wire slot 1 with its secondary physical result.");
        Check(world.TryGetCombatState(3, out var after) && after!.CurrentScore.Hp == before!.CurrentScore.Hp,
            "The original skill request in reserved wire slot 1 mutated its target.");
    }

    public static void MixedType28PrecedesPartyType27PerSlot()
    {
        var world = CreateMixedWorld();
        var skill = AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 1);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(2, LegacyAttackActionSlotKind.Skill, 2),
                new LegacyAttackActionBatchSlotRequest(3, LegacyAttackActionSlotKind.Skill, 3),
            ],
            Skill: skill,
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true, SlotOutcomes.Count: 2 },
            "Multiple skill slots did not produce an ordered batch result.");
        Check(action!.SlotOutcomes.All(slot => slot.SkillResult == LegacySkillAttackResult.TargetLimitExceeded && slot.SkillOutcome is null),
            "A later bParty/type27 check suppressed a per-slot MaxTarget/type28 result.");
        var crackErrors = action.SlotOutcomes.Count(slot => slot.SkillResult == LegacySkillAttackResult.TargetLimitExceeded)
            * LegacyAttackTargetLimitGate.CrackErrorPoints;
        Check(crackErrors == 20, "The two type28 skill slots were collapsed instead of retaining one error result per slot.");
    }

    public static void CrackErrorRemainsPerSlot()
    {
        var world = CreateMixedWorld();
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(2, LegacyAttackActionSlotKind.Skill, 2),
                new LegacyAttackActionBatchSlotRequest(3, LegacyAttackActionSlotKind.Skill, 3),
            ],
            Skill: AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 1),
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true, SlotOutcomes.Count: 2 },
            "The per-slot crack-error batch did not complete.");
        var erroredSlots = action!.SlotOutcomes
            .Where(slot => slot.SkillResult == LegacySkillAttackResult.TargetLimitExceeded)
            .Select(slot => slot.SlotIndex)
            .ToArray();
        Check(erroredSlots.SequenceEqual([2, 3]), "The batch did not preserve one type28 result for each offending skill slot.");
        var crackPoints = erroredSlots.Aggregate(0, (total, _) =>
            LegacyCrackErrorRules.Add(total, LegacyAttackTargetLimitGate.CrackErrorPoints, LegacyAttackTargetLimitGate.CrackErrorType).TotalPoints);
        Check(crackPoints == 20, "The type28 batch results do not yield independent 10-point crack-error updates per slot.");
    }

    public static void UnsupportedSkillMarkerDoesNotMutateItsTarget()
    {
        var world = CreateMixedWorld();
        var hasPhysicalTarget = world.TryGetCombatState(2, out var physicalTargetBefore);
        var hasSkillTarget = world.TryGetCombatState(3, out var skillTargetBefore);
        Check(hasPhysicalTarget && physicalTargetBefore is not null && hasSkillTarget && skillTargetBefore is not null,
            "Unsupported-marker fixture targets were unavailable.");
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Physical, 2,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3),
            ],
            Skill: AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0) with { Id = 30, InstanceType = 7, Aggressive = 1 },
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true, SlotOutcomes.Count: 2 },
            "An unsupported skill marker interrupted a mixed physical action.");
        Check(action!.SlotOutcomes[0].PhysicalResult == LegacyPhysicalAttackResult.Accepted &&
            action.SlotOutcomes[1].SkillResult == LegacySkillAttackResult.UnsupportedSkillType && action.SlotOutcomes[1].SkillOutcome is null,
            "The mixed action did not preserve the supported physical result and unsupported-skill result independently.");
        Check(world.TryGetCombatState(2, out var physicalTargetAfter) && physicalTargetAfter!.CurrentScore.Hp < physicalTargetBefore!.CurrentScore.Hp &&
            world.TryGetCombatState(3, out var skillTargetAfter) && skillTargetAfter!.CurrentScore.Hp == skillTargetBefore!.CurrentScore.Hp,
            "The unsupported marker changed its target or prevented the physical slot mutation.");
    }

    public static void HostCanRunSkillGatesWithoutApplyingUnportedEffects()
    {
        var world = CreateMixedWorld();
        Check(world.TryGetCombatState(3, out var before) && before is not null,
            "The unported-skill fixture target was unavailable.");
        var skill = AttackPartySkillGateTests.CreateHealingSkill(partyCheck: 0) with
        {
            Id = 30,
            InstanceType = 0,
            Aggressive = 1,
            AffectType = 0,
            TickType = 0,
        };
        var request = new LegacyAttackActionBatchRequest(
            1,
            [new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 3)],
            Skill: skill,
            ItemData: null,
            ClientTick: 100)
        {
            ApplySkillEffects = false,
        };

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true, SlotOutcomes.Count: 1 },
            "An unported skill with missing item data threw or rejected its mixed action.");
        Check(action!.SlotOutcomes[0] is { SkillResult: LegacySkillAttackResult.UnsupportedSkillType, SkillOutcome: null },
            "A skill whose effects are unported did not remain a marker-only slot.");
        Check(world.TryGetCombatState(3, out var after) && after!.CurrentScore.Hp == before!.CurrentScore.Hp &&
            after.CurrentMana == before.CurrentMana,
            "The gate-only skill path mutated its target or resources.");
    }

    public static void MixedSkillNpcDeathFinalizesOnceAfterNormalCompletion()
    {
        var world = CreateMixedWorld(attackerLevel: 50);
        var npcId = AddSkillNpc(world, hp: 50, damage: 100);
        var skill = CreateDamageSkill(maxTarget: 12);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, npcId),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, npcId),
            ],
            Skill: skill,
            ItemData: EmptyItemData.Value,
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true, SlotOutcomes.Count: 2 },
            "The repeated-NPC-skill action did not complete normally.");
        var firstSkillOutcome = action!.SlotOutcomes[0].SkillOutcome;
        Check(action.SlotOutcomes[0].SkillResult == LegacySkillAttackResult.Accepted &&
            firstSkillOutcome is { TargetRemoved: true, ExperienceAwarded: > 0, ExperienceAwards.Count: > 0 } &&
            action.SlotOutcomes[1].SkillResult == LegacySkillAttackResult.TargetNotAlive && action.SlotOutcomes[1].SkillOutcome is null,
            $"The skill NPC death was not finalized once after the action completed: removed={firstSkillOutcome?.TargetRemoved}, xp={firstSkillOutcome?.ExperienceAwarded}, awards={firstSkillOutcome?.ExperienceAwards?.Count}, second={action.SlotOutcomes[1].SkillResult}.");
        Check(!world.TryGetNpcSnapshot(npcId, out _), "The dead NPC remained registered after normal completion.");
    }

    public static void MixedPlayerResurrectionFinalizesAfterNormalCompletion()
    {
        var world = CreateMixedWorld();
        var target = GetPlayerMob(world, 2);
        BinaryPrimitives.WriteInt32LittleEndian(target.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 1);
        const int scrollSlot = 7;
        new LegacyItem(3463, 61, 2, 0, 0, 0, 0).Write(target.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (scrollSlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        Check(world.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, target), "Resurrection target with scroll could not be staged.");
        var skill = CreateDamageSkill(maxTarget: 12);
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillRolls: new LegacySkillAttackBatchSlotRolls(AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 2, SkillRolls: new LegacySkillAttackBatchSlotRolls(AttackMissRoll: 99)),
            ],
            Skill: skill,
            ItemData: EmptyItemData.Value,
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { CompletedNormally: true, SlotOutcomes.Count: 2 },
            "The repeated-player-skill action did not complete normally.");
        Check(action!.SlotOutcomes[0].SkillOutcome is { TargetRevived: true, RemainingHp: > 0, ConsumedItemSlot: scrollSlot } &&
            action.SlotOutcomes[1].SkillResult == LegacySkillAttackResult.TargetNotAlive && action.SlotOutcomes[1].SkillOutcome is null,
            "The player resurrection scroll was not consumed once after all attack slots finished.");
    }

    public static void PhysicalOutOfRangeAbortDefersEarlierSkillAndPhysicalDeaths()
    {
        var world = CreateMixedWorld(targetCount: 4, strength: 1000);
        var npcId = AddSkillNpc(world, hp: 50, damage: 100);
        var playerTarget = GetPlayerMob(world, 2);
        BinaryPrimitives.WriteInt32LittleEndian(playerTarget.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 1);
        const int scrollSlot = 7;
        new LegacyItem(3463, 61, 2, 0, 0, 0, 0).Write(playerTarget.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (scrollSlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        Check(world.SetCharacterState(2, 0, 0, 2, 0, 0, 2005, 2005, playerTarget), "Abort player death fixture could not be staged.");
        var farTarget = GetPlayerMob(world, 3);
        Check(world.SetCharacterState(3, 0, 0, 3, 0, 0, 2024, 2000, farTarget), "Abort range target could not be staged.");
        Check(world.TryGetCombatState(4, out var skippedTargetBefore) && skippedTargetBefore is not null,
            "The post-abort target was unavailable.");
        var request = new LegacyAttackActionBatchRequest(
            1,
            [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, npcId),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, 2,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(2, LegacyAttackActionSlotKind.Physical, 3,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
                new LegacyAttackActionBatchSlotRequest(3, LegacyAttackActionSlotKind.Skill, 4),
            ],
            Skill: CreateDamageSkill(maxTarget: 12),
            ItemData: EmptyItemData.Value,
            ClientTick: 100);

        Check(world.TryApplyAttackActionBatch(request, out var action) && action is { AbortSlotIndex: 2, CompletedNormally: false, SlotOutcomes.Count: 3 },
            "A later out-of-range physical slot did not abort the mixed batch.");
        Check(world.TryGetCombatState(4, out var skippedTargetAfter) && skippedTargetAfter!.CurrentScore.Hp == skippedTargetBefore!.CurrentScore.Hp,
            "The slot after the physical range abort was processed.");
        Check(action!.SlotOutcomes[0].SkillOutcome is { TargetRemoved: false, TargetRevived: false, ExperienceAwarded: 0 } &&
            world.TryGetNpcSnapshot(npcId, out var retainedNpc) && retainedNpc is not null && LegacyMobCombatState.Read(retainedNpc.MobSnapshot).CurrentScore.Hp == 0,
            "The aborted batch finalized a prior skill NPC death or rewards.");
        var physicalDeath = action.SlotOutcomes[1].PhysicalOutcome;
        Check(physicalDeath is { TargetDied: true, TargetRevived: false, ConsumedItemSlot: -1 } &&
            world.TryGetResourceState(2, out var deadState, out var requestedHp, out _) && deadState!.CurrentScore.Hp == 0 && requestedHp == 0 &&
            world.TryGetCharacterSnapshot(2, out var deadMob, out _, out _, out _) && deadMob is not null &&
            LegacyItem.Read(deadMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (scrollSlot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes)) is { Index: 3463, Effect1: 61, Value1: 2 },
            "The aborted batch consumed the scroll or finalized the physical player death.");
    }

    private static bool SkillOutcomesMatch(LegacySkillAttackOutcome? left, LegacySkillAttackOutcome? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.TargetConnectionId == right.TargetConnectionId &&
            left.BaseDamage == right.BaseDamage && left.Damage == right.Damage &&
            left.Resistance == right.Resistance && left.RemainingHp == right.RemainingHp &&
            left.TargetResourceChanged == right.TargetResourceChanged &&
            left.UpdatesAttackerState == right.UpdatesAttackerState &&
            left.TargetRevived == right.TargetRevived && left.TargetRemoved == right.TargetRemoved &&
            left.ExperienceAwarded == right.ExperienceAwarded && left.TargetHpChanged == right.TargetHpChanged &&
            (left.TargetMobSnapshot is null ? right.TargetMobSnapshot is null :
                right.TargetMobSnapshot is not null && left.TargetMobSnapshot.AsSpan().SequenceEqual(right.TargetMobSnapshot));
    }

    private static WorldHub CreateMixedWorld(
        int targetCount = 3,
        byte attackerClass = 0,
        uint learnedSkill = 0,
        byte attackRun = 0,
        short strength = 0,
        short special4 = 0,
        int attackerLevel = 100)
    {
        var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        attackerMob[LegacyAccountSnapshot.MobClassOffset] = attackerClass;
        BinaryPrimitives.WriteUInt32LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkill);
        new LegacyScore(attackerLevel, 0, 0, 0, attackRun, 0, 0, 5000, 5000, 5000, 5000, strength, 0, 0, 0, 0, 0, 0, special4)
            .Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var world = new WorldHub(itemData: EmptyItemData.Value);
        Check(world.Enter(1, "MIXED_ACTION_ATTACKER", (_, _) => ValueTask.CompletedTask), "Mixed-action attacker could not enter.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, attackerMob), "Mixed-action attacker state could not be installed.");
        for (var connectionId = 2; connectionId <= targetCount; connectionId++)
        {
            var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
            new LegacyScore(50, 10, 200, 0, 0, 0, 0, 5000, 5000, 5000, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
                .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            Check(world.Enter(connectionId, $"MIXED_ACTION_TARGET_{connectionId}", (_, _) => ValueTask.CompletedTask),
                $"Mixed-action target {connectionId} could not enter.");
            Check(world.SetCharacterState(connectionId, 0, 0, connectionId, 0, 0, 2005, 2005, targetMob),
                $"Mixed-action target {connectionId} state could not be installed.");
        }
        return world;
    }

    private static LegacySkillDefinition CreateDamageSkill(int maxTarget) => new(
        79, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, [], [], 0, 0, 1, maxTarget, 0, 0, 0, 0, "MIXED_DAMAGE");

    private static int AddSkillNpc(WorldHub world, int hp, int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 10, damage, 0, 0, 0, 0, 5000, 5000, hp, 500, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClanOffset] = 1;
        BinaryPrimitives.WriteInt64LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 1000);
        var npcId = world.EnterNpc(mob, 2005, 2005, requestedConnectionId: 20_010);
        Check(npcId > 0, "Mixed skill NPC target could not enter.");
        return npcId;
    }

    private static byte[] GetPlayerMob(WorldHub world, int connectionId)
    {
        Check(world.TryGetCharacterSnapshot(connectionId, out var mob, out _, out _, out _) && mob is not null,
            $"Mixed-action target {connectionId} snapshot was unavailable.");
        return mob!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
