using System.Text;

namespace WydCdk.Protocol;

/// <summary>
/// Explicitly projects the current W2PP-shaped selection into the 7.69 client wire DTO.
/// The projection is lossy by design: W2PP has 16 equipment entries and four score bytes
/// that have no semantic counterparts in the 7.69 selection score.
/// </summary>
public static class W2ppCharacterSelectionV1Adapter
{
    public static CharacterSelectionV769 Adapt(LegacyCharacterSelection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var slots = new CharacterSelectionSlotV769[LegacyCharacterSelection.CharacterCount];

        for (var character = 0; character < slots.Length; character++)
        {
            var legacy = source.Slots[character];
            var name = EncodeName(legacy.Name);
            var equipment = new LegacyItem[CharacterSelectionV769.EquipmentCount];
            for (var item = 0; item < LegacyCharacterSelection.EquipmentCount; item++)
                equipment[item] = legacy.Equipment[item];

            slots[character] = new CharacterSelectionSlotV769(
                unchecked((ushort)legacy.SavedPositionX),
                unchecked((ushort)legacy.SavedPositionY),
                name,
                AdaptScore(legacy.Score),
                equipment,
                legacy.Guild,
                legacy.Coin,
                legacy.Experience);
        }

        return new CharacterSelectionV769(slots);
    }

    private static ClientScoreV769 AdaptScore(LegacyScore source)
    {
        var level = checked((short)source.Level);
        return new ClientScoreV769(
            level,
            source.Ac,
            source.Damage,
            Reserved: 0,
            source.AttackRun,
            source.MaxHp,
            source.MaxMp,
            source.Hp,
            source.Mp,
            source.Strength,
            source.Intelligence,
            source.Dexterity,
            source.Constitution,
            unchecked((ushort)source.Special1),
            unchecked((ushort)source.Special2),
            unchecked((ushort)source.Special3),
            unchecked((ushort)source.Special4));
    }

    private static byte[] EncodeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > CharacterSelectionV769.NameLength)
            throw new ArgumentOutOfRangeException(nameof(name), "A 7.69 character name cannot exceed sixteen wire bytes.");
        if (name.Any(static character => character > 0x7F || character == '\0'))
            throw new NotSupportedException("The W2PP string snapshot cannot safely project non-ASCII or embedded-NUL name bytes.");

        var bytes = new byte[CharacterSelectionV769.NameLength];
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
        return bytes;
    }
}
