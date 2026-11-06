using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Maps the three localized <c>_MSG_Sell</c> rejections whose text and gate are present in TMProject 7.69.</summary>
internal static class LegacyNpcSellRejectionNoticeMapper
{
    public const int OnlyMerchantLanguageId = 116;
    public const int CannotSellPotionLanguageId = 167;
    public const int AutoTradeCargoLanguageId = 170;

    // Text is copied from TMProject 7.69's TMSrv/run/Language.txt. Keep the
    // source spelling, including its typo in entry 170, for wire compatibility.
    public static string? For(LegacyNpcSellPreparationOutcome preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.Status != LegacyNpcSellPreparationStatus.Evaluated || preparation.Flow is not { } flow)
            return null;

        return flow.Stage switch
        {
            LegacyNpcSellFlowStage.CargoBlockedDuringTradeMode => "Não é possiível durante a auto venda.",
            LegacyNpcSellFlowStage.TargetRejected when flow.Target?.Status == LegacyNpcSellTargetStatus.NotMerchant =>
                "Possível vender somente para comerciantes.",
            LegacyNpcSellFlowStage.PotionSaleBlocked => "Poções não podem ser vendidas.",
            _ => null,
        };
    }
}
