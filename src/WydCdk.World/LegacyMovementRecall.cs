using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Authoritative position change emitted by the legacy movement recall path.</summary>
public sealed record LegacyMovementRecallOutcome(short FromX, short FromY, short ToX, short ToY);

/// <summary>Recall plus the spatial player views captured atomically around its authoritative move.</summary>
public sealed record LegacyRecallTransition(
    int ConnectionId,
    LegacyMovementRecallOutcome Recall,
    IReadOnlyList<int> OriginViewIds,
    IReadOnlyList<int> DestinationViewIds,
    IReadOnlyList<LegacyRecallItem> OriginItems,
    IReadOnlyList<LegacyRecallItem> DestinationItems);

/// <summary>Immutable item presentation captured from one side of a recall's field view.</summary>
public sealed record LegacyRecallItem(
    ushort WireItemId,
    ushort GridX,
    ushort GridY,
    LegacyItem Item,
    int Rotate,
    byte State,
    byte Height);
