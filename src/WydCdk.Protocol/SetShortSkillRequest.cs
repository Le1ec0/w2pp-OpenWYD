namespace WydCdk.Protocol;

/// <summary>Legacy <c>MSG_SetShortSkill</c>: four MOB skill-bar slots plus sixteen character shortcut slots.</summary>
public sealed record SetShortSkillRequest(byte[] SkillBar, byte[] ShortSkills)
{
    public const ushort MessageType = 0x0378; // 120 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const int PacketSize = PacketHeader.SizeInBytes + 20;

    public static bool TryParse(DecodedFrame frame, out SetShortSkillRequest? request)
    {
        request = null;
        if (!frame.IsChecksumValid || frame.Header.Type != MessageType || frame.Header.Size != PacketSize || frame.Payload.Length != 20)
            return false;

        request = new SetShortSkillRequest(frame.Payload.Span[..4].ToArray(), frame.Payload.Span[4..20].ToArray());
        return true;
    }
}
