namespace WydCdk.World;

public sealed partial class WorldHub
{
    private readonly Dictionary<int, TradeCloseState> tradeCloseReservations = [];

    internal object? CaptureTradeCloseIdentity(int connectionId)
    {
        lock (gate) return participants.GetValueOrDefault(connectionId);
    }

    internal bool IsTradeCloseIdentityCurrent(int connectionId, object? expected)
    {
        lock (gate) return expected is not null && participants.TryGetValue(connectionId, out var current) && ReferenceEquals(expected, current);
    }

    public int GetTradeOpponentId(int connectionId)
    {
        lock (gate) return tradeStates.TryGetValue(connectionId, out var state) ? state.OpponentId : 0;
    }

    public LegacyTradeClosePrepareResult TryReserveTradeClose(int connectionId, int expectedOpponentId,
        out LegacyTradeCloseReservation? reservation)
        => TryReserveTradeCloseCore(connectionId, expectedOpponentId, actionTriggered: false, out reservation);

    public LegacyTradeClosePrepareResult TryReserveTradeCloseForAction(int connectionId, int expectedOpponentId,
        out LegacyTradeCloseReservation? reservation)
        => TryReserveTradeCloseCore(connectionId, expectedOpponentId, actionTriggered: true, out reservation);

    private LegacyTradeClosePrepareResult TryReserveTradeCloseCore(int connectionId, int expectedOpponentId,
        bool actionTriggered, out LegacyTradeCloseReservation? reservation)
    {
        lock (gate)
        {
            reservation = null;
            if (!participants.TryGetValue(connectionId, out var participant)) return LegacyTradeClosePrepareResult.ParticipantNotFound;
            if (tradeCloseReservations.ContainsKey(connectionId) || expectedOpponentId > 0 && tradeCloseReservations.ContainsKey(expectedOpponentId))
                return LegacyTradeClosePrepareResult.Busy;
            if (GetTradeOpponentId(connectionId) != expectedOpponentId) return LegacyTradeClosePrepareResult.PairChanged;
            if (!HasLiveCombatState(participant)) return LegacyTradeClosePrepareResult.CombatStateUnavailable;
            var hp = ReadCurrentHp(participant);
            if (actionTriggered ? hp == 0 : hp <= 0) return LegacyTradeClosePrepareResult.NotAlive;
            Participant? opponent = null;
            if (expectedOpponentId > 0 && !participants.TryGetValue(expectedOpponentId, out opponent))
                return LegacyTradeClosePrepareResult.PairChanged;
            reservation = new(Guid.NewGuid(), connectionId, expectedOpponentId, actionTriggered);
            var state = new TradeCloseState(reservation, participant, opponent);
            tradeCloseReservations.Add(connectionId, state);
            if (expectedOpponentId > 0) tradeCloseReservations.Add(expectedOpponentId, state);
            return LegacyTradeClosePrepareResult.Prepared;
        }
    }

    public bool TryAbortTradeClose(LegacyTradeCloseReservation reservation)
    {
        lock (gate)
        {
            if (!tradeCloseReservations.TryGetValue(reservation.ConnectionId, out var state) || state.Reservation != reservation) return false;
            tradeCloseReservations.Remove(reservation.ConnectionId);
            if (reservation.OpponentId > 0) tradeCloseReservations.Remove(reservation.OpponentId);
            return true;
        }
    }

    public bool TryFinishTradeClose(LegacyTradeCloseReservation reservation, out LegacyTradeCloseOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!tradeCloseReservations.TryGetValue(reservation.ConnectionId, out var state) || state.Reservation != reservation) return false;
            TryAbortTradeClose(reservation);
            // References, not ids alone: an old completion cannot close a replacement session.
            if (!participants.TryGetValue(reservation.ConnectionId, out var participant) || !ReferenceEquals(participant, state.Participant) ||
                GetTradeOpponentId(reservation.ConnectionId) != reservation.OpponentId ||
                reservation.OpponentId > 0 && (!participants.TryGetValue(reservation.OpponentId, out var opponent) || !ReferenceEquals(opponent, state.Opponent)))
                return false;
            ClearTradePairLocked(reservation.ConnectionId);
            outcome = reservation.ActionTriggered
                ? new LegacyTradeCloseOutcome(participant.ConnectionId, reservation.OpponentId)
                : BuildTradeCloseOutcomeLocked(participant, reservation.OpponentId);
            return true; // HP was checked at reservation, never after durable removal.
        }
    }

    private LegacyTradeCloseOutcome BuildTradeCloseOutcomeLocked(Participant participant, int opponentId)
    {
        var guilty = combatWorldState.NewbieEventServer ? 0 : ReadLegacyGuilty(participant.Mob);
        if (!combatWorldState.NewbieEventServer) participant.Guilty = guilty != 0;
        return new(participant.ConnectionId, opponentId)
        {
            PkState = LegacyTradePkRules.Resolve(guilty, participant.PkMode, participant.PositionX, participant.PositionY, combatWorldState),
            PkRecipients = GetParticipantIdsInPlayerView(participant.ConnectionId),
        };
    }

    private sealed record TradeCloseState(LegacyTradeCloseReservation Reservation, Participant Participant, Participant? Opponent);
}
