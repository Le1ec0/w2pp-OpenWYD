using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyNpcSellPreparationCoordinatorTests
{
    public static void Run()
    {
        CapturesRealStateAndPreparesNetPriceWithoutMutation();
        RejectsNonPlayingSessionBeforeStorageLookup();
        StopsAtTheUnverifiedVillageFiveTaxBoundary();
    }

    private static void CapturesRealStateAndPreparesNetPriceWithoutMutation()
    {
        var fixture = CreateFixture(sellerX: 2_080, sellerY: 2_140, targetX: 2_088, targetY: 2_148);
        var item = new LegacyItem(501, 1, 2, 3, 4, 5, 6);
        item.Write(fixture.SellerMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
        fixture.Initialize();

        var outcome = LegacyNpcSellPreparationCoordinator.PrepareAsync(
            fixture.Session,
            new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Carry, 0),
            fixture.World,
            new LegacyAutoTradeBook(),
            fixture.AccountStore,
            CreateItemData(501, 80_000)).GetAwaiter().GetResult();

        Check(outcome.Status == LegacyNpcSellPreparationStatus.Evaluated &&
              outcome.Flow is { Stage: LegacyNpcSellFlowStage.ReadyForSaleMutation,
                  PreparedSale: { Item: var preparedItem, NetPrice: 9_500, NpcVillage: 0, RequiresRecycleBinInsertion: true } } &&
              preparedItem == item,
            $"Live sale state did not produce the expected post-tax plan: status={outcome.Status}, flow={outcome.Flow?.Stage}.");
        Check(fixture.World.TryGetCharacterSnapshot(40, out var afterMob, out _, out _) &&
              afterMob is not null && LegacyItem.Read(afterMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) == item,
            "Preparation mutated the authoritative carry item.");
        Check(fixture.World.TryGetRecycleBinSnapshot(40, out var recycleBin) &&
              recycleBin!.All(static entry => entry.Item.Index == 0),
            "Preparation inserted a recycle-bin entry before the sale commit.");
    }

    private static void RejectsNonPlayingSessionBeforeStorageLookup()
    {
        var fixture = CreateFixture(sellerX: 2_080, sellerY: 2_140, targetX: 2_088, targetY: 2_148);
        var session = fixture.Session with { State = LoginSessionState.CharacterSelection };
        var outcome = LegacyNpcSellPreparationCoordinator.PrepareAsync(
            session,
            new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Carry, 0),
            fixture.World,
            new LegacyAutoTradeBook(),
            accountSnapshots: null,
            itemData: null).GetAwaiter().GetResult();

        Check(outcome.Status == LegacyNpcSellPreparationStatus.Evaluated &&
              outcome.Flow?.Stage == LegacyNpcSellFlowStage.HpModeRejected,
            "A non-Playing session reached storage/item data lookup instead of the opening HP/mode gate.");
    }

    private static void StopsAtTheUnverifiedVillageFiveTaxBoundary()
    {
        var fixture = CreateFixture(sellerX: 3_000, sellerY: 3_000, targetX: 3_008, targetY: 3_008);
        var item = new LegacyItem(501, 0, 0, 0, 0, 0, 0);
        item.Write(fixture.SellerMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
        fixture.Initialize();

        var outcome = LegacyNpcSellPreparationCoordinator.PrepareAsync(
            fixture.Session,
            new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Carry, 0),
            fixture.World,
            new LegacyAutoTradeBook(),
            fixture.AccountStore,
            CreateItemData(501, 80_000)).GetAwaiter().GetResult();

        Check(outcome.Status == LegacyNpcSellPreparationStatus.VillageFiveCityTaxUnverified &&
              outcome.Flow is null,
            $"Village 5 proceeded with an invented CityTax despite the source's out-of-bounds GuildZone access: status={outcome.Status}, flow={outcome.Flow?.Stage}, village={outcome.Location?.Village}.");
    }

    private static SellFixture CreateFixture(short sellerX, short sellerY, short targetX, short targetY)
    {
        const string accountName = "SELL-PREPARE";
        var slots = Enumerable.Range(0, LegacyAccountSnapshot.CharacterCount)
            .Select(static _ => new LegacyCharacterSlot(
                0, 0, string.Empty, default,
                new LegacyItem[LegacyCharacterSelection.EquipmentCount], 0, 0, 0))
            .ToArray();
        var account = new LegacyAccountSnapshot(
            accountName,
            new LegacyCharacterSelection(slots),
            new LegacyItem[LegacyAccountSnapshot.CargoCount],
            BankCoin: 0,
            WelcomeItemReceived: false,
            DailyQuest: default,
            BlockPassword: string.Empty,
            IsBlocked: false);
        var world = new WorldHub();
        var session = new LoginSession(
            40, LoginSessionState.Playing, accountName, null,
            SecureVerified: true, CharacterSlot: 0, PositionX: sellerX, PositionY: sellerY);
        var mob = NewMob("SELLER", merchant: 0);
        BinaryPrimitives.WriteInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
            100);

        return new(world, session, new FixedSnapshotStore(account), mob, targetX, targetY);
    }

    private static LegacyItemDataTable CreateItemData(short itemIndex, int price)
    {
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(
            encoded.AsSpan((itemIndex * LegacyItemDataTable.RecordSizeInBytes) + 128),
            price ^ 0x5A5A5A5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static byte[] NewMob(string name, byte merchant)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        mob[LegacyAccountSnapshot.MobMerchantOffset] = merchant;
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedSnapshotStore(LegacyAccountSnapshot snapshot) : IAccountSnapshotStore
    {
        public ValueTask<LegacyAccountSnapshot?> ReadSnapshotAsync(
            string accountName,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<LegacyAccountSnapshot?>(
                string.Equals(accountName, snapshot.AccountName, StringComparison.OrdinalIgnoreCase)
                    ? snapshot
                    : null);
    }

    private sealed class SellFixture(
        WorldHub world,
        LoginSession session,
        IAccountSnapshotStore accountStore,
        byte[] sellerMob,
        short targetX,
        short targetY)
    {
        public WorldHub World { get; } = world;
        public LoginSession Session { get; } = session;
        public IAccountSnapshotStore AccountStore { get; } = accountStore;
        public byte[] SellerMob { get; } = sellerMob;

        public void Initialize()
        {
            Check(World.Enter(Session.ConnectionId, Session.AccountName!, NoopSend), "The seller did not enter the world.");
            Check(World.SetCharacterState(
                    Session.ConnectionId, Session.CharacterSlot, 0, 0, 0, 0,
                    Session.PositionX, Session.PositionY, SellerMob,
                    LegacyAccountSnapshot.ClassMasterMortal, new byte[LegacyAccountSnapshot.AffectStride]),
                "The seller MOB was not installed.");
            var npcMob = NewMob("MERCHANT", merchant: 1);
            Check(World.EnterNpc(npcMob, targetX, targetY, requestedConnectionId: 1_000) == 1_000,
                "The merchant NPC was not installed.");
        }
    }

    private static ValueTask NoopSend(ReadOnlyMemory<byte> _, CancellationToken __) => ValueTask.CompletedTask;
}
