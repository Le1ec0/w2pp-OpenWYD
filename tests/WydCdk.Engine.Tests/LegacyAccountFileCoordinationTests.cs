using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyAccountFileCoordinationTests
{
    public static async Task AccountAndAutoTradeWritersShareRootGate()
    {
        var accountRoot = Path.Combine(Path.GetTempPath(), "wyd-cdk-account-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(accountRoot);
        try
        {
            var accountStore = new LegacyFileAccountStore(accountRoot);
            var autoTradeStore = new LegacyAutoTradeFileStore(Path.Combine(accountRoot, "UP.json"), "UP", accountRoot);
            var rootGate = LegacyAccountFileStateGate.ForRoot(accountRoot);
            var items = new LegacyItem[AutoTradeListConfirmation.SlotCount];
            var carryPositions = Enumerable.Repeat((sbyte)-1, AutoTradeListConfirmation.SlotCount).ToArray();
            var prices = new int[AutoTradeListConfirmation.SlotCount];
            items[0] = new LegacyItem(1234, 43, 233, 0, 0, 0, 0);
            carryPositions[0] = 17;
            prices[0] = 456_789;
            var listing = new LegacyAutoTradeSnapshot(12, 2112, 2042, "TEST", items, carryPositions, prices, 5);

            await rootGate.WaitAsync();
            Task<CharacterGuildSaveResult> accountWrite;
            Task<LegacyAutoTradeStateResult> autoTradeWrite;
            try
            {
                accountWrite = accountStore.TrySaveCharacterGuildAsync("MISSING", 0, 0, 0).AsTask();
                autoTradeWrite = autoTradeStore.SaveAsync("SHOPPER", 0, listing).AsTask();
                await Task.Yield();

                if (accountWrite.IsCompleted || autoTradeWrite.IsCompleted)
                    throw new InvalidOperationException("Account and auto-trade writers do not share the account-root gate.");
            }
            finally
            {
                rootGate.Release();
            }

            if (await accountWrite != CharacterGuildSaveResult.AccountNotFound)
                throw new InvalidOperationException("Missing account write returned an unexpected result.");
            if (await autoTradeWrite != LegacyAutoTradeStateResult.Saved)
                throw new InvalidOperationException("Auto-trade writer did not continue after the shared account-root gate was released.");
        }
        finally
        {
            Directory.Delete(accountRoot, recursive: true);
        }
    }
}
