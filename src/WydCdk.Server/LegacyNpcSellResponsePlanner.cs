using WydCdk.Protocol;
using WydCdk.World;
using System.Globalization;

namespace WydCdk.Server;

/// <summary>Builds the native success-response order for a committed 7.69 NPC sale without sending it.</summary>
internal static class LegacyNpcSellResponsePlanner
{
    public static LegacyNpcSellResponseBuildResult TryBuild(
        WorldHub world,
        DecodedFrame requestFrame,
        LegacyNpcSellCompletionOutcome outcome,
        LegacyFrameCodec codec,
        out LegacyNpcSellResponsePlan? response)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(codec);
        response = null;

        if (!ShopSellMessage.TryParse(requestFrame, out var request) || request is null)
            return LegacyNpcSellResponseBuildResult.InvalidRequest;

        var sale = outcome.Plan;
        if (!sale.Source.IsValid ||
            sale.Source.Kind is not { } sourceKind ||
            sale.Target.ConnectionId is < 0 or > ushort.MaxValue ||
            sale.ConnectionId is <= 0 or >= LegacyMobAiPlanner.MaximumPlayerId ||
            request.TargetId != sale.Target.ConnectionId ||
            request.MyType != (short)sourceKind ||
            request.MyPos != sale.Source.Position)
            return LegacyNpcSellResponseBuildResult.SaleMismatch;

        var sellerId = checked((ushort)sale.ConnectionId);
        var steps = new List<LegacyNpcSellResponseStep>(capacity: 4)
        {
            new(
                LegacyNpcSellResponseDestination.Seller,
                new ExperienceMessagePanel(
                    $"Adquiriu {sale.Sale.NetPrice.ToString(CultureInfo.InvariantCulture)} de gold")
                    .ToFrame(codec, requestFrame.Header.ClientTick, requestFrame.Header.KeywordIndex, sellerId),
                Array.AsReadOnly([sale.ConnectionId])),
            new(
                LegacyNpcSellResponseDestination.Seller,
                request.ToFrame(codec, requestFrame.Header.ClientTick, requestFrame.Header.KeywordIndex, requestFrame.Header.Id),
                Array.AsReadOnly([sale.ConnectionId])),
        };

        if (sale.Source.RequiresEquipmentUpdate)
        {
            if (!world.TryBuildUpdateEquipFrame(
                    sale.ConnectionId,
                    codec,
                    requestFrame.Header.ClientTick,
                    requestFrame.Header.KeywordIndex,
                    out var equipmentFrame) || equipmentFrame is null)
                return LegacyNpcSellResponseBuildResult.WorldStateUnavailable;

            var viewRecipients = world.GetParticipantIdsInView(sale.SellerPositionX, sale.SellerPositionY)
                .Where(connectionId => connectionId != sale.ConnectionId)
                .Distinct()
                .Order()
                .ToArray();
            steps.Add(new(
                LegacyNpcSellResponseDestination.View,
                equipmentFrame,
                Array.AsReadOnly(viewRecipients)));
        }

        if (!world.TryBuildUpdateEtcFrame(
                sale.ConnectionId,
                codec,
                requestFrame.Header.ClientTick,
                requestFrame.Header.KeywordIndex,
                out var updateEtcFrame) || updateEtcFrame is null)
            return LegacyNpcSellResponseBuildResult.WorldStateUnavailable;

        steps.Add(new(
            LegacyNpcSellResponseDestination.Seller,
            updateEtcFrame,
            Array.AsReadOnly([sale.ConnectionId])));

        response = new(sellerId, Array.AsReadOnly(steps.ToArray()));
        return LegacyNpcSellResponseBuildResult.Built;
    }
}

internal enum LegacyNpcSellResponseBuildResult
{
    Built,
    InvalidRequest,
    SaleMismatch,
    WorldStateUnavailable,
}

internal enum LegacyNpcSellResponseDestination
{
    Seller,
    View,
}

internal sealed record LegacyNpcSellResponseStep(
    LegacyNpcSellResponseDestination Destination,
    byte[] Frame,
    IReadOnlyList<int> RecipientConnectionIds);

internal sealed record LegacyNpcSellResponsePlan(
    ushort SellerConnectionId,
    IReadOnlyList<LegacyNpcSellResponseStep> Steps);
