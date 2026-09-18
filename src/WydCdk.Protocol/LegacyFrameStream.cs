using System.Runtime.InteropServices;

namespace WydCdk.Protocol;

/// <summary>Buffers TCP bytes until the legacy handshake and complete frames are available.</summary>
public sealed class LegacyFrameStream
{
    private readonly List<byte> pending = [];
    private readonly LegacyFrameCodec codec;
    private bool handshakeReceived;

    public LegacyFrameStream(LegacyFrameCodec codec) => this.codec = codec;
    public bool HandshakeReceived => handshakeReceived;

    public void Append(ReadOnlySpan<byte> bytes) => pending.AddRange(bytes.ToArray());

    public bool TryRead(out DecodedFrame frame)
    {
        frame = default;
        if (!handshakeReceived)
        {
            if (pending.Count < sizeof(uint)) return false;
            var initCode = BitConverter.ToUInt32(pending.Take(sizeof(uint)).ToArray());
            if (initCode != LegacyFrameCodec.InitCode) throw new InvalidDataException($"Unexpected handshake 0x{initCode:X8}.");
            pending.RemoveRange(0, sizeof(uint));
            handshakeReceived = true;
        }

        if (pending.Count < PacketHeader.SizeInBytes) return false;
        var header = PacketHeader.Read(CollectionsMarshal.AsSpan(pending));
        if (header.Size < PacketHeader.SizeInBytes || header.Size > PacketHeader.MaxPacketSize) throw new InvalidDataException($"Invalid legacy frame size {header.Size}.");
        if (pending.Count < header.Size) return false;
        var wireFrame = pending.Take(header.Size).ToArray();
        pending.RemoveRange(0, header.Size);
        frame = codec.Decode(wireFrame);
        return true;
    }
}
