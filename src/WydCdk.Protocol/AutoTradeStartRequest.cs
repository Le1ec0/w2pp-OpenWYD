namespace WydCdk.Protocol;

/// <summary>
/// Client-to-server view of the 7.69 <c>MSG_SendAutoTrade</c> packet. Its
/// bytes are shared with <see cref="AutoTradeListConfirmation"/>; the final
/// short is the request's legacy <c>Index</c>, not a target selected by the
/// requester.
/// </summary>
public sealed record AutoTradeStartRequest(
    string Title,
    IReadOnlyList<LegacyItem> Items,
    IReadOnlyList<sbyte> CarryPositions,
    IReadOnlyList<int> Prices,
    short RequestedTax = 0,
    short Index = 0)
{
    public const ushort MessageType = AutoTradeListConfirmation.MessageType;
    public const int PacketSize = AutoTradeListConfirmation.PacketSize;
    public const int PayloadSize = AutoTradeListConfirmation.PayloadSize;

    public byte[] ToPayload() => new AutoTradeListConfirmation(
        Title,
        Items,
        CarryPositions,
        Prices,
        RequestedTax,
        unchecked((ushort)Index)).ToPayload();

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex, ushort headerId = 0) =>
        new AutoTradeListConfirmation(
            Title,
            Items,
            CarryPositions,
            Prices,
            RequestedTax,
            unchecked((ushort)Index)).ToFrame(codec, clientTick, keywordIndex, headerId);

    public static bool TryParse(DecodedFrame frame, out AutoTradeStartRequest? request)
    {
        request = null;
        if (!AutoTradeListConfirmation.TryParse(frame, out var listing) || listing is null)
            return false;

        request = new AutoTradeStartRequest(
            listing.Description,
            listing.Items,
            listing.CarryPositions,
            listing.TradeMoney,
            listing.Tax,
            unchecked((short)listing.TargetId));
        return true;
    }
}
