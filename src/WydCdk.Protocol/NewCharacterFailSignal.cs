namespace WydCdk.Protocol;

/// <summary>
/// Encoder for legacy <c>_MSG_NewCharacterFail</c>: the empty-payload MSG_STANDARD signal TMSrv sends the
/// client whenever character creation is rejected, whatever the reason (SendFunc.cpp's SendClientSignal,
/// called from both Exec_MSG_CreateCharacter and ProcessDBMessage.cpp's _MSG_DBNewCharacterFail handler).
/// </summary>
public static class NewCharacterFailSignal
{
    public const ushort MessageType = 26 | 0x0100; // 26 | FLAG_GAME2CLIENT

    public static byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, 0, clientTick, ReadOnlySpan<byte>.Empty, keywordIndex);
    }
}
