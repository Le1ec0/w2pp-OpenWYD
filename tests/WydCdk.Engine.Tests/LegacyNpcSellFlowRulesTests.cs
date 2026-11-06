using WydCdk.World;
using WydCdk.Protocol;

internal static class LegacyNpcSellFlowRulesTests
{
    public static void Run()
    {
        HpAndTradeBranchesPrecedeAccountAndInventoryGates();
        CargoTradeModeAndAccountGatesPreserveWireOrder();
        TargetAndItemGatesPrecedePotionAndPrice();
        SuccessfulPreparationReturnsPostTaxPriceWithoutMutatingWorldState();
    }

    private static void HpAndTradeBranchesPrecedeAccountAndInventoryGates()
    {
        var dead = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            CurrentHp = 0,
            TradeOpponentId = 42,
            AccountBlocked = true,
            MyType = short.MaxValue,
        });
        Check(dead.Stage == LegacyNpcSellFlowStage.HpModeRejected,
            "The HP/USER_PLAY gate did not run before the active-trade and blocked-account branches.");

        var trade = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            TradeOpponentId = 42,
            AccountBlocked = true,
            MyType = short.MaxValue,
        });
        Check(trade.Stage == LegacyNpcSellFlowStage.TradeCancellationRequired &&
              trade.Trade is { FirstRemoveTradeConnectionId: 42, SecondRemoveTradeConnectionId: 100 },
            "The active-trade branch did not precede blocked-account and source validation.");
    }

    private static void CargoTradeModeAndAccountGatesPreserveWireOrder()
    {
        var cargoTrade = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            TradeMode = true,
            AccountBlocked = true,
            MyType = (short)LegacyNpcSellSourceKind.Cargo,
        });
        Check(cargoTrade.Stage == LegacyNpcSellFlowStage.CargoBlockedDuringTradeMode,
            "Cargo in TradeMode did not stop before the 7.69 account-block gate.");

        var blocked = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            AccountBlocked = true,
            MyType = short.MaxValue,
        });
        Check(blocked.Stage == LegacyNpcSellFlowStage.AccountBlocked,
            "The blocked-account gate did not precede source/item/target processing.");
    }

    private static void TargetAndItemGatesPrecedePotionAndPrice()
    {
        var playerTarget = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            TargetId = 42,
            Target = null,
        });
        Check(playerTarget.Stage == LegacyNpcSellFlowStage.TargetRejected &&
              playerTarget.Target?.Status == LegacyNpcSellTargetStatus.NotNpcTargetId,
            "A player-range target did not fail its ID gate before requiring a mob snapshot.");

        var targetFirst = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            SelectedItem = new LegacyItem(0, 0, 0, 0, 0, 0, 0),
            Target = Npc(merchant: 0),
        });
        Check(targetFirst.Stage == LegacyNpcSellFlowStage.TargetRejected &&
              targetFirst.Target?.Status == LegacyNpcSellTargetStatus.NotMerchant,
            "The target merchant gate did not precede empty-item eligibility.");

        var potion = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            VolatileValue = LegacyNpcSellVolatileRules.BlockedVolatileValue,
            ItemBasePrice = -1,
        });
        Check(potion.Stage == LegacyNpcSellFlowStage.PotionSaleBlocked,
            "EF_VOLATILE == 1 did not reject before price calculation.");
    }

    private static void SuccessfulPreparationReturnsPostTaxPriceWithoutMutatingWorldState()
    {
        var prepared = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            ItemBasePrice = 80_000,
            NpcVillage = 2,
            CityTax = 25,
        });
        Check(prepared.Stage == LegacyNpcSellFlowStage.ReadyForSaleMutation &&
              prepared.Price is { IsSuccess: true, Amount: 7_500, NpcVillage: 2 } &&
              prepared.PreparedSale is { NetPrice: 7_500, NpcVillage: 2, RequiresRecycleBinInsertion: true } sale &&
              sale.Item == new LegacyItem(501, 2, 3, 4, 5, 6, 7),
            "A valid sell did not reach the sale-mutation boundary with its exact item, post-tax price, and recycle-bin decision.");

        var outsideVillage = LegacyNpcSellFlowRules.Resolve(Context() with
        {
            NpcVillage = -1,
            CityTax = 99,
        });
        Check(outsideVillage.Stage == LegacyNpcSellFlowStage.ReadyForSaleMutation &&
              outsideVillage.PreparedSale is { NpcVillage: -1, RequiresRecycleBinInsertion: false },
            "A sale outside a village did not preserve LABEL_S2's no-tax/no-recycle-bin continuation.");
    }

    private static LegacyNpcSellFlowContext Context() => new(
        ConnectionState: LoginSessionState.Playing,
        CurrentHp: 100,
        ConnectionId: 100,
        TradeOpponentId: 0,
        TradeMode: false,
        AccountBlocked: false,
        MyType: (short)LegacyNpcSellSourceKind.Carry,
        MyPos: 0,
        SelectedItemAvailable: true,
        SelectedItem: new LegacyItem(501, 2, 3, 4, 5, 6, 7),
        TargetId: 1_000,
        Target: Npc(merchant: 1),
        SellerClass: 0,
        LearnedSkillWord0: 0,
        SellerX: 2_100,
        SellerY: 2_100,
        ItemBasePrice: 80_000,
        Special2: 0,
        NpcVillage: 2,
        CityTax: 0,
        VolatileValue: 0);

    private static LegacyWorldMobSnapshot Npc(byte merchant)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        mob[LegacyAccountSnapshot.MobMerchantOffset] = merchant;
        return new(1_000, LegacyWorldMobKind.Npc, mob, 2_100, 2_100);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
