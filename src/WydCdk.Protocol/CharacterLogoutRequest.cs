namespace WydCdk.Protocol;

/// <summary>Parsed legacy <c>MSG_CharacterLogout</c>: an empty MSG_STANDARD request.</summary>
public static class CharacterLogoutRequest
{
    public const ushort MessageType = 0x0215; // 21 | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes;

    public static bool IsValid(DecodedFrame frame) =>
        frame.IsChecksumValid &&
        frame.Header.Type == MessageType &&
        frame.Header.Size == PacketSize &&
        frame.Payload.IsEmpty;
}
