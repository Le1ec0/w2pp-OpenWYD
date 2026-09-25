using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Client 7.69 affect entry. The target ABI orders Type, Level, Value, Time.</summary>
public readonly record struct ClientAffectV769(byte Type, sbyte Level, short Value, int Time)
{
    public const int SizeInBytes = 8;

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes)
            throw new ArgumentException("Client 7.69 affect requires eight bytes.", nameof(destination));

        destination[0] = Type;
        destination[1] = unchecked((byte)Level);
        BinaryPrimitives.WriteInt16LittleEndian(destination[2..], Value);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], Time);
    }
}

/// <summary>
/// Client 7.69 <c>MSG_UpdateAffect</c> wire contract. It is structurally
/// separate from W2PP because each eight-byte affect swaps Level and Value.
/// </summary>
public sealed class UpdateAffectConfirmationV769
{
    public const ushort MessageType = 0x03B9;
    public const int AffectCount = 32;
    public const int PayloadSize = AffectCount * ClientAffectV769.SizeInBytes;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    private readonly ClientAffectV769[] affects;

    public UpdateAffectConfirmationV769(IReadOnlyList<ClientAffectV769> affects)
    {
        if (affects.Count != AffectCount)
            throw new ArgumentException($"Client 7.69 UpdateAffect requires {AffectCount} entries.", nameof(affects));

        this.affects = affects.ToArray();
        Affects = Array.AsReadOnly(this.affects);
    }

    public IReadOnlyList<ClientAffectV769> Affects { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PayloadSize];
        for (var index = 0; index < affects.Length; index++)
            affects[index].Write(payload.AsSpan(index * ClientAffectV769.SizeInBytes, ClientAffectV769.SizeInBytes));
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, ushort mobId, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, mobId, clientTick, ToPayload(), keywordIndex);
    }
}
