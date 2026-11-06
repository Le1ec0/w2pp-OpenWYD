namespace WydCdk.World;

/// <summary>
/// Composes the already-portable NPC sell gates in TMProject 7.69 wire order.
/// This is a read-only preparation step: it stops at the sale-mutation boundary
/// and deliberately does not call GetRecycleBin or change inventory/coin state.
/// </summary>
public static class LegacyNpcSellFlowRules
{
    public static LegacyNpcSellFlowResult Resolve(LegacyNpcSellFlowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var hpMode = LegacyNpcSellHpModeRules.Resolve(context.CurrentHp, context.ConnectionState);
        if (!hpMode.IsReady)
            return new(LegacyNpcSellFlowStage.HpModeRejected);

        var trade = LegacyNpcSellTradeRules.Resolve(context.ConnectionId, context.TradeOpponentId);
        if (!trade.CanContinueSelling)
            return new(LegacyNpcSellFlowStage.TradeCancellationRequired, Trade: trade);

        // The source helper also validates positions. At this point only its
        // early cargo/TradeMode branch belongs before IsBlocked in the native
        // handler; all other source validation follows the account gate.
        var tradeModeSource = LegacyNpcSellSourceRules.Resolve(context.MyType, context.MyPos, context.TradeMode);
        if (tradeModeSource.Status == LegacyNpcSellSourceStatus.CargoBlockedDuringAutoTrade)
            return new(LegacyNpcSellFlowStage.CargoBlockedDuringTradeMode, Source: tradeModeSource);

        var account = LegacyNpcSellAccountRules.Resolve(context.AccountBlocked);
        if (!account.CanContinueSelling)
            return new(LegacyNpcSellFlowStage.AccountBlocked);

        var source = LegacyNpcSellSourceRules.Resolve(context.MyType, context.MyPos, tradeMode: false);
        if (!source.IsValid)
            return new(LegacyNpcSellFlowStage.SourceRejected, Source: source);

        if (!context.SelectedItemAvailable)
            return new(LegacyNpcSellFlowStage.SelectedItemUnavailable, Source: source);

        var targetWithoutSnapshot = LegacyNpcSellTargetRules.Resolve(
            context.TargetId,
            context.SellerClass,
            context.LearnedSkillWord0,
            targetMerchant: 0,
            context.SellerX,
            context.SellerY,
            targetX: 0,
            targetY: 0);
        if (targetWithoutSnapshot.Status is LegacyNpcSellTargetStatus.TargetIdOutsideMobTable or LegacyNpcSellTargetStatus.NotNpcTargetId)
            return new(LegacyNpcSellFlowStage.TargetRejected, Source: source, Target: targetWithoutSnapshot);

        if (context.Target is not { } target || target.MobSnapshot.Length <= LegacyAccountSnapshot.MobMerchantOffset)
            return new(LegacyNpcSellFlowStage.TargetUnavailable, Source: source);

        var targetResolution = LegacyNpcSellTargetRules.Resolve(
            context.TargetId,
            context.SellerClass,
            context.LearnedSkillWord0,
            target.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset],
            context.SellerX,
            context.SellerY,
            target.PositionX,
            target.PositionY);
        if (!targetResolution.IsAccepted)
            return new(LegacyNpcSellFlowStage.TargetRejected, Source: source, Target: targetResolution);

        var itemResolution = LegacyNpcSellItemRules.Resolve(source, context.SelectedItem);
        if (!itemResolution.IsSellable)
            return new(LegacyNpcSellFlowStage.ItemRejected, Source: source, Target: targetResolution, Item: itemResolution);

        var volatileResolution = LegacyNpcSellVolatileRules.Resolve(context.VolatileValue);
        if (!volatileResolution.CanContinueSelling)
            return new(LegacyNpcSellFlowStage.PotionSaleBlocked, Source: source, Target: targetResolution, Item: itemResolution);

        var price = LegacyNpcSellPriceMath.Calculate(
            context.ItemBasePrice,
            context.SelectedItem.Index,
            context.SellerClass,
            unchecked((uint)context.LearnedSkillWord0),
            context.Special2,
            context.NpcVillage,
            context.CityTax);
        if (!price.IsSuccess)
            return new(LegacyNpcSellFlowStage.PriceRejected, Source: source, Target: targetResolution, Item: itemResolution, Price: price);

        var sale = new LegacyNpcSellPreparedSale(
            context.SelectedItem,
            price.GrossAmount,
            price.Amount,
            price.NpcVillage,
            RequiresRecycleBinInsertion: price.NpcVillage is >= 0 and <= 5);
        return new(LegacyNpcSellFlowStage.ReadyForSaleMutation, Source: source, Target: targetResolution, Item: itemResolution, Price: price, PreparedSale: sale);
    }
}

public sealed record LegacyNpcSellFlowContext(
    LoginSessionState ConnectionState,
    int CurrentHp,
    int ConnectionId,
    int TradeOpponentId,
    bool TradeMode,
    bool AccountBlocked,
    short MyType,
    short MyPos,
    bool SelectedItemAvailable,
    WydCdk.Protocol.LegacyItem SelectedItem,
    ushort TargetId,
    LegacyWorldMobSnapshot? Target,
    int SellerClass,
    int LearnedSkillWord0,
    short SellerX,
    short SellerY,
    int ItemBasePrice,
    int Special2,
    int NpcVillage,
    int CityTax,
    int VolatileValue);

public enum LegacyNpcSellFlowStage
{
    HpModeRejected,
    TradeCancellationRequired,
    CargoBlockedDuringTradeMode,
    AccountBlocked,
    SourceRejected,
    SelectedItemUnavailable,
    TargetUnavailable,
    TargetRejected,
    ItemRejected,
    PotionSaleBlocked,
    PriceRejected,
    ReadyForSaleMutation,
}

public sealed record LegacyNpcSellFlowResult(
    LegacyNpcSellFlowStage Stage,
    LegacyNpcSellTradeResolution? Trade = null,
    LegacyNpcSellSourceResolution? Source = null,
    LegacyNpcSellTargetResolution? Target = null,
    LegacyNpcSellItemResolution? Item = null,
    LegacyNpcSellPriceResult? Price = null,
    LegacyNpcSellPreparedSale? PreparedSale = null);

public sealed record LegacyNpcSellPreparedSale(
    WydCdk.Protocol.LegacyItem Item,
    int GrossPrice,
    int NetPrice,
    int NpcVillage,
    bool RequiresRecycleBinInsertion);
