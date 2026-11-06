using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSamaritanoAffectTests
{
    private const ushort BlockRsvFlag = 0x0080;

    public static void Run()
    {
        CleanupRemovesOnlyTheFirstType24AndReprojectsScore();
        CleanupFailsClosedWithoutItemData();
    }

    private static void CleanupRemovesOnlyTheFirstType24AndReprojectsScore()
    {
        var affect = CreateAffect(
            (Slot: 0, Type: (byte)24, Value: (byte)8),
            (Slot: 1, Type: (byte)24, Value: (byte)16),
            (Slot: 2, Type: (byte)19, Value: (byte)0));
        var itemData = CreateNeutralItemData();
        var world = CreateWorld(characterClass: 2, affect, itemData);
        Check(world.TryGetCharacterSnapshot(1, out var initialMob, out var positionX, out var positionY,
                out var initialMobExtra, out _, out var equipment) &&
              initialMob is not null && initialMobExtra is not null && equipment is not null,
            "The Samaritan cleanup fixture could not be read.");

        var expectedAffect = affect.ToArray();
        expectedAffect.AsSpan(0, 8).Clear();
        var expected = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                initialMob!,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                expectedAffect,
                initialMobExtra!,
                Donate: 0,
                positionX,
                positionY,
                equipment!),
            itemData);

        var result = world.TryClearFirstSamaritanAffect(1, out var outcome);
        Check(result == LegacyAttackSamaritanCleanupResult.Cleared && outcome is not null,
            "The first Type 24 Samaritan cleanup did not complete for a non-class-3 character.");
        Check(outcome!.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              outcome.AffectSnapshot.AsSpan(8).SequenceEqual(affect.AsSpan(8)),
            "The cleanup did not clear exactly the first Type 24 record.");
        Check(outcome.MobSnapshot.AsSpan().SequenceEqual(expected.Mob),
            "The cleanup did not reproject the score from the remaining affects.");
        var state = LegacyMobCombatState.Read(outcome.MobSnapshot);
        Check((state.Rsv & BlockRsvFlag) != 0,
            "The score projection did not preserve RSV_BLOCK for the unrelated Type 19 affect.");

        result = world.TryClearFirstSamaritanAffect(1, out outcome);
        Check(result == LegacyAttackSamaritanCleanupResult.Cleared && outcome is not null &&
              outcome.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]) &&
              outcome.AffectSnapshot.AsSpan(16, 8).SequenceEqual(affect.AsSpan(16, 8)),
            "The next attack did not remove only the next Type 24 record.");
        var secondOutcome = outcome!;
        Check(world.TryGetCharacterSnapshot(1, out var savedMob, out _, out _, out _, out var savedAffect) &&
              savedMob is not null && savedAffect is not null &&
              savedMob.AsSpan().SequenceEqual(secondOutcome.MobSnapshot) &&
              savedAffect.AsSpan().SequenceEqual(secondOutcome.AffectSnapshot),
            "The cleanup did not atomically update the authoritative MOB and Affect snapshots.");

        Check(world.TryClearFirstSamaritanAffect(1, out _) == LegacyAttackSamaritanCleanupResult.NoAffect,
            "The cleanup changed unrelated affects when no Type 24 record remained.");
    }

    private static void CleanupFailsClosedWithoutItemData()
    {
        var affect = CreateAffect((Slot: 0, Type: (byte)24, Value: (byte)8));
        var world = CreateWorld(characterClass: 0, affect, itemData: null);
        Check(world.TryGetCharacterSnapshot(1, out var beforeMob, out _, out _, out _, out var beforeAffect) &&
              beforeMob is not null && beforeAffect is not null,
            "The missing ItemList fixture could not be read.");

        var result = world.TryClearFirstSamaritanAffect(1, out var outcome);
        Check(result == LegacyAttackSamaritanCleanupResult.ItemDataUnavailable && outcome is null,
            "The cleanup did not fail closed when ItemList data was unavailable.");
        Check(world.TryGetCharacterSnapshot(1, out var afterMob, out _, out _, out _, out var afterAffect) &&
              afterMob is not null && afterAffect is not null &&
              afterMob.AsSpan().SequenceEqual(beforeMob) && afterAffect.AsSpan().SequenceEqual(beforeAffect),
            "A failed cleanup partially mutated character state.");
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

        Check(world.Enter(1, "ATTACK_SAMARITANO", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, affect, new byte[LegacyAccountSnapshot.MobExtraStride]),
            "The Samaritan cleanup fixture was not installed.");
        return world;
    }

    private static byte[] CreateAffect(params (int Slot, byte Type, byte Value)[] records)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in records)
        {
            var offset = checked(record.Slot * 8);
            affect[offset] = record.Type;
            affect[offset + 1] = record.Value;
        }
        return affect;
    }

    private static LegacyItemDataTable CreateNeutralItemData() =>
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
