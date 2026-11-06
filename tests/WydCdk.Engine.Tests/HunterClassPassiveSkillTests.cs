using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class HunterClassPassiveSkillTests
{
    private const int HunterClass = 3;
    private const int WeaponIndex = 500;

    public static void AggressivenessAddsSpecial2PlusTenForClawOrBow()
    {
        var itemData = ItemDataWithWeaponUnique(43);
        var baseline = Project(itemData, learnedSkills: 0, characterClass: HunterClass, special2: 20, weaponIndex: WeaponIndex);
        var trained = Project(itemData, learnedSkills: 1u << 2, characterClass: HunterClass, special2: 20, weaponIndex: WeaponIndex);

        Check(trained.CurrentScore.Damage == baseline.CurrentScore.Damage + 30,
            $"Agressividade did not add Special2+10 for a claw: baseline={baseline.CurrentScore.Damage}, trained={trained.CurrentScore.Damage}.");

        var wrongWeaponData = ItemDataWithWeaponUnique(42);
        var bowTrained = Project(wrongWeaponData, learnedSkills: 1u << 2, characterClass: HunterClass, special2: 20, weaponIndex: WeaponIndex);
        Check(bowTrained.CurrentScore.Damage == baseline.CurrentScore.Damage + 30,
            "Agressividade did not accept a bow weapon unique.");

        var otherWeaponData = ItemDataWithWeaponUnique(46);
        var otherWeapon = Project(otherWeaponData, learnedSkills: 1u << 2, characterClass: HunterClass, special2: 20, weaponIndex: WeaponIndex);
        Check(otherWeapon.CurrentScore.Damage == baseline.CurrentScore.Damage,
            "Agressividade added damage for a weapon outside the 7.69 claw/bow gate.");
    }

    public static void LightningStormAddsTwiceSpecial2()
    {
        var itemData = EmptyItemData();
        var baseline = Project(itemData, learnedSkills: 0, characterClass: HunterClass, special2: 20);
        var trained = Project(itemData, learnedSkills: 1u << 7, characterClass: HunterClass, special2: 20);
        var wrongClass = Project(itemData, learnedSkills: 1u << 7, characterClass: 0, special2: 20);

        Check(trained.CurrentScore.Damage == baseline.CurrentScore.Damage + 40 &&
              wrongClass.CurrentScore.Damage == baseline.CurrentScore.Damage,
            $"Tempestade de Raios did not apply Special2*2 only to a trained Hunter: baseline={baseline.CurrentScore.Damage}, trained={trained.CurrentScore.Damage}.");
    }

    public static void ShadowProtectionRequiresClawAndAddsSpecial4ThirdPlusTenArmor()
    {
        var clawData = ItemDataWithWeaponUnique(43);
        var baseline = Project(clawData, learnedSkills: 0, characterClass: HunterClass, special4: 61, weaponIndex: WeaponIndex);
        var trained = Project(clawData, learnedSkills: 1u << 22, characterClass: HunterClass, special4: 61, weaponIndex: WeaponIndex);
        var noWeapon = Project(clawData, learnedSkills: 1u << 22, characterClass: HunterClass, special4: 61);
        var wrongClass = Project(clawData, learnedSkills: 1u << 22, characterClass: 0, special4: 61, weaponIndex: WeaponIndex);

        Check(trained.CurrentScore.Ac == baseline.CurrentScore.Ac + 30 &&
              noWeapon.CurrentScore.Ac == baseline.CurrentScore.Ac &&
              wrongClass.CurrentScore.Ac == baseline.CurrentScore.Ac,
            $"Protecao das Sombras did not apply Special4/3+10 only with a Hunter claw: baseline={baseline.CurrentScore.Ac}, trained={trained.CurrentScore.Ac}.");
    }

    public static void HunterVisionAddsDexterityOverFiftyCritical()
    {
        var itemData = EmptyItemData();
        var baseline = Project(itemData, learnedSkills: 0, characterClass: HunterClass, dexterity: 100);
        var trained = Project(itemData, learnedSkills: 1u << 18, characterClass: HunterClass, dexterity: 100);
        var wrongClass = Project(itemData, learnedSkills: 1u << 18, characterClass: 0, dexterity: 100);

        Check(trained.Critical == baseline.Critical + 2 && wrongClass.Critical == baseline.Critical,
            $"Visao do Cacador did not add Dexterity/50 Critical only to a trained Hunter: baseline={baseline.Critical}, trained={trained.Critical}.");
    }

    private static LegacyMobCombatState Project(
        LegacyItemDataTable itemData,
        uint learnedSkills,
        int characterClass,
        short special2 = 0,
        short special4 = 0,
        short dexterity = 0,
        int weaponIndex = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(
            Level: 100, Ac: 101, Damage: 500, Merchant: 0, AttackRun: 0x96, Direction: 0,
            ChaosRate: 0, MaxHp: 1_000, MaxMp: 500, Hp: 900, Mp: 400,
            Strength: 0, Intelligence: 0, Dexterity: dexterity, Constitution: 0,
            Special1: 0, Special2: special2, Special3: 0, Special4: special4);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = checked((byte)characterClass);
        BinaryPrimitives.WriteUInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkills);
        if (weaponIndex != 0)
        {
            new LegacyItem(checked((short)weaponIndex), 0, 0, 0, 0, 0, 0).Write(
                mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 6 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        }

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
        return LegacyMobCombatState.Read(projected.Mob);
    }

    private static LegacyItemDataTable ItemDataWithWeaponUnique(short unique)
    {
        var encoded = new byte[LegacyItemDataTable.BodySizeInBytes];
        var uniqueOffset = (WeaponIndex * LegacyItemDataTable.RecordSizeInBytes) + 132;
        encoded[uniqueOffset] = (byte)(unique ^ 0x5A);
        encoded[uniqueOffset + 1] = 0x5A;
        return LegacyItemDataTable.Load(encoded);
    }

    private static LegacyItemDataTable EmptyItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
