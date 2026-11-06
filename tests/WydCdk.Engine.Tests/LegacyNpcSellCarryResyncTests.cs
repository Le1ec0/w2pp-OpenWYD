using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyNpcSellCarryResyncTests
{
    public static void Run()
    {
        EncodesEmptyCarryResyncFromAuthoritativeSnapshot();
        LeavesOtherItemRejectionsSilent();
    }

    private static void EncodesEmptyCarryResyncFromAuthoritativeSnapshot()
    {
        var preparation = Outcome(
            LegacyNpcSellFlowStage.ItemRejected,
            LegacyNpcSellSourceKind.Carry,
            new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.EmptyItem, RequiresCarryItemResync: true));
        var codec = LegacyFrameCodec.CreateDefault();

        Check(LegacyNpcSellCarryResync.TryBuildFrame(preparation, 7, codec, out var bytes) && bytes is not null,
            "The empty carry sell rejection did not request its source-backed SendItem resync.");
        var frame = codec.Decode(bytes!);
        Check(frame.IsChecksumValid && frame.Header.Type == SendItemConfirmation.MessageType &&
              frame.Header.Id == 7 && frame.Header.Size == SendItemConfirmation.PacketSize &&
              frame.Header.ClientTick == 0 && frame.Header.KeywordIndex == 0,
            "The empty carry resync did not encode the MSG_SendItem response header.");
        Check(SendItemConfirmation.TryParse(frame, out var confirmation) && confirmation is not null &&
              confirmation.InventoryType == (short)LegacyNpcSellSourceKind.Carry && confirmation.Slot == 5 &&
              confirmation.Item == default,
            "The empty carry resync did not return the authoritative empty item at the selected slot.");
    }

    private static void LeavesOtherItemRejectionsSilent()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        foreach (var preparation in new[]
                 {
                     Outcome(LegacyNpcSellFlowStage.ItemRejected, LegacyNpcSellSourceKind.Carry,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.ItemPointerUnavailable, false)),
                     Outcome(LegacyNpcSellFlowStage.ItemRejected, LegacyNpcSellSourceKind.Carry,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.ItemPointerUnavailable, true), itemIndex: 1),
                     Outcome(LegacyNpcSellFlowStage.ItemRejected, LegacyNpcSellSourceKind.Carry,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.ItemPointerUnavailable, true), itemIndex: 6500),
                     Outcome(LegacyNpcSellFlowStage.ItemRejected, LegacyNpcSellSourceKind.Equipment,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.ItemPointerUnavailable, true), itemIndex: 6500),
                     Outcome(LegacyNpcSellFlowStage.ItemRejected, LegacyNpcSellSourceKind.Equipment,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.EmptyItem, false)),
                     Outcome(LegacyNpcSellFlowStage.ItemRejected, LegacyNpcSellSourceKind.Cargo,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.EmptyItem, false)),
                     Outcome(LegacyNpcSellFlowStage.ItemRejected, LegacyNpcSellSourceKind.Carry,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.EmptyItem, true), itemIndex: 1),
                     Outcome(LegacyNpcSellFlowStage.SourceRejected, LegacyNpcSellSourceKind.Carry,
                         new LegacyNpcSellItemResolution(LegacyNpcSellItemStatus.EmptyItem, true)),
                 })
        {
            Check(!LegacyNpcSellCarryResync.TryBuildFrame(preparation, 7, codec, out var bytes) && bytes is null,
                "An unsupported or silent NPC sell rejection emitted a carry resync.");
        }
    }

    private static LegacyNpcSellPreparationOutcome Outcome(
        LegacyNpcSellFlowStage stage,
        LegacyNpcSellSourceKind sourceKind,
        LegacyNpcSellItemResolution item,
        short itemIndex = 0)
    {
        var selectedItem = new LegacyItem(itemIndex, 0, 0, 0, 0, 0, 0);
        var source = new LegacyNpcSellSourceResolution(
            LegacyNpcSellSourceStatus.Valid,
            sourceKind,
            5,
            RequiresEquipmentUpdate: false);
        var state = new LegacyNpcSellAuthoritativeState(
            "SELLER",
            0,
            20_000,
            new byte[LegacyAccountSnapshot.CharacterStride],
            2_000,
            2_000,
            false,
            source,
            selectedItem,
            null);
        var flow = new LegacyNpcSellFlowResult(stage, Source: source, Item: item);
        return new LegacyNpcSellPreparationOutcome(
            LegacyNpcSellPreparationStatus.Evaluated,
            flow,
            state,
            LegacyNpcSellLookupStatus.Ready,
            null);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
