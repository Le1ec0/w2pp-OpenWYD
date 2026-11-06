using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackHideAffectTests
{
    private const ushort HideRsvFlag = 0x0010;
    private const ushort BlockRsvFlag = 0x0080;

    public static void Run()
    {
        Type28AddsTheHideRsvFlag();
        CleanupRemovesOnlyTheFirstType28AndReprojectsScore();
        CleanupHonorsClassAndFailsClosedWithoutItemData();
    }

    private static void Type28AddsTheHideRsvFlag()
    {
        var affect = CreateAffect((Slot: 0, Type: (byte)28));
        var stage = LegacyCurrentScoreMath.CalculateCurrentScoreAffectCompositionStage(
            affect,
            startingMaxHp: 100,
            startingMaxMp: 80,
            startingArmorClass: 20,
            startingDamage: 30,
            sourceMobMagic: 40,
            startingMagic: 40,
            startingLearnedSkills: 0);

        Check(stage.RsvFlagsToAdd == HideRsvFlag,
            "Type 28 did not contribute RSV_HIDE to the score projection.");
    }

    private static void CleanupRemovesOnlyTheFirstType28AndReprojectsScore()
    {
        var affect = CreateAffect(
            (Slot: 0, Type: (byte)28),
            (Slot: 1, Type: (byte)28),
            (Slot: 2, Type: (byte)19));
        var world = CreateWorld(characterClass: 3, affect, CreateNeutralItemData());

        var result = world.TryClearFirstClassThreeHideAffect(1, out var outcome);
        Check(result == LegacyAttackHideCleanupResult.Cleared && outcome is not null,
            "The first class 3 Type 28 cleanup did not complete.");
        Check(outcome!.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcome.AffectSnapshot.AsSpan(8, 8).SequenceEqual(affect.AsSpan(8, 8)),
            "The cleanup did not clear exactly the first Type 28 record.");
        var firstState = LegacyMobCombatState.Read(outcome.MobSnapshot);
        Check((firstState.Rsv & HideRsvFlag) != 0 && (firstState.Rsv & BlockRsvFlag) != 0,
            "Score projection did not preserve RSV_HIDE for the remaining Type 28 and RSV_BLOCK for Type 19.");
        Check(firstState.CurrentScore.Hp == 700 && firstState.CurrentScore.Mp == 180,
            "The hide cleanup changed current HP or MP.");

        result = world.TryClearFirstClassThreeHideAffect(1, out outcome);
        Check(result == LegacyAttackHideCleanupResult.Cleared && outcome is not null,
            "The second attack did not remove the next Type 28 record.");
        var secondState = LegacyMobCombatState.Read(outcome!.MobSnapshot);
        Check((secondState.Rsv & HideRsvFlag) == 0 && (secondState.Rsv & BlockRsvFlag) != 0,
            "Removing the last Type 28 did not clear only RSV_HIDE.");
        Check(world.TryGetCharacterSnapshot(1, out var mob, out _, out _, out _, out var savedAffect) &&
              mob is not null && savedAffect is not null &&
              mob.AsSpan().SequenceEqual(outcome.MobSnapshot) && savedAffect.AsSpan().SequenceEqual(outcome.AffectSnapshot),
            "The hide cleanup did not atomically update the authoritative MOB and Affect snapshots.");
    }

    private static void CleanupHonorsClassAndFailsClosedWithoutItemData()
    {
        var affect = CreateAffect((Slot: 0, Type: (byte)28));
        var nonClassThree = CreateWorld(characterClass: 2, affect, CreateNeutralItemData());
        Check(nonClassThree.TryClearFirstClassThreeHideAffect(1, out _) == LegacyAttackHideCleanupResult.NotApplicable,
            "A non-class-3 character entered the hide cleanup path.");

        var worldWithoutItemData = CreateWorld(characterClass: 3, affect, itemData: null);
        Check(worldWithoutItemData.TryGetCharacterSnapshot(1, out var beforeMob, out _, out _, out _, out var beforeAffect) &&
              beforeMob is not null && beforeAffect is not null,
            "The hide cleanup failure fixture could not be read.");
        Check(worldWithoutItemData.TryClearFirstClassThreeHideAffect(1, out var unavailableOutcome) == LegacyAttackHideCleanupResult.ItemDataUnavailable &&
              unavailableOutcome is null,
            "The hide cleanup did not fail closed when ItemList data was unavailable.");
        Check(worldWithoutItemData.TryGetCharacterSnapshot(1, out var afterMob, out _, out _, out _, out var afterAffect) &&
              afterMob is not null && afterAffect is not null &&
              afterMob.AsSpan().SequenceEqual(beforeMob) && afterAffect.AsSpan().SequenceEqual(beforeAffect),
            "A failed hide cleanup partially mutated character state.");

        var noHideAffect = CreateWorld(characterClass: 3, CreateAffect((Slot: 0, Type: (byte)4)), CreateNeutralItemData());
        Check(noHideAffect.TryClearFirstClassThreeHideAffect(1, out _) == LegacyAttackHideCleanupResult.NoAffect,
            "The hide cleanup changed a Foema without Type 28.");
    }

    private static WorldHub CreateWorld(int characterClass, byte[] affect, LegacyItemDataTable? itemData)
    {
        var world = new WorldHub(itemData: itemData);
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1_000, 500, 700, 180,
            0, 0, 180, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1_000, 500, 900, 220,
            0, 0, 100, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        mob[LegacyAccountSnapshot.MobRsvOffset] = (byte)(BlockRsvFlag & 0xFF);
        mob[LegacyAccountSnapshot.MobRsvOffset + 1] = (byte)(BlockRsvFlag >> 8);

        Check(world.Enter(1, "ATTACK_HIDE", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, affect, new byte[LegacyAccountSnapshot.MobExtraStride]),
            "The hide cleanup fixture was not installed.");
        return world;
    }

    private static byte[] CreateAffect(params (int Slot, byte Type)[] records)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in records)
            affect[checked(record.Slot * 8)] = record.Type;
        return affect;
    }

    private static LegacyItemDataTable CreateNeutralItemData() =>
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
