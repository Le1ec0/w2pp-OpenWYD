using System.Buffers.Binary;
using WydCdk.Protocol;

namespace WydCdk.World;

public sealed partial class WorldHub
{
    private const int LegacyTradeCarryCount = LegacyAccountSnapshot.MobCarryCount - 4;
    private const int LegacyTradeMoneyLimit = 2_000_000_000;
    private readonly Dictionary<int, LegacyTradeState> tradeStates = [];

    public bool IsLiveTradeActive(int connectionId)
    {
        lock (gate)
            return tradeStates.ContainsKey(connectionId);
    }

    /// <summary>
    /// Validates and stages one side of the bilateral legacy MSG_Trade state.
    /// This is intentionally before the item-exchange/persistence step: the
    /// source server validates the live carry and keeps the offer until both
    /// sides check it.
    /// </summary>
    public LegacyTradeOfferResult TryStageTradeOffer(int connectionId, TradeOfferRequest request, out LegacyTradeOfferOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyTradeOfferResult.ParticipantNotFound;

            var opponentId = request.OpponentId;
            if (opponentId <= 0)
                return LegacyTradeOfferResult.InvalidOpponent;
            if (opponentId == connectionId)
                return LegacyTradeOfferResult.SameParticipant;
            if (!participants.TryGetValue(opponentId, out var opponent))
                return LegacyTradeOfferResult.TargetNotFound;

            if (!HasLiveCombatState(participant))
                return LegacyTradeOfferResult.CombatStateUnavailable;
            if (ReadCurrentHp(participant) <= 0)
                return LegacyTradeOfferResult.NotAlive;
            if (!HasLiveCombatState(opponent))
                return LegacyTradeOfferResult.TargetCombatStateUnavailable;
            if (participant.PkMode || opponent.PkMode)
                return LegacyTradeOfferResult.PkModeBlocked;

            if (!ValidateTradeOfferLocked(participant, opponent, request, out var validationResult))
            {
                if (tradeStates.TryGetValue(connectionId, out var activeState) && activeState.OpponentId == opponentId)
                    ClearTradePairLocked(connectionId);
                return validationResult;
            }

            tradeStates.TryGetValue(connectionId, out var ownState);
            tradeStates.TryGetValue(opponentId, out var opponentState);

            if (ownState is not null && ownState.OpponentId != opponentId)
                return LegacyTradeOfferResult.AlreadyTrading;
            if (opponentState is not null && opponentState.OpponentId != connectionId)
                return LegacyTradeOfferResult.TargetBusy;

            if (opponentState?.HasOffer == true &&
                !ValidateStoredOfferLocked(opponent, participant, opponentState, out validationResult))
            {
                ClearTradePairLocked(connectionId);
                return validationResult;
            }

            if (ownState?.HasOffer == true && ownState.MyCheck == 1 && request.MyCheck == 1 &&
                !SameTradeOffer(ownState.ToRequest(), request))
            {
                ClearTradePairLocked(connectionId);
                return LegacyTradeOfferResult.OfferChangedAfterCheck;
            }

            var paired = opponentState is not null && opponentState.OpponentId == connectionId;
            ownState ??= new LegacyTradeState(connectionId, opponentId);
            ownState.OpponentId = opponentId;
            ownState.SetOffer(request);

            // The reference handler ignores the first unchecked side's money
            // and check bit until the opponent has entered the trade window.
            if (!paired || opponentState?.HasOffer != true)
            {
                ownState.TradeMoney = 0;
                ownState.MyCheck = 0;
            }

            tradeStates[connectionId] = ownState;
            opponentState ??= new LegacyTradeState(opponentId, connectionId);
            opponentState.OpponentId = connectionId;
            if (ownState.MyCheck == 0)
                opponentState.MyCheck = 0;
            tradeStates[opponentId] = opponentState;

            var bothChecked = paired && ownState.HasOffer && opponentState.HasOffer &&
                              ownState.MyCheck == 1 && opponentState.MyCheck == 1;
            outcome = new LegacyTradeOfferOutcome(
                connectionId,
                opponentId,
                paired,
                ownState.ToSnapshot(),
                opponentState.ToSnapshot());
            return bothChecked ? LegacyTradeOfferResult.BothChecked : LegacyTradeOfferResult.Accepted;
        }
    }

    /// <summary>Clears both sides of an in-memory trade without touching inventory or persistence.</summary>
    public LegacyTradeCloseResult TryCloseTrade(int connectionId, out LegacyTradeCloseOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.ContainsKey(connectionId))
                return LegacyTradeCloseResult.ParticipantNotFound;
            if (!tradeStates.TryGetValue(connectionId, out var state))
                return LegacyTradeCloseResult.NoActiveTrade;

            var opponentId = state.OpponentId;
            ClearTradePairLocked(connectionId);
            outcome = new LegacyTradeCloseOutcome(connectionId, opponentId);
            return LegacyTradeCloseResult.Accepted;
        }
    }

    /// <summary>
    /// Atomically applies a checked bilateral trade to both authoritative MOBs.
    /// The returned snapshots are the persistence/send plan for the caller; no
    /// database write or network frame is emitted by this domain method.
    /// </summary>
    public LegacyTradeCompletionResult TryCompleteTrade(int connectionId, out LegacyTradeCompletionOutcome? outcome)
    {
        lock (gate)
        {
            outcome = null;
            if (!participants.TryGetValue(connectionId, out var participant))
                return LegacyTradeCompletionResult.ParticipantNotFound;
            if (!tradeStates.TryGetValue(connectionId, out var ownState))
                return LegacyTradeCompletionResult.NoActiveTrade;
            if (!participants.TryGetValue(ownState.OpponentId, out var opponent))
            {
                ClearTradePairLocked(connectionId);
                return LegacyTradeCompletionResult.TargetNotFound;
            }
            if (!tradeStates.TryGetValue(opponent.ConnectionId, out var opponentState) ||
                opponentState.OpponentId != connectionId)
            {
                ClearTradePairLocked(connectionId);
                return LegacyTradeCompletionResult.NotPaired;
            }
            if (!ownState.HasOffer || !opponentState.HasOffer)
                return LegacyTradeCompletionResult.NotPaired;
            if (ownState.MyCheck != 1 || opponentState.MyCheck != 1)
                return LegacyTradeCompletionResult.NotChecked;
            if (!HasLiveCombatState(participant) || !HasCarryState(participant) ||
                !HasLiveCombatState(opponent) || !HasCarryState(opponent))
                return LegacyTradeCompletionResult.CombatStateUnavailable;
            if (ReadCurrentHp(participant) <= 0)
                return LegacyTradeCompletionResult.NotAlive;
            if (ReadCurrentHp(opponent) <= 0)
                return LegacyTradeCompletionResult.TargetNotAlive;
            if (participant.PkMode || opponent.PkMode)
            {
                ClearTradePairLocked(connectionId);
                return LegacyTradeCompletionResult.PkModeBlocked;
            }

            if (!ValidateStoredOfferLocked(participant, opponent, ownState, out var ownValidation))
            {
                ClearTradePairLocked(connectionId);
                return MapCompletionValidation(ownValidation);
            }
            if (!ValidateStoredOfferLocked(opponent, participant, opponentState, out var opponentValidation))
            {
                ClearTradePairLocked(connectionId);
                return MapCompletionValidation(opponentValidation);
            }

            if (!TryBuildTradeDestinationLocked(participant, ownState, opponentState, out var participantMob, out var participantBuildResult))
                return participantBuildResult;
            if (!TryBuildTradeDestinationLocked(opponent, opponentState, ownState, out var opponentMob, out var opponentBuildResult))
                return opponentBuildResult;

            var participantPreviousMob = participant.Mob.ToArray();
            var opponentPreviousMob = opponent.Mob.ToArray();
            var participantPreviousCoin = participant.Coin;
            var opponentPreviousCoin = opponent.Coin;
            var participantPreviousPositionX = participant.PositionX;
            var participantPreviousPositionY = participant.PositionY;
            var opponentPreviousPositionX = opponent.PositionX;
            var opponentPreviousPositionY = opponent.PositionY;
            var participantPreviousMobExtra = participant.MobExtra.ToArray();
            var opponentPreviousMobExtra = opponent.MobExtra.ToArray();

            var participantCoin = (long)participant.Coin + opponentState.TradeMoney - ownState.TradeMoney;
            var opponentCoin = (long)opponent.Coin + ownState.TradeMoney - opponentState.TradeMoney;
            if (participantCoin is < 0 or > LegacyTradeMoneyLimit || opponentCoin is < 0 or > LegacyTradeMoneyLimit)
            {
                ClearTradePairLocked(connectionId);
                return LegacyTradeCompletionResult.GoldOverflow;
            }

            BinaryPrimitives.WriteInt32LittleEndian(participantMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), checked((int)participantCoin));
            BinaryPrimitives.WriteInt32LittleEndian(opponentMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), checked((int)opponentCoin));
            participant.Mob = participantMob;
            opponent.Mob = opponentMob;
            participant.Coin = checked((int)participantCoin);
            opponent.Coin = checked((int)opponentCoin);
            ClearTradePairLocked(connectionId);

            outcome = new LegacyTradeCompletionOutcome(
                participant.ConnectionId,
                opponent.ConnectionId,
                participant.Coin,
                opponent.Coin,
                participant.Mob.ToArray(),
                opponent.Mob.ToArray(),
                participant.AccountName,
                opponent.AccountName,
                participant.CharacterSlot,
                opponent.CharacterSlot,
                participant.PositionX,
                participant.PositionY,
                opponent.PositionX,
                opponent.PositionY,
                participant.MobExtra.ToArray(),
                opponent.MobExtra.ToArray(),
                ownState.TradeMoney,
                opponentState.TradeMoney,
                RequiresPersistence: true,
                participantPreviousMob,
                opponentPreviousMob,
                participantPreviousCoin,
                opponentPreviousCoin,
                participantPreviousPositionX,
                participantPreviousPositionY,
                opponentPreviousPositionX,
                opponentPreviousPositionY,
                participantPreviousMobExtra,
                opponentPreviousMobExtra);
            return LegacyTradeCompletionResult.Accepted;
        }
    }

    /// <summary>
    /// Restores a completed trade only when both participants still contain
    /// the exact post-completion snapshots. This prevents a failed persistence
    /// result from overwriting movement, combat, or inventory changes that
    /// arrived after the completion boundary.
    /// </summary>
    public bool TryRollbackTradeCompletion(LegacyTradeCompletionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        lock (gate)
        {
            if (!participants.TryGetValue(outcome.FirstConnectionId, out var first) ||
                !participants.TryGetValue(outcome.SecondConnectionId, out var second) ||
                first.Mob.Length != outcome.FirstMobSnapshot.Length ||
                second.Mob.Length != outcome.SecondMobSnapshot.Length ||
                !first.Mob.SequenceEqual(outcome.FirstMobSnapshot) ||
                !second.Mob.SequenceEqual(outcome.SecondMobSnapshot) ||
                first.Coin != outcome.FirstCoin ||
                second.Coin != outcome.SecondCoin ||
                first.PositionX != outcome.FirstPositionX ||
                first.PositionY != outcome.FirstPositionY ||
                second.PositionX != outcome.SecondPositionX ||
                second.PositionY != outcome.SecondPositionY ||
                !first.MobExtra.SequenceEqual(outcome.FirstMobExtraSnapshot) ||
                !second.MobExtra.SequenceEqual(outcome.SecondMobExtraSnapshot))
                return false;

            first.Mob = outcome.FirstPreviousMobSnapshot.ToArray();
            second.Mob = outcome.SecondPreviousMobSnapshot.ToArray();
            first.Coin = outcome.FirstPreviousCoin;
            second.Coin = outcome.SecondPreviousCoin;
            first.PositionX = outcome.FirstPreviousPositionX;
            first.PositionY = outcome.FirstPreviousPositionY;
            second.PositionX = outcome.SecondPreviousPositionX;
            second.PositionY = outcome.SecondPreviousPositionY;
            first.MobExtra = outcome.FirstPreviousMobExtraSnapshot.ToArray();
            second.MobExtra = outcome.SecondPreviousMobExtraSnapshot.ToArray();
            return true;
        }
    }

    private bool TryBuildTradeDestinationLocked(
        Participant receiver,
        LegacyTradeState receiverState,
        LegacyTradeState donorState,
        out byte[] destinationMob,
        out LegacyTradeCompletionResult result)
    {
        destinationMob = receiver.Mob.ToArray();
        result = LegacyTradeCompletionResult.Accepted;
        var destinationItems = new LegacyItem[LegacyAccountSnapshot.MobCarryCount];
        for (var slot = 0; slot < destinationItems.Length; slot++)
            destinationItems[slot] = ReadCarryItem(receiver, slot);

        foreach (var position in receiverState.InventoryPositions.Where(static position => position >= 0))
            destinationItems[position] = default;

        var maxCarry = Math.Min(GetLegacyMaxCarry(receiver), LegacyTradeCarryCount);
        var offeredItems = donorState.Items
            .Select((item, index) => (item, index))
            .Where(static pair => pair.item.Index != 0)
            .OrderByDescending(pair => itemData?.GetItemAbility(pair.item, 33) ?? 0)
            .ThenBy(static pair => pair.index)
            .Select(static pair => pair.item);

        foreach (var item in offeredItems)
        {
            var destinationSlot = -1;
            for (var slot = 0; slot < maxCarry; slot++)
            {
                if (destinationItems[slot].Index == 0)
                {
                    destinationSlot = slot;
                    break;
                }
            }
            if (destinationSlot < 0)
            {
                result = LegacyTradeCompletionResult.NoSpace;
                return false;
            }

            destinationItems[destinationSlot] = item;
        }

        for (var slot = 0; slot < destinationItems.Length; slot++)
            destinationItems[slot].Write(destinationMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + (slot * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        return true;
    }

    private static LegacyTradeCompletionResult MapCompletionValidation(LegacyTradeOfferResult result) => result switch
    {
        LegacyTradeOfferResult.InvalidMoney => LegacyTradeCompletionResult.InvalidMoney,
        LegacyTradeOfferResult.ItemChanged => LegacyTradeCompletionResult.ItemChanged,
        LegacyTradeOfferResult.NotTradeable => LegacyTradeCompletionResult.NotTradeable,
        LegacyTradeOfferResult.GuildItemRestricted => LegacyTradeCompletionResult.GuildItemRestricted,
        LegacyTradeOfferResult.PkModeBlocked => LegacyTradeCompletionResult.PkModeBlocked,
        _ => LegacyTradeCompletionResult.InvalidOffer,
    };

    private bool ValidateStoredOfferLocked(Participant owner, Participant opponent, LegacyTradeState state, out LegacyTradeOfferResult result)
    {
        return ValidateTradeOfferLocked(owner, opponent, state.ToRequest(), out result);
    }

    private bool ValidateTradeOfferLocked(Participant owner, Participant opponent, TradeOfferRequest request, out LegacyTradeOfferResult result)
    {
        result = LegacyTradeOfferResult.Accepted;
        if (request.Items.Count != TradeOfferRequest.ItemCount || request.InventoryPositions.Count != TradeOfferRequest.ItemCount)
        {
            result = LegacyTradeOfferResult.InvalidOfferShape;
            return false;
        }
        if (request.MyCheck > 1)
        {
            result = LegacyTradeOfferResult.InvalidCheck;
            return false;
        }
        if (request.TradeMoney < 0 || request.TradeMoney > LegacyTradeMoneyLimit || request.TradeMoney > owner.Coin)
        {
            result = LegacyTradeOfferResult.InvalidMoney;
            return false;
        }
        if (!HasCarryState(owner))
        {
            result = LegacyTradeOfferResult.CombatStateUnavailable;
            return false;
        }

        var usedPositions = new HashSet<int>();
        for (var index = 0; index < TradeOfferRequest.ItemCount; index++)
        {
            var position = request.InventoryPositions[index];
            if (position < -1 || position >= LegacyTradeCarryCount)
            {
                result = LegacyTradeOfferResult.InvalidInventoryPosition;
                return false;
            }
            if (position >= 0 && !usedPositions.Add(position))
            {
                result = LegacyTradeOfferResult.DuplicateInventoryPosition;
                return false;
            }

            var item = request.Items[index];
            if (item.Index < 0)
            {
                result = LegacyTradeOfferResult.InvalidItem;
                return false;
            }
            if (item.Index == 0)
                continue;
            if (position < 0 || !ReadCarryItem(owner, position).Equals(item))
            {
                result = LegacyTradeOfferResult.ItemChanged;
                return false;
            }
            if (itemData is not null && itemData.GetItemAbility(item, LegacyItemEffect.NoTrade) != 0)
            {
                result = LegacyTradeOfferResult.NotTradeable;
                return false;
            }
            if (IsGuildRestrictedTradeItem(item, owner, opponent))
            {
                result = LegacyTradeOfferResult.GuildItemRestricted;
                return false;
            }
        }

        return true;
    }

    private static bool HasLiveCombatState(Participant participant) =>
        participant.Mob.Length >= LegacyAccountSnapshot.MobCurrentScoreOffset + LegacyScore.SizeInBytes;

    private static bool HasCarryState(Participant participant) =>
        participant.Mob.Length >= LegacyAccountSnapshot.MobCarryOffset +
        (LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes);

    private static int ReadCurrentHp(Participant participant) =>
        LegacyMobCombatState.Read(participant.Mob).CurrentScore.Hp;

    private static bool IsGuildRestrictedTradeItem(LegacyItem item, Participant owner, Participant opponent)
    {
        if (item.Index is 747 or 3993 or 3994)
            return true;
        if (item.Index is not (446 or 508 or 522 or >= 526 and <= 537))
            return false;

        var guild = GetTradeItemGuild(item);
        return (guild != owner.GuildId || owner.GuildLevel == 0) &&
               (guild != opponent.GuildId || opponent.GuildLevel == 0);
    }

    private static int GetTradeItemGuild(LegacyItem item)
    {
        var high = item.Effect1 == LegacyItemEffect.HwordGuild ? item.Value1
            : item.Effect2 == LegacyItemEffect.HwordGuild ? item.Value2
            : item.Effect3 == LegacyItemEffect.HwordGuild ? item.Value3 : 0;
        var low = item.Effect1 == LegacyItemEffect.LwordGuild ? item.Value1
            : item.Effect2 == LegacyItemEffect.LwordGuild ? item.Value2
            : item.Effect3 == LegacyItemEffect.LwordGuild ? item.Value3 : 0;
        return (high * 256) + low;
    }

    private static bool SameTradeOffer(TradeOfferRequest left, TradeOfferRequest right) =>
        left.TradeMoney == right.TradeMoney &&
        left.MyCheck == right.MyCheck &&
        left.OpponentId == right.OpponentId &&
        left.Items.SequenceEqual(right.Items) &&
        left.InventoryPositions.SequenceEqual(right.InventoryPositions);

    private void ClearTradePairLocked(int connectionId)
    {
        if (!tradeStates.Remove(connectionId, out var state))
            return;
        if (tradeStates.TryGetValue(state.OpponentId, out var opponentState) && opponentState.OpponentId == connectionId)
            tradeStates.Remove(state.OpponentId);
    }

    private sealed class LegacyTradeState(int connectionId, int opponentId)
    {
        public int ConnectionId { get; } = connectionId;
        public int OpponentId { get; set; } = opponentId;
        public LegacyItem[] Items { get; private set; } = new LegacyItem[TradeOfferRequest.ItemCount];
        public sbyte[] InventoryPositions { get; private set; } = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
        public int TradeMoney { get; set; }
        public byte MyCheck { get; set; }
        public bool HasOffer { get; private set; }

        public void SetOffer(TradeOfferRequest request)
        {
            Items = request.Items.ToArray();
            InventoryPositions = request.InventoryPositions.ToArray();
            TradeMoney = request.TradeMoney;
            MyCheck = request.MyCheck;
            HasOffer = true;
        }

        public TradeOfferRequest ToRequest() => new(Items, InventoryPositions, TradeMoney, MyCheck, checked((ushort)OpponentId));

        public LegacyTradeParticipantState ToSnapshot() =>
            new(ConnectionId, OpponentId, HasOffer, Items.ToArray(), InventoryPositions.ToArray(), TradeMoney, MyCheck);
    }
}

public enum LegacyTradeOfferResult
{
    Accepted,
    BothChecked,
    ParticipantNotFound,
    TargetNotFound,
    SameParticipant,
    TargetBusy,
    AlreadyTrading,
    CombatStateUnavailable,
    TargetCombatStateUnavailable,
    NotAlive,
    PkModeBlocked,
    InvalidOpponent,
    InvalidOfferShape,
    InvalidMoney,
    InvalidCheck,
    InvalidInventoryPosition,
    DuplicateInventoryPosition,
    InvalidItem,
    ItemChanged,
    NotTradeable,
    GuildItemRestricted,
    OfferChangedAfterCheck,
}

public sealed record LegacyTradeParticipantState(
    int ConnectionId,
    int OpponentId,
    bool HasOffer,
    IReadOnlyList<LegacyItem> Items,
    IReadOnlyList<sbyte> InventoryPositions,
    int TradeMoney,
    byte MyCheck);

public sealed record LegacyTradeOfferOutcome(
    int ConnectionId,
    int OpponentId,
    bool Paired,
    LegacyTradeParticipantState OwnState,
    LegacyTradeParticipantState OpponentState);

public enum LegacyTradeCloseResult
{
    Accepted,
    ParticipantNotFound,
    NoActiveTrade,
}

public sealed record LegacyTradeCloseOutcome(int ConnectionId, int OpponentId);

public enum LegacyTradeCompletionResult
{
    Accepted,
    ParticipantNotFound,
    TargetNotFound,
    NoActiveTrade,
    NotPaired,
    NotChecked,
    CombatStateUnavailable,
    NotAlive,
    TargetNotAlive,
    PkModeBlocked,
    InvalidMoney,
    ItemChanged,
    NotTradeable,
    GuildItemRestricted,
    InvalidOffer,
    NoSpace,
    GoldOverflow,
}

public sealed record LegacyTradeCompletionOutcome(
    int FirstConnectionId,
    int SecondConnectionId,
    int FirstCoin,
    int SecondCoin,
    byte[] FirstMobSnapshot,
    byte[] SecondMobSnapshot,
    string FirstAccountName,
    string SecondAccountName,
    int FirstCharacterSlot,
    int SecondCharacterSlot,
    short FirstPositionX,
    short FirstPositionY,
    short SecondPositionX,
    short SecondPositionY,
    byte[] FirstMobExtraSnapshot,
    byte[] SecondMobExtraSnapshot,
    int FirstTradeMoney,
    int SecondTradeMoney,
    bool RequiresPersistence,
    byte[] FirstPreviousMobSnapshot,
    byte[] SecondPreviousMobSnapshot,
    int FirstPreviousCoin,
    int SecondPreviousCoin,
    short FirstPreviousPositionX,
    short FirstPreviousPositionY,
    short SecondPreviousPositionX,
    short SecondPreviousPositionY,
    byte[] FirstPreviousMobExtraSnapshot,
    byte[] SecondPreviousMobExtraSnapshot);
