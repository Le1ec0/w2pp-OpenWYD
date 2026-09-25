namespace WydCdk.World;

/// <summary>Authoritative position change emitted by the legacy movement recall path.</summary>
public sealed record LegacyMovementRecallOutcome(short FromX, short FromY, short ToX, short ToY);
