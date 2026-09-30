using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AutoTradeCloseFixtures
{
    internal static (WorldHub World, LegacyAutoTradeBook Book, LegacyAutoTradePurchaseExecutionRequest Request)
        CreatePurchase()
    {
        var item = new LegacyItem(900, 1, 2, 3, 4, 5, 6);
        var items = new LegacyItem[AutoTradeListConfirmation.SlotCount];
        var positions = Enumerable.Repeat((sbyte)-1, items.Length).ToArray();
        var prices = new int[items.Length];
        items[0] = item; positions[0] = 4; prices[0] = 100;
        var cargo = new LegacyItem[LegacyAutoTradeBook.CargoSlotCount];
        cargo[4] = item;
        var book = new LegacyAutoTradeBook();
        AutoTradeCloseTests.Check(book.TryStart(12,
            new AutoTradeStartRequest("AFK SHOP", items, positions, prices),
            new LegacyAutoTradeStartContext(true, 100, false, true, 12, 2000, 2001, cargo, new HashSet<short>()),
            out var listing) == LegacyAutoTradeStartResult.Accepted && listing is not null, "Cannot create shop fixture.");
        var world = new WorldHub();
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        mob[LegacyAccountSnapshot.MobNameOffset] = (byte)'B';
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 150);
        AutoTradeCloseTests.Check(world.Enter(7, "BUYER", (_, _) => ValueTask.CompletedTask) &&
            world.SetCharacterState(7, 0, 0, 0, 0, 150, 2010, 2011, mob), "Cannot create buyer fixture.");
        AutoTradeCloseTests.Check(world.EnterNpc(new byte[LegacyAccountSnapshot.CharacterStride], 2000, 2001,
            new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 12, autoTradeSnapshot: listing,
            autoTradeOwnerAccount: "SELLER", autoTradeCharacterSlot: 1) == 12, "Cannot create shop NPC.");
        var persisted = mob.ToArray();
        BinaryPrimitives.WriteInt64LittleEndian(persisted.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), 77);
        var context = new LegacyAutoTradePurchaseContext(100, true, false, 0, 12, true, false, true, true, true,
            0, 100, 12, item, listing!, cargo, new LegacyItem[LegacyAccountSnapshot.MobCarryCount], 150, 10, 0);
        var expected = new LegacyAutoTradePersistedListing("SELLER", 1, 2000, 2001, "AFK SHOP", items, positions, prices, 12);
        return (world, book, new LegacyAutoTradePurchaseExecutionRequest(7, context, "BUYER", 0,
            mob.ToArray(), persisted, 2010, 2011, new byte[LegacyAccountSnapshot.MobExtraStride], "SELLER", 1, expected, true,
            book.Snapshot().Single().Generation));
    }
}
