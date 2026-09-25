using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Projects packed W2PP MSG_UpdateEtc into the client 7.69 layout. W2PP's
/// Magic field is intentionally discarded because the target UpdateEtc wire
/// has no Magic member; Magic is carried by the target UpdateScore contract.
/// </summary>
public static class W2ppUpdateEtcV1Adapter
{
    public const int SourcePayloadSize = 36;
    public const int SourcePacketSize = PacketHeader.SizeInBytes + SourcePayloadSize;
    public const int SourceHoldOffset = 0;
    public const int SourceExperienceOffset = 4;
    public const int SourceLearnOffset = 12;
    public const int SourceScoreBonusOffset = 20;
    public const int SourceSpecialBonusOffset = 22;
    public const int SourceSkillBonusOffset = 24;
    public const int SourceMagicOffset = 26;
    public const int SourceCoinOffset = 28;

    public static UpdateEtcConfirmationV769 AdaptPayload(ReadOnlySpan<byte> sourcePayload)
    {
        if (sourcePayload.Length != SourcePayloadSize)
            throw new ArgumentException($"W2PP UpdateEtc requires exactly {SourcePayloadSize} payload bytes.", nameof(sourcePayload));

        var hold = BinaryPrimitives.ReadUInt32LittleEndian(sourcePayload[SourceHoldOffset..]);
        if (hold > int.MaxValue)
            throw new OverflowException($"W2PP Hold value {hold} does not fit the client 7.69 FakeExp field.");

        var learn = unchecked((ulong)BinaryPrimitives.ReadInt64LittleEndian(sourcePayload[SourceLearnOffset..]));
        var learnedSkill = new[]
        {
            unchecked((uint)learn),
            unchecked((uint)(learn >> 32))
        };

        return new UpdateEtcConfirmationV769(
            (int)hold,
            BinaryPrimitives.ReadInt64LittleEndian(sourcePayload[SourceExperienceOffset..]),
            learnedSkill,
            unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceScoreBonusOffset..])),
            unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceSpecialBonusOffset..])),
            unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload[SourceSkillBonusOffset..])),
            BinaryPrimitives.ReadInt32LittleEndian(sourcePayload[SourceCoinOffset..]));
    }
}
