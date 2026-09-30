using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

// Detects missing early HP/mode gates, HP<=0 instead of HP==0, and private response leaks.
internal static class TradeListGateNetwork
{
    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile!;
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var peerHeader = new AccountHeader(CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());
        var hp = options.TradeListNegativeHp ? -1 : 0;
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "LIST_ACTOR_", verifyTradeListModeGate: true))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "LIST_OWNER_"))
        {
            var actorId = Id(actor.CharacterLoginFrame); var peerId = Id(peer.CharacterLoginFrame);
            Check(actorId > 0 && peerId > 0 && actorId != peerId && Hp(actor.CharacterLoginFrame) == hp && Hp(peer.CharacterLoginFrame) > 0, "Wrong list fixture/IDs.");
            Console.WriteLine($"LIST_ACTOR_ID={actorId}");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await Phase("LIST-READY", false);
            var items = new LegacyItem[12]; items[0] = LegacyItem.Read(peerBefore.AsSpan(3480 + 4 * 8, 8));
            Check(items[0].Index == 900, "Owner fixture must offer item900 from cargo4.");
            var positions = Enumerable.Repeat((sbyte)-1, 12).ToArray(); positions[0] = 4;
            var prices = new int[12]; prices[0] = 100;
            await peer.Stream.WriteAsync(new AutoTradeStartRequest("LIST-SMOKE", items, positions, prices).ToFrame(codec, 120, 16, peerId));
            var opened = await Phase("LIST-OPEN", true);
            var opening = opened.Peer.Single(f => f.Header.Type == AutoTradeListConfirmation.MessageType);
            AssertListing(opening, peerId, items[0]);
            var openingVisual = opened.Peer.Single(f => f.Header.Type == 0x0363);
            AssertVisual([openingVisual], peerId, openingVisual);
            AssertVisual(opened.Actor, peerId, openingVisual);
            var durableListing = await File.ReadAllBytesAsync(options.AutoTradeStateFile!);

            // Target zero proves the requester gate runs before target lookup; negative HP gets no response.
            await actor.Stream.WriteAsync(new TradeListRequest(0).ToFrame(codec, 121, 17, actorId));
            var invalid = await Phase("LIST-INVALID", false);
            AssertQuery(invalid, hp == 0, null);
            await actor.Stream.WriteAsync(new TradeListRequest(peerId).ToFrame(codec, 122, 18, actorId));
            var query = await Phase("LIST-QUERY", false);
            AssertQuery(query, hp == 0, hp == 0 ? null : opening);

            // Positive-HP owner can query; accepted queries must not add crack points.
            await peer.Stream.WriteAsync(new TradeListRequest(peerId).ToFrame(codec, 123, 19, peerId));
            var living = await Phase("LIST-LIVING", true);
            AssertListing(living.Peer.Single(f => f.Header.Type == AutoTradeListConfirmation.MessageType), peerId, items[0]);
            AssertVisual(living.Peer, peerId, openingVisual);
            MotionGateNetwork.AssertNoHpMode(living.Actor.Concat(living.Peer).ToArray());
            Check(!living.Actor.Any(f => f.Header.Type is AutoTradeListConfirmation.MessageType or 0x0363), "Living query leaked to neighbor.");
            Check((await File.ReadAllBytesAsync(options.AutoTradeStateFile!)).SequenceEqual(durableListing), "Queries mutated durable shop.");
            foreach (var target in new[] { 0, -1, 65536 })
                await peer.Stream.WriteAsync(new TradeListRequest(target).ToFrame(codec, 123, 19, peerId));
            var invalidLiving = await Phase("LIST-LIVING-INVALID", true);
            Check(!invalidLiving.Actor.Concat(invalidLiving.Peer).Any(f => f.Header.Type is SetHpModeConfirmation.MessageType or AutoTradeListConfirmation.MessageType or 0x0363), "Invalid living target emitted response.");
            await actor.Stream.WriteAsync(TradeCloseRequest.ToFrame(codec, 124, 20, actorId));
            var quit = await Phase("LIST-QUIT", false);
            MotionGateNetwork.AssertHpMode(quit.Actor, actorId, hp);
            MotionGateNetwork.AssertNoHpMode(quit.Peer);
            Check(!quit.Actor.Concat(quit.Peer).Any(f => f.Header.Type is TradeCloseRequest.MessageType or PkInfoConfirmation.MessageType), "Dead QuitTrade changed trade.");
            await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
            await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
            await actor.LogoutAsync(); await peer.LogoutAsync();

            Task<(IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer)> Phase(string label, bool peerFirst)
                => AutoTradeCloseNetwork.CapturePhase(actor.Stream, peer.Stream, actorId, peerId, codec, options.Timeout, label, peerFirst);
            void AssertQuery((IReadOnlyList<DecodedFrame> Actor, IReadOnlyList<DecodedFrame> Peer) frames, bool dead, DecodedFrame? expected)
            {
                MotionGateNetwork.AssertNoHpMode(frames.Peer);
                Check(!frames.Peer.Any(f => f.Header.Type is AutoTradeListConfirmation.MessageType or 0x0363), "Query response leaked to shop owner.");
                if (dead) MotionGateNetwork.AssertHpMode(frames.Actor, actorId, 0);
                else MotionGateNetwork.AssertNoHpMode(frames.Actor);
                if (expected is not null)
                {
                    var listed = frames.Actor.Single(f => f.Header.Type == AutoTradeListConfirmation.MessageType);
                    AssertListing(listed, peerId, items[0]);
                    Check(listed.Payload.Span.SequenceEqual(expected.Value.Payload.Span), "Queried list differs from owner opening.");
                    AssertVisual(frames.Actor, peerId, openingVisual);
                }
                else Check(!frames.Actor.Any(f => f.Header.Type is AutoTradeListConfirmation.MessageType or 0x0363), "Rejected query emitted listing/visual.");
            }
        }
        await using (var actor = await E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "LIST_RELOGIN_"))
        await using (var peer = await E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "LIST_OWNER_RELOGIN_"))
        {
            Check(Hp(actor.CharacterLoginFrame) == hp, "Query mutated persisted HP.");
            MotionGateNetwork.AssertLogin(actor.CharacterLoginFrame, actorBefore, options.Slot);
            MotionGateNetwork.AssertLogin(peer.CharacterLoginFrame, peerBefore, options.Slot);
            await actor.LogoutAsync(); await peer.LogoutAsync();
        }
        await MotionGateNetwork.AssertDisk(options.AccountFile, actorBefore, options.Slot);
        await MotionGateNetwork.AssertDisk(peerFile, peerBefore, options.Slot);
        Console.WriteLine($"CLIENT_E2E_PASS trade-list-gates hp={hp} private-hp-list196-visual260 shared-counter carry-cargo-gold-relogin");
    }

    internal static void AssertVisual(IReadOnlyList<DecodedFrame> frames, ushort owner, DecodedFrame expected)
    {
        var visuals = frames.Where(f => f.Header.Type == 0x0363).ToArray();
        Check(visuals.Length == 1 && visuals[0].Header.Size == 260 &&
            BinaryPrimitives.ReadUInt16LittleEndian(visuals[0].Payload.Span[CreateMobTradeConfirmationV769.MobIdOffset..]) == owner &&
            visuals[0].Payload.Span.SequenceEqual(expected.Payload.Span), "Shop visual260 identity/payload missing or changed.");
    }

    private static void AssertListing(DecodedFrame frame, ushort owner, LegacyItem item)
        => Check(frame.Header.Size == 196 && AutoTradeListConfirmation.TryParse(frame, out var list) && list is not null &&
            list.TargetId == owner && list.Items[0] == item && list.CarryPositions[0] == 4 && list.TradeMoney[0] == 100 && list.Tax == 5,
            "List196 differs from authoritative cargo/price/tax.");
    private static int Hp(DecodedFrame frame) => BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span[(CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.CurrentScoreOffset + 24)..]);
    private static ushort Id(DecodedFrame frame) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
    private static string CString(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).Split('\0')[0];
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
