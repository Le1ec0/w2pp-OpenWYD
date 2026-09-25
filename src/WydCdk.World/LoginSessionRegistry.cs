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
/// </summary>
public sealed class LoginSessionRegistry
{
    private readonly Dictionary<int, LoginSession> sessions = [];

    public bool Open(int connectionId)
    {
        if (connectionId <= 0 || sessions.ContainsKey(connectionId)) return false;
        sessions.Add(connectionId, new LoginSession(connectionId, LoginSessionState.Accepted, null, null, SecureVerified: false));
        return true;
    }

    public bool Close(int connectionId) => sessions.Remove(connectionId);

    public bool TryGet(int connectionId, out LoginSession? session) => sessions.TryGetValue(connectionId, out session);

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

        sessions[connectionId] = session with { State = LoginSessionState.Playing, CharacterSlot = characterSlot, PositionX = positionX, PositionY = positionY };
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
        if (request.TargetX is <= 0 or >= 4096 || request.TargetY is <= 0 or >= 4096) return MovementResult.OutOfBounds;
        if (!LegacyMovementRules.IsTargetWithinViewGrid(session.PositionX, session.PositionY, request.TargetX, request.TargetY)) return MovementResult.StepTooLarge;

        sessions[connectionId] = session with { PositionX = request.TargetX, PositionY = request.TargetY };
        return MovementResult.Accepted;
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
    {
        if (!sessions.TryGetValue(connectionId, out var session)) return AttackTimingResult.UnknownConnection;
        if (session.State != LoginSessionState.Playing) return AttackTimingResult.InvalidState;
        if (!ClientTickPolicy.IsAllowedFromClient(clientTick)) return AttackTimingResult.ReservedTimestamp;

        if (session.LastAttackTick != ClientTickPolicy.SkipCheckTick)
        {
            if (clientTick < unchecked(session.LastAttackTick + 800u)) return AttackTimingResult.TooSoon;

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
public sealed record LoginSession(int ConnectionId, LoginSessionState State, string? AccountName, IReadOnlyList<uint>? AdapterName, bool SecureVerified = false, int CharacterSlot = -1, short PositionX = 0, short PositionY = 0, uint LastAttackTick = ClientTickPolicy.SkipCheckTick);
