using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WydCdk.Protocol;

// Test-only network assertions. Removing the host's durable shop close or
// normal CreateMob relay must fail this smoke, unlike a bilateral-only test.
internal static class AutoTradeCloseNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var peerBytes = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBytes.AsSpan(0, 16)), CString(peerBytes.AsSpan(16, 12)), peerBytes.AsSpan(202, 6).ToArray());
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = peerBytes.ToArray();
        var item = LegacyItem.Read(actorBefore.AsSpan(3480 + 4 * 8, 8));
        Check(item.Index == 900, "Autotrade fixture must offer cargo slot4 item900.");
        LegacyItem[] actorCarry, peerCarry;
        int actorCoin, peerCoin;
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "SHOP_OWNER_", verifyQuitTradeModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "SHOP_PEER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame); var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId, "Shop needs distinct server IDs.");
            actorCarry = Carry(actor.CharacterLoginFrame); peerCarry = Carry(peer.CharacterLoginFrame);
            actorCoin = Coin(actor.CharacterLoginFrame); peerCoin = Coin(peer.CharacterLoginFrame);
            Check(actorCarry[63].Effect2 == 0, "Autotrade neutral-PK fixture has nonzero guilty.");
            foreach (var session in new[] { actor, peer })
            {
                var p = session.CharacterLoginFrame.Payload.Span;
                var x = BinaryPrimitives.ReadInt16LittleEndian(p); var y = BinaryPrimitives.ReadInt16LittleEndian(p[2..]);
                Check(x is >= 2052 and <= 2171 && y is >= 2052 and <= 2163 && !(x is >= 2123 and <= 2148 && y is >= 2139 and <= 2157),
                    "Shop fixture did not spawn in allowed Armia village outside protected rectangle.");
            }
            await Phase(actor, peer, actorId, peerId, codec, options.Timeout, "READY");
            var items = new LegacyItem[12]; items[0] = item;
            var positions = Enumerable.Repeat((sbyte)-1, 12).ToArray(); positions[0] = 4;
            var prices = new int[12]; prices[0] = 100;
            await actor.Stream.WriteAsync(new AutoTradeStartRequest("SHOP-SMOKE", items, positions, prices).ToFrame(codec, 40, 16, actorId));
            var ownerList = await Until(actor.Stream, codec, options.Timeout, AutoTradeListConfirmation.MessageType);
            Check(AutoTradeListConfirmation.TryParse(ownerList, out var listing) && listing is not null && listing.TargetId == actorId &&
                listing.Items[0] == item && listing.CarryPositions[0] == 4 && listing.TradeMoney[0] == 100 && listing.Tax == 5,
                "Owner opening list differs from authoritative cargo/price/tax.");
            Check((await Until(actor.Stream, codec, options.Timeout, 0x0363)).Header.Size == 260, "Owner did not receive shop visual260.");
            Check((await Until(peer.Stream, codec, options.Timeout, 0x0363)).Header.Size == 260, "Neighbor did not receive shop visual260.");
            Check(await ListingCount(options.AutoTradeStateFile!) == 1, "Opening relay preceded persisted listing.");

            var attackDuringShop = BuildAttack(codec, actorId, peerId, 42, 18);
            var decodedAttack = codec.Decode(attackDuringShop);
            var attackParsed = AttackRequest.TryParse(decodedAttack, out var parsedAttack);
            var hasDamageSlot = AttackRequest.TryReadDamageSlot(decodedAttack, 0, out var targetId, out var damage);
            Check(attackParsed && parsedAttack is not null && parsedAttack.AttackerId == actorId && hasDamageSlot &&
                targetId == peerId && damage == -2,
                $"Attack fixture is invalid: checksum={decodedAttack.IsChecksumValid}, type=0x{decodedAttack.Header.Type:X4}, size={decodedAttack.Header.Size}.");
            await actor.Stream.WriteAsync(attackDuringShop);
            var blockedAttack = await CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec,
                options.Timeout, "ATTACK-DURING-SHOP", peerFirst: false);
            AssertTradeModeAttackBlocked(blockedAttack.Actor, actorId);
            Check(!blockedAttack.Peer.Any(IsAttackFrame) &&
                !blockedAttack.Peer.Any(frame => frame.Header.Type is MessagePanelConfirmation.MessageType or SetHpMpConfirmation.MessageType),
                "Attack during auto-trade reached the observer or emitted a resource update.");
            Check(await ListingCount(options.AutoTradeStateFile!) == 1, "Attack during auto-trade changed the active listing.");

            await peer.Stream.WriteAsync(new TradeListRequest(actorId).ToFrame(codec, 41, 17, peerId));
            var queried = await Until(peer.Stream, codec, options.Timeout, AutoTradeListConfirmation.MessageType);
            Check(AutoTradeListConfirmation.TryParse(queried, out var query) && query is not null && query.TargetId == actorId &&
                query.Items.SequenceEqual(listing!.Items) && query.TradeMoney.SequenceEqual(listing.TradeMoney), "Query differs from opening listing.");
            Check((await Until(peer.Stream, codec, options.Timeout, 0x0363)).Header.Size == 260, "Query shop visual absent.");
            await Phase(actor, peer, actorId, peerId, codec, options.Timeout, "OPEN");

            var emptyTradeItems = new LegacyItem[TradeOfferRequest.ItemCount];
            var emptyTradePositions = Enumerable.Repeat((sbyte)-1, TradeOfferRequest.ItemCount).ToArray();
            await peer.Stream.WriteAsync(new TradeOfferRequest(emptyTradeItems, emptyTradePositions, 0, 0, actorId)
                .ToFrame(codec, 41, 17, peerId));
            var stagedTrade = await AutoTradeCloseNetwork.CapturePhase(
                actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, "ACTION-TRADE-STAGED", peerFirst: true);
            Check(stagedTrade.Actor.Any(frame => frame.IsChecksumValid && frame.Header.Type == TradeOfferRequest.MessageType),
                "Bilateral trade was not staged alongside the online AFK shop.");

            var spawnX = BinaryPrimitives.ReadInt16LittleEndian(actor.CharacterLoginFrame.Payload.Span);
            var spawnY = BinaryPrimitives.ReadInt16LittleEndian(actor.CharacterLoginFrame.Payload.Span[2..]);
            var action = new ActionRequest(spawnX, spawnY, 0, 2, new byte[24], spawnX, spawnY);
            await actor.Stream.WriteAsync(action.ToFrame(codec, 42, 18, actorId));
            var actionClose = await Phase(actor, peer, actorId, peerId, codec, options.Timeout, "ACTION-CLOSE");
            AssertActionClose(actionClose.Actor, actorId, actorId, 1);
            AssertActionClose(actionClose.Peer, actorId, peerId, 1);
            Check(await ListingCount(options.AutoTradeStateFile!) == 0, "Action left the AFK shop in durable state.");
            await AssertDisk(options.AccountFile, options.Slot, actorCarry, actorCoin, actorBefore);
            await AssertDisk(peerFile, options.Slot, peerCarry, peerCoin, peerBefore);

            await actor.Stream.WriteAsync(new AutoTradeStartRequest("SHOP-REOPEN", items, positions, prices).ToFrame(codec, 43, 19, actorId));
            var reopenedListFrame = await Until(actor.Stream, codec, options.Timeout, AutoTradeListConfirmation.MessageType);
            Check(AutoTradeListConfirmation.TryParse(reopenedListFrame, out var reopenedList) && reopenedList is not null && reopenedList.TargetId == actorId,
                "Owner could not reopen the AFK shop after Action closed it.");
            Check((await Until(actor.Stream, codec, options.Timeout, 0x0363)).Header.Size == 260 &&
                (await Until(peer.Stream, codec, options.Timeout, 0x0363)).Header.Size == 260,
                "Reopened AFK shop visual did not reach owner and neighbor.");
            Check(await ListingCount(options.AutoTradeStateFile!) == 1, "Reopened AFK shop was not durable.");
            await Phase(actor, peer, actorId, peerId, codec, options.Timeout, "REOPENED");

            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 42, 18, actorId));
            var close = await Phase(actor, peer, actorId, peerId, codec, options.Timeout, "CLOSE");
            AssertClose(close.Actor, actorId, 1, true);
            AssertClose(close.Peer, actorId, 0, true);
            Check(await ListingCount(options.AutoTradeStateFile!) == 0, "QuitTrade left durable shop active.");
            await AssertDisk(options.AccountFile, options.Slot, actorCarry, actorCoin, actorBefore);
            await AssertDisk(peerFile, options.Slot, peerCarry, peerCoin, peerBefore);
            // A purchase after close must not sell anything, even with the exact old offer.
            await peer.Stream.WriteAsync(new AutoTradePurchaseRequest(0, actorId, 100, 5, item).ToFrame(codec, 43, 19, peerId));
            var purchase = await Phase(actor, peer, actorId, peerId, codec, options.Timeout, "BUY-REJECT");
            Check(!purchase.Actor.Concat(purchase.Peer).Any(frame => frame.Header.Type == UpdateCarryConfirmation.MessageType || frame.Header.Type == ItemSoldConfirmation.MessageType || frame.Header.Type is 0x0363 or 0x0364),
                "Purchase after close emitted sale/carry/shop visual.");
            for (var repeat = 0; repeat < 2; repeat++) await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, (uint)(44 + repeat), 20, actorId));
            var retry = await Phase(actor, peer, actorId, peerId, codec, options.Timeout, "RETRY");
            AssertClose(retry.Actor, actorId, 2, false);
            AssertClose(retry.Peer, actorId, 0, false);
            Check(await ListingCount(options.AutoTradeStateFile!) == 0, "Retry recreated shop.");
            await AssertDisk(options.AccountFile, options.Slot, actorCarry, actorCoin, actorBefore);
            await AssertDisk(peerFile, options.Slot, peerCarry, peerCoin, peerBefore);
            await actor.LogoutAsync(); await peer.LogoutAsync();
        }
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "SHOP_RELOGIN_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "PEER_RELOGIN_"))
        {
            Check(Carry(actor.CharacterLoginFrame).SequenceEqual(actorCarry) && Coin(actor.CharacterLoginFrame) == actorCoin &&
                Carry(peer.CharacterLoginFrame).SequenceEqual(peerCarry) && Coin(peer.CharacterLoginFrame) == peerCoin,
                "Autotrade close/retry changed carry or Gold at relogin.");
            await actor.LogoutAsync(); await peer.LogoutAsync();
        }
        await AssertDisk(options.AccountFile, options.Slot, actorCarry, actorCoin, actorBefore);
        await AssertDisk(peerFile, options.Slot, peerCarry, peerCoin, peerBefore);
        Console.WriteLine("CLIENT_E2E_PASS autotrade-open-query-close-normal236-retry-buy-rejected-carry-cargo-gold-relogin");
    }

    private static void AssertActionClose(IReadOnlyList<DecodedFrame> frames, ushort ownerId, ushort recipientId, int signals)
    {
        var visuals = frames.Where(frame => frame.Header.Type == 0x0364).ToArray();
        Check(visuals.Length == 1 && visuals[0].Header.Size == 236 && visuals[0].IsChecksumValid &&
            visuals[0].Header.Id == CreateMobConfirmationV769.SceneId && visuals[0].Header.ClientTick == 0 && visuals[0].Header.KeywordIndex == 0 &&
            BinaryPrimitives.ReadUInt16LittleEndian(visuals[0].Payload.Span[4..]) == ownerId,
            "Action close did not multicast one normal zero-header CreateMob to each view.");
        var cancellations = frames.Where(frame => frame.Header.Type == TradeCloseRequest.MessageType).ToArray();
        Check(cancellations.Length == signals && cancellations.All(frame => frame.Header.Size == 12 && frame.IsChecksumValid &&
            frame.Header.Id == recipientId && frame.Header.ClientTick == 0 && frame.Header.KeywordIndex == 0),
            "Action close emitted the wrong private RemoveTrade signal.");
        Check(!frames.Any(frame => frame.Header.Type is 0x0363 or 0x0366 or 0x0165 || ActionRequest.TryParse(frame, out _)),
            "Action close emitted a shop visual, PK refresh, RemoveMob, or Action relay.");
    }

    internal static void AssertClose(IReadOnlyList<DecodedFrame> frames, ushort actorId, int signals, bool visual)
    {
        var creates = frames.Where(frame => frame.Header.Type == 0x0364).ToArray();
        Check(creates.Length == (visual ? 1 : 0) && creates.All(frame => frame.Header.Size == 236 &&
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[4..]) == actorId), "Autotrade close missing normal236 or duplicated shop visual.");
        var cancellations = frames.Where(frame => frame.Header.Type == TradeCloseRequest.MessageType).ToArray();
        Check(cancellations.Length == signals && cancellations.All(frame => frame.Header.Size == 12 && frame.Header.Id == actorId),
            "Autotrade close has wrong cancellation signals.");
        Check(!frames.Any(frame => frame.Header.Type == 0x0165), "QuitTrade removed the player instead of restoring normal visual.");
        Check(!frames.Any(frame => frame.Header.Type == 0x0363), "QuitTrade/retry emitted stale shop visual.");
        var pkFrames = frames.Where(frame => frame.Header.Type == PkInfoConfirmation.MessageType).ToArray();
        Check(pkFrames.Length == (visual ? 1 : 2) && pkFrames.All(frame => frame.Header.Size == 16 && frame.Header.Id == actorId &&
            frame.Payload.Length == 4 && BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 0),
            "Autotrade close PK refresh absent or wrong author.");
    }

    private static Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(
        E2eSession actor, E2eSession peer, ushort actorId, ushort peerId, LegacyFrameCodec codec, TimeSpan timeout, string label)
        => CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, timeout, label, peerFirst: label == "BUY-REJECT");

    internal static async Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> CapturePhase(
        NetworkStream actor, NetworkStream peer, ushort actorId, ushort peerId, LegacyFrameCodec codec, TimeSpan timeout, string label, bool peerFirst)
    {
        var marker = "SHOP-" + label;
        if (peerFirst)
        {
            await peer.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 81, 22, peerId));
            var actorReplies = await ThroughMarker(actor, codec, timeout, peerId, marker);
            await actor.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 80, 21, actorId));
            var peerReplies = await ThroughMarker(peer, codec, timeout, actorId, marker);
            return (actorReplies, peerReplies);
        }
        await actor.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 80, 21, actorId));
        var peerFrames = await ThroughMarker(peer, codec, timeout, actorId, marker);
        await peer.WriteAsync(new MessageChatConfirmation(marker).ToFrame(codec, 81, 22, peerId));
        var actorFrames = await ThroughMarker(actor, codec, timeout, peerId, marker);
        return (actorFrames, peerFrames);
    }

    private static async Task<IReadOnlyList<DecodedFrame>> ThroughMarker(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, ushort id, string marker)
    {
        var frames = new List<DecodedFrame>();
        for (var i = 0; i < 64; i++)
        {
            var frame = await Read(stream, codec, timeout);
            if (frame.Header.Type == MessageChatRequest.MessageType && frame.Header.Id == id && CString(frame.Payload.Span) == marker) return frames;
            frames.Add(frame);
        }
        throw new InvalidOperationException("Shop processing barrier missing.");
    }
    private static async Task<DecodedFrame> Until(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, ushort type)
    {
        for (var i = 0; i < 64; i++) { var frame = await Read(stream, codec, timeout); if (frame.Header.Type == type) return frame; }
        throw new InvalidOperationException("Shop response missing.");
    }
    private static async Task<DecodedFrame> Read(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var ct = new CancellationTokenSource(timeout);
        var header = new byte[12]; await stream.ReadExactlyAsync(header, ct.Token);
        var size = PacketHeader.Read(header).Size; Check(size >= 12 && size <= PacketHeader.MaxPacketSize, "Invalid shop frame size.");
        var bytes = new byte[size]; header.CopyTo(bytes, 0); await stream.ReadExactlyAsync(bytes.AsMemory(12), ct.Token);
        var frame = codec.Decode(bytes); Check(frame.IsChecksumValid, "Shop frame checksum invalid.");
        Console.WriteLine($"SHOP_RX type=0x{frame.Header.Type:X4} size={size} checksum=True"); return frame;
    }
    private static async Task<int> ListingCount(string path)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(path)); return doc.RootElement.GetProperty("Listings").GetArrayLength();
    }

    private static byte[] BuildAttack(LegacyFrameCodec codec, ushort actorId, ushort targetId, uint tick, byte keyword)
    {
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(30), actorId);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(48), targetId);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(52), -2);
        return codec.Encode(AttackRequest.OneTargetMessageType, actorId, tick, payload, keyword);
    }

    private static void AssertTradeModeAttackBlocked(IReadOnlyList<DecodedFrame> frames, ushort actorId)
    {
        var expectedText = Encoding.Latin1.GetBytes("N\u00E3o \u00E9 possi\u00EDvel durante a auto venda.");
        var expectedPayload = new byte[MessagePanelConfirmation.MessageLength];
        expectedText.CopyTo(expectedPayload, 0);
        var notices = frames.Where(frame => frame.Header.Type == MessagePanelConfirmation.MessageType).ToArray();
        Check(frames.Count == 1 && notices.Length == 1 && notices[0].IsChecksumValid &&
            notices[0].Header.Size == MessagePanelConfirmation.PacketSize && notices[0].Header.Id == 0 &&
            notices[0].Header.ClientTick == 0 && notices[0].Header.KeywordIndex == 0 &&
            notices[0].Payload.Span.SequenceEqual(expectedPayload),
            "Attack during auto-trade did not return the exact private legacy MessagePanel 170.");
        Check(!frames.Any(IsAttackFrame) && !frames.Any(frame => frame.Header.Type == SetHpMpConfirmation.MessageType),
            $"Attack during auto-trade mutated combat resources or relayed to the owner {actorId}.");
    }

    private static bool IsAttackFrame(DecodedFrame frame) => frame.Header.Type is
        AttackRequest.AreaMessageType or AttackRequest.OneTargetMessageType or AttackRequest.TwoTargetMessageType;

    private static async Task AssertDisk(string path, int slot, LegacyItem[] carry, int coin, byte[] baseline)
    {
        var bytes = await File.ReadAllBytesAsync(path); var offset = 216 + slot * 816;
        Check(Enumerable.Range(0, 64).Select(i => LegacyItem.Read(bytes.AsSpan(offset + 268 + i * 8, 8))).SequenceEqual(carry) &&
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 28)) == coin && bytes.AsSpan(3480, 128 * 8).SequenceEqual(baseline.AsSpan(3480, 128 * 8)),
            "Shop close or rejected purchase changed durable carry/cargo/Gold.");
    }
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static int Coin(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CoinOffset)..]);
    private static LegacyItem[] Carry(DecodedFrame frame) => Enumerable.Range(0, 64).Select(i => LegacyItem.Read(frame.Payload.Span.Slice(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CarryOffset + i * 8, 8))).ToArray();
    private static string CString(ReadOnlySpan<byte> bytes) { var end = bytes.IndexOf((byte)0); return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]); }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
