namespace WydCdk.Protocol;

/// <summary>Encoder for the legacy <c>MSG_CNFDeleteCharacter</c> selection response.</summary>
public sealed class DeleteCharacterConfirmation(LegacyCharacterSelection selection)
{
    public const ushort MessageType = 0x0112; // 18 | FLAG_GAME2CLIENT
    public const ushort SceneId = 30001; // ESCENE_FIELD + 1
    public const int PayloadSize = LegacyCharacterSelection.SizeInBytes;
    public const int PacketSize = PacketHeader.SizeInBytes + PayloadSize;

    public LegacyCharacterSelection Selection { get; } = selection ?? throw new ArgumentNullException(nameof(selection));

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex) =>
        codec.Encode(MessageType, SceneId, clientTick, Selection.ToBytes(), keywordIndex);
}
