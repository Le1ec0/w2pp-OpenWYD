using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackBloodHandsTests
{
    private const int ClawWeaponIndex6 = 100;
    private const int ClawWeaponIndex7 = 101;

    public static void Run()
    {
        DoubleCriticalWithLearnedTkAndTwoClawsHealsAndQueuesHpRelay();
        ZeroAmountProcRemainsAProc();
        ProcChanceIsStrictlyBelowTwentyPercent();
        UnlearnedOrWrongClassDoesNotHeal();
        BothWeaponSlotsMustBeClaws();
        DoubleCriticalFlagIsRequired();
    }

    private static void DoubleCriticalWithLearnedTkAndTwoClawsHealsAndQueuesHpRelay()
    {
        var fixture = CreateFixture();
        var request = new LegacyAttackActionBatchRequest(
            1,
            [new LegacyAttackActionBatchSlotRequest(
                0,
                LegacyAttackActionSlotKind.Physical,
                2,
                new LegacyPhysicalAttackBatchSlotRolls(
                    RandomFactor: 99,
                    AttackMissRoll: 99,
                    BloodHandsAmountRoll: 14,
                    BloodHandsChanceRoll: 19))],
            ItemData: fixture.ItemData,
            PhysicalActionRolls: new LegacyPhysicalAttackBatchActionRolls(
                DoubleCriticalChanceRoll: 0,
                DoubleCriticalDamageRoll: 0));

        Check(fixture.World.TryApplyAttackActionBatch(request, out var outcome) && outcome is not null,
            "The physical action batch did not resolve.");
        Check(outcome!.SlotOutcomes[0].PhysicalResult == LegacyPhysicalAttackResult.Accepted &&
            outcome.DoubleCritical == 2,
            $"The fixture did not reach the 7.69 DoubleCritical branch: result={outcome.SlotOutcomes[0].PhysicalResult}, flags={outcome.DoubleCritical}.");

        var expectedHeal = (50 * 10 / 2) + 14;
        AssertHp(fixture.World, 100 + expectedHeal, "Mãos Sangrentas did not update current/requested HP");
        Check(outcome.SecondaryActions.Count == 1 &&
            outcome.SecondaryActions[0].Kind == LegacyAttackActionSecondaryOutcomeKind.SetHpDam &&
            outcome.SecondaryActions[0].WireDamage == expectedHeal &&
            outcome.SecondaryActions[0].RecipientConnectionIds.Contains(1),
            "The 7.69 healing amount was not queued as a view-scoped SetHpDam relay.");
    }

    private static void ProcChanceIsStrictlyBelowTwentyPercent()
    {
        var fixture = CreateFixture();
        var result = fixture.World.TryApplyPhysicalAttack(
            1,
            2,
            randomFactor: 99,
            out _,
            attackMissRoll: 99,
            itemData: fixture.ItemData,
            doubleCriticalChanceRoll: 0,
            doubleCriticalDamageRoll: 0,
            bloodHandsAmountRoll: 14,
            bloodHandsChanceRoll: 20);

        Check(result == LegacyPhysicalAttackResult.Accepted,
            $"The 20-percent boundary attack was rejected: result={result}.");
        AssertHp(fixture.World, 100, "A roll of 20 triggered the strict <20 Mãos Sangrentas chance");
    }

    private static void ZeroAmountProcRemainsAProc()
    {
        var resolution = LegacyBloodHandsMath.Resolve(special3: 0, amountRoll: 0, chanceRoll: 0);
        Check(resolution == new LegacyBloodHandsResolution(Triggered: true, Healing: 0),
            $"A successful 20-percent roll with zero calculated healing was lost: {resolution}.");
    }

    private static void UnlearnedOrWrongClassDoesNotHeal()
    {
        foreach (var (name, learned, characterClass) in new[]
        {
            ("unlearned", false, (byte)0),
            ("wrong class", true, (byte)1),
        })
        {
            var fixture = CreateFixture(learned: learned, characterClass: characterClass);
            var result = fixture.World.TryApplyPhysicalAttack(
                1,
                2,
                randomFactor: 99,
                out _,
                attackMissRoll: 99,
                itemData: fixture.ItemData,
                doubleCriticalChanceRoll: 0,
                doubleCriticalDamageRoll: 0,
                bloodHandsAmountRoll: 14,
                bloodHandsChanceRoll: 0);

            Check(result == LegacyPhysicalAttackResult.Accepted,
                $"The {name} attack was rejected: result={result}.");
            AssertHp(fixture.World, 100, $"Mãos Sangrentas healed an {name} character");
        }
    }

    private static void BothWeaponSlotsMustBeClaws()
    {
        var fixture = CreateFixture(weaponPosition7: 191);
        var result = fixture.World.TryApplyPhysicalAttack(
            1,
            2,
            randomFactor: 99,
            out _,
            attackMissRoll: 99,
            itemData: fixture.ItemData,
            doubleCriticalChanceRoll: 0,
            doubleCriticalDamageRoll: 0,
            bloodHandsAmountRoll: 14,
            bloodHandsChanceRoll: 0);

        Check(result == LegacyPhysicalAttackResult.Accepted,
            $"The non-claw attack was rejected: result={result}.");
        AssertHp(fixture.World, 100, "Mãos Sangrentas healed without two position-192 weapons");
    }

    private static void DoubleCriticalFlagIsRequired()
    {
        var fixture = CreateFixture();
        var result = fixture.World.TryApplyPhysicalAttack(
            1,
            2,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: fixture.ItemData,
            doubleCriticalChanceRoll: 1,
            doubleCriticalDamageRoll: 0,
            bloodHandsAmountRoll: 14,
            bloodHandsChanceRoll: 0);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { DoubleCritical: 0 },
            $"The non-critical attack did not preserve the no-critical flags: result={result}, flags={outcome?.DoubleCritical}.");
        AssertHp(fixture.World, 100, "Mãos Sangrentas ran without DoubleCritical flag 2");
    }

    private static (WorldHub World, LegacyItemDataTable ItemData) CreateFixture(
        bool learned = true,
        byte characterClass = 0,
        int weaponPosition7 = 192)
    {
        var itemData = CreateClawItemData(weaponPosition7);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "BLOOD_HANDS", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "BLOOD_HANDS_TARGET", (_, _) => ValueTask.CompletedTask),
            "The Mãos Sangrentas participants were not registered.");

        var attackerMob = CreateMob("BLOOD_HANDS", currentHp: 100, special3: 50);
        attackerMob[LegacyAccountSnapshot.MobClassOffset] = characterClass;
        attackerMob[LegacyAccountSnapshot.MobCriticalOffset] = 1;
        new LegacyItem(ClawWeaponIndex6, 0, 0, 0, 0, 0, 0).Write(
            attackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (6 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        new LegacyItem(ClawWeaponIndex7, 0, 0, 0, 0, 0, 0).Write(
            attackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        if (learned)
            BinaryPrimitives.WriteUInt32LittleEndian(
                mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset),
                0x10);

        Check(world.SetCharacterState(
                1, 0, 0, 0, 0, 0, 2_000, 2_000, attackerMob,
                LegacyAccountSnapshot.ClassMasterMortal,
                new byte[LegacyAccountSnapshot.AffectStride],
                mobExtra) &&
            world.SetCharacterState(
                2, 0, 0, 0, 0, 0, 2_005, 2_005,
                CreateMob("BLOOD_HANDS_TARGET", currentHp: 5_000, special3: 0)),
            "The Mãos Sangrentas character state was not initialized.");

        return (world, itemData);
    }

    private static LegacyItemDataTable CreateClawItemData(int weaponPosition7)
    {
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        SetPosition(ClawWeaponIndex6, 192);
        SetPosition(ClawWeaponIndex7, weaponPosition7);
        return LegacyItemDataTable.Load(encoded);

        void SetPosition(int index, int position)
        {
            Span<byte> decoded = stackalloc byte[LegacyItemDataTable.RecordSizeInBytes];
            BinaryPrimitives.WriteInt32LittleEndian(decoded[136..], position);
            var record = encoded.AsSpan(index * LegacyItemDataTable.RecordSizeInBytes, LegacyItemDataTable.RecordSizeInBytes);
            for (var offset = 0; offset < decoded.Length; offset++)
                record[offset] = (byte)(decoded[offset] ^ 0x5A);
        }
    }

    private static byte[] CreateMob(string name, int currentHp, int special3)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        new LegacyScore(50, 0, 1_000, 0, 0, 0, 0, 1_000, 1_000, currentHp, 1_000,
            0, 0, 0, 0, 0, 0, checked((short)special3), 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void AssertHp(WorldHub world, int expectedHp, string message)
    {
        var found = world.TryGetResourceState(1, out var state, out var requestedHp, out _);
        Check(found && state is not null && state.CurrentScore.Hp == expectedHp && requestedHp == expectedHp,
            $"{message}: hp={state?.CurrentScore.Hp}, requested={requestedHp}, expected={expectedHp}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
