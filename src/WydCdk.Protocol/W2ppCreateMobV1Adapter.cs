using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Projects the raw W2PP 16-slot CreateMob payload into the client 7.69
/// message. Visual-code calculation, NPC policy, and relay remain outside this
/// structural adapter.
/// </summary>
public static class W2ppCreateMobV1Adapter
{
    public const int SourcePacketSize = 232;
    public const int SourcePayloadSize = SourcePacketSize - PacketHeader.SizeInBytes;
    public const int SourceMobNameOffset = 6;
    public const int SourceEquipmentOffset = 22;
    public const int SourceEquipmentCount = 16;
    public const int SourceAffectOffset = 54;
    public const int SourceGuildOffset = 118;
    public const int SourceGuildLevelOffset = 120;
    public const int SourceScoreOffset = 124;
    public const int SourceCreateTypeOffset = 172;
    public const int SourceAncientCodeOffset = 174;
    public const int SourceAncientCodeCount = 16;
    public const int SourceNickOffset = 190;
    public const int SourceNickLength = 26;

    public static CreateMobConfirmationV769 AdaptPayload(
        ReadOnlySpan<byte> sourcePayload,
        IReadOnlyList<ushort>? visualEquipmentOverride = null,
        IReadOnlyList<byte>? ancientCodesOverride = null)
    {
        if (sourcePayload.Length != SourcePayloadSize)
            throw new ArgumentException($"W2PP CreateMob payload requires exactly {SourcePayloadSize} bytes.", nameof(sourcePayload));
        if (visualEquipmentOverride is not null && visualEquipmentOverride.Count != CreateMobConfirmationV769.EquipmentCount)
            throw new ArgumentException($"The client 7.69 CreateMob projection requires exactly {CreateMobConfirmationV769.EquipmentCount} visual equipment entries.", nameof(visualEquipmentOverride));
        if (ancientCodesOverride is not null && ancientCodesOverride.Count != CreateMobConfirmationV769.AncientCodeCount)
            throw new ArgumentException($"The client 7.69 CreateMob projection requires exactly {CreateMobConfirmationV769.AncientCodeCount} ancient-code entries.", nameof(ancientCodesOverride));

        var visualEquipment = visualEquipmentOverride?.ToArray() ?? new ushort[CreateMobConfirmationV769.EquipmentCount];
        for (var index = 0; visualEquipmentOverride is null && index < SourceEquipmentCount; index++)
        {
            visualEquipment[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                sourcePayload.Slice(SourceEquipmentOffset + index * sizeof(ushort), sizeof(ushort)));
        }

        var affects = new ushort[CreateMobConfirmationV769.AffectCount];
        for (var index = 0; index < affects.Length; index++)
        {
            affects[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                sourcePayload.Slice(SourceAffectOffset + index * sizeof(ushort), sizeof(ushort)));
        }

        var ancientCodes = ancientCodesOverride?.ToArray() ?? new byte[CreateMobConfirmationV769.AncientCodeCount];
        if (ancientCodesOverride is null)
            sourcePayload.Slice(SourceAncientCodeOffset, SourceAncientCodeCount).CopyTo(ancientCodes);

        return new CreateMobConfirmationV769(
            BinaryPrimitives.ReadInt16LittleEndian(sourcePayload),
            BinaryPrimitives.ReadInt16LittleEndian(sourcePayload[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[4..]),
            sourcePayload.Slice(SourceMobNameOffset, CreateMobConfirmationV769.MobNameLength).ToArray(),
            visualEquipment,
            affects,
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceGuildOffset..]),
            sourcePayload[SourceGuildLevelOffset],
            AdaptScore(LegacyScore.Read(sourcePayload.Slice(SourceScoreOffset, LegacyScore.SizeInBytes))),
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceCreateTypeOffset..]),
            ancientCodes,
            sourcePayload.Slice(SourceNickOffset, SourceNickLength).ToArray(),
            server: 0);
    }

    private static ClientScoreV769 AdaptScore(LegacyScore score) => new(
        checked((short)score.Level),
        score.Ac,
        score.Damage,
        Reserved: 0,
        score.AttackRun,
        score.MaxHp,
        score.MaxMp,
        score.Hp,
        score.Mp,
        score.Strength,
        score.Intelligence,
        score.Dexterity,
        score.Constitution,
        unchecked((ushort)score.Special1),
        unchecked((ushort)score.Special2),
        unchecked((ushort)score.Special3),
        unchecked((ushort)score.Special4));
}
