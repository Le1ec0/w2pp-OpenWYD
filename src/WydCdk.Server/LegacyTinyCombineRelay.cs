using WydCdk.Protocol;
using WydCdk.World;

namespace WydCdk.Server;

internal static class LegacyTinyCombineRelay
{
    public const string ProcessingCompleteMessage = "Processo de combinação foi concluído.";
    public const string CombineFailedMessage = "Combinação de item falhou.";
    public const string WrongCombinationMessage = "Há algo de errado na combinação.";

    public static bool TryBuild(
        LegacyTinyCombineOutcome? outcome,
        byte[]? updateEtcFrame,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        ushort headerId,
        string successMessage,
        string failureMessage,
        out IReadOnlyList<byte[]>? frames)
    {
        ArgumentNullException.ThrowIfNull(codec);
        frames = null;
        if (outcome is null || updateEtcFrame is null || outcome.ItemUpdates is null ||
            outcome.ItemUpdatePositions is null || outcome.ItemUpdates.Count != outcome.ItemUpdatePositions.Count ||
            !outcome.ItemUpdates.Select(static update => update.Position).SequenceEqual(outcome.ItemUpdatePositions))
            return false;

        var resultUpdateCount = outcome.Succeeded ? 3 : 1;
        var initialUpdateCount = outcome.ItemUpdates.Count - resultUpdateCount;
        if (initialUpdateCount < 1)
            return false;

        var relayFrames = new List<byte[]>(outcome.ItemUpdates.Count + 3);
        void AddItemUpdate(LegacyTinyCombineItemUpdate update)
        {
            relayFrames.Add(new SendItemConfirmation(
                    LegacyWorldItem.CarryDestinationType,
                    update.Position,
                    update.Item)
                .ToFrame(codec, clientTick, keywordIndex, headerId));
        }

        for (var index = 0; index < initialUpdateCount; index++)
            AddItemUpdate(outcome.ItemUpdates[index]);

        if (outcome.Succeeded)
        {
            for (var index = initialUpdateCount; index < outcome.ItemUpdates.Count - 1; index++)
                AddItemUpdate(outcome.ItemUpdates[index]);

            relayFrames.Add(updateEtcFrame);
            relayFrames.Add(new MessagePanelConfirmation(successMessage)
                .ToFrame(codec, clientTick, keywordIndex));
            relayFrames.Add(new TinyCombineCompleteConfirmation()
                .ToFrame(codec, clientTick, keywordIndex, result: 1));
            AddItemUpdate(outcome.ItemUpdates[^1]);
        }
        else
        {
            AddItemUpdate(outcome.ItemUpdates[^1]);
            relayFrames.Add(new MessagePanelConfirmation(failureMessage)
                .ToFrame(codec, clientTick, keywordIndex));
            relayFrames.Add(new TinyCombineCompleteConfirmation()
                .ToFrame(codec, clientTick, keywordIndex, result: 2));
            relayFrames.Add(updateEtcFrame);
        }

        frames = Array.AsReadOnly(relayFrames.ToArray());
        return true;
    }
}
