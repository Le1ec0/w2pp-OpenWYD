using System.Diagnostics;

namespace WydCdk.Protocol;

/// <summary>Byte-for-byte CPSock frame cipher and checksum compatible with the legacy server.</summary>
public sealed class LegacyFrameCodec
{
    public const uint InitCode = 0x1F11F311;
    private readonly byte[] keywordTable;

    public LegacyFrameCodec(ReadOnlySpan<byte> keywordTable)
    {
        if (keywordTable.Length != 512) throw new ArgumentException("The legacy keyword table must contain 512 bytes.", nameof(keywordTable));
        this.keywordTable = keywordTable.ToArray();
    }

    public static LegacyFrameCodec CreateDefault() => new(LegacyKeywordTable.Bytes.Span);

    public byte[] Encode(ushort type, ushort id, uint clientTick, ReadOnlySpan<byte> payload, byte keywordIndex)
    {
        var size = checked(PacketHeader.SizeInBytes + payload.Length);
        if (size > PacketHeader.MaxPacketSize) throw new ArgumentOutOfRangeException(nameof(payload), "Packet exceeds the legacy 8192-byte limit.");
        var frame = new byte[size];
        payload.CopyTo(frame.AsSpan(PacketHeader.SizeInBytes));
        new PacketHeader((ushort)size, keywordIndex, 0, type, id, clientTick).Write(frame);
        frame[3] = Transform(frame, keywordIndex, encode: true);
        return frame;
    }

    public DecodedFrame Decode(ReadOnlySpan<byte> frame)
    {
        var wireHeader = PacketHeader.Read(frame);
        if (wireHeader.Size < PacketHeader.SizeInBytes || wireHeader.Size > PacketHeader.MaxPacketSize || wireHeader.Size != frame.Length) throw new InvalidDataException($"Invalid legacy frame size {wireHeader.Size}.");
        var decoded = frame.ToArray();
        var checksum = Transform(decoded, wireHeader.KeywordIndex, encode: false);
        return new DecodedFrame(PacketHeader.Read(decoded), decoded.AsMemory(PacketHeader.SizeInBytes), checksum == wireHeader.Checksum);
    }

    private byte Transform(Span<byte> frame, byte keywordIndex, bool encode)
    {
        byte plainSum = 0;
        byte wireSum = 0;
        var position = keywordTable[keywordIndex * 2];
        for (var offset = 4; offset < frame.Length; offset++, position++)
        {
            var transform = keywordTable[(position % 256) * 2 + 1];
            var value = frame[offset];
            if (encode)
            {
                plainSum += value;
                frame[offset] = value = Apply(value, transform, offset, invert: false);
                wireSum += value;
            }
            else
            {
                wireSum += value;
                frame[offset] = value = Apply(value, transform, offset, invert: true);
                plainSum += value;
            }
        }
        return unchecked((byte)(wireSum - plainSum));
    }

    private static byte Apply(byte value, byte transform, int offset, bool invert) => (offset & 3, invert) switch
    {
        (0, false) => unchecked((byte)(value + (transform << 1))), (1, false) => unchecked((byte)(value - (transform >> 3))),
        (2, false) => unchecked((byte)(value + (transform << 2))), (3, false) => unchecked((byte)(value - (transform >> 5))),
        (0, true) => unchecked((byte)(value - (transform << 1))), (1, true) => unchecked((byte)(value + (transform >> 3))),
        (2, true) => unchecked((byte)(value - (transform << 2))), (3, true) => unchecked((byte)(value + (transform >> 5))),
        _ => throw new UnreachableException(),
    };
}

public readonly record struct DecodedFrame(PacketHeader Header, ReadOnlyMemory<byte> Payload, bool IsChecksumValid);
