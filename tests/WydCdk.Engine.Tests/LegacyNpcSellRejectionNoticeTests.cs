using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyNpcSellRejectionNoticeTests
{
    public static void Run()
    {
        MapsOnlyLocalizedSellRejections();
        MapsTheNonMerchantTargetOnly();
        EncodesTheLegacyMessagesAsMessagePanelFrames();
        EncodesTheTargetOutOfViewCloseShopSignal();
    }

    private static void MapsOnlyLocalizedSellRejections()
    {
        Check(LegacyNpcSellRejectionNoticeMapper.For(Outcome(LegacyNpcSellFlowStage.CargoBlockedDuringTradeMode)) ==
              "Não é possiível durante a auto venda.",
            "The auto-trade cargo rejection did not preserve the TMProject Language.txt text.");
        Check(LegacyNpcSellRejectionNoticeMapper.For(Outcome(LegacyNpcSellFlowStage.PotionSaleBlocked)) ==
              "Poções não podem ser vendidas.",
            "The volatile-item rejection did not preserve the TMProject Language.txt text.");
        Check(LegacyNpcSellRejectionNoticeMapper.For(Outcome(LegacyNpcSellFlowStage.AccountBlocked)) is null &&
              LegacyNpcSellRejectionNoticeMapper.For(Outcome(LegacyNpcSellFlowStage.SourceRejected)) is null &&
              LegacyNpcSellRejectionNoticeMapper.For(Outcome(LegacyNpcSellFlowStage.PriceRejected)) is null,
            "A rejection without a source-backed localized sell message was assigned one.");
    }

    private static void MapsTheNonMerchantTargetOnly()
    {
        var nonMerchant = Outcome(
            LegacyNpcSellFlowStage.TargetRejected,
            new LegacyNpcSellTargetResolution(LegacyNpcSellTargetStatus.NotMerchant));
        Check(LegacyNpcSellRejectionNoticeMapper.For(nonMerchant) ==
              "Possível vender somente para comerciantes.",
            "The non-merchant target rejection did not preserve the TMProject Language.txt text.");

        var invalidTarget = Outcome(
            LegacyNpcSellFlowStage.TargetRejected,
            new LegacyNpcSellTargetResolution(LegacyNpcSellTargetStatus.TargetIdOutsideMobTable));
        Check(LegacyNpcSellRejectionNoticeMapper.For(invalidTarget) is null,
            "The silent out-of-range target rejection was incorrectly localized.");

        var outOfView = Outcome(
            LegacyNpcSellFlowStage.TargetRejected,
            new LegacyNpcSellTargetResolution(LegacyNpcSellTargetStatus.TargetNotInView));
        Check(LegacyNpcSellRejectionNoticeMapper.For(outOfView) is null,
            "The out-of-view target rejection was incorrectly turned into a text notice.");
    }

    private static void EncodesTheLegacyMessagesAsMessagePanelFrames()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        foreach (var flow in new[]
                 {
                     Outcome(LegacyNpcSellFlowStage.CargoBlockedDuringTradeMode),
                     Outcome(LegacyNpcSellFlowStage.TargetRejected,
                         new LegacyNpcSellTargetResolution(LegacyNpcSellTargetStatus.NotMerchant)),
                     Outcome(LegacyNpcSellFlowStage.PotionSaleBlocked),
                 })
        {
            var message = LegacyNpcSellRejectionNoticeMapper.For(flow);
            Check(message is not null, "A source-backed rejection did not produce a message.");
            var frame = codec.Decode(new MessagePanelConfirmation(message!)
                .ToFrame(codec, clientTick: 0, keywordIndex: 0));
            var expected = System.Text.Encoding.Latin1.GetBytes(message!);
            Check(frame.IsChecksumValid && frame.Header.Type == MessagePanelConfirmation.MessageType &&
                  frame.Header.Id == 0 && frame.Header.Size == MessagePanelConfirmation.PacketSize &&
                  frame.Header.ClientTick == 0 && frame.Header.KeywordIndex == 0 &&
                  frame.Payload.Span[..expected.Length].SequenceEqual(expected),
                "The localized sell rejection did not use the legacy MessagePanel wire and Latin-1 text.");
        }
    }

    private static void EncodesTheTargetOutOfViewCloseShopSignal()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var frame = codec.Decode(CloseShopSignal.ToFrame(codec));
        Check(frame.IsChecksumValid && frame.Header.Type == CloseShopSignal.MessageType &&
              frame.Header.Id == CloseShopSignal.SceneId && frame.Header.Size == PacketHeader.SizeInBytes &&
              frame.Header.ClientTick == 0 && frame.Header.KeywordIndex == 0 && frame.Payload.IsEmpty,
            "The out-of-view rejection did not encode SendClientSignal(ESCENE_FIELD, _MSG_CloseShop).");
    }

    private static LegacyNpcSellPreparationOutcome Outcome(
        LegacyNpcSellFlowStage stage,
        LegacyNpcSellTargetResolution? target = null) =>
        new(LegacyNpcSellPreparationStatus.Evaluated,
            new LegacyNpcSellFlowResult(stage, Target: target), null, null, null);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
