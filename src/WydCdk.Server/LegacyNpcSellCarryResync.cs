using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

/// <summary>Builds the _MSG_Sell rejection that resends an empty carry slot.</summary>
internal static class LegacyNpcSellCarryResync
{
    public static bool TryBuildFrame(
        LegacyNpcSellPreparationOutcome preparation,
        ushort connectionId,
        LegacyFrameCodec codec,
        out byte[]? frame)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(codec);
        frame = null;

        if (preparation.Status != LegacyNpcSellPreparationStatus.Evaluated ||
            preparation.Flow is not { Stage: LegacyNpcSellFlowStage.ItemRejected } flow ||
            flow.Source is not { } source ||
            source.Status != LegacyNpcSellSourceStatus.Valid ||
            source.Kind != LegacyNpcSellSourceKind.Carry ||
            source.Position < 0 || source.Position >= LegacyAccountSnapshot.MobCarryCount - 4 ||
            flow.Item is not { Status: LegacyNpcSellItemStatus.EmptyItem, RequiresCarryItemResync: true } ||
            preparation.State?.SelectedItem is not { } selectedItem ||
            selectedItem.Index != 0)
        {
            return false;
        }

        frame = new SendItemConfirmation(
                (short)LegacyNpcSellSourceKind.Carry,
                source.Position,
                selectedItem)
            .ToFrame(codec, clientTick: 0, keywordIndex: 0, id: connectionId);
        return true;
    }
}
