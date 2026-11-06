using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>One audited CurrentScore value cannot be represented in the W2PP account MOB field.</summary>
public sealed class LegacyCurrentScoreProjectionException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

/// <summary>
/// Rebuilds the audited 7.69 CurrentScore subset into a copied W2PP account MOB.
/// This is a compatibility projection, not complete BASE_GetCurrentScore parity.
/// </summary>
public static class LegacyCurrentScoreLoginProjector
{
    private const int BeastMasterClass = 2;
    private const uint TormentShieldLearnedSkillFlag = 1u << 19;
    private const int TormentShieldPosition = 128;
    private const int OffhandEquipmentSlot = 7;

    public static LegacyCharacterLoginData Project(LegacyCharacterLoginData data, LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(itemData);
        RequireLength(data.Mob, LegacyAccountSnapshot.CharacterStride, "MOB");
        RequireLength(data.ShortSkill, LegacyAccountSnapshot.ShortSkillStride, "ShortSkill");
        RequireLength(data.Affect, LegacyAccountSnapshot.AffectStride, "Affect");
        RequireLength(data.MobExtra, LegacyAccountSnapshot.MobExtraStride, "MOBEXTRA");

        var mob = data.Mob.ToArray();
        var affect = data.Affect.ToArray();
        var mobExtra = data.MobExtra.ToArray();
        var shortSkill = data.ShortSkill.ToArray();
        var equipment = ReadEquipment(mob, data.ClientEquipment);
        ValidateEquipmentIndexes(equipment);

        var character = LegacyMobCombatState.Read(mob);
        var classMaster = BinaryPrimitives.ReadInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset));
        var soul = mobExtra[LegacyAccountSnapshot.MobExtraSoulOffset];
        var originalFace = equipment[0].Index / 10;
        var baseLevel = character.BaseScore.Level;

        // CMob::GetCurrentScore initializes player resistances from equipment below level 2000,
        // clears the first head effect, and then BASE_GetCurrentScore resets RSV.
        Span<int> resist = stackalloc int[4];
        if (baseLevel < 2000)
        {
            resist[0] = Math.Min(100, LegacyMobAbilityMath.GetAbility(equipment, character.CharacterClass, character.LearnedSkill, LegacyItemEffect.Resist1, itemData));
            resist[1] = Math.Min(100, LegacyMobAbilityMath.GetAbility(equipment, character.CharacterClass, character.LearnedSkill, LegacyItemEffect.Resist2, itemData));
            resist[2] = Math.Min(100, LegacyMobAbilityMath.GetAbility(equipment, character.CharacterClass, character.LearnedSkill, LegacyItemEffect.Resist3, itemData));
            resist[3] = Math.Min(100, LegacyMobAbilityMath.GetAbility(equipment, character.CharacterClass, character.LearnedSkill, LegacyItemEffect.Resist4, itemData));
            equipment[0] = equipment[0] with { Effect1 = 0, Value1 = 0 };
        }
        else
        {
            for (var index = 0; index < resist.Length; index++)
                resist[index] = unchecked((sbyte)mob[LegacyAccountSnapshot.MobResistOffset + index]);
        }

        byte[]? pursuitBaseResistances = null;
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
        {
            if (affect[offset] != 3)
                continue;

            pursuitBaseResistances = new byte[resist.Length];
            for (var index = 0; index < resist.Length; index++)
                pursuitBaseResistances[index] = checked((byte)Math.Clamp(resist[index], 0, 100));
            break;
        }

        var stages = LegacyCurrentScoreMath.CalculateCurrentScoreCoordinatorStage(
            character,
            equipment,
            itemData,
            affect,
            classMaster,
            soul,
            isSummon: false,
            resist[0],
            resist[1],
            resist[2],
            resist[3]);

        var frozenBlade = LegacyCurrentScoreMath.CalculateFrozenBladeAffectStage(affect, equipment[0].Index);
        var tormentShieldArmorClassAddition = CalculateTormentShieldArmorClassAddition(character, equipment, itemData);
        var terminal = stages.AfterKibita with
        {
            CurrentScore = stages.AfterKibita.CurrentScore with
            {
                Ac = unchecked(stages.AfterKibita.CurrentScore.Ac + tormentShieldArmorClassAddition),
                Intelligence = unchecked((short)(stages.AfterKibita.CurrentScore.Intelligence + frozenBlade.IntelligenceDelta)),
            },
        };
        var weaken = LegacyCurrentScoreMath.CalculateWeakenAffectStage(affect);
        // Type=21 AC is projected by the ordered AC stage. Its DAMAGEMULTI uses target 7.69's
        // Level / 25 + Value formula; W2PP diverges with Level / 10 + Value.
        var damageMultiplier = checked(100
            + stages.Meditation.DamageMultiplierAdjustment
            + stages.Transformation.Damage.DamageMultiplierAdjustment
            + stages.MagicWeapon.DamageMultiplierAdjustment
            + stages.Assault.DamageMultiplierAdjustment);
        var damage = LegacyCurrentScoreMath.ApplyFinalDamageStage(
            unchecked(terminal.CurrentScore.Damage - weaken.DamageReduction),
            terminal.CurrentScore.Strength,
            terminal.CurrentScore.Dexterity,
            terminal.CurrentScore.Special1,
            terminal.CurrentScore.Level,
            originalFace,
            classMaster,
            damageMultiplier);

        var mountRunFloor = LegacyMountRunRules.TryResolveRunFloor(equipment[14], out var resolvedRunFloor)
            ? resolvedRunFloor
            : 0;
        var speed = LegacyCurrentScoreMath.ApplyFinalAttackRunStage(
            checked(stages.Abilities.AttackSpeedSeed + frozenBlade.AttackSpeedDelta),
            checked(stages.Abilities.RunSeed + stages.Haste.RunAddition),
            checked(stages.Transformation.Speed.AttackSpeedBonus + stages.HolyTouch.AttackDelta),
            checked(stages.Transformation.Speed.RunSpeedBonus + stages.HolyTouch.RunDelta),
            terminal.CurrentScore.Dexterity,
            originalFace,
            mountRunFloor);

        var scalar = LegacyCurrentScoreMath.ApplyFinalScalarClamps(
            terminal.CurrentScore.Hp,
            terminal.CurrentScore.Mp,
            terminal.CurrentScore.MaxHp,
            terminal.CurrentScore.MaxMp,
            stages.Abilities.RegenHpSeed,
            stages.Abilities.RegenMpSeed,
            terminal.Magic,
            checked(stages.Abilities.CriticalSeed +
                    stages.Transformation.Critical.CriticalAddition +
                    stages.HunterClassPassives.HunterVisionCriticalAddition +
                    stages.ArmorCritical.CriticalAddition));
        var critical = NarrowSignedByte(scalar.Critical, "Critical");
        var saveMana = NarrowSignedByte(stages.Abilities.SaveMana, "SaveMana");

        var score = terminal.CurrentScore with
        {
            Damage = damage.Damage,
            AttackRun = speed.PackedAttackRun,
            Merchant = (byte)((terminal.CurrentScore.Merchant & 0xF0) | mob[LegacyAccountSnapshot.MobMerchantOffset]),
            Hp = scalar.Hp,
            Mp = scalar.Mp,
        };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes));
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), stages.RsvWithAffects);
        BinaryPrimitives.WriteUInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), terminal.LearnedSkill);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), scalar.Magic);
        mob[LegacyAccountSnapshot.MobCriticalOffset] = unchecked((byte)critical);
        mob[LegacyAccountSnapshot.MobSaveManaOffset] = unchecked((byte)saveMana);
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRegenHpOffset), scalar.RegenHp);
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRegenMpOffset), scalar.RegenMp);
        mob[LegacyAccountSnapshot.MobResistOffset] = stages.Resistance.Holy;
        mob[LegacyAccountSnapshot.MobResistOffset + 1] = stages.Resistance.Thunder;
        mob[LegacyAccountSnapshot.MobResistOffset + 2] = stages.Resistance.Fire;
        mob[LegacyAccountSnapshot.MobResistOffset + 3] = stages.Resistance.Ice;

        equipment[0] = stages.Transformation.Equipment.HeadItem;
        for (var slot = 0; slot < LegacyCharacterSelection.EquipmentCount; slot++)
            equipment[slot].Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + slot * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));

        return data with
        {
            Mob = mob,
            ShortSkill = shortSkill,
            Affect = affect,
            MobExtra = mobExtra,
            ClientEquipment = data.ClientEquipment is null ? null : equipment,
            PursuitBaseResistances = pursuitBaseResistances,
        };
    }

    private static LegacyItem[] ReadEquipment(byte[] mob, IReadOnlyList<LegacyItem>? clientEquipment)
    {
        if (clientEquipment is not null)
        {
            if (clientEquipment.Count != CharacterMobV769.EquipmentCount)
                throw new ArgumentException($"Versioned client equipment must contain exactly {CharacterMobV769.EquipmentCount} slots.", nameof(clientEquipment));
            return clientEquipment.ToArray();
        }

        var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
        for (var slot = 0; slot < equipment.Length; slot++)
        {
            var offset = LegacyAccountSnapshot.MobEquipmentOffset + slot * LegacyItem.SizeInBytes;
            equipment[slot] = LegacyItem.Read(mob.AsSpan(offset, LegacyItem.SizeInBytes));
        }
        return equipment;
    }

    private static int CalculateTormentShieldArmorClassAddition(
        LegacyMobCombatState character,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData)
    {
        if (character.CharacterClass != BeastMasterClass ||
            (character.LearnedSkill & TormentShieldLearnedSkillFlag) == 0)
            return 0;

        var offhand = equipment[OffhandEquipmentSlot];
        if (itemData.GetItemPosition(offhand) != TormentShieldPosition)
            return 0;

        // CMob::GetCurrentScore applies the Beastmaster shield passive after BASE_GetCurrentScore.
        return (itemData.GetItemAbility(offhand, LegacyItemEffect.Ac) + 1) / 7;
    }

    private static void ValidateEquipmentIndexes(ReadOnlySpan<LegacyItem> equipment)
    {
        for (var slot = 0; slot < equipment.Length; slot++)
        {
            if (equipment[slot].Index < 0 || equipment[slot].Index >= LegacyItemDataTable.MaxItemIndex)
                throw new ArgumentOutOfRangeException(nameof(equipment), equipment[slot].Index, $"Equipment item index at slot {slot} is outside the ItemList range.");
        }
    }

    private static sbyte NarrowSignedByte(int value, string field)
    {
        try
        {
            return checked((sbyte)value);
        }
        catch (OverflowException exception)
        {
            throw new LegacyCurrentScoreProjectionException($"Projected {field} value {value} cannot be represented by the signed 7.69 account field.", exception);
        }
    }

    private static void RequireLength(byte[] bytes, int expectedLength, string field)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != expectedLength)
            throw new ArgumentException($"{field} must contain exactly {expectedLength} bytes.", field);
    }
}
