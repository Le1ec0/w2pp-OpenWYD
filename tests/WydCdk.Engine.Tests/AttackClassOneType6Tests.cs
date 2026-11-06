using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackClassOneType6Tests
{
    public static void Run()
    {
        SkillGateMarksClassOneCleanupBeforeLaterRejects();
        WorldHubRemovesOnlyTheFirstType6AndReprojectsScore();
        CleanupFailuresLeaveCharacterStateUntouched();
    }

    private static void SkillGateMarksClassOneCleanupBeforeLaterRejects()
    {
        const string activeSkill = "10,69,1,20,15,1,1,40,0,0,0,0,0,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,0,Active";
        const string passiveSkill = "9,45,0,0,0,0,0,0,0,0,0,0,0,20.0.0.24.0.0.0.0,20.0.0.8.0.0.0.0,0,0,0,0,0,0,1,0,Passive";
        var table = LegacySkillDataTable.Load(new StringReader(string.Join(Environment.NewLine, activeSkill, passiveSkill)));
        SkillAttackGateDecision Evaluate(int skill, int characterClass, uint clientTick, uint previousAttackTick) =>
            LegacySkillAttackGate.Evaluate(table, skill, characterClass, uint.MaxValue, clientTick, previousAttackTick);

        var delayedOtherClassSkill = Evaluate(10, 1, 1_699, 1_000);
        Check(delayedOtherClassSkill.Result == SkillAttackValidationResult.SkillDelayTooShort && delayedOtherClassSkill.ClearClassOneType6Affect,
            "Class 1 Type 6 cleanup was not requested before the delay and class gates.");
        var rejectedOtherClassSkill = Evaluate(10, 1, 1_700, 1_000);
        Check(rejectedOtherClassSkill.Result == SkillAttackValidationResult.WrongClass && rejectedOtherClassSkill.ClearClassOneType6Affect,
            "Class 1 Type 6 cleanup was not requested before the other-class rejection.");
        Check(!Evaluate(9, 1, 1_100, 1_000).ClearClassOneType6Affect,
            "A passive skill requested the class 1 Type 6 cleanup.");
        Check(!Evaluate(10, 0, 1_700, 1_000).ClearClassOneType6Affect,
            "A non-class-1 skill user requested the cleanup.");
        Check(!Evaluate(10, 1, ClientTickPolicy.SkipCheckTick, uint.MaxValue).ClearClassOneType6Affect,
            "The reserved server tick requested the cleanup.");
        Check(!Evaluate(-1, 1, 1_700, 1_000).ClearClassOneType6Affect,
            "A physical attack requested the cleanup.");
    }

    private static void WorldHubRemovesOnlyTheFirstType6AndReprojectsScore()
    {
        var affect = CreateAffect(
            (Slot: 0, Value: (byte)50, Level: (ushort)0x1234, Time: 0x78563412u),
            (Slot: 1, Value: (byte)20, Level: (ushort)0x5678, Time: 0x12345678u));
        var world = CreateWorld(characterClass: 1, affect, CreateNeutralItemData());

        var result = world.TryClearFirstClassOneType6Affect(1, out var outcome);
        Check(result == LegacyClassOneType6CleanupResult.Cleared && outcome is not null,
            "The class 1 Type 6 cleanup did not complete.");
        Check(outcome!.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The first Type 6 affect was not fully cleared.");
        Check(outcome.AffectSnapshot.AsSpan(8, 8).SequenceEqual(affect.AsSpan(8, 8)),
            "The cleanup removed more than the first Type 6 affect.");
        Check(LegacyMobCombatState.Read(outcome.MobSnapshot).CurrentScore.Dexterity == 120,
            "Score projection did not remove the first Type 6 Dexterity bonus while retaining the second.");
        Check(world.TryGetCharacterSnapshot(1, out var mob, out _, out _, out _, out var savedAffect) &&
              mob is not null && savedAffect is not null &&
              mob.AsSpan().SequenceEqual(outcome.MobSnapshot) && savedAffect.AsSpan().SequenceEqual(outcome.AffectSnapshot),
            "The class 1 Type 6 cleanup did not atomically update the authoritative snapshots.");
    }

    private static void CleanupFailuresLeaveCharacterStateUntouched()
    {
        var affect = CreateAffect((Slot: 0, Value: (byte)50, Level: (ushort)1, Time: 10u));
        var worldWithoutItemData = CreateWorld(characterClass: 1, affect, itemData: null);
        Check(worldWithoutItemData.TryGetCharacterSnapshot(1, out var beforeMob, out _, out _, out _, out var beforeAffect) &&
              beforeMob is not null && beforeAffect is not null,
            "The class 1 Type 6 failure fixture could not be read.");
        Check(worldWithoutItemData.TryClearFirstClassOneType6Affect(1, out var unavailableOutcome) == LegacyClassOneType6CleanupResult.ItemDataUnavailable &&
              unavailableOutcome is null,
            "The cleanup did not fail closed when score projection lacked ItemList data.");
        Check(worldWithoutItemData.TryGetCharacterSnapshot(1, out var afterMob, out _, out _, out _, out var afterAffect) &&
              afterMob is not null && afterAffect is not null &&
              afterMob.AsSpan().SequenceEqual(beforeMob) && afterAffect.AsSpan().SequenceEqual(beforeAffect),
            "A failed class 1 Type 6 cleanup partially mutated character state.");

        var classZero = CreateWorld(characterClass: 0, affect, CreateNeutralItemData());
        Check(classZero.TryClearFirstClassOneType6Affect(1, out _) == LegacyClassOneType6CleanupResult.NotApplicable,
            "A non-class-1 character entered the cleanup path.");
        var noType6 = CreateWorld(characterClass: 1, CreateAffect((Slot: 0, Value: (byte)1, Level: (ushort)1, Time: 10u, Type: (byte)4)), CreateNeutralItemData());
        Check(noType6.TryClearFirstClassOneType6Affect(1, out _) == LegacyClassOneType6CleanupResult.NoAffect,
            "The cleanup changed a character with no Type 6 affect.");
    }

    private static WorldHub CreateWorld(int characterClass, byte[] affect, LegacyItemDataTable? itemData)
    {
        var world = new WorldHub(itemData: itemData);
        var mob = CreateMob(characterClass);
        Check(world.Enter(1, "CLASS_ONE_TYPE6", (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, affect, new byte[LegacyAccountSnapshot.MobExtraStride]),
            "Class 1 Type 6 cleanup fixture was not installed.");
        return world;
    }

    private static byte[] CreateMob(int characterClass)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1_000, 500, 800, 250,
            0, 0, 180, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1_000, 500, 800, 250,
            0, 0, 100, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        return mob;
    }

    private static byte[] CreateAffect(params (int Slot, byte Value, ushort Level, uint Time)[] records) =>
        CreateAffect(records.Select(static record => (record.Slot, record.Value, record.Level, record.Time, Type: (byte)6)).ToArray());

    private static byte[] CreateAffect(params (int Slot, byte Value, ushort Level, uint Time, byte Type)[] records)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        foreach (var record in records)
        {
            var offset = checked(record.Slot * 8);
            affect[offset] = record.Type;
            affect[offset + 1] = record.Value;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(offset + 2), record.Level);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(offset + 4), record.Time);
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
