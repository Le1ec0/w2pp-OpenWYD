using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>Wire header used by the legacy WYD CPSock protocol.</summary>
public readonly record struct PacketHeader(ushort Size, byte KeywordIndex, byte Checksum, ushort Type, ushort Id, uint ClientTick)
{
    public const int SizeInBytes = 12;
    public const int MaxPacketSize = 8192;

    public static PacketHeader Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < SizeInBytes) throw new ArgumentException("A packet header requires 12 bytes.", nameof(source));
        return new PacketHeader(BinaryPrimitives.ReadUInt16LittleEndian(source), source[2], source[3], BinaryPrimitives.ReadUInt16LittleEndian(source[4..]), BinaryPrimitives.ReadUInt16LittleEndian(source[6..]), BinaryPrimitives.ReadUInt32LittleEndian(source[8..]));
    }

    public void Write(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes) throw new ArgumentException("A packet header requires 12 bytes.", nameof(destination));
        BinaryPrimitives.WriteUInt16LittleEndian(destination, Size);
        destination[2] = KeywordIndex;
        destination[3] = Checksum;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], Type);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], Id);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], ClientTick);
    }
}
