using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

internal static class LegacyRecycleBinRequestHandler
{
    public static LegacyRecycleBinRequestResult TryCreateResponse(
        WorldHub world,
        int connectionId,
        ushort requestHeaderId,
        LoginSessionState sessionState,
        RepurchaseItemsMessage request,
        out RepurchaseItemsMessage? response)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);
        response = null;

        if (requestHeaderId != connectionId)
            return LegacyRecycleBinRequestResult.HeaderIdMismatch;
        if (sessionState != LoginSessionState.Playing)
            return LegacyRecycleBinRequestResult.SessionNotPlaying;
        if (!world.TryGetRecycleBinSnapshot(connectionId, out var entries) || entries is null)
            return LegacyRecycleBinRequestResult.WorldSessionUnavailable;

        response = LegacyRecycleBinClientProjection.ToRepurchaseItemsMessage(request.Target, entries);
        return LegacyRecycleBinRequestResult.Ready;
    }
}

internal enum LegacyRecycleBinRequestResult
{
    Ready,
    HeaderIdMismatch,
    SessionNotPlaying,
    WorldSessionUnavailable,
}
