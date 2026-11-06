using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Server broadcast for the legacy 7.69 event banner using MSG_ChatColor layout.</summary>
public sealed class MagicTrumpetConfirmation(string message)
{
    // _MSG_MagicTrumpet = 29 | FLAG_GAME2CLIENT | FLAG_DB2GAME | FLAG_GAME2DB.
    public const ushort MessageType = 0x0D1D;
    public const int MessageLength = 92;
    public const int PayloadSize = MessageLength + sizeof(int);
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;
    public const int SpeakColor = unchecked((int)0xFF00CD00u);

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var payload = new byte[PayloadSize];
        var count = Math.Min(Encoding.Latin1.GetByteCount(message), MessageLength - 1);
        Encoding.Latin1.GetBytes(message.AsSpan(), payload.AsSpan(0, count));
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(MessageLength), SpeakColor);
        return codec.Encode(MessageType, 0, clientTick, payload, keywordIndex);
    }
}
