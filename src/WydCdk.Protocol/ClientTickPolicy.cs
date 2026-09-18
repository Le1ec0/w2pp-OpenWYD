namespace WydCdk.Protocol;

/// <summary>Legacy ownership rules for the timestamp in <see cref="PacketHeader.ClientTick"/>.</summary>
public static class ClientTickPolicy
{
    /// <summary>
    /// Internal-server marker from <c>Basedef.h:SKIPCHECKTICK</c>. The legacy TMSrv rejects it when received from a client.
    /// </summary>
    public const uint SkipCheckTick = 235_543_242;

    public static bool IsAllowedFromClient(uint clientTick) => clientTick != SkipCheckTick;
}
