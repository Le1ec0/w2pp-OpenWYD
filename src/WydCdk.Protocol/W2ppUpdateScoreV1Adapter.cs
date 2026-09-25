using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Projects the packed W2PP MSG_UpdateScore into the client 7.69 layout.
/// W2PP has RegenHP/MP, a 32-bit Magic and four Special bytes in the tail;
/// the target has ReqHp/ReqMp, a 16-bit Magic, Rsv and LearnedSkill instead.
/// </summary>
public static class W2ppUpdateScoreV1Adapter
{
    public const int SourcePayloadSize = 140;
    public const int SourcePacketSize = PacketHeader.SizeInBytes + SourcePayloadSize;
    public const int SourceScoreOffset = 0;
    public const int SourceCriticalOffset = 48;
    public const int SourceSaveManaOffset = 49;
    public const int SourceAffectOffset = 50;
    public const int SourceGuildOffset = 114;
    public const int SourceGuildLevelOffset = 116;
    public const int SourceResistOffset = 118;
    public const int SourceCurrentHpOffset = 124;
    public const int SourceCurrentMpOffset = 128;
    public const int SourceMagicOffset = 132;
    public const int SourceSpecialOffset = 136;

    public static UpdateScoreConfirmationV769 AdaptPayload(ReadOnlySpan<byte> sourcePayload)
    {
        if (sourcePayload.Length != SourcePayloadSize)
            throw new ArgumentException($"W2PP UpdateScore requires exactly {SourcePayloadSize} payload bytes.", nameof(sourcePayload));

        var sourceScore = LegacyScore.Read(sourcePayload[SourceScoreOffset..]);
        var affects = new ushort[UpdateScoreConfirmationV769.AffectCount];
        for (var index = 0; index < affects.Length; index++)
            affects[index] = BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[(SourceAffectOffset + (index * sizeof(ushort)))..]);

        var magic = BinaryPrimitives.ReadInt32LittleEndian(sourcePayload[SourceMagicOffset..]);
        if (magic is < ushort.MinValue or > ushort.MaxValue)
            throw new OverflowException($"W2PP Magic value {magic} does not fit the client 7.69 ushort field.");

        return new UpdateScoreConfirmationV769(
            AdaptScore(sourceScore),
            sourcePayload[SourceCriticalOffset],
            sourcePayload[SourceSaveManaOffset],
            affects,
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceGuildOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceGuildLevelOffset..]),
            sourcePayload.Slice(SourceResistOffset, 4).ToArray(),
            BinaryPrimitives.ReadInt32LittleEndian(sourcePayload[SourceCurrentHpOffset..]),
            BinaryPrimitives.ReadInt32LittleEndian(sourcePayload[SourceCurrentMpOffset..]),
            (ushort)magic,
            0,
            0);
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
