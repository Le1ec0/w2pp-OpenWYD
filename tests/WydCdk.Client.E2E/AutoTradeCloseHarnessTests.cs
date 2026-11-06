using System.Net;
using System.Net.Sockets;
using WydCdk.Protocol;

internal static class AutoTradeCloseHarnessTests
{
    public static async Task RunAsync()
    {
        var scaledHealRoot = Path.Combine(Path.GetTempPath(), "Backup", "Build", "AutoTradeCloseSmoke", "scaled-heal-harness-test");
        var scaledHealArgs = new[] { "--host", "127.0.0.1", "--port", "18283", "--account-file",
            Path.Combine(scaledHealRoot, "account", "T", "TESTE2"), "--autotrade-close-peer-account-file",
            Path.Combine(scaledHealRoot, "account", "T", "TESTE"), "--autotrade-state-file",
            Path.Combine(scaledHealRoot, "autotrade.json"), "--attack-skill-heal-smoke",
            "--expected-double-mode", "3" };
        var scaledHealAccepted = false;
        try
        {
            var parsed = E2eOptions.Parse(scaledHealArgs);
            scaledHealAccepted = parsed.AttackSkillHealSmoke &&
                parsed.GetType().GetProperty("ExpectedDoubleModeMultiplier")?.GetValue(parsed) is 3;
        }
        catch (ArgumentException) { }
        Check(scaledHealAccepted, "Attack heal smoke cannot assert a non-default DoubleMode FakeExp multiplier.");

        var root = Path.Combine(Path.GetTempPath(), "Backup", "Build", "AutoTradeCloseSmoke", "nonexistent-guard-fixture");
        var args = new[] { "--host", "127.0.0.1", "--account-file", Path.Combine(root, "account", "T", "TESTE2"),
            "--autotrade-close-peer-account-file", Path.Combine(root, "account", "T", "TESTE"), "--autotrade-state-file", Path.Combine(root, "autotrade.json") };
        var blessingRoot = Path.Combine(Path.GetTempPath(), "Backup", "Build", "AutoTradeCloseSmoke", "divine-blessing-harness-test");
        var blessingActor = Path.Combine(blessingRoot, "account", "T", "TESTE2");
        var blessingObserver = Path.Combine(blessingRoot, "account", "T", "TESTE");
        var blessingDistantObserver = Path.Combine(blessingRoot, "account", "T", "TESTE4");
        var blessingArgs = new[] { "--host", "127.0.0.1", "--port", "18283", "--account-file", blessingActor,
            "--autotrade-close-peer-account-file", blessingObserver, "--autotrade-state-file", Path.Combine(blessingRoot, "autotrade.json"),
            "--attack-divine-blessing-distant-observer-account-file", blessingDistantObserver,
            "--attack-divine-blessing-smoke" };
        var blessingAccepted = false;
        try
        {
            var parsed = E2eOptions.Parse(blessingArgs);
            blessingAccepted = parsed.GetType().GetProperty("AttackDivineBlessingSmoke")?.GetValue(parsed) is true;
        }
        catch (ArgumentException) { }
        Check(blessingAccepted, "Divine Blessing Attack loopback mode unavailable.");
        var lowManaArgs = blessingArgs[..^1].Concat(new[] { "--attack-divine-blessing-insufficient-mana-smoke" }).ToArray();
        var lowManaAccepted = false;
        try
        {
            var parsed = E2eOptions.Parse(lowManaArgs);
            lowManaAccepted = parsed.GetType().GetProperty("AttackDivineBlessingInsufficientManaSmoke")?.GetValue(parsed) is true;
        }
        catch (ArgumentException) { }
        Check(lowManaAccepted, "Divine Blessing insufficient-mana loopback mode unavailable.");
        var rejected = false;
        try { E2eOptions.Parse(blessingArgs.Select((value, index) => index == 3 ? "8281" : value).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Divine Blessing Attack accepted the normal port 8281.");
        rejected = false;
        try { E2eOptions.Parse(blessingArgs.Select((value, index) => index == 1 ? "192.0.2.1" : value).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Divine Blessing Attack accepted a non-loopback host.");
        rejected = false;
        try { E2eOptions.Parse(blessingArgs.Select((value, index) => index == 7 ? blessingActor : value).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Divine Blessing Attack accepted duplicate actor/observer fixture paths.");
        rejected = false;
        try { E2eOptions.Parse(blessingArgs.Select((value, index) => index == 11 ? blessingActor : value).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Divine Blessing Attack accepted a distant observer that aliases the actor fixture.");
        var originalActor = Path.Combine(Path.GetTempPath(), "Backup", "Build", "E2E7670", "account", "T", "TESTE2");
        rejected = false;
        try { E2eOptions.Parse(blessingArgs.Select((value, index) => index == 5 ? originalActor : value).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Divine Blessing Attack accepted an original E2E fixture path.");
        // Parse only: no files, credentials or connections needed for the unsafe-port RED.
        try { E2eOptions.Parse(args); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Autotrade parser accepted the normal port8281.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--motion-gate-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Motion parser accepted the normal port8281.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--motion-negative-hp" }).ToArray()).MotionNegativeHp, "Motion guarded mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--trade-list-gate-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "ReqTradeList parser accepted port8281.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--trade-list-negative-hp" }).ToArray()).TradeListNegativeHp, "ReqTradeList guarded mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--trade-list-gate-smoke", "--motion-gate-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting ReqTradeList/Motion modes accepted.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--action-gate-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Action parser accepted port8281.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-negative-hp" }).ToArray()).ActionNegativeHp, "Action guarded mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--attack-dead-gate-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Dead Attack parser accepted port8281.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-dead-gate-smoke" }).ToArray()).AttackDeadGateSmoke,
            "Dead Attack guarded mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--attack-golden-shield-coin-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Golden Shield coin parser accepted the normal port 8281.");
        var goldenShieldMode = E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-golden-shield-coin-smoke" }).ToArray());
        Check(goldenShieldMode.AttackGoldenShieldCoinSmoke, "Golden Shield coin loopback mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-golden-shield-coin-smoke", "--attack-skill-gate-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting Golden Shield and Attack skill gate modes accepted.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--attack-mana-control-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Physical ManaControl parser accepted the normal port8281.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-mana-control-smoke" }).ToArray()).AttackManaControlSmoke,
            "Physical ManaControl loopback mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--attack-type6-protection-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Type 6 protection parser accepted the normal port8281.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-type6-protection-smoke" }).ToArray()).AttackType6ProtectionSmoke,
            "Type 6 protection loopback mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--attack-party-skill-gate-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Party skill gate parser accepted the normal port8281.");
        var partySkillMode = E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-party-skill-gate-smoke" }).ToArray());
        Check(partySkillMode.AttackPartySkillGateSmoke, "Party skill gate loopback mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-party-skill-gate-smoke", "--attack-skill-gate-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting party and general Attack skill gate modes accepted.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-type6-protection-smoke", "--attack-multi-target-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting Type 6 protection/multi-target smoke modes accepted.");
        rejected = false;
        var mixedFixtureArgs = args.Concat(new[] { "--attack-mixed-out-of-range-account-file", Path.Combine(root, "account", "T", "TESTE3") }).ToArray();
        try { E2eOptions.Parse(mixedFixtureArgs.Concat(new[] { "--attack-mixed-slots-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Mixed-slot Attack parser accepted the normal port 8281.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-mixed-slots-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Mixed-slot Attack parser accepted a missing out-of-range fixture.");
        var mixedMode = E2eOptions.Parse(mixedFixtureArgs.Concat(new[] { "--port", "18283", "--attack-mixed-slots-smoke" }).ToArray());
        Check(mixedMode.AttackMixedSlotsSmoke, "Mixed-slot Attack loopback mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(mixedFixtureArgs.Concat(new[] { "--host", "192.0.2.1", "--port", "18283", "--attack-mixed-slots-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Mixed-slot Attack parser accepted a non-loopback host.");
        rejected = false;
        var duplicateMixedFixtureArgs = mixedFixtureArgs.Concat(new[] {
            "--autotrade-close-peer-account-file", Path.Combine(root, "account", "T", "TESTE3") }).ToArray();
        try { E2eOptions.Parse(duplicateMixedFixtureArgs.Concat(new[] { "--port", "18283", "--attack-mixed-slots-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Mixed-slot Attack parser accepted a duplicate peer/out-of-range fixture.");
        rejected = false;
        try { E2eOptions.Parse(mixedFixtureArgs.Concat(new[] { "--port", "18283", "--attack-mixed-slots-smoke", "--attack-multi-target-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting mixed-slot/multi-target Attack smoke modes accepted.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--attack-dead-gate-smoke", "--action-gate-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting dead Attack/Action modes accepted.");
        var recallFlags = new[] { "--action-origin-recall-smoke",
            "--action-recall-destination-observer-account-file", Path.Combine(root, "account", "T", "TESTE3"),
            "--action-recall-distant-observer-account-file", Path.Combine(root, "account", "T", "TESTE4") };
        rejected = false;
        try { E2eOptions.Parse(args.Concat(recallFlags).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Action origin recall parser accepted the normal port8281.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-origin-recall-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Action origin recall parser accepted an incomplete observer matrix.");
        var recallArgs = args.Concat(new[] { "--port", "18283" }).Concat(recallFlags).ToArray();
        var recallModeAccepted = false;
        try { recallModeAccepted = E2eOptions.Parse(recallArgs).ActionOriginRecallSmoke; } catch (ArgumentException) { }
        Check(recallModeAccepted, "Action origin recall guarded mode unavailable.");
        var mapRecallArgs = args.Concat(new[] { "--port", "18283", "--movement-restriction-recall-smoke" }).Concat(recallFlags.Skip(1)).ToArray();
        var mapRecallModeAccepted = false;
        try { mapRecallModeAccepted = E2eOptions.Parse(mapRecallArgs).MovementRestrictionRecallSmoke; } catch (ArgumentException) { }
        Check(mapRecallModeAccepted, "Movement restriction recall guarded mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(mapRecallArgs.Concat(new[] { "--action-origin-recall-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting recall smoke modes accepted.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283" }).Concat(recallFlags).Concat(new[] { "--action-gate-smoke" }).ToArray()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting Action origin recall/Action modes accepted.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-gate-smoke", "--trade-list-gate-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting Action/ReqTradeList modes accepted.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-range-smoke" }).ToArray()).ActionRangeSmoke, "Range guarded mode unavailable.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-illusion-mana-smoke" }).ToArray()).ActionIllusionManaSmoke, "Action3 mana guarded mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-illusion-mana-smoke", "--action-range-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting Action3 mana/range modes accepted.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-illusion-eligibility-smoke" }).ToArray()).ActionIllusionEligibilitySmoke, "Action3 eligibility guarded mode unavailable.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-illusion-eligibility-smoke", "--action-range-smoke" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting Action3 eligibility/range modes accepted.");
        rejected = false;
        try { E2eOptions.Parse(args.Concat(new[] { "--port", "18283", "--action-range-smoke", "--action-negative-hp" }).ToArray()); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Conflicting Action range/HP modes accepted.");
        Check(E2eOptions.Parse(args.Concat(new[] { "--port", "18283" }).ToArray()).Port == 18283, "Autotrade parser rejected isolated port18283.");
        var dualWorldRoot = Path.Combine(Path.GetTempPath(), "Backup", "Build", "AutoTradeCloseSmoke", "dual-world-harness-test");
        var clientAccountRoot = Path.Combine(dualWorldRoot, "account", "T");
        var sharedReviewRoot = Path.Combine(dualWorldRoot, "account-review");
        var dualWorldArgs = new[]
        {
            "--host", "127.0.0.2", "--port", "18283", "--account-file", Path.Combine(clientAccountRoot, "TESTE2"),
            "--account-review-peer-account-file", Path.Combine(clientAccountRoot, "TESTE"), "--account-review-root", sharedReviewRoot,
            "--action-account-analysis-smoke", "--account-review-actor-state-file", Path.Combine(dualWorldRoot, "UP", "accounts", "T", "TESTE2"),
            "--account-review-peer-state-file", Path.Combine(dualWorldRoot, "UP", "accounts", "T", "TESTE"),
            "--account-review-action-ready-file", Path.Combine(sharedReviewRoot, "action-barrier", "up.ready"),
            "--account-review-action-release-file", Path.Combine(sharedReviewRoot, "action-barrier", "release.signal"),
        };
        var thresholdArgs = dualWorldArgs.Select(static arg => arg == "--action-account-analysis-smoke"
            ? "--action-account-analysis-threshold-smoke"
            : arg).ToArray();
        var thresholdAccepted = false;
        try { thresholdAccepted = E2eOptions.Parse(thresholdArgs).ActionAccountAnalysisThresholdSmoke; }
        catch (ArgumentException) { }
        Check(thresholdAccepted, "The isolated Account Analysis threshold network scenario is unavailable.");
        var dualWorldOptions = E2eOptions.Parse(dualWorldArgs);
        Check(dualWorldOptions.AccountReviewActorStateFile == Path.Combine(dualWorldRoot, "UP", "accounts", "T", "TESTE2") &&
            dualWorldOptions.AccountReviewPeerStateFile == Path.Combine(dualWorldRoot, "UP", "accounts", "T", "TESTE") &&
            dualWorldOptions.AccountReviewActionReadyFile == Path.Combine(sharedReviewRoot, "action-barrier", "up.ready") &&
            dualWorldOptions.AccountReviewActionReleaseFile == Path.Combine(sharedReviewRoot, "action-barrier", "release.signal"),
            "Account Analysis parser did not preserve isolated per-world state files and action barrier paths.");
        rejected = false;
        try
        {
            E2eOptions.Parse(dualWorldArgs.Select((value, index) => index == 18 ? Path.Combine(sharedReviewRoot, "outside", "release.signal") : value).ToArray());
        }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Account Analysis parser accepted a release marker outside its dedicated barrier directory.");
        rejected = false;
        try
        {
            E2eOptions.Parse(dualWorldArgs.Select((value, index) => index == 12 ? Path.Combine(dualWorldRoot, "elsewhere", "TESTE2") : value).ToArray());
        }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Account Analysis parser accepted a per-world state file outside UP/PVP/accounts/T.");
        Console.WriteLine("HARNESS_GUARD_PASS normal-port-rejected isolated-port-accepted");
        await BuyerBarrier();
        CloseWireAssertions();
        var leakedHp = LegacyFrameCodec.CreateDefault();
        var rejectedHp = false;
        try { MotionGateNetwork.AssertNoHpMode([leakedHp.Decode(leakedHp.Encode(SetHpModeConfirmation.MessageType, 7, 1, new byte[8], 0))]); }
        catch (InvalidOperationException) { rejectedHp = true; }
        Check(rejectedHp, "Motion peer assertion accepted a private HpMode.");
        MotionGateNetwork.AssertNoHpMode([]);
        Console.WriteLine("HARNESS_MOTION_GUARD_PASS peer-hp-mode-rejected");
        var visualCodec = LegacyFrameCodec.CreateDefault();
        var visualPayload = new byte[248]; visualPayload[4] = 7;
        var visual = visualCodec.Decode(visualCodec.Encode(0x0363, 30000, 1, visualPayload, 0));
        TradeListGateNetwork.AssertVisual([visual], 7, visual);
        foreach (var changed in new[] { 4, 222 })
        {
            var payload = visualPayload.ToArray(); payload[changed]++;
            var rejectedVisual = false;
            try { TradeListGateNetwork.AssertVisual([visualCodec.Decode(visualCodec.Encode(0x0363, 30000, 1, payload, 0))], 7, visual); }
            catch (InvalidOperationException) { rejectedVisual = true; }
            Check(rejectedVisual, "ReqTradeList visual assertion accepted wrong owner/title.");
        }
        Console.WriteLine("HARNESS_LIST_GUARD_PASS wrong-visual-owner-title-rejected");
        var action = new ActionRequest(2100, 2100, 0, 2, new byte[24], 2101, 2100);
        ActionGateNetwork.AssertRelay([visualCodec.Decode(action.ToFrame(visualCodec, 1, 0, 7))], action, 7, 1);
        foreach (var mutation in new[] { "opcode", "id", "tick", "route" })
        {
            var payload = action.ToPayload();
            if (mutation == "route") payload[12]++;
            var changed = visualCodec.Decode(visualCodec.Encode(mutation == "opcode" ? ActionRequest.StopMessageType : ActionRequest.MessageType,
                mutation == "id" ? (ushort)8 : (ushort)7, mutation == "tick" ? 2u : 1u, payload, 0));
            var rejectedAction = false;
            try { ActionGateNetwork.AssertRelay([changed], action, 7, 1); } catch (InvalidOperationException) { rejectedAction = true; }
            Check(rejectedAction, $"Action relay assertion accepted wrong {mutation}.");
        }
        Console.WriteLine("HARNESS_ACTION_GUARD_PASS wrong-opcode-id-tick-route-rejected");
        var correction = new ActionRequest(2100, 2100, 1, 6, new byte[24], 2100, 2100) { WireType = ActionRequest.IllusionMessageType };
        ActionRangeNetwork.AssertCorrection([visualCodec.Decode(correction.ToFrame(visualCodec, 1, 0, 7))], 7, (2100, 2100));
        foreach (var field in new[] { 0, 4, 8, 12, 36 })
        {
            var payload = correction.ToPayload(); payload[field]++;
            var rejectedCorrection = false;
            try { ActionRangeNetwork.AssertCorrection([visualCodec.Decode(visualCodec.Encode(0x0368, 7, 1, payload, 0))], 7, (2100, 2100)); }
            catch (InvalidOperationException) { rejectedCorrection = true; }
            Check(rejectedCorrection, "Range correction assertion accepted wrong origin/effect/speed/route/target.");
        }
        Console.WriteLine("HARNESS_RANGE_GUARD_PASS wrong-correction-fields-rejected");
        var recall = new ActionRequest(1664, 1920, 1, 2, new byte[24], 2112, 2042);
        ActionOriginRecallNetwork.AssertRecall([visualCodec.Decode(recall.ToFrame(visualCodec, 1, 0, 7))], 7, 1664, 1920);
        foreach (var mutation in new[] { "opcode", "id", "origin", "effect", "speed", "route", "destination" })
        {
            var payload = recall.ToPayload();
            if (mutation == "origin") payload[0]++;
            if (mutation == "effect") payload[4]++;
            if (mutation == "speed") payload[8]++;
            if (mutation == "route") payload[12]++;
            if (mutation == "destination")
            {
                payload[36] = 0x80;
                payload[37] = 0x06;
                payload[38] = 0x80;
                payload[39] = 0x07;
            }
            var changed = visualCodec.Decode(visualCodec.Encode(mutation == "opcode" ? ActionRequest.StopMessageType : ActionRequest.MessageType,
                mutation == "id" ? (ushort)8 : (ushort)7, 1, payload, 0));
            var rejectedRecall = false;
            try { ActionOriginRecallNetwork.AssertRecall([changed], 7, 1664, 1920); }
            catch (InvalidOperationException) { rejectedRecall = true; }
            Check(rejectedRecall, $"Recall assertion accepted wrong {mutation}.");
        }
        Console.WriteLine("HARNESS_RECALL_GUARD_PASS wrong-opcode-id-origin-effect-speed-route-destination-rejected");
    }

    private static void CloseWireAssertions()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        foreach (var vector in new[] { "wrong-signal-id", "wrong-pk-state" })
        {
            var visualPayload = new byte[224]; visualPayload[4] = 7;
            var pkPayload = new byte[4]; if (vector == "wrong-pk-state") pkPayload[0] = 1;
            var frames = new[] {
                codec.Decode(codec.Encode(0x0384, vector == "wrong-signal-id" ? (ushort)12 : (ushort)7, 1, [], 0)),
                codec.Decode(codec.Encode(0x0364, 30000, 1, visualPayload, 0)),
                codec.Decode(codec.Encode(0x0366, 7, 1, pkPayload, 0)),
            };
            var rejected = false;
            try { AutoTradeCloseNetwork.AssertClose(frames, 7, 1, true); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, $"Close assertion accepted {vector}.");
            Console.WriteLine($"HARNESS_CLOSE_GUARD_PASS {vector}");
        }
    }

    private static async Task BuyerBarrier()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var actor = new TcpClient(); await actor.ConnectAsync(endpoint.Address, endpoint.Port);
        using var actorServer = await listener.AcceptTcpClientAsync();
        using var peer = new TcpClient(); await peer.ConnectAsync(endpoint.Address, endpoint.Port);
        using var peerServer = await listener.AcceptTcpClientAsync();
        var codec = LegacyFrameCodec.CreateDefault();
        var actorRead = Read(actorServer.GetStream()); var peerRead = Read(peerServer.GetStream());
        var phase = AutoTradeCloseNetwork.CapturePhase(actor.GetStream(), peer.GetStream(), 7, 12, codec, TimeSpan.FromSeconds(2), "BUY-TEST", peerFirst: true);
        try
        {
            Check(await Task.WhenAny(actorRead, peerRead) == peerRead, "Buyer barrier began on the other connection.");
            await peerRead;
            // The buyer response happens before its marker's relay. It must be
            // captured even though it has no corresponding seller response.
            await peerServer.GetStream().WriteAsync(new UpdateCarryConfirmation(new LegacyItem[64], 1000).ToFrame(codec, 1, 0, 12));
            await actorServer.GetStream().WriteAsync(new MessageChatConfirmation("SHOP-BUY-TEST").ToFrame(codec, 1, 0, 12));
            await actorRead;
            await peerServer.GetStream().WriteAsync(new MessageChatConfirmation("SHOP-BUY-TEST").ToFrame(codec, 1, 0, 7));
            var captured = await phase;
            Check(captured.Peer.Count == 1 && captured.Peer[0].Header.Type == UpdateCarryConfirmation.MessageType,
                "Buyer-only response escaped the purchase barrier.");
            Console.WriteLine("HARNESS_BARRIER_PASS buyer-first buyer-only-response-captured");
        }
        finally
        {
            actor.Dispose(); peer.Dispose(); actorServer.Dispose(); peerServer.Dispose();
            try { await phase; } catch (Exception) { }
            try { await actorRead; } catch (Exception) { }
            try { await peerRead; } catch (Exception) { }
        }
    }
    private static async Task Read(NetworkStream stream)
    {
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var header = new byte[12]; await stream.ReadExactlyAsync(header, ct.Token);
        var bytes = new byte[PacketHeader.Read(header).Size - 12]; await stream.ReadExactlyAsync(bytes, ct.Token);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
