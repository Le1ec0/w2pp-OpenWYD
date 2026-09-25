using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Projects the raw W2PP <c>MSG_CreateMobTrade</c> payload into the client
/// 7.69 contract. W2PP has 16 visual equipment/appearance entries and no
/// server byte; the two client-only entries and <c>Server</c> are zeroed.
/// </summary>
public static class W2ppCreateMobTradeV1Adapter
{
    public const int SourcePacketSize = 252;
    public const int SourcePayloadSize = SourcePacketSize - PacketHeader.SizeInBytes;
    public const int SourceMobNameOffset = 6;
    public const int SourceEquipmentOffset = 22;
    public const int SourceEquipmentCount = 16;
    public const int SourceAffectOffset = 54;
    public const int SourceGuildOffset = 118;
    public const int SourceGuildLevelOffset = 120;
    public const int SourceScoreOffset = 124;
    public const int SourceCreateTypeOffset = 172;
    public const int SourceAppearanceOffset = 174;
    public const int SourceAppearanceCount = 16;
    public const int SourceNickOffset = 190;
    public const int SourceNickLength = 26;
    public const int SourceDescriptionOffset = 216;
    public const int SourceDescriptionLength = 24;

    public static CreateMobTradeConfirmationV769 AdaptPayload(ReadOnlySpan<byte> sourcePayload)
    {
        if (sourcePayload.Length != SourcePayloadSize)
            throw new ArgumentException($"W2PP CreateMobTrade payload requires exactly {SourcePayloadSize} bytes.", nameof(sourcePayload));

        var visualEquipment = new ushort[CreateMobTradeConfirmationV769.EquipmentCount];
        for (var index = 0; index < SourceEquipmentCount; index++)
        {
            visualEquipment[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                sourcePayload.Slice(SourceEquipmentOffset + (index * sizeof(ushort)), sizeof(ushort)));
        }

        var affects = new ushort[CreateMobTradeConfirmationV769.AffectCount];
        for (var index = 0; index < affects.Length; index++)
        {
            affects[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                sourcePayload.Slice(SourceAffectOffset + (index * sizeof(ushort)), sizeof(ushort)));
        }

        var secondaryAppearance = new byte[CreateMobTradeConfirmationV769.SecondaryAppearanceLength];
        sourcePayload.Slice(SourceAppearanceOffset, SourceAppearanceCount).CopyTo(secondaryAppearance);

        return new CreateMobTradeConfirmationV769(
            BinaryPrimitives.ReadInt16LittleEndian(sourcePayload),
            BinaryPrimitives.ReadInt16LittleEndian(sourcePayload[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[4..]),
            sourcePayload.Slice(SourceMobNameOffset, CreateMobTradeConfirmationV769.MobNameLength).ToArray(),
            visualEquipment,
            affects,
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceGuildOffset..]),
            sourcePayload[SourceGuildLevelOffset],
            AdaptScore(LegacyScore.Read(sourcePayload.Slice(SourceScoreOffset, LegacyScore.SizeInBytes))),
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceCreateTypeOffset..]),
            secondaryAppearance,
            sourcePayload.Slice(SourceNickOffset, SourceNickLength).ToArray(),
            sourcePayload.Slice(SourceDescriptionOffset, SourceDescriptionLength).ToArray(),
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
