using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyNpcSellAuthoritativeLookupTests
{
    public static void Run()
    {
        CapturesLiveCarryAndNpcState();
        ResolvesEquipmentAndCargoFromTheirAuthoritativeOwners();
        CapturesAPlayerMobForTheClassThreeTargetBypassPath();
        PreservesTheCargoTradeModeGateAndRejectsUnavailableSessionState();
    }

    private static void CapturesLiveCarryAndNpcState()
    {
        var fixture = CreateFixture(accountBlocked: true);
        var carryItem = new LegacyItem(501, 1, 2, 3, 4, 5, 6);
        PutItem(fixture.SellerMob, LegacyAccountSnapshot.MobCarryOffset, 7, carryItem);
        fixture.InitializeSeller();
        fixture.AddNpc(1000, "MERCHANT", x: 2088, y: 2148, merchant: 1);

        var outcome = LegacyNpcSellAuthoritativeLookup.CaptureAsync(
            fixture.Session,
            new ShopSellMessage(1000, (short)LegacyNpcSellSourceKind.Carry, 7),
            fixture.World,
            fixture.AccountStore,
            tradeMode: false).GetAwaiter().GetResult();

        Check(outcome.Status == LegacyNpcSellLookupStatus.Ready && outcome.State is not null,
            $"Valid USER_PLAY seller/item/NPC snapshots were not captured: {outcome.Status}.");
        Check(outcome.State!.SelectedItem == carryItem && outcome.State.Source.Kind == LegacyNpcSellSourceKind.Carry &&
              outcome.State.Source.Position == 7 && !outcome.State.Source.RequiresEquipmentUpdate,
            "The selected carry item or its source metadata did not come from the live MOB.");
        Check(outcome.State.AccountBlocked && outcome.State.SellerPositionX == 2080 && outcome.State.SellerPositionY == 2140,
            "The account gate or seller position was not read from its authoritative snapshot.");
        Check(outcome.State.Target is { Kind: LegacyWorldMobKind.Npc, PositionX: 2088, PositionY: 2148 } target &&
              target.MobSnapshot[LegacyAccountSnapshot.MobMerchantOffset] == 1 &&
              ReadName(target.MobSnapshot) == "MERCHANT",
            "The target MOB, merchant byte, name, or position did not come from the live NPC state.");
    }

    private static void ResolvesEquipmentAndCargoFromTheirAuthoritativeOwners()
    {
        var fixture = CreateFixture(accountBlocked: false);
        var equipmentItem = new LegacyItem(620, 3, 2, 1, 4, 5, 6);
        var cargoItem = new LegacyItem(740, 9, 8, 7, 6, 5, 4);
        PutItem(fixture.SellerMob, LegacyAccountSnapshot.MobEquipmentOffset, 3, equipmentItem);
        fixture.Cargo[11] = cargoItem;
        fixture.InitializeSeller();
        fixture.AddNpc(1001, "MERCHANT2", x: 2081, y: 2141, merchant: 1);

        var equipment = Capture(fixture, 1001, (short)LegacyNpcSellSourceKind.Equipment, 3, tradeMode: false);
        Check(equipment.Status == LegacyNpcSellLookupStatus.Ready &&
              equipment.State is { SelectedItem: var selectedEquipment } && selectedEquipment == equipmentItem &&
              equipment.State.Source.RequiresEquipmentUpdate,
            "Equipment lookup did not resolve the live MOB slot or its legacy refresh flag.");

        var cargo = Capture(fixture, 1001, (short)LegacyNpcSellSourceKind.Cargo, 11, tradeMode: false);
        Check(cargo.Status == LegacyNpcSellLookupStatus.Ready &&
              cargo.State is { SelectedItem: var selectedCargo } && selectedCargo == cargoItem &&
              cargo.State.Source.Kind == LegacyNpcSellSourceKind.Cargo,
            "Cargo lookup did not resolve the account snapshot slot.");
    }

    private static void CapturesAPlayerMobForTheClassThreeTargetBypassPath()
    {
        var fixture = CreateFixture(accountBlocked: false);
        fixture.InitializeSeller();
        var targetMob = NewMob("PLAYER-TARGET", merchant: 0);
        Check(fixture.World.Enter(41, "TARGET", NoopSend), "The target player did not enter the world.");
        Check(fixture.World.SetCharacterState(
                41, 0, 0, 0, 0, 0, 2090, 2150, targetMob,
                LegacyAccountSnapshot.ClassMasterMortal, new byte[LegacyAccountSnapshot.AffectStride]),
            "The target player's authoritative MOB state was not installed.");

        var outcome = Capture(fixture, 41, (short)LegacyNpcSellSourceKind.Carry, 0, tradeMode: false);
        Check(outcome.Status == LegacyNpcSellLookupStatus.Ready &&
              outcome.State?.Target is { Kind: LegacyWorldMobKind.Player, PositionX: 2090, PositionY: 2150 } target &&
              ReadName(target.MobSnapshot) == "PLAYER-TARGET",
            "The class-3 target-bypass lookup did not capture an active player target.");
    }

    private static void PreservesTheCargoTradeModeGateAndRejectsUnavailableSessionState()
    {
        var fixture = CreateFixture(accountBlocked: false);
        fixture.Cargo[2] = new LegacyItem(502, 0, 0, 0, 0, 0, 0);
        fixture.InitializeSeller();
        fixture.AddNpc(1002, "MERCHANT3", x: 2080, y: 2140, merchant: 1);

        var blockedCargo = Capture(fixture, 1002, (short)LegacyNpcSellSourceKind.Cargo, 2, tradeMode: true);
        Check(blockedCargo.Status == LegacyNpcSellLookupStatus.SourceRejected &&
              blockedCargo.State?.Source.Status == LegacyNpcSellSourceStatus.CargoBlockedDuringAutoTrade &&
              blockedCargo.State.SelectedItem is null,
            "Cargo in TradeMode reached the authoritative item lookup.");

        var notPlaying = fixture.Session with { State = LoginSessionState.CharacterSelection };
        var rejectedSession = LegacyNpcSellAuthoritativeLookup.CaptureAsync(
            notPlaying,
            new ShopSellMessage(1002, (short)LegacyNpcSellSourceKind.Carry, 0),
            fixture.World,
            fixture.AccountStore,
            tradeMode: false).GetAwaiter().GetResult();
        Check(rejectedSession.Status == LegacyNpcSellLookupStatus.SessionNotPlaying && rejectedSession.State is null,
            "A session outside USER_PLAY reached authoritative sell-state lookup.");

        var noTarget = Capture(fixture, 1003, (short)LegacyNpcSellSourceKind.Carry, 0, tradeMode: false);
        Check(noTarget.Status == LegacyNpcSellLookupStatus.TargetUnavailable && noTarget.State?.SelectedItem is not null &&
              noTarget.State.Target is null,
            "A missing target discarded the selected item needed to preserve the later item-gate order.");
    }

    private static LegacyNpcSellLookupOutcome Capture(
        SellFixture fixture,
        ushort targetId,
        short myType,
        short myPos,
        bool tradeMode) =>
        LegacyNpcSellAuthoritativeLookup.CaptureAsync(
            fixture.Session,
            new ShopSellMessage(targetId, myType, myPos),
            fixture.World,
            fixture.AccountStore,
            tradeMode,
            cargoRuntime: fixture.CargoRuntime).GetAwaiter().GetResult();

    private static SellFixture CreateFixture(bool accountBlocked)
    {
        const string accountName = "SELL-LOOKUP";
        var cargo = new LegacyItem[LegacyAccountSnapshot.CargoCount];
        var slots = Enumerable.Range(0, LegacyAccountSnapshot.CharacterCount)
            .Select(static _ => new LegacyCharacterSlot(
                0, 0, string.Empty, default,
                new LegacyItem[LegacyCharacterSelection.EquipmentCount], 0, 0, 0))
            .ToArray();
        var snapshot = new LegacyAccountSnapshot(
            accountName,
            new LegacyCharacterSelection(slots),
            cargo,
            BankCoin: 0,
            WelcomeItemReceived: false,
            DailyQuest: default,
            BlockPassword: string.Empty,
            IsBlocked: accountBlocked);
        var world = new WorldHub();
        var session = new LoginSession(
            40, LoginSessionState.Playing, accountName, null,
            SecureVerified: true, CharacterSlot: 0, PositionX: 2080, PositionY: 2140);
        return new SellFixture(
            world,
            session,
            new FixedSnapshotStore(accountName, snapshot),
            new byte[LegacyAccountSnapshot.CharacterStride],
            cargo);
    }

    private static void PutItem(byte[] mob, int startOffset, int position, LegacyItem item) =>
        item.Write(mob.AsSpan(startOffset + (position * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));

    private static byte[] NewMob(string name, byte merchant)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, LegacyCharacterSelection.NameLength - 1))
            .CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        mob[LegacyAccountSnapshot.MobMerchantOffset] = merchant;
        return mob;
    }

    private static string ReadName(ReadOnlySpan<byte> mob)
    {
        var name = mob.Slice(LegacyAccountSnapshot.MobNameOffset, LegacyCharacterSelection.NameLength);
        var terminator = name.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator < 0 ? name : name[..terminator]);
    }

    private static ValueTask NoopSend(ReadOnlyMemory<byte> _, CancellationToken __) => ValueTask.CompletedTask;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedSnapshotStore(string accountName, LegacyAccountSnapshot snapshot) : IAccountSnapshotStore
    {
        public ValueTask<LegacyAccountSnapshot?> ReadSnapshotAsync(
            string requestedAccountName,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<LegacyAccountSnapshot?>(
                string.Equals(requestedAccountName, accountName, StringComparison.OrdinalIgnoreCase)
                    ? snapshot
                    : null);
    }

    private sealed class SellFixture(
        WorldHub world,
        LoginSession session,
        IAccountSnapshotStore accountStore,
        byte[] sellerMob,
        LegacyItem[] cargo)
    {
        public WorldHub World { get; } = world;
        public LoginSession Session { get; } = session;
        public IAccountSnapshotStore AccountStore { get; } = accountStore;
        public byte[] SellerMob { get; } = sellerMob;
        public LegacyItem[] Cargo { get; } = cargo;
        public LegacyAccountCargoRuntime CargoRuntime { get; } = new("UP");

        public void InitializeSeller()
        {
            Check(World.Enter(Session.ConnectionId, Session.AccountName!, NoopSend), "The seller did not enter the world.");
            Check(World.SetCharacterState(
                    Session.ConnectionId, Session.CharacterSlot, 0, 0, 0, 0,
                    Session.PositionX, Session.PositionY, SellerMob,
                    LegacyAccountSnapshot.ClassMasterMortal, new byte[LegacyAccountSnapshot.AffectStride]),
                "The seller's authoritative MOB state was not installed.");
        }

        public void AddNpc(int connectionId, string name, short x, short y, byte merchant) =>
            Check(World.EnterNpc(NewMob(name, merchant), x, y, requestedConnectionId: connectionId) == connectionId,
                "The merchant NPC did not enter the world.");
    }
}
