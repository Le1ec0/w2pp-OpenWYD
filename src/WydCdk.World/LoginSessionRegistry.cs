using System.Collections.Concurrent;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Login-related connection states corresponding to the legacy TMSrv user modes.</summary>
public enum LoginSessionState
{
    Closed = 0,
    Accepted = 1,          // USER_ACCEPT
    LoginPending = 2,      // USER_LOGIN: waiting for the account-store result
    CharacterSelection = 11, // USER_SELCHAR
    CharacterWait = 12,    // USER_CHARWAIT: asynchronous character DB operation
    Playing = 22,          // USER_PLAY
}

public enum LoginTransitionResult
{
    Accepted,
    UnknownConnection,
    InvalidState,
    AccountMismatch,
}

public enum MovementResult
{
    Accepted,
    UnknownConnection,
    InvalidState,
    OutOfBounds,
    StepTooLarge,
    RepeatedStop,
}

public enum AttackTimingResult
{
    Accepted,
    UnknownConnection,
    InvalidState,
    ReservedTimestamp,
    TooSoon,
    OutsideServerWindow,
}

/// <summary>
/// Owns the login state transitions that the C++ server currently stores in pUser[conn].Mode.
/// Authentication and persistence are intentionally outside this class.
/// Different connections may update concurrently and readers observe immutable snapshots.
/// Transitions for a given connection remain serialized by its transport dispatcher.
/// </summary>
public sealed class LoginSessionRegistry
{
    private readonly ConcurrentDictionary<int, LoginSession> sessions = new();

    public bool Open(int connectionId)
    {
        return connectionId > 0 && sessions.TryAdd(connectionId,
            new LoginSession(connectionId, LoginSessionState.Accepted, null, null, SecureVerified: false));
    }

    public bool Close(int connectionId) => sessions.TryRemove(connectionId, out _);

    public bool TryGet(int connectionId, out LoginSession? session) => sessions.TryGetValue(connectionId, out session);

    public IReadOnlyList<LoginSession> GetPlayingForAccount(string accountName)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var canonical))
            return Array.Empty<LoginSession>();
        return sessions.Values
            .Where(session => session.State == LoginSessionState.Playing &&
                string.Equals(session.AccountName, canonical, StringComparison.Ordinal))
            .OrderBy(static session => session.ConnectionId)
            .ToArray();
    }

    /// <summary>Resets the legacy NumError counter for one active connection.</summary>
    public bool TryResetCrackErrorPoints(int connectionId)
    {
        while (sessions.TryGetValue(connectionId, out var session))
        {
            if (session.CrackErrorPoints == 0)
                return true;
            if (sessions.TryUpdate(connectionId, session with { CrackErrorPoints = 0 }, session))
                return true;
        }
        return false;
    }

    public LoginTransitionResult BeginAccountLogin(int connectionId, AccountLoginRequest request)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return LoginTransitionResult.UnknownConnection;
        if (session.State != LoginSessionState.Accepted) return LoginTransitionResult.InvalidState;

        sessions[connectionId] = session with
        {
            State = LoginSessionState.LoginPending,
            AccountName = request.AccountName,
            AdapterName = request.AdapterName.ToArray(),
        };
        return LoginTransitionResult.Accepted;
    }

    public LoginTransitionResult CompleteAccountLogin(int connectionId, string accountName)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return LoginTransitionResult.UnknownConnection;
        if (session.State != LoginSessionState.LoginPending) return LoginTransitionResult.InvalidState;
        if (!string.Equals(session.AccountName, accountName, StringComparison.Ordinal)) return LoginTransitionResult.AccountMismatch;

        sessions[connectionId] = session with { State = LoginSessionState.CharacterSelection };
        return LoginTransitionResult.Accepted;
    }

    public LoginTransitionResult CompleteCharacterLogin(int connectionId, int characterSlot = -1, short positionX = 0, short positionY = 0)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return LoginTransitionResult.UnknownConnection;
        if (session.State != LoginSessionState.CharacterWait) return LoginTransitionResult.InvalidState;

        sessions[connectionId] = session with
        {
            State = LoginSessionState.Playing,
            CharacterSlot = characterSlot,
            PositionX = positionX,
            PositionY = positionY,
            CrackErrorPoints = 0,
            LastMoveType = 0,
        };
        return LoginTransitionResult.Accepted;
    }

    /// <summary>Starts the legacy asynchronous DB wait from USER_SELCHAR.</summary>
    public LoginTransitionResult BeginCharacterWait(int connectionId)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return LoginTransitionResult.UnknownConnection;
        if (session.State != LoginSessionState.CharacterSelection) return LoginTransitionResult.InvalidState;

        sessions[connectionId] = session with { State = LoginSessionState.CharacterWait };
        return LoginTransitionResult.Accepted;
    }

    /// <summary>Completes a create/delete DB refresh and returns to the legacy USER_SELCHAR state.</summary>
    public LoginTransitionResult CompleteCharacterRefresh(int connectionId)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return LoginTransitionResult.UnknownConnection;
        if (session.State != LoginSessionState.CharacterWait) return LoginTransitionResult.InvalidState;

        sessions[connectionId] = session with { State = LoginSessionState.CharacterSelection };
        return LoginTransitionResult.Accepted;
    }

    public MovementResult TryApplyMovement(int connectionId, ActionRequest request)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return MovementResult.UnknownConnection;
        if (session.State != LoginSessionState.Playing) return MovementResult.InvalidState;
        if (request.WireType == ActionRequest.StopMessageType && session.LastMoveType == ActionRequest.StopMessageType)
            return MovementResult.RepeatedStop;
        if (request.TargetX is <= 0 or >= 4096 || request.TargetY is <= 0 or >= 4096) return MovementResult.OutOfBounds;
        if (!LegacyMovementRules.IsTargetWithinViewGrid(session.PositionX, session.PositionY, request.TargetX, request.TargetY)) return MovementResult.StepTooLarge;

        var targetChanged = request.TargetX != session.PositionX || request.TargetY != session.PositionY;
        sessions[connectionId] = session with
        {
            PositionX = request.TargetX,
            PositionY = request.TargetY,
            LastMoveType = targetChanged ? request.WireType : session.LastMoveType,
        };
        return MovementResult.Accepted;
    }

    /// <summary>Records the legacy LastMove side effect for Action paths that return before movement acceptance.</summary>
    public bool TryRecordMovementType(int connectionId, ushort wireType)
    {
        if (!sessions.TryGetValue(connectionId, out var session) || session.State != LoginSessionState.Playing)
            return false;

        sessions[connectionId] = session with { LastMoveType = wireType };
        return true;
    }

    public bool SetServerPosition(int connectionId, short positionX, short positionY)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return false;
        sessions[connectionId] = session with { PositionX = positionX, PositionY = positionY };
        return true;
    }

    /// <summary>
    /// Mirrors the cheap ClientTick gates at the start of <c>Exec_MSG_Attack</c> before any spell or damage work.
    /// The caller supplies the server's monotonic millisecond clock, equivalent to legacy <c>CurrentTime</c>.
    /// </summary>
    public AttackTimingResult TryAcceptAttackTiming(int connectionId, uint clientTick, uint serverTick)
        => TryAcceptAttackTiming(connectionId, clientTick, serverTick, out _);

    /// <summary>
    /// Also reports the legacy nonterminal 4/type7 backstep side effect. That check follows the 800ms gate,
    /// uses unsigned 32-bit arithmetic, updates LastAttackTick, and continues into the server-window gate.
    /// </summary>
    public AttackTimingResult TryAcceptAttackTiming(int connectionId, uint clientTick, uint serverTick, out bool hasBackwardTickError)
        => TryAcceptAttackTiming(connectionId, clientTick, serverTick, out hasBackwardTickError, out _);

    /// <summary>
    /// Also reports the handler-local <c>LastAttackTick</c> copy used later by the skill delay gate:
    /// zero when the session had no previous attack (legacy SKIPCHECKTICK), otherwise the previous tick.
    /// </summary>
    public AttackTimingResult TryAcceptAttackTiming(int connectionId, uint clientTick, uint serverTick, out bool hasBackwardTickError, out uint previousAttackTick)
    {
        hasBackwardTickError = false;
        previousAttackTick = 0;
        if (!sessions.TryGetValue(connectionId, out var session)) return AttackTimingResult.UnknownConnection;
        if (session.State != LoginSessionState.Playing) return AttackTimingResult.InvalidState;
        if (!ClientTickPolicy.IsAllowedFromClient(clientTick)) return AttackTimingResult.ReservedTimestamp;

        if (session.LastAttackTick != ClientTickPolicy.SkipCheckTick)
        {
            if (clientTick < unchecked(session.LastAttackTick + 800u)) return AttackTimingResult.TooSoon;

            hasBackwardTickError = clientTick < unchecked(session.LastAttackTick - 100u);
            previousAttackTick = session.LastAttackTick;

            // The legacy handler updates LastAttackTick before its clock-window rejection when there was a prior attack.
            session = session with { LastAttackTick = clientTick };
            sessions[connectionId] = session;
        }

        var oldestAllowedTick = serverTick <= 120_000u ? 0u : serverTick - 120_000u;
        if (clientTick > unchecked(serverTick + 15_000u) || clientTick < oldestAllowedTick)
            return AttackTimingResult.OutsideServerWindow;

        sessions[connectionId] = session with { LastAttackTick = clientTick };
        return AttackTimingResult.Accepted;
    }

    public LoginTransitionResult CompleteCharacterLogout(int connectionId)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return LoginTransitionResult.UnknownConnection;
        if (session.State != LoginSessionState.Playing) return LoginTransitionResult.InvalidState;

        sessions[connectionId] = session with { State = LoginSessionState.CharacterSelection };
        return LoginTransitionResult.Accepted;
    }

    public bool TryAddCrackError(int connectionId, int points, int type, out LegacyCrackErrorOutcome? outcome)
    {
        outcome = null;
        while (sessions.TryGetValue(connectionId, out var session))
        {
            var candidate = LegacyCrackErrorRules.Add(session.CrackErrorPoints, points, type);
            if (!sessions.TryUpdate(connectionId, session with { CrackErrorPoints = candidate.TotalPoints }, session))
                continue;
            outcome = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Mirrors <c>pAccountList[Idx].SecurePass</c> (-1 = unverified/failed, 1 = verified) as a bool, set by
    /// <see cref="AccountSecureCoordinator"/>. Unlike the login transitions above, this is not gated by
    /// <see cref="LoginSessionState"/>: <c>Exec_MSG_AccountSecure</c> (TMSrv) accepts the message in any state.
    /// </summary>
    public LoginTransitionResult SetSecureVerified(int connectionId, bool verified)
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return LoginTransitionResult.UnknownConnection;
        sessions[connectionId] = session with { SecureVerified = verified };
        return LoginTransitionResult.Accepted;
    }
}

/// <summary>Immutable snapshot; the password and reconnect token never enter connection state.</summary>
public sealed record LoginSession(int ConnectionId, LoginSessionState State, string? AccountName, IReadOnlyList<uint>? AdapterName, bool SecureVerified = false, int CharacterSlot = -1, short PositionX = 0, short PositionY = 0, uint LastAttackTick = ClientTickPolicy.SkipCheckTick, int CrackErrorPoints = 0)
{
    public ushort LastMoveType { get; init; }
}
