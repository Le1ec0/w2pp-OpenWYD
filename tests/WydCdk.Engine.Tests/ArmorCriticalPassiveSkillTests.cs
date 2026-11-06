using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class ArmorCriticalPassiveSkillTests
{
    private const int ArmorCriticalSkillId = 15;
    private const uint ArmorCriticalLearnedFlag = 1u << ArmorCriticalSkillId;

    public static void Run()
    {
        LearnedSkillAddsTenPercentArmorAndTwentyFiveCritical();
        ClassAndLearnedBitGateThePassive();
    }

    private static void LearnedSkillAddsTenPercentArmorAndTwentyFiveCritical()
    {
        var itemData = EmptyItemData();
        var baseline = Project(itemData, learned: false, characterClass: 0);
        var trained = Project(itemData, learned: true, characterClass: 0);
        var baselineState = LegacyMobCombatState.Read(baseline.Mob);
        var trainedState = LegacyMobCombatState.Read(trained.Mob);

        Check(trainedState.CurrentScore.Ac == (int)(baselineState.CurrentScore.Ac * 1.1f),
            $"Armadura Critica did not multiply ArmorClass by 1.1f: baseline={baselineState.CurrentScore.Ac}, trained={trainedState.CurrentScore.Ac}.");
        Check(trainedState.Critical == baselineState.Critical + 25,
            $"Armadura Critica did not add 25 Critical: baseline={baselineState.Critical}, trained={trainedState.Critical}.");
    }

    private static void ClassAndLearnedBitGateThePassive()
    {
        var itemData = EmptyItemData();
        var baseline = Project(itemData, learned: false, characterClass: 0);
        var baselineState = LegacyMobCombatState.Read(baseline.Mob);
        var unlearned = LegacyMobCombatState.Read(Project(itemData, learned: false, characterClass: 0).Mob);
        var wrongClass = LegacyMobCombatState.Read(Project(itemData, learned: true, characterClass: 1).Mob);

        Check(unlearned.CurrentScore.Ac == baselineState.CurrentScore.Ac &&
              unlearned.Critical == baselineState.Critical &&
              wrongClass.CurrentScore.Ac == baselineState.CurrentScore.Ac &&
              wrongClass.Critical == baselineState.Critical,
            "Armadura Critica applied without both the learned bit and the Transknight class gate.");
    }

    private static LegacyCharacterLoginData Project(
        LegacyItemDataTable itemData,
        bool learned,
        int characterClass)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(
            Level: 100, Ac: 101, Damage: 500, Merchant: 0, AttackRun: 0x96, Direction: 0,
            ChaosRate: 0, MaxHp: 1_000, MaxMp: 500, Hp: 900, Mp: 400,
            Strength: 0, Intelligence: 0, Dexterity: 0, Constitution: 0,
            Special1: 0, Special2: 0, Special3: 0, Special4: 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset),
            learned ? ArmorCriticalLearnedFlag : 0);
        var extra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            extra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset),
            LegacyAccountSnapshot.ClassMasterMortal);

        return LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                new byte[LegacyAccountSnapshot.AffectStride],
                extra,
                Donate: 0,
                SavedPositionX: 0,
                SavedPositionY: 0),
            itemData);
    }

    private static LegacyItemDataTable EmptyItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
