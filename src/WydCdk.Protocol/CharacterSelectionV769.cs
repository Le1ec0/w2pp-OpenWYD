using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>One fixed-width character entry in the 7.69 client's STRUCT_SELCHAR.</summary>
public sealed record CharacterSelectionSlotV769(
    ushort HomeTownX,
    ushort HomeTownY,
    ReadOnlyMemory<byte> MobName,
    ClientScoreV769 Score,
    IReadOnlyList<LegacyItem> Equipment,
    ushort Guild,
    int Coin,
    long Experience);

/// <summary>
/// Explicit 904-byte client 7.69 STRUCT_SELCHAR serializer. It does not decode or reinterpret
/// the W2PP account blob; an adapter must project source data into this wire DTO.
/// </summary>
public sealed class CharacterSelectionV769
{
    public const int CharacterCount = 4;
    public const int EquipmentCount = 18;
    public const int NameLength = 16;
    public const int SizeInBytes = 904;

    public const int HomeTownXOffset = 0;
    public const int HomeTownYOffset = 8;
    public const int MobNameOffset = 16;
    public const int ScoreOffset = 80;
    public const int EquipmentOffset = 272;
    public const int GuildOffset = 848;
    public const int CoinOffset = 856;
    public const int ExperienceOffset = 872;

    public IReadOnlyList<CharacterSelectionSlotV769> Slots { get; }

    public CharacterSelectionV769(IReadOnlyList<CharacterSelectionSlotV769> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (slots.Count != CharacterCount)
            throw new ArgumentException("Client 7.69 selection requires exactly four characters.", nameof(slots));

        foreach (var slot in slots)
        {
            if (slot.MobName.Length != NameLength)
                throw new ArgumentException("Each client 7.69 character name must preserve exactly sixteen wire bytes.", nameof(slots));
            if (slot.Equipment is null || slot.Equipment.Count != EquipmentCount)
                throw new ArgumentException("Each client 7.69 character requires exactly eighteen equipment entries.", nameof(slots));
        }

        Slots = slots.ToArray();
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[SizeInBytes];
        for (var character = 0; character < CharacterCount; character++)
        {
            var slot = Slots[character];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(HomeTownXOffset + (character * sizeof(ushort))), slot.HomeTownX);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(HomeTownYOffset + (character * sizeof(ushort))), slot.HomeTownY);
            slot.MobName.Span.CopyTo(bytes.AsSpan(MobNameOffset + (character * NameLength), NameLength));
            slot.Score.Write(bytes.AsSpan(ScoreOffset + (character * ClientScoreV769.SizeInBytes), ClientScoreV769.SizeInBytes));

            for (var equipment = 0; equipment < EquipmentCount; equipment++)
            {
                var itemOffset = EquipmentOffset + (((character * EquipmentCount) + equipment) * LegacyItem.SizeInBytes);
                slot.Equipment[equipment].Write(bytes.AsSpan(itemOffset, LegacyItem.SizeInBytes));
            }

            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(GuildOffset + (character * sizeof(ushort))), slot.Guild);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(CoinOffset + (character * sizeof(int))), slot.Coin);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(ExperienceOffset + (character * sizeof(long))), slot.Experience);
        }

        return bytes;
    }
}
