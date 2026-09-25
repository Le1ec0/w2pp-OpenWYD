using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>
/// Projects the W2PP <c>MSG_SendAffect</c> payload into the client 7.69
/// <c>MSG_UpdateAffect</c> payload. W2PP stores Type, byte Value, ushort Level,
/// uint Time; the target stores Type, signed byte Level, short Value, int Time.
/// Narrowing is rejected instead of silently corrupting active effects.
/// </summary>
public static class W2ppUpdateAffectV1Adapter
{
    public const int SourcePayloadSize = UpdateAffectConfirmationV769.PayloadSize;
    public const int SourceAffectOffset = 0;
    public const int SourceAffectSize = 8;
    public const int SourceAffectCount = UpdateAffectConfirmationV769.AffectCount;

    public static UpdateAffectConfirmationV769 AdaptPayload(ReadOnlySpan<byte> sourcePayload)
    {
        if (sourcePayload.Length != SourcePayloadSize)
            throw new ArgumentException($"W2PP UpdateAffect payload requires exactly {SourcePayloadSize} bytes.", nameof(sourcePayload));

        var affects = new ClientAffectV769[SourceAffectCount];
        for (var index = 0; index < SourceAffectCount; index++)
        {
            var offset = SourceAffectOffset + index * SourceAffectSize;
            var level = BinaryPrimitives.ReadUInt16LittleEndian(sourcePayload.Slice(offset + 2, sizeof(ushort)));
            var time = BinaryPrimitives.ReadUInt32LittleEndian(sourcePayload.Slice(offset + 4, sizeof(uint)));
            if (level > sbyte.MaxValue)
                throw new OverflowException($"W2PP affect level {level} does not fit the signed 7.69 affect level field.");
            if (time > int.MaxValue)
                throw new OverflowException($"W2PP affect time {time} does not fit the signed 7.69 affect time field.");

            affects[index] = new ClientAffectV769(
                sourcePayload[offset],
                checked((sbyte)level),
                sourcePayload[offset + 1],
                checked((int)time));
        }

        return new UpdateAffectConfirmationV769(affects);
    }
}
