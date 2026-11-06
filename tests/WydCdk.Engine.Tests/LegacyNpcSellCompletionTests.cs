using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class LegacyNpcSellCompletionTests
{
    public static void Run()
    {
        CommitsCarryGoldAndRecycleBinTogetherAndCanRollback();
        CommitsEquipmentMirrorAndCanRollback();
        RejectsStaleSellerRecycleBinAndTarget();
        BuildsCargoCasAndCommitsCarriedGoldWithoutTouchingMobItems();
        LeavesInt32OverflowWithoutAPlan();
        CreditsCityAndGlobalTaxAndCanRollbackAtomically();
        ZoneFourCreditsOneTreasuryTwiceInSourceOrder();
        EnforcesStrictTaxExperienceCapsWithOvershoot();
        MissingInactiveAndOutOfRangeTreasuriesDoNotRejectSale();
        CreditsTheFirstNpcIdAtMaxUserBoundary();
    }

    public static void RunResponseSequenceTests()
    {
        BuildsCarrySaleEchoThenUpdateEtc();
        BuildsEquipmentSaleBroadcastBetweenSellerFrames();
        OmitsEquipmentBroadcastForSlotEight();
        RejectsMalformedAndMismatchedRequests();
    }

    public static void RunTreasuryCheckpointBoundaryTests()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        fixture.AddTreasury(zone: 0, connectionId: 1_001, experience: 1_000);
        fixture.AddTreasury(zone: 4, connectionId: 1_002, experience: 2_000);
        var root = Path.Combine(Path.GetTempPath(), $"wyd-cdk-npc-sell-treasury-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new LegacyGuildTreasuryFileStore(root);
            var initialSaves = store.SaveRegisteredAsync(fixture.World).AsTask().GetAwaiter().GetResult();
            Check(initialSaves.SequenceEqual([
                    (1_001, LegacyGuildTreasurySaveResult.Saved),
                    (1_002, LegacyGuildTreasurySaveResult.Saved)]),
                "The sale treasury checkpoint fixture did not persist both initial raw MOB files.");

            var cityPath = Path.Combine(root, "TAX0-1001");
            var globalPath = Path.Combine(root, "TAX4-1002");
            var cityBeforeSale = File.ReadAllBytes(cityPath);
            var globalBeforeSale = File.ReadAllBytes(globalPath);

            Check(fixture.World.TryCommitNpcSell(fixture.BuildPlan(), out _) == LegacyNpcSellCommitResult.Committed &&
                  fixture.GetTreasuryExperience(1_001) == 1_125 &&
                  fixture.GetTreasuryExperience(1_002) == 2_125,
                "The committed sale did not update the in-memory city and global tax recipients.");
            Check(File.ReadAllBytes(cityPath).SequenceEqual(cityBeforeSale) &&
                  File.ReadAllBytes(globalPath).SequenceEqual(globalBeforeSale),
                "The in-memory sale commit synchronously changed a treasury file before its separate checkpoint.");

            var laterSaves = store.SaveRegisteredAsync(fixture.World).AsTask().GetAwaiter().GetResult();
            var cityAfterSale = File.ReadAllBytes(cityPath);
            var globalAfterSale = File.ReadAllBytes(globalPath);
            Check(laterSaves.SequenceEqual(initialSaves) &&
                  BinaryPrimitives.ReadInt64LittleEndian(cityAfterSale.AsSpan(LegacyAccountSnapshot.MobExperienceOffset)) == 1_125 &&
                  BinaryPrimitives.ReadInt64LittleEndian(globalAfterSale.AsSpan(LegacyAccountSnapshot.MobExperienceOffset)) == 2_125,
                "The later independent treasury checkpoint did not persist the sale's committed tax EXP.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void BuildsCarrySaleEchoThenUpdateEtc()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry, sourcePosition: 3);
        var outcome = Commit(fixture);
        var codec = LegacyFrameCodec.CreateDefault();
        var requestBytes = new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Carry, 3, 0xA55A)
            .ToFrame(codec, 0x12345678, 0x37, (ushort)fixture.Session.ConnectionId);
        var request = codec.Decode(requestBytes);

        var result = LegacyNpcSellResponsePlanner.TryBuild(fixture.World, request, outcome, codec, out var response);
        Check(result == LegacyNpcSellResponseBuildResult.Built && response is not null,
            $"A committed carry sale did not produce a response plan: {result}.");
        Check(response!.Steps.Count == 3 &&
              response.Steps.All(step => step.Destination == LegacyNpcSellResponseDestination.Seller),
            "A carry sale did not order the success panel, echo, and UpdateEtc to the seller.");
        var notice = codec.Decode(response.Steps[0].Frame);
        Check(notice.Header.Type == ExperienceMessagePanel.MessageType &&
              Encoding.Latin1.GetString(notice.Payload.Span[..$"Adquiriu {outcome.Plan.Sale.NetPrice} de gold".Length]) ==
              $"Adquiriu {outcome.Plan.Sale.NetPrice} de gold" &&
              BinaryPrimitives.ReadUInt32LittleEndian(notice.Payload.Span[ExperienceMessagePanel.ColorOffset..]) == ExperienceMessagePanel.Yellow,
            "The successful sale notice did not precede the echo with the net Gold amount in yellow.");
        Check(response.Steps[1].Frame.AsSpan().SequenceEqual(requestBytes),
            "The MSG_Sell acknowledgement did not preserve the original request frame.");
        var etc = codec.Decode(response.Steps[2].Frame);
        Check(etc.Header.Type == UpdateEtcConfirmationV769.MessageType &&
              etc.Header.Id == fixture.Session.ConnectionId &&
              BinaryPrimitives.ReadInt32LittleEndian(etc.Payload.Span[UpdateEtcConfirmationV769.CoinOffset..]) == outcome.Plan.CoinAfter,
            "The final seller response did not refresh the committed Gold through MSG_UpdateEtc.");
    }

    private static void BuildsEquipmentSaleBroadcastBetweenSellerFrames()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Equipment, sourcePosition: 1);
        var viewerMob = SellFixture.NewMob("VIEWER", merchant: 0);
        Check(fixture.World.Enter(41, "VIEWER", static (_, _) => ValueTask.CompletedTask),
            "The response planner viewer did not enter the world.");
        Check(fixture.World.SetCharacterState(
                41,
                characterSlot: 0,
                guildId: 0,
                clan: 0,
                guildLevel: 0,
                coin: 0,
                fixture.Session.PositionX,
                fixture.Session.PositionY,
                viewerMob,
                LegacyAccountSnapshot.ClassMasterMortal,
                new byte[LegacyAccountSnapshot.AffectStride]),
            "The response planner viewer did not receive a position in the seller's view.");

        var outcome = Commit(fixture);
        var codec = LegacyFrameCodec.CreateDefault();
        var requestBytes = new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Equipment, 1)
            .ToFrame(codec, 77, 5, (ushort)fixture.Session.ConnectionId);
        var request = codec.Decode(requestBytes);
        var result = LegacyNpcSellResponsePlanner.TryBuild(fixture.World, request, outcome, codec, out var response);

        Check(result == LegacyNpcSellResponseBuildResult.Built && response is not null && response.Steps.Count == 4,
            $"An equipment sale did not produce four ordered response steps: {result}.");
        Check(response!.Steps[0].Destination == LegacyNpcSellResponseDestination.Seller &&
              response.Steps[1].Destination == LegacyNpcSellResponseDestination.Seller &&
              response.Steps[2].Destination == LegacyNpcSellResponseDestination.View &&
              response.Steps[3].Destination == LegacyNpcSellResponseDestination.Seller,
            "The equipment multicast was not placed between the seller echo and UpdateEtc after the success notice.");
        Check(response.Steps[2].RecipientConnectionIds.SequenceEqual([41]),
            "The equipment update did not target only the visible player while excluding the seller.");
        var equipment = codec.Decode(response.Steps[2].Frame);
        Check(equipment.Header.Type == EquipmentAppearanceV769.MessageType &&
              equipment.Header.Id == fixture.Session.ConnectionId,
            "The view update did not use MSG_UpdateEquip with the seller as its actor ID.");
    }

    private static void OmitsEquipmentBroadcastForSlotEight()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Equipment, sourcePosition: 8);
        var outcome = Commit(fixture);
        var codec = LegacyFrameCodec.CreateDefault();
        var request = codec.Decode(new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Equipment, 8)
            .ToFrame(codec, 78, 6, (ushort)fixture.Session.ConnectionId));

        var result = LegacyNpcSellResponsePlanner.TryBuild(fixture.World, request, outcome, codec, out var response);
        Check(result == LegacyNpcSellResponseBuildResult.Built && response is not null && response.Steps.Count == 3 &&
              response.Steps.All(step => step.Destination == LegacyNpcSellResponseDestination.Seller),
            "Equipment slot 8 received a MSG_UpdateEquip even though 7.69 limits SendEquip to slots below 8.");
    }

    private static void RejectsMalformedAndMismatchedRequests()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry, sourcePosition: 3);
        var outcome = Commit(fixture);
        var codec = LegacyFrameCodec.CreateDefault();
        var requestBytes = new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Carry, 3)
            .ToFrame(codec, 79, 7, (ushort)fixture.Session.ConnectionId);
        var malformedBytes = requestBytes.ToArray();
        malformedBytes[3] ^= 1;

        AssertRejected(codec.Decode(malformedBytes), LegacyNpcSellResponseBuildResult.InvalidRequest,
            "A corrupted MSG_Sell checksum produced a response plan.");

        var wrongOpcode = codec.Decode(codec.Encode(
            0x0379,
            (ushort)fixture.Session.ConnectionId,
            79,
            new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Carry, 3).ToPayload(),
            7));
        AssertRejected(wrongOpcode, LegacyNpcSellResponseBuildResult.InvalidRequest,
            "A different opcode with the same payload produced a sale response plan.");

        var fullRequest = codec.Decode(requestBytes);
        var truncatedRequest = new DecodedFrame(
            fullRequest.Header with { Size = (ushort)(fullRequest.Header.Size - 1) },
            fullRequest.Payload[..^1],
            IsChecksumValid: true);
        AssertRejected(truncatedRequest, LegacyNpcSellResponseBuildResult.InvalidRequest,
            "A truncated MSG_Sell payload produced a response plan.");

        var mismatchedRequest = codec.Decode(new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Carry, 4)
            .ToFrame(codec, 80, 8, (ushort)fixture.Session.ConnectionId));
        AssertRejected(mismatchedRequest, LegacyNpcSellResponseBuildResult.SaleMismatch,
            "A request for a different source slot produced a response for the committed sale.");

        var mismatchedTarget = codec.Decode(new ShopSellMessage(1_001, (short)LegacyNpcSellSourceKind.Carry, 3)
            .ToFrame(codec, 81, 9, (ushort)fixture.Session.ConnectionId));
        AssertRejected(mismatchedTarget, LegacyNpcSellResponseBuildResult.SaleMismatch,
            "A request for a different target produced a response for the committed sale.");

        var mismatchedSourceKind = codec.Decode(new ShopSellMessage(1_000, (short)LegacyNpcSellSourceKind.Equipment, 3)
            .ToFrame(codec, 82, 10, (ushort)fixture.Session.ConnectionId));
        AssertRejected(mismatchedSourceKind, LegacyNpcSellResponseBuildResult.SaleMismatch,
            "A request with a different inventory source produced a response for the committed sale.");

        void AssertRejected(DecodedFrame frame, LegacyNpcSellResponseBuildResult expected, string message)
        {
            var result = LegacyNpcSellResponsePlanner.TryBuild(fixture.World, frame, outcome, codec, out var response);
            Check(result == expected && response is null, message);
        }
    }

    private static LegacyNpcSellCompletionOutcome Commit(SellFixture fixture)
    {
        Check(fixture.World.TryCommitNpcSell(fixture.BuildPlan(), out var outcome) == LegacyNpcSellCommitResult.Committed && outcome is not null,
            "The response planner fixture sale did not commit.");
        return outcome!;
    }

    private static void CommitsCarryGoldAndRecycleBinTogetherAndCanRollback()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry, initialCoin: 1_999_990_500);
        var preparation = fixture.Prepare();
        var beforeMob = fixture.GetMob();
        var beforeRecycleBin = fixture.GetRecycleBin();
        var buildStatus = LegacyNpcSellCompletionRules.TryBuild(
            preparation,
            fixture.Session.ConnectionId,
            beforeRecycleBin,
            randomValue: 154_321,
            out var plan);

        Check(buildStatus == LegacyNpcSellCompletionBuildStatus.Ready && plan is not null,
            $"A valid carry sale failed to build a commit plan: {buildStatus}.");
        Check(plan!.CoinAfter == 2_000_000_000,
            "The source's strict 2,000,000,000 Gold cap was not applied at the boundary.");
        Check(plan.ExpectedCargo is null && plan.UpdatedCargo is null && plan.CargoRevision is null,
            "A non-Cargo sale plan carried an empty Cargo CAS payload.");
        Check(LegacyItem.Read(plan.UpdatedMob.Span.Slice(
                  LegacyAccountSnapshot.MobCarryOffset,
                  LegacyItem.SizeInBytes)) == default,
            "The commit plan did not clear exactly the selected carry slot.");
        Check(LegacyItem.Read(beforeMob.AsSpan(
                  LegacyAccountSnapshot.MobCarryOffset,
                  LegacyItem.SizeInBytes)) == fixture.Item,
            "Building the commit plan mutated its source MOB snapshot.");

        var commitStatus = fixture.World.TryCommitNpcSell(plan, out var outcome);
        Check(commitStatus == LegacyNpcSellCommitResult.Committed && outcome is not null,
            $"A valid sale did not commit: {commitStatus}.");
        var committedMob = fixture.GetMob();
        var committedRecycleBin = fixture.GetRecycleBin();
        Check(LegacyItem.Read(committedMob.AsSpan(
                  LegacyAccountSnapshot.MobCarryOffset,
                  LegacyItem.SizeInBytes)) == default &&
              BinaryPrimitives.ReadInt32LittleEndian(committedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == 2_000_000_000 &&
              committedRecycleBin[0] == new LegacyRecycleBinEntry(9_500, fixture.Item, 54_321),
            "The live commit did not update item, Gold, and buyback as one sale.");

        Check(fixture.World.TryRollbackNpcSell(outcome!),
            "A failed downstream persistence step could not conditionally roll back the sale.");
        Check(fixture.GetMob().AsSpan().SequenceEqual(beforeMob) &&
              fixture.GetRecycleBin().SequenceEqual(beforeRecycleBin),
            "Conditional rollback did not restore item, Gold, and all recycle-bin slots.");
    }

    private static void RejectsStaleSellerRecycleBinAndTarget()
    {
        var equipmentFixture = SellFixture.Create(LegacyNpcSellSourceKind.Equipment, sourcePosition: 1);
        var equipmentPlan = equipmentFixture.BuildPlan();
        var changedEquipment = equipmentFixture.GetClientEquipment().ToArray();
        changedEquipment[1] = new LegacyItem(600, 0, 0, 0, 0, 0, 0);
        equipmentFixture.SetClientEquipment(changedEquipment);
        Check(equipmentFixture.World.TryCommitNpcSell(equipmentPlan, out _) == LegacyNpcSellCommitResult.StateChanged &&
              LegacyItem.Read(equipmentFixture.GetMob().AsSpan(
                  LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes,
                  LegacyItem.SizeInBytes)) == equipmentFixture.Item,
            "A stale 7.69 equipment mirror committed a sale or changed the MOB item.");

        var sellerFixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        var sellerPlan = sellerFixture.BuildPlan();
        var changedMob = sellerFixture.GetMob();
        BinaryPrimitives.WriteInt32LittleEndian(
            changedMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset),
            1);
        sellerFixture.SetSellerMob(changedMob, coin: 1);
        Check(sellerFixture.World.TryCommitNpcSell(sellerPlan, out _) == LegacyNpcSellCommitResult.StateChanged &&
              LegacyItem.Read(sellerFixture.GetMob().AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) == sellerFixture.Item,
            "A stale seller snapshot committed a sale or changed the selected item.");

        var binFixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        var binPlan = binFixture.BuildPlan();
        Check(binFixture.World.TryRecordRecycleBinSale(
                binFixture.Session.ConnectionId,
                new LegacyItem(600, 0, 0, 0, 0, 0, 0),
                1,
                1,
                out _),
            "The fixture could not change the recycle bin after preparation.");
        var mobBeforeRejectedCommit = binFixture.GetMob();
        Check(binFixture.World.TryCommitNpcSell(binPlan, out _) == LegacyNpcSellCommitResult.StateChanged &&
              binFixture.GetMob().AsSpan().SequenceEqual(mobBeforeRejectedCommit),
            "A stale recycle-bin snapshot committed a sale or mutated the seller.");

        var targetFixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        var targetPlan = targetFixture.BuildPlan();
        Check(targetFixture.World.TryRemoveNpc(1_000, out _),
            "The fixture could not replace the merchant after preparation.");
        Check(targetFixture.World.EnterNpc(
                SellFixture.NewMob("MERCHANT", merchant: 1),
                2_089,
                2_148,
                requestedConnectionId: 1_000) == 1_000,
            "The fixture could not restore the merchant at a changed target location.");
        var targetMobBeforeRejectedCommit = targetFixture.GetMob();
        Check(targetFixture.World.TryCommitNpcSell(targetPlan, out _) == LegacyNpcSellCommitResult.TargetChanged &&
              targetFixture.GetMob().AsSpan().SequenceEqual(targetMobBeforeRejectedCommit),
            "A changed NPC target location was not rejected before any sale mutation.");
    }

    private static void CommitsEquipmentMirrorAndCanRollback()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Equipment, sourcePosition: 1);
        var plan = fixture.BuildPlan();
        var beforeEquipment = fixture.GetClientEquipment();
        Check(beforeEquipment[1] == fixture.Item,
            "The fixture did not initialize the persisted 18-slot equipment mirror.");

        Check(fixture.World.TryCommitNpcSell(plan, out var outcome) == LegacyNpcSellCommitResult.Committed && outcome is not null,
            "A valid equipment sale did not commit.");
        var committedEquipment = fixture.GetClientEquipment();
        Check(committedEquipment[1] == default &&
              LegacyItem.Read(fixture.GetMob().AsSpan(
                  LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes,
                  LegacyItem.SizeInBytes)) == default,
            "The equipment sale did not clear both MOB and 7.69 equipment state.");

        Check(fixture.World.TryRollbackNpcSell(outcome!),
            "An equipment sale could not be conditionally rolled back.");
        Check(fixture.GetClientEquipment()[1] == fixture.Item &&
              LegacyItem.Read(fixture.GetMob().AsSpan(
                  LegacyAccountSnapshot.MobEquipmentOffset + LegacyItem.SizeInBytes,
                  LegacyItem.SizeInBytes)) == fixture.Item,
            "Equipment rollback did not restore both live representations.");
    }

    private static void BuildsCargoCasAndCommitsCarriedGoldWithoutTouchingMobItems()
    {
        var cargo = SellFixture.Create(LegacyNpcSellSourceKind.Cargo);
        var cargoPlan = cargo.BuildPlan();
        Check(cargoPlan.ExpectedCargo is { } expected && cargoPlan.UpdatedCargo is { } updated &&
              cargoPlan.CargoRevision == 0 &&
              LegacyItem.Read(expected.Span.Slice(0, LegacyItem.SizeInBytes)) == cargo.Item &&
              LegacyItem.Read(updated.Span.Slice(0, LegacyItem.SizeInBytes)) == default &&
              expected.Span[LegacyItem.SizeInBytes..].SequenceEqual(updated.Span[LegacyItem.SizeInBytes..]),
            "The Cargo sale plan did not carry a full-array CAS that clears only the selected slot.");
        var mobBefore = cargo.GetMob();
        Check(cargo.World.TryCommitNpcSell(cargoPlan, out var cargoOutcome) == LegacyNpcSellCommitResult.Committed && cargoOutcome is not null,
            "A valid Cargo sale did not commit its WorldHub-owned Gold and recycle-bin state.");
        Check(cargo.GetMob().AsSpan(LegacyAccountSnapshot.MobCarryOffset,
                  LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes)
              .SequenceEqual(mobBefore.AsSpan(LegacyAccountSnapshot.MobCarryOffset,
                  LegacyAccountSnapshot.MobCarryCount * LegacyItem.SizeInBytes)) &&
              LegacyItem.Read(cargo.GetMob().AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes)) ==
                  LegacyItem.Read(mobBefore.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes)) &&
              BinaryPrimitives.ReadInt32LittleEndian(cargo.GetMob().AsSpan(LegacyAccountSnapshot.MobCoinOffset)) == cargoPlan.CoinAfter,
            "The Cargo sale WorldHub commit changed MOB item arrays or missed the carried Gold update.");
        Check(cargo.World.TryRollbackNpcSell(cargoOutcome!) &&
              cargo.GetMob().AsSpan().SequenceEqual(mobBefore),
            "A Cargo sale WorldHub rollback did not restore the prior MOB snapshot.");
    }

    private static void LeavesInt32OverflowWithoutAPlan()
    {

        var overflow = SellFixture.Create(
            LegacyNpcSellSourceKind.Carry,
            initialCoin: int.MaxValue - 5_000);
        var overflowStatus = LegacyNpcSellCompletionRules.TryBuild(
            overflow.Prepare(),
            overflow.Session.ConnectionId,
            overflow.GetRecycleBin(),
            randomValue: 0,
            out var overflowPlan);
        Check(overflowStatus == LegacyNpcSellCompletionBuildStatus.CoinOverflowUnverified && overflowPlan is null,
            "A signed int32 coin addition outside the source's defined range produced a sale plan.");
    }

    private static void CreditsCityAndGlobalTaxAndCanRollbackAtomically()
    {
        var fixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        fixture.AddTreasury(zone: 0, connectionId: 1_001, experience: 199_999_999_999L);
        fixture.AddTreasury(zone: 4, connectionId: 1_002, experience: 19_999_999_999_900L);
        var plan = fixture.BuildPlan();
        var beforeMob = fixture.GetMob();

        Check(fixture.World.TryCommitNpcSell(plan, out var outcome) == LegacyNpcSellCommitResult.Committed && outcome is not null,
            "A valid sale with both registered tax recipients failed to commit.");
        Check(fixture.GetTreasuryExperience(1_001) == 200_000_000_124L &&
              fixture.GetTreasuryExperience(1_002) == 20_000_000_000_025L,
            "The sale did not credit city then global tax EXP, preserving full-credit threshold overshoot.");

        Check(fixture.World.TryRemoveNpc(1_002, out _),
            "The test could not stale the second treasury before rollback.");
        Check(!fixture.World.TryRollbackNpcSell(outcome!) &&
              !fixture.GetMob().AsSpan().SequenceEqual(beforeMob) &&
              fixture.GetTreasuryExperience(1_001) == 200_000_000_124L,
            "Rollback partially restored the seller or first treasury after a later treasury changed.");
    }

    private static void ZoneFourCreditsOneTreasuryTwiceInSourceOrder()
    {
        var fixture = SellFixture.Create(
            LegacyNpcSellSourceKind.Carry,
            sellerX: 1_039,
            sellerY: 1_710,
            merchantX: 1_040,
            merchantY: 1_710);
        fixture.AddTreasury(zone: 4, connectionId: 1_001, experience: 1_000);

        Check(fixture.World.TryCommitNpcSell(fixture.BuildPlan(), out var outcome) == LegacyNpcSellCommitResult.Committed && outcome is not null &&
              fixture.GetTreasuryExperience(1_001) == 1_250,
            "Village 4 and the global zone-4 recipient did not credit the same treasury twice in order.");

        Check(outcome!.GuildTaxNpcUpdates.Count == 1,
            "The shared zone-4 recipient was not coalesced to one rollback snapshot.");
        Check(fixture.World.TryRollbackNpcSell(outcome) && fixture.GetTreasuryExperience(1_001) == 1_000,
            "Rollback did not restore the shared recipient to its pre-sale snapshot.");
    }

    private static void EnforcesStrictTaxExperienceCapsWithOvershoot()
    {
        var overshootFixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        overshootFixture.AddTreasury(zone: 0, connectionId: 1_001, experience: 199_999_999_999L);
        overshootFixture.AddTreasury(zone: 4, connectionId: 1_002, experience: 20_000_000_000_000L);
        Check(overshootFixture.World.TryCommitNpcSell(overshootFixture.BuildPlan(), out _) == LegacyNpcSellCommitResult.Committed &&
              overshootFixture.GetTreasuryExperience(1_001) == 200_000_000_124L &&
              overshootFixture.GetTreasuryExperience(1_002) == 20_000_000_000_000L,
            "The city threshold did not allow its source overshoot or the global strict cap was not enforced.");

        var globalOvershootFixture = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        globalOvershootFixture.AddTreasury(zone: 0, connectionId: 1_001, experience: 200_000_000_000L);
        globalOvershootFixture.AddTreasury(zone: 4, connectionId: 1_002, experience: 19_999_999_999_999L);
        Check(globalOvershootFixture.World.TryCommitNpcSell(globalOvershootFixture.BuildPlan(), out _) == LegacyNpcSellCommitResult.Committed &&
              globalOvershootFixture.GetTreasuryExperience(1_001) == 200_000_000_000L &&
              globalOvershootFixture.GetTreasuryExperience(1_002) == 20_000_000_000_124L,
            "Tax credit did not use strict less-than caps independently and preserve global overshoot.");
    }

    private static void MissingInactiveAndOutOfRangeTreasuriesDoNotRejectSale()
    {
        var missing = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        Check(missing.World.TryCommitNpcSell(missing.BuildPlan(), out _) == LegacyNpcSellCommitResult.Committed,
            "A missing tax recipient rejected a valid sale.");

        var zeroTax = SellFixture.Create(LegacyNpcSellSourceKind.Carry, cityTax: 0);
        zeroTax.AddTreasury(zone: 0, connectionId: 1_001, experience: 0);
        zeroTax.AddTreasury(zone: 4, connectionId: 1_002, experience: 0);
        Check(zeroTax.World.TryCommitNpcSell(zeroTax.BuildPlan(), out var zeroTaxOutcome) == LegacyNpcSellCommitResult.Committed &&
              zeroTaxOutcome is { GuildTaxNpcUpdates.Count: 0 } &&
              zeroTax.GetTreasuryExperience(1_001) == 0 && zeroTax.GetTreasuryExperience(1_002) == 0,
            "A zero tcoin sale credited a city/global treasury.");

        var inactive = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        inactive.AddTreasury(zone: 0, connectionId: 1_001, experience: 0);
        Check(inactive.World.SetNpcCombatState(1_001, LegacyNpcMode.Empty, LegacyNpcMode.EmptyTarget, [0]) &&
              inactive.World.TryCommitNpcSell(inactive.BuildPlan(), out _) == LegacyNpcSellCommitResult.Committed &&
              inactive.GetTreasuryExperience(1_001) == 0,
            "An inactive registered treasury received EXP or rejected a valid sale.");

        var outOfRange = SellFixture.Create(LegacyNpcSellSourceKind.Carry);
        outOfRange.AddTreasury(zone: 0, connectionId: LegacyMobAiPlanner.MaximumMobId, experience: 0);
        Check(outOfRange.World.TryCommitNpcSell(outOfRange.BuildPlan(), out _) == LegacyNpcSellCommitResult.Committed &&
              outOfRange.GetTreasuryExperience(LegacyMobAiPlanner.MaximumMobId) == 0,
            "A recipient at the exclusive MAX_MOB ID was credited or rejected the sale.");
    }

    private static void CreditsTheFirstNpcIdAtMaxUserBoundary()
    {
        var fixture = SellFixture.Create(
            LegacyNpcSellSourceKind.Carry,
            merchantConnectionId: 1_001);
        fixture.AddTreasury(zone: 0, connectionId: LegacyMobAiPlanner.MaximumPlayerId, experience: 0);

        Check(fixture.World.TryCommitNpcSell(fixture.BuildPlan(), out _) == LegacyNpcSellCommitResult.Committed &&
              fixture.GetTreasuryExperience(LegacyMobAiPlanner.MaximumPlayerId) == 125,
            "A tax NPC at MAX_USER was excluded even though the source accepts IDs >= MAX_USER.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SellFixture
    {
        private readonly FixedSnapshotStore accountStore;
        private readonly LegacyNpcSellSourceKind sourceKind;
        private readonly short sourcePosition;
        private readonly short merchantX;
        private readonly short merchantY;
        private readonly int merchantConnectionId;
        public LegacyAccountCargoRuntime CargoRuntime { get; } = new("UP");

        private SellFixture(
            WorldHub world,
            LoginSession session,
            FixedSnapshotStore accountStore,
            byte[] sellerMob,
            LegacyNpcSellSourceKind sourceKind,
            short sourcePosition,
            short merchantX,
            short merchantY,
            int merchantConnectionId,
            LegacyItem item)
        {
            World = world;
            Session = session;
            this.accountStore = accountStore;
            SellerMob = sellerMob;
            this.sourceKind = sourceKind;
            this.sourcePosition = sourcePosition;
            this.merchantX = merchantX;
            this.merchantY = merchantY;
            this.merchantConnectionId = merchantConnectionId;
            Item = item;
        }

        public WorldHub World { get; }
        public LoginSession Session { get; }
        public byte[] SellerMob { get; }
        public LegacyItem Item { get; }

        public static SellFixture Create(
            LegacyNpcSellSourceKind sourceKind,
            int initialCoin = 0,
            short sourcePosition = 0,
            short sellerX = 2_080,
            short sellerY = 2_140,
            short merchantX = 2_088,
            short merchantY = 2_148,
            int cityTax = 5,
            int merchantConnectionId = 1_000)
        {
            const string accountName = "SELL-COMMIT";
            var item = new LegacyItem(501, 1, 2, 3, 4, 5, 6);
            var cargoItems = new LegacyItem[LegacyAccountSnapshot.CargoCount];
            var sellerMob = NewMob("SELLER", merchant: 0);
            BinaryPrimitives.WriteInt32LittleEndian(
                sellerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset),
                initialCoin);
            BinaryPrimitives.WriteInt32LittleEndian(
                sellerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
                100);

            switch (sourceKind)
            {
                case LegacyNpcSellSourceKind.Equipment:
                    item.Write(sellerMob.AsSpan(
                        LegacyAccountSnapshot.MobEquipmentOffset + sourcePosition * LegacyItem.SizeInBytes,
                        LegacyItem.SizeInBytes));
                    break;
                case LegacyNpcSellSourceKind.Carry:
                    item.Write(sellerMob.AsSpan(
                        LegacyAccountSnapshot.MobCarryOffset + sourcePosition * LegacyItem.SizeInBytes,
                        LegacyItem.SizeInBytes));
                    break;
                case LegacyNpcSellSourceKind.Cargo:
                    cargoItems[sourcePosition] = item;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceKind));
            }

            var slots = Enumerable.Range(0, LegacyAccountSnapshot.CharacterCount)
                .Select(static _ => new LegacyCharacterSlot(
                    0, 0, string.Empty, default,
                    new LegacyItem[LegacyCharacterSelection.EquipmentCount], 0, 0, 0))
                .ToArray();
            var account = new LegacyAccountSnapshot(
                accountName,
                new LegacyCharacterSelection(slots),
                cargoItems,
                initialCoin,
                WelcomeItemReceived: false,
                DailyQuest: default,
                BlockPassword: string.Empty,
                IsBlocked: false);
            var guildZones = new LegacyGuildZoneState(
                guildCounter: 0,
                chargeGuilds: [0, 0, 0, 0, 0],
                challengerGuilds: [0, 0, 0, 0, 0],
                cityTaxes: [cityTax, cityTax, cityTax, cityTax, cityTax],
                clans: [0, 0, 0, 0, 0],
                victories: [0, 0, 0, 0, 0]);
            var world = new WorldHub(guildZones: guildZones);
            var session = new LoginSession(
                40,
                LoginSessionState.Playing,
                accountName,
                null,
                SecureVerified: true,
                CharacterSlot: 0,
                PositionX: sellerX,
                PositionY: sellerY);
            var fixture = new SellFixture(
                world,
                session,
                new FixedSnapshotStore(account),
                sellerMob,
                sourceKind,
                sourcePosition,
                merchantX,
                merchantY,
                merchantConnectionId,
                item);
            fixture.Initialize();
            return fixture;
        }

        public LegacyNpcSellPreparationOutcome Prepare() =>
            LegacyNpcSellPreparationCoordinator.PrepareAsync(
                Session,
                new ShopSellMessage((ushort)merchantConnectionId, (short)sourceKind, sourcePosition),
                World,
                new LegacyAutoTradeBook(),
                accountStore,
                CreateItemData(Item.Index, 80_000),
                cargoRuntime: CargoRuntime).GetAwaiter().GetResult();

        public LegacyNpcSellCompletionPlan BuildPlan()
        {
            var preparation = Prepare();
            var status = LegacyNpcSellCompletionRules.TryBuild(
                preparation,
                Session.ConnectionId,
                GetRecycleBin(),
                randomValue: 9,
                out var plan);
            Check(status == LegacyNpcSellCompletionBuildStatus.Ready && plan is not null,
                $"The fixture did not build a sale plan: preparation={preparation.Status}/{preparation.Flow?.Stage}, result={status}.");
            return plan!;
        }

        public byte[] GetMob()
        {
            Check(World.TryGetCharacterSnapshot(Session.ConnectionId, out var mob, out _, out _) && mob is not null,
                "The seller MOB snapshot was unavailable.");
            return mob!;
        }

        public LegacyRecycleBinEntry[] GetRecycleBin()
        {
            Check(World.TryGetRecycleBinSnapshot(Session.ConnectionId, out var entries) && entries is not null,
                "The seller recycle-bin snapshot was unavailable.");
            return entries!;
        }

        public LegacyItem[] GetClientEquipment()
        {
            Check(World.TryGetClientEquipmentSnapshot(Session.ConnectionId, out var equipment) && equipment is not null,
                "The seller's independent 7.69 equipment snapshot was unavailable.");
            return equipment!.ToArray();
        }

        public void AddTreasury(int zone, int connectionId, long experience)
        {
            var mob = NewMob($"TAX{zone}-{connectionId}", merchant: 6);
            new LegacyScore(zone, 0, 0, 0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0)
                .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            BinaryPrimitives.WriteInt64LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobExperienceOffset), experience);
            Check(World.EnterNpc(mob, 2_100, 2_100, requestedConnectionId: connectionId) == connectionId,
                $"The zone-{zone} tax NPC was not registered.");
        }

        public long GetTreasuryExperience(int connectionId)
        {
            Check(World.TryGetNpcSnapshot(connectionId, out var npc) && npc is not null,
                $"The treasury NPC {connectionId} was unavailable.");
            return BinaryPrimitives.ReadInt64LittleEndian(
                npc!.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobExperienceOffset));
        }

        public void SetClientEquipment(IReadOnlyList<LegacyItem> equipment)
        {
            var coin = BinaryPrimitives.ReadInt32LittleEndian(
                SellerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset));
            Check(World.SetCharacterState(
                    Session.ConnectionId,
                    Session.CharacterSlot,
                    guildId: 0,
                    clan: 0,
                    guildLevel: 0,
                    coin,
                    Session.PositionX,
                    Session.PositionY,
                    SellerMob,
                    LegacyAccountSnapshot.ClassMasterMortal,
                    new byte[LegacyAccountSnapshot.AffectStride],
                    ReadOnlyMemory<byte>.Empty,
                    equipment),
                "The independent client equipment state was not changed.");
        }

        public void SetSellerMob(byte[] mob, int coin)
        {
            Check(World.SetCharacterState(
                    Session.ConnectionId,
                    Session.CharacterSlot,
                    guildId: 0,
                    clan: 0,
                    guildLevel: 0,
                    coin,
                    Session.PositionX,
                    Session.PositionY,
                    mob,
                    LegacyAccountSnapshot.ClassMasterMortal,
                    new byte[LegacyAccountSnapshot.AffectStride]),
                "The changed seller MOB was not installed.");
        }

        private void Initialize()
        {
            Check(World.Enter(Session.ConnectionId, Session.AccountName!, NoopSend),
                "The seller did not enter the world.");
            Check(World.SetCharacterState(
                    Session.ConnectionId,
                    Session.CharacterSlot,
                    guildId: 0,
                    clan: 0,
                    guildLevel: 0,
                    coin: BinaryPrimitives.ReadInt32LittleEndian(SellerMob.AsSpan(LegacyAccountSnapshot.MobCoinOffset)),
                    Session.PositionX,
                    Session.PositionY,
                    SellerMob,
                    LegacyAccountSnapshot.ClassMasterMortal,
                    new byte[LegacyAccountSnapshot.AffectStride]),
                "The seller MOB was not installed.");
            Check(World.EnterNpc(NewMob("MERCHANT", merchant: 1), merchantX, merchantY, requestedConnectionId: merchantConnectionId) == merchantConnectionId,
                "The merchant NPC was not installed.");
        }

        private static LegacyItemDataTable CreateItemData(short itemIndex, int price)
        {
            var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(
                encoded.AsSpan((itemIndex * LegacyItemDataTable.RecordSizeInBytes) + 128),
                price ^ 0x5A5A5A5A);
            return LegacyItemDataTable.Load(encoded);
        }

        public static byte[] NewMob(string name, byte merchant)
        {
            var mob = new byte[LegacyAccountSnapshot.CharacterStride];
            Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
            mob[LegacyAccountSnapshot.MobMerchantOffset] = merchant;
            return mob;
        }

        private static ValueTask NoopSend(ReadOnlyMemory<byte> _, CancellationToken __) => ValueTask.CompletedTask;

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
    }
}
