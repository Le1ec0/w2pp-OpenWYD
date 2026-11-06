using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Projects the 7.69 AttackRun and Intelligence fields changed by NPC Type 1/7 affects.</summary>
internal static class LegacyNpcAffectScoreProjector
{
    public static LegacyNpcAffectScoreProjection Project(
        ReadOnlySpan<byte> mobSnapshot,
        ReadOnlySpan<byte> affectSnapshot,
        LegacyItemDataTable itemData)
    {
        if (mobSnapshot.Length < LegacyAccountSnapshot.CharacterStride)
            throw new ArgumentException("A legacy MOB snapshot is truncated.", nameof(mobSnapshot));
        if (affectSnapshot.Length < LegacyAccountSnapshot.AffectStride)
            throw new ArgumentException("A legacy affect snapshot is truncated.", nameof(affectSnapshot));
        ArgumentNullException.ThrowIfNull(itemData);

        var equipment = new LegacyItem[LegacyCharacterSelection.EquipmentCount];
        for (var slot = 0; slot < equipment.Length; slot++)
        {
            var offset = LegacyAccountSnapshot.MobEquipmentOffset + slot * LegacyItem.SizeInBytes;
            equipment[slot] = LegacyItem.Read(mobSnapshot.Slice(offset, LegacyItem.SizeInBytes));
            if (equipment[slot].Index < 0 || equipment[slot].Index >= LegacyItemDataTable.MaxItemIndex)
                throw new ArgumentOutOfRangeException(nameof(mobSnapshot), equipment[slot].Index,
                    $"NPC equipment item index at slot {slot} is outside the ItemList range.");
        }

        var mob = LegacyMobCombatState.Read(mobSnapshot);
        var scoreStages = LegacyCurrentScoreMath.CalculateCurrentScoreCoordinatorStage(
            mob,
            equipment,
            itemData,
            affectSnapshot,
            classMaster: 0,
            soul: 0,
            isSummon: mobSnapshot[LegacyAccountSnapshot.MobClanOffset] == 4,
            unchecked((sbyte)mobSnapshot[LegacyAccountSnapshot.MobResistOffset]),
            unchecked((sbyte)mobSnapshot[LegacyAccountSnapshot.MobResistOffset + 1]),
            unchecked((sbyte)mobSnapshot[LegacyAccountSnapshot.MobResistOffset + 2]),
            unchecked((sbyte)mobSnapshot[LegacyAccountSnapshot.MobResistOffset + 3]));
        var frozenBlade = LegacyCurrentScoreMath.CalculateFrozenBladeAffectStage(affectSnapshot, equipment[0].Index);
        var terminal = scoreStages.AfterKibita;
        var mountRunFloor = LegacyMountRunRules.TryResolveRunFloor(equipment[14], out var runFloor) ? runFloor : 0;
        var speed = LegacyCurrentScoreMath.ApplyFinalAttackRunStage(
            checked(scoreStages.Abilities.AttackSpeedSeed + frozenBlade.AttackSpeedDelta),
            checked(scoreStages.Abilities.RunSeed + scoreStages.Haste.RunAddition + scoreStages.HolyTouch.RunDelta),
            checked(scoreStages.Transformation.Speed.AttackSpeedBonus + scoreStages.HolyTouch.AttackDelta),
            scoreStages.Transformation.Speed.RunSpeedBonus,
            terminal.CurrentScore.Dexterity,
            equipment[0].Index / 10,
            mountRunFloor);

        return new LegacyNpcAffectScoreProjection(
            speed.PackedAttackRun,
            unchecked((short)(terminal.CurrentScore.Intelligence + frozenBlade.IntelligenceDelta)));
    }
}

internal readonly record struct LegacyNpcAffectScoreProjection(byte AttackRun, short Intelligence);
