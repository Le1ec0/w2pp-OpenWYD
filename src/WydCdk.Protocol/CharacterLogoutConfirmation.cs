namespace WydCdk.Protocol;

/// <summary>
/// Encoder for legacy <c>_MSG_CNFCharacterLogout</c>. The reference calls
/// <c>SendClientSignal(conn, conn, _MSG_CNFCharacterLogout)</c>, so the connection id is the frame id.
/// </summary>
public static class CharacterLogoutConfirmation
{
    public const ushort MessageType = 0x0116; // 22 | FLAG_GAME2CLIENT
    public const int PacketSize = PacketHeader.SizeInBytes;

    public static byte[] ToFrame(LegacyFrameCodec codec, int connectionId, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (connectionId < 0 || connectionId > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(connectionId));
        return codec.Encode(MessageType, (ushort)connectionId, clientTick, ReadOnlySpan<byte>.Empty, keywordIndex);
    }
}
