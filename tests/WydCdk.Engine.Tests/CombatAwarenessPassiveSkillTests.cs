using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class CombatAwarenessPassiveSkillTests
{
    private const int CombatAwarenessSkillId = 14;
    private const uint CombatAwarenessLearnedFlag = 1u << CombatAwarenessSkillId;

    public static void Run()
    {
        LearnedSkillAddsHalfOfComposedSpecial3ToDamage();
        ClassAndLearnedBitGateThePassive();
    }

    private static void LearnedSkillAddsHalfOfComposedSpecial3ToDamage()
    {
        var itemData = EmptyItemData();
        var baseline = LegacyMobCombatState.Read(Project(itemData, learned: false, characterClass: 0, special3: 51).Mob);
        var trained = LegacyMobCombatState.Read(Project(itemData, learned: true, characterClass: 0, special3: 51).Mob);

        Check(trained.CurrentScore.Damage == baseline.CurrentScore.Damage + 25,
            $"Nocao de Combate did not add Special3 / 2 to damage: baseline={baseline.CurrentScore.Damage}, trained={trained.CurrentScore.Damage}.");
    }

    private static void ClassAndLearnedBitGateThePassive()
    {
        var itemData = EmptyItemData();
        var baseline = LegacyMobCombatState.Read(Project(itemData, learned: false, characterClass: 0, special3: 51).Mob);
        var unlearned = LegacyMobCombatState.Read(Project(itemData, learned: false, characterClass: 0, special3: 51).Mob);
        var wrongClass = LegacyMobCombatState.Read(Project(itemData, learned: true, characterClass: 1, special3: 51).Mob);

        Check(unlearned.CurrentScore.Damage == baseline.CurrentScore.Damage &&
              wrongClass.CurrentScore.Damage == baseline.CurrentScore.Damage,
            "Nocao de Combate changed damage without both the learned bit and the Transknight class gate.");
    }

    private static LegacyCharacterLoginData Project(
        LegacyItemDataTable itemData,
        bool learned,
        int characterClass,
        int special3)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(
            Level: 100, Ac: 101, Damage: 500, Merchant: 0, AttackRun: 0x96, Direction: 0,
            ChaosRate: 0, MaxHp: 1_000, MaxMp: 500, Hp: 900, Mp: 400,
            Strength: 0, Intelligence: 0, Dexterity: 0, Constitution: 0,
            Special1: 0, Special2: 0, Special3: checked((short)special3), Special4: 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset),
            learned ? CombatAwarenessLearnedFlag : 0);
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
