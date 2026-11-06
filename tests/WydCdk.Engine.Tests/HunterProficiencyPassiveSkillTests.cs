using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class HunterProficiencyPassiveSkillTests
{
    private const int HunterClass = 3;
    private const int HunterProficiencySkillId = 82;
    private const int HunterProficiencyLearnedBit = HunterProficiencySkillId % 24;
    private const short WeaponItemIndex = 500;
    private const short ShieldItemIndex = 501;
    private const int BaseDamage = 500;
    private const short Special2 = 30;

    public static void Run()
    {
        AddsSpecial2AndTwelveForLearnedHunterWithWeaponAndNoShield();
        DoesNotAddBonusWhenAnySourceEquipmentGateFails();
    }

    private static void AddsSpecial2AndTwelveForLearnedHunterWithWeaponAndNoShield()
    {
        var itemData = EmptyItemData();
        var baseline = ProjectDamage(itemData, learned: false, characterClass: HunterClass,
            weaponIndex: WeaponItemIndex, shieldIndex: 0);
        var learned = ProjectDamage(itemData, learned: true, characterClass: HunterClass,
            weaponIndex: WeaponItemIndex, shieldIndex: 0);

        Check(learned == baseline + Special2 + 12,
            $"Learned skill 82 did not add Special2+12 to the hunter's score damage: baseline={baseline}, learned={learned}.");
    }

    private static void DoesNotAddBonusWhenAnySourceEquipmentGateFails()
    {
        var itemData = EmptyItemData();
        var baseline = ProjectDamage(itemData, learned: false, characterClass: HunterClass,
            weaponIndex: WeaponItemIndex, shieldIndex: 0);
        var noWeapon = ProjectDamage(itemData, learned: true, characterClass: HunterClass,
            weaponIndex: 0, shieldIndex: 0);
        var withShield = ProjectDamage(itemData, learned: true, characterClass: HunterClass,
            weaponIndex: WeaponItemIndex, shieldIndex: ShieldItemIndex);
        var wrongClass = ProjectDamage(itemData, learned: true, characterClass: 2,
            weaponIndex: WeaponItemIndex, shieldIndex: 0);

        Check(noWeapon == baseline && withShield == baseline && wrongClass == baseline,
            $"Skill 82 escaped a 7.69 class/equipment gate: base={baseline}, noWeapon={noWeapon}, shield={withShield}, wrongClass={wrongClass}.");
    }

    private static int ProjectDamage(
        LegacyItemDataTable itemData,
        bool learned,
        int characterClass,
        short weaponIndex,
        short shieldIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(
            Level: 100, Ac: 20, Damage: BaseDamage, Merchant: 0, AttackRun: 0x96, Direction: 0,
            ChaosRate: 0, MaxHp: 1_000, MaxMp: 500, Hp: 1_000, Mp: 500,
            Strength: 0, Intelligence: 0, Dexterity: 0, Constitution: 0,
            Special1: 0, Special2: Special2, Special3: 0, Special4: 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset),
            learned ? 1u << HunterProficiencyLearnedBit : 0);
        new LegacyItem(weaponIndex, 0, 0, 0, 0, 0, 0).Write(
            mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 6 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        new LegacyItem(shieldIndex, 0, 0, 0, 0, 0, 0).Write(
            mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 7 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));

        var extra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            extra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset),
            LegacyAccountSnapshot.ClassMasterMortal);
        var projected = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                new byte[LegacyAccountSnapshot.AffectStride],
                extra,
                Donate: 0,
                SavedPositionX: 0,
                SavedPositionY: 0),
            itemData);
        return LegacyMobCombatState.Read(projected.Mob).CurrentScore.Damage;
    }

    private static LegacyItemDataTable EmptyItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
