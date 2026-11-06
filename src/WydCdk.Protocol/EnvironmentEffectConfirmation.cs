using System.Buffers.Binary;

namespace WydCdk.Protocol;

/// <summary>7.69 MSG_EnvEffect area rectangle and effect parameters.</summary>
public sealed class EnvironmentEffectConfirmation
{
    public const ushort MessageType = 0x03A2; // 162 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = 24;
    public const ushort ServerSceneId = 30_000; // ESCENE_FIELD

    public EnvironmentEffectConfirmation(int x1, int y1, int x2, int y2, int effect, int effectParameter)
    {
        X1 = checked((short)x1);
        Y1 = checked((short)y1);
        X2 = checked((short)x2);
        Y2 = checked((short)y2);
        Effect = checked((short)effect);
        EffectParameter = checked((short)effectParameter);
    }

    public short X1 { get; }
    public short Y1 { get; }
    public short X2 { get; }
    public short Y2 { get; }
    public short Effect { get; }
    public short EffectParameter { get; }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(), X1);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), Y1);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), X2);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6), Y2);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(8), Effect);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), EffectParameter);
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort id = ServerSceneId)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, id, clientTick, ToPayload(), keywordIndex);
    }
}
