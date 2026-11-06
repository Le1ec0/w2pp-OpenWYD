using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackDivineBlessingTests
{
    private static readonly Lazy<LegacyItemDataTable> EmptyItemData = new(() =>
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray()));

    public static void SecondarySkillAdapterUsesMeasuredOffsetAndReadsOnlyBit2()
    {
        Check(LegacyAccountSnapshot.MobExtraStride == 552, "The MOBEXTRA fixture size is no longer 552 bytes.");
        var mobExtra = Enumerable.Repeat((byte)0xA5, LegacyAccountSnapshot.MobExtraStride).ToArray();

        foreach (var mask in new uint[] { 0x0, 0x1, 0x2, 0x4, 0x5, 0x7 })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), mask);
            var before = mobExtra.ToArray();
            Check(LegacyMobExtra.TryReadSecondaryLearnedSkill(mobExtra, out var actual) && actual == mask,
                $"The secondary-skill adapter did not read mask 0x{mask:X} at its measured offset.");
            Check(mobExtra.SequenceEqual(before), "Reading SecLearnedSkill changed the MOBEXTRA bytes.");

            var divineBlessingEnabled = (actual & 0x2) != 0;
            Check(divineBlessingEnabled == (mask is 0x2 or 0x7),
                $"Adjacent secondary-skill bits incorrectly changed the Divine Blessing decision for 0x{mask:X}.");
        }

        var truncated = new byte[LegacyAccountSnapshot.MobExtraStride - 1];
        Check(!LegacyMobExtra.TryReadSecondaryLearnedSkill(truncated, out _), "A truncated SecLearnedSkill field did not fail closed.");
    }

    public static void SecondarySkillAdapterWritesOnlyBit2()
    {
        var mobExtra = Enumerable.Repeat((byte)0x5A, LegacyAccountSnapshot.MobExtraStride).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), 0x5);
        var before = mobExtra.ToArray();

        Check(LegacyMobExtra.TrySetDivineBlessingLearnedBit(mobExtra, enabled: true),
            "The fixture adapter rejected a complete MOBEXTRA slot.");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset)) == 0x7,
            "Enabling Divine Blessing did not set only bit 0x2.");
        CheckUnrelatedBytesUnchanged(before, mobExtra);

        Check(LegacyMobExtra.TrySetDivineBlessingLearnedBit(mobExtra, enabled: false),
            "The fixture adapter could not clear bit 0x2.");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset)) == 0x5,
            "Disabling Divine Blessing did not preserve adjacent bits.");
        CheckUnrelatedBytesUnchanged(before, mobExtra);

        var truncated = new byte[LegacyAccountSnapshot.MobExtraStride - 1];
        Check(!LegacyMobExtra.TrySetDivineBlessingLearnedBit(truncated, enabled: true),
            "The fixture adapter accepted a truncated SecLearnedSkill field.");
    }

    public static void CombatStateReadsSecondarySkillFromAccountExtra()
    {
        var mob = CreateMob();
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        const uint learnedSecondaryMask = 0xA;
        BinaryPrimitives.WriteUInt32LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), learnedSecondaryMask);

        var world = new WorldHub();
        Check(world.Enter(1, "DIVINE_EXTRA_STATE", (_, _) => ValueTask.CompletedTask),
            "The Divine Blessing combat-state fixture could not enter the world.");
        Check(world.SetCharacterState(
                1, 0, 0, 0, 0, 0, 3000, 3000, mob,
                LegacyAccountSnapshot.ClassMasterMortal,
                new byte[LegacyAccountSnapshot.AffectStride], mobExtra),
            "The Divine Blessing combat-state fixture could not install the MOBEXTRA bytes.");

        Check(world.TryGetCombatState(1, out var state) && state is not null &&
            state.SecondaryLearnedSkill == learnedSecondaryMask,
            "WorldHub did not project the authoritative SecLearnedSkill mask into combat state.");
    }

    public static void Skill201MissingCsvDefinitionPassesTheSkillGate()
    {
        var table = LegacySkillDataTable.Load(new StringReader(string.Empty));
        Check(table[201] is null, "The test table unexpectedly contains a SkillData.csv row for skill 201.");

        var accepted = LegacySkillAttackGate.Evaluate(
            table, 201, characterClass: 3, learnedSkill: 0, clientTick: 1700, previousAttackTick: 1000);
        Check(accepted is { IsAccepted: true, IsPhysical: false, LearnedSkillError: null },
            $"Skill 201 required a CSV row, primary learned bit, or class-book membership: {accepted.Result}.");

        var tooSoon = LegacySkillAttackGate.Evaluate(
            table, 201, characterClass: 3, learnedSkill: 0, clientTick: 1699, previousAttackTick: 1000);
        Check(tooSoon.Result == SkillAttackValidationResult.SkillDelayTooShort,
            "Skill 201 bypassed the common 700 ms timing gate.");
        Check(LegacySkillAttackGate.Evaluate(
                table, 202, characterClass: 3, learnedSkill: 0, clientTick: 1700, previousAttackTick: 1000)
                .Result == SkillAttackValidationResult.MissingSkillDefinition,
            "The skill-201 exception admitted another undefined skill.");
    }

    public static void Skill201UsesZeroCostParentDescriptor()
    {
        var table = LegacySkillDataTable.Load(new StringReader(string.Empty));
        var descriptorType = typeof(WorldHub).Assembly.GetType("WydCdk.World.LegacyDivineBlessing", throwOnError: false);
        var descriptorProperty = descriptorType?.GetProperty("ParentActionDefinition");
        Check(descriptorProperty?.GetValue(null) is LegacySkillDefinition,
            "The server-only skill-201 parent descriptor is missing.");
        var parent = (LegacySkillDefinition)descriptorProperty!.GetValue(null)!;
        Check(parent.Id == 201 && parent.ManaSpent == 0 && parent.SkillPoint == 0 && parent.TargetType == 0 &&
            parent.Delay == 0 && parent.Range == 0 && parent.InstanceType == 0 && parent.InstanceValue == 0 &&
            parent.TickType == 0 && parent.TickValue == 0 && parent.AffectType == 0 && parent.AffectValue == 0 &&
            parent.AffectTime == 0 && parent.Action1.All(static value => value == 0) && parent.Action2.All(static value => value == 0) &&
            parent.InstanceAttribute == 0 && parent.TickAttribute == 0 && parent.Aggressive == 0 && parent.MaxTarget == 0 &&
            parent.PartyCheck == 0 && parent.AffectResist == 0 && parent.Passive == 0 && parent.ClientForceDamage == 0 &&
            parent.Name.Length == 0,
            "The server-only skill-201 descriptor differs from the zero-initialized 7.69 table entry.");
        Check(table[201] is null, "The server-only skill-201 descriptor was inserted into the CSV-backed table.");

        var mob = CreateMob();
        var world = new WorldHub();
        Check(world.Enter(1, "DIVINE_PARENT_MANA", (_, _) => ValueTask.CompletedTask),
            "The skill-201 mana fixture could not enter the world.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 3000, 3000, mob),
            "The skill-201 mana fixture could not install its MOB.");
        Check(world.TryGetCombatState(1, out var before) && before is not null,
            "The skill-201 mana fixture had no combat state.");
        Check(world.TryGetResourceState(1, out _, out _, out var requestedMpBefore),
            "The skill-201 mana fixture had no authoritative requested MP.");

        var result = world.TryConsumeSkillMana(1, parent, out var afterDebit, out _, out var requestedMpAfter);
        Check(result == LegacySkillManaResult.Accepted && afterDebit?.CurrentMana == before!.CurrentMana &&
            requestedMpAfter == requestedMpBefore,
            "The skill-201 parent descriptor changed authoritative MP or ReqMp.");
    }

    public static void DivineBlessingRequiresMobExtraBit2()
    {
        var withoutBit = CreateBlessingWorld(secondaryMask: 0, hp: 900, maxHp: 1000);
        var noBitBatch = ApplyBlessing(withoutBit.World);
        Check(noBitBatch.SlotOutcomes[0].SkillResult == LegacySkillAttackResult.Accepted,
            "Skill 201 without its secondary bit did not remain a valid no-op action.");
        Check(HasSecondaryActionsContract() && ReadSecondaryActions(noBitBatch).Length == 0,
            "A missing SecLearnedSkill bit created a Divine Blessing child action.");
        Check(CurrentScore(withoutBit.World).Hp == 900 && CurrentScore(withoutBit.World).Mp == 500,
            "Skill 201 without bit 0x2 healed or charged mana.");

        var withBit = CreateBlessingWorld(secondaryMask: 0x2, hp: 900, maxHp: 1000);
        var bitBatch = ApplyBlessing(withBit.World);
        var children = ReadSecondaryActions(bitBatch);
        Check(children.Length == 1 && ReadProperty(children[0], "Kind")?.ToString() == "AttackOne",
            "Authoritative SecLearnedSkill bit 0x2 did not create exactly one child AttackOne.");
        Check(CurrentScore(withBit.World).Hp == 1000,
            "A learned Divine Blessing child did not heal from authoritative world state.");
    }

    public static void DivineBlessingUsesSkill29SpecialZeroAndSaveMana()
    {
        var fixture = CreateBlessingWorld(secondaryMask: 0x2, hp: 900, maxHp: 1000, saveMana: 10, special2: 100);
        var batch = ApplyBlessing(fixture.World);
        Check(batch.CompletedNormally, "The skill-201 batch did not complete normally.");
        Check(CurrentScore(fixture.World).Mp == 473,
            "The child did not debit skill 29 using Special=0 and the legacy SaveMana formula (expected 27 MP).");
        var child = ReadSecondaryActions(batch).SingleOrDefault();
        Check(child is not null && ReadInt(child, "RequestedMp") == 473,
            "The child AttackOne did not capture authoritative post-debit ReqMp.");
    }

    public static void DivineBlessingInsufficientManaCapturesSetHpMpWithoutHealing()
    {
        var fixture = CreateBlessingWorld(secondaryMask: 0x2, hp: 900, maxHp: 1000, mp: 15, saveMana: 10);
        var batch = ApplyBlessing(fixture.World);
        var children = ReadSecondaryActions(batch);
        Check(CurrentScore(fixture.World) is { Hp: 900, Mp: 15 },
            "Insufficient child mana changed HP or MP.");
        Check(children.Length == 1 && ReadProperty(children[0], "Kind")?.ToString() == "SetHpMp" &&
            ReadInt(children[0], "RequestedHp") == 900 && ReadInt(children[0], "RequestedMp") == 15,
            "Insufficient mana did not capture the authoritative SetHpMp correction.");
    }

    public static void DivineBlessingUses769MountDivisorsAndHpCap()
    {
        AssertHeal(100, 100, mountIndex: 0, sanction: 0, expectedHp: 100, expectedWireDamage: 0);
        AssertHeal(95, 100, mountIndex: 0, sanction: 0, expectedHp: 100, expectedWireDamage: -6);
        AssertHeal(995, 1000, mountIndex: 0, sanction: 0, expectedHp: 1000, expectedWireDamage: -6);
        AssertHeal(90, 100, mountIndex: 786, sanction: 2, expectedHp: 95, expectedWireDamage: -10);
        AssertHeal(90, 100, mountIndex: 1936, sanction: 2, expectedHp: 90, expectedWireDamage: -10);
        AssertHeal(90, 100, mountIndex: 1937, sanction: 1, expectedHp: 90, expectedWireDamage: -10);
        AssertHeal(80, 100, mountIndex: 1936, sanction: 1, expectedHp: 81, expectedWireDamage: -20);
        AssertHeal(80, 100, mountIndex: 1937, sanction: 1, expectedHp: 80, expectedWireDamage: -20);
    }

    public static void DivineBlessingClanAndSelfMapPredicatesMatch769()
    {
        var clanFour = CreateBlessingWorld(secondaryMask: 0x2, hp: 900, maxHp: 1000, clan: 4);
        var clanBatch = ApplyBlessing(clanFour.World);
        Check(CurrentScore(clanFour.World).Hp == 900 && CurrentScore(clanFour.World).Mp == 470,
            "Clan 4 did not consume child mana while preserving HP.");
        var clanChild = ReadSecondaryActions(clanBatch).SingleOrDefault();
        Check(clanChild is not null && ReadProperty(clanChild, "Kind")?.ToString() == "AttackOne" &&
            ReadInt(clanChild, "WireTargetId") == 1 && ReadInt(clanChild, "WireDamage") == -1,
            "Clan 4 did not preserve the source-initialized self-target (-1) AttackOne slot.");

        var mapAttribute40 = CreateBlessingWorld(secondaryMask: 0x2, hp: 900, maxHp: 1000, mapAttribute: 0x40);
        var mapBatch = ApplyBlessing(mapAttribute40.World);
        Check(mapBatch.CompletedNormally && CurrentScore(mapAttribute40.World).Hp == 1000,
            "A self target in a 0x40 map cell was incorrectly blocked by the 7.69 caster/target attribute predicate.");
    }

    public static void DivineBlessingZeroAndNegativeHpFollowDifferentGates()
    {
        Check(LegacyAttackDeadGateRules.ShouldReject(0, LegacyDivineBlessing.SkillId),
            "The outer MSG_Attack gate did not reject exactly-zero HP for skill 201.");
        Check(!LegacyAttackDeadGateRules.ShouldReject(-1, LegacyDivineBlessing.SkillId),
            "The outer MSG_Attack gate treated negative HP as exactly zero.");

        var negativeHp = CreateBlessingWorld(secondaryMask: 0x2, hp: -1, maxHp: 1000);
        var batch = ApplyBlessing(negativeHp.World);
        Check(CurrentScore(negativeHp.World) is { Hp: -1, Mp: 470 },
            "Negative HP did not debit skill 29 before the child target-death check, or it revived the caster.");
        var child = ReadSecondaryActions(batch).SingleOrDefault();
        Check(child is not null && ReadProperty(child, "Kind")?.ToString() == "AttackOne" &&
            ReadInt(child, "WireTargetId") == 0 && ReadInt(child, "WireDamage") == 0,
            "The negative-HP child did not relay the source-invalidated (0,0) target slot.");
    }

    public static void DivineBlessingSelfHealDoesNotAddFakeOrPersistedExp()
    {
        const long initialExperience = 12_345;
        var fixture = CreateBlessingWorld(secondaryMask: 0x2, hp: 900, maxHp: 1000, experience: initialExperience);
        var batch = ApplyBlessing(fixture.World);
        var mob = MobSnapshot(fixture.World);
        Check(CurrentScore(fixture.World).Hp == 1000, "Self-heal did not update authoritative HP.");
        Check(BinaryPrimitives.ReadInt64LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset)) == initialExperience,
            "Divine Blessing changed persisted MOB.Exp for a self-heal.");
        Check(batch.HealingExperience is null or 0,
            "The self-target child contributed FakeExp to the outer attack confirmation.");
        var child = ReadSecondaryActions(batch).SingleOrDefault();
        Check(child is not null && ReadInt(child, "WireDamage") == -100,
            "The synthetic child did not preserve its signed source heal amount.");
    }

    public static void DivineBlessingChildSurvivesLaterBatchAbort()
    {
        var fixture = CreateBlessingWorld(secondaryMask: 0x2, hp: 900, maxHp: 1000, withFarTarget: true);
        var request = CreateBlessingRequest() with
        {
            Slots = [
                new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 0),
                new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Physical, 3,
                    PhysicalRolls: new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 100)),
            ],
        };
        if (!fixture.World.TryApplyAttackActionBatch(request, out var batch) || batch is null)
            throw new InvalidOperationException("The Divine Blessing mixed batch could not be evaluated.");
        Check(batch is { CompletedNormally: false, AbortSlotIndex: 1 } && CurrentScore(fixture.World).Hp == 1000,
            "A later physical abort rolled back the earlier Divine Blessing child mutation.");
        var children = ReadSecondaryActions(batch);
        Check(children.Length == 1 && ReadProperty(children[0], "Kind")?.ToString() == "AttackOne" &&
            ReadInt(children[0], "ParentSlotIndex") == 0,
            "A captured child AttackOne did not survive a later batch abort in parent-slot order.");
        Check(batch.HealingExperience is null,
            "A later abort retained parent-only deferred healing experience.");
    }

    public static void SyntheticDivineBlessingRelayUsesAttackOneLayout()
    {
        var mob = CreateRelayMob(hp: 876, mp: 4321, experience: 987_654_321);
        var confirmation = CreateSyntheticConfirmation(mob, actorId: 47, targetX: 2050, targetY: 2075, wireTargetId: 47, wireDamage: -124);
        var codec = LegacyFrameCodec.CreateDefault();
        var frame = codec.Decode(confirmation.ToFrame(codec, ClientTickPolicy.SkipCheckTick, keywordIndex: 19));
        Check(frame.IsChecksumValid && frame.Header.Type == AttackRequest.OneTargetMessageType &&
            frame.Header.Size == AttackRequest.OneTargetPacketSize,
            "The synthetic blessing confirmation did not use the 68-byte MSG_AttackOne wire type.");
        Check(AttackRequest.TryParse(frame, out var parsed) && parsed is not null && parsed.Motion == 255 &&
            parsed.AttackerId == 47 && parsed.SkillIndex == LegacyDivineBlessing.HealSkillId &&
            parsed.TargetX == 2050 && parsed.TargetY == 2075 && parsed.DoubleCritical == 0,
            "The synthetic blessing frame fields differ from the server-created skill-29 AttackOne.");
        Check(AttackRequest.TryReadDamageSlot(frame, 0, out var targetId, out var damage) && targetId == 47 && damage == -124,
            "The synthetic blessing frame did not carry its captured self-target and signed heal amount.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.FakeExpOffset..]) == 0,
            "The synthetic blessing child unexpectedly carried FakeExp.");
    }

    public static void SyntheticDivineBlessingRelayUsesAuthoritativeResources()
    {
        const int authoritativeMp = 70_000;
        const int authoritativeHp = 876;
        const long authoritativeExp = 987_654_321;
        var mob = CreateRelayMob(authoritativeHp, authoritativeMp, authoritativeExp);
        var confirmation = CreateSyntheticConfirmation(mob, actorId: 47, targetX: 2050, targetY: 2075, wireTargetId: 47, wireDamage: -124);
        var codec = LegacyFrameCodec.CreateDefault();
        var frame = codec.Decode(confirmation.ToFrame(codec, ClientTickPolicy.SkipCheckTick, keywordIndex: 20));
        Check(BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentHpOffset..]) == authoritativeHp &&
            BinaryPrimitives.ReadInt64LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentExpOffset..]) == authoritativeExp &&
            BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[AttackConfirmation.CurrentMpOffset..]) == authoritativeMp,
            "Synthetic AttackOne resources were not copied from the authoritative post-child MOB snapshot.");
        Check(BinaryPrimitives.ReadInt16LittleEndian(frame.Payload.Span[AttackConfirmation.RequestedMpOffset..]) ==
            unchecked((short)authoritativeMp),
            "Synthetic AttackOne ReqMp did not use the legacy signed-short narrowing of authoritative CurrentMp.");
        Check(AttackRequest.TryReadDamageSlot(frame, 0, out _, out var wireDamage) && wireDamage == -124,
            "Mount-adjusted HP was incorrectly substituted for the original -RateRegen wire damage.");
    }

    private static (WorldHub World, byte[] MobExtra) CreateBlessingWorld(
        uint secondaryMask,
        int hp,
        int maxHp,
        int mp = 500,
        int maxMp = 1000,
        byte saveMana = 0,
        short special2 = 0,
        byte clan = 0,
        byte mapAttribute = 0,
        long experience = 0,
        short mountIndex = 0,
        byte mountSanction = 0,
        bool withFarTarget = false)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(100, 0, 0, 0, 0, 0, 0, maxHp, maxMp, hp, mp, 0, 0, 0, 0, 0, special2, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 3;
        mob[LegacyAccountSnapshot.MobSaveManaOffset] = saveMana;
        BinaryPrimitives.WriteInt64LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), experience);
        if (mountIndex != 0)
        {
            new LegacyItem(mountIndex, 0, mountSanction, 0, 0, 0, 0)
                .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        }

        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteUInt32LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), secondaryMask);
        var world = new WorldHub(itemData: EmptyItemData.Value);
        Check(world.Enter(1, "DIVINE_BLESSING_ACTOR", (_, _) => ValueTask.CompletedTask),
            "The Divine Blessing actor could not enter the test world.");
        Check(world.SetCharacterState(1, 0, 0, clan, 0, 0, 2000, 2000, mob,
                LegacyAccountSnapshot.ClassMasterMortal, new byte[LegacyAccountSnapshot.AffectStride], mobExtra) &&
            world.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: mapAttribute),
            "The Divine Blessing actor state could not be installed.");

        AddTarget(2, 2005, 2005);
        if (withFarTarget)
            AddTarget(3, 2024, 2000);

        void AddTarget(int connectionId, short x, short y)
        {
            var targetMob = new byte[LegacyAccountSnapshot.CharacterStride];
            new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1000, 1000, 1000, 500, 0, 0, 0, 0, 0, 0, 0, 0)
                .Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            Check(world.Enter(connectionId, $"DIVINE_BLESSING_TARGET_{connectionId}", (_, _) => ValueTask.CompletedTask) &&
                world.SetCharacterState(connectionId, 0, 0, connectionId, 0, 0, x, y, targetMob),
                $"Divine Blessing target {connectionId} could not enter the test world.");
        }

        return (world, mobExtra);
    }

    private static LegacyAttackActionBatchRequest CreateBlessingRequest() => new(
        1,
        [new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 2, SkillTargetIndex: 0)],
        Skill: LegacyDivineBlessing.ParentActionDefinition,
        ItemData: EmptyItemData.Value,
        ClientTick: ClientTickPolicy.SkipCheckTick)
    {
        DivineBlessingHealSkill = CreateHealSkill(),
    };

    private static LegacyAttackActionBatchOutcome ApplyBlessing(WorldHub world)
    {
        Check(world.TryApplyAttackActionBatch(CreateBlessingRequest(), out var batch) && batch is not null,
            "The Divine Blessing action batch could not be evaluated.");
        return batch!;
    }

    private static LegacySkillDefinition CreateHealSkill() => new(
        LegacyDivineBlessing.HealSkillId,
        0, 0, 30, 0, 0, 6, 0, 0, 0, 0, 0, 0,
        [], [], 0, 0, 0, 0, 0, 0, 0, 0, "DIVINE_BLESSING_HEAL");

    private static byte[] CreateRelayMob(int hp, int mp, long experience)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(100, 0, 0, 0, 0, 0, 0, 1000, 100_000, hp, mp, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteInt64LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), experience);
        return mob;
    }

    private static AttackConfirmation CreateSyntheticConfirmation(
        byte[] mob,
        ushort actorId,
        short targetX,
        short targetY,
        ushort wireTargetId,
        int wireDamage)
    {
        Check(AttackConfirmation.TryCreateSyntheticOneTarget(
                mob, actorId, targetX, targetY, wireTargetId, wireDamage, out var confirmation) && confirmation is not null,
            "The synthetic MSG_AttackOne confirmation factory rejected a complete authoritative MOB snapshot.");
        return confirmation!;
    }

    private static LegacyScore CurrentScore(WorldHub world)
    {
        Check(world.TryGetCombatState(1, out var state) && state is not null,
            "The Divine Blessing combat state could not be read.");
        return state!.CurrentScore;
    }

    private static byte[] MobSnapshot(WorldHub world)
    {
        Check(world.TryGetCharacterSnapshot(1, out var mob, out _, out _, out _) && mob is not null,
            "The Divine Blessing MOB snapshot could not be read.");
        return mob!;
    }

    private static bool HasSecondaryActionsContract() =>
        typeof(LegacyAttackActionBatchOutcome).GetProperty("SecondaryActions", BindingFlags.Public | BindingFlags.Instance) is not null;

    private static object[] ReadSecondaryActions(LegacyAttackActionBatchOutcome outcome)
    {
        var value = typeof(LegacyAttackActionBatchOutcome).GetProperty("SecondaryActions", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(outcome);
        return value is IEnumerable items ? items.Cast<object>().ToArray() : [];
    }

    private static object? ReadProperty(object instance, string name) =>
        instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

    private static int ReadInt(object instance, string name)
    {
        var value = ReadProperty(instance, name);
        return value is null ? int.MinValue : Convert.ToInt32(value);
    }

    private static void AssertHeal(int hp, int maxHp, int mountIndex, int sanction, int expectedHp, int expectedWireDamage)
    {
        var blessingType = typeof(WorldHub).Assembly.GetType("WydCdk.World.LegacyDivineBlessing", throwOnError: false);
        var calculate = blessingType?.GetMethod("CalculateHeal", BindingFlags.Public | BindingFlags.Static);
        Check(calculate is not null, "The dedicated 7.69 Divine Blessing heal formula is missing.");
        var result = calculate!.Invoke(null, [hp, maxHp, mountIndex, sanction]);
        Check(result is not null && ReadInt(result, "CurrentHp") == expectedHp &&
            ReadInt(result, "WireDamage") == expectedWireDamage,
            $"Divine Blessing heal mismatch for mount {mountIndex}/{sanction}, HP {hp}/{maxHp}.");
    }

    private static byte[] CreateMob()
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 40, 0, 0, 0, 0, 0, 1000, 1000, 1000, 500, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void CheckUnrelatedBytesUnchanged(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        var offset = LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset;
        Check(before[..offset].SequenceEqual(after[..offset]), "Writing SecLearnedSkill changed bytes before the field.");
        Check(before[(offset + sizeof(uint))..].SequenceEqual(after[(offset + sizeof(uint))..]),
            "Writing SecLearnedSkill changed bytes after the field.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
