namespace WydCdk.Protocol;

/// <summary>
/// Encoder for legacy <c>MSG_CNFNewCharacter</c>: a CPSock header followed by the
/// 840-byte <see cref="LegacyCharacterSelection"/> snapshot returned after creation.
/// </summary>
public sealed class NewCharacterConfirmation(LegacyCharacterSelection selection)
{
    public const ushort MessageType = 0x0110; // 16 | FLAG_GAME2CLIENT
    public const ushort SceneId = 30001; // ESCENE_FIELD + 1
    public const int PayloadSize = LegacyCharacterSelection.SizeInBytes;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public LegacyCharacterSelection Selection { get; } = selection ?? throw new ArgumentNullException(nameof(selection));

    public byte[] ToPayload() => Selection.ToBytes();

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
    }
}
