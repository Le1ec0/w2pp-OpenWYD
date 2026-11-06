using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using WydCdk.Protocol;

internal static class CurrentScoreLoginProjectionNetwork
{
    private const ushort CreateMobType = 0x0364;
    private const ushort UpdateEtcType = 0x0337;
    private const ushort UpdateScoreType = 0x0336;

    public static async Task RunRejectAsync(
        E2eOptions actorOptions,
        E2eOptions observerOptions,
        AccountHeader actorHeader,
        AccountHeader observerHeader,
        LegacyFrameCodec codec)
    {
        Ensure(actorOptions.Host == "127.0.0.1" && actorOptions.Port == 18283, "CurrentScore rejection smoke requires loopback port 18283.");
        var evidenceRoot = FindEvidenceRoot(actorOptions.AccountFile);
        var sourceAccountPath = Path.Combine(evidenceRoot, "source", "account", "S", "SCORETEST");
        var clientItemDataPath = Path.GetFullPath(Path.Combine(FindServerRoot(), "..", "Client", "7670", "ItemList.bin"));
        var modifiedItemDataPath = Path.Combine(evidenceRoot, "reject", "ItemList.bin");
        var actorAccountHash = SHA256.HashData(await File.ReadAllBytesAsync(actorOptions.AccountFile));
        var sourceAccountHash = SHA256.HashData(await File.ReadAllBytesAsync(sourceAccountPath));
        var clientItemDataHash = SHA256.HashData(await File.ReadAllBytesAsync(clientItemDataPath));
        var modifiedItemDataHash = SHA256.HashData(await File.ReadAllBytesAsync(modifiedItemDataPath));

        E2eSession? observer = null;
        using var actorClient = new TcpClient();
        try
        {
            observer = await E2eSession.ConnectAndLoginAsync(observerOptions, observerHeader, codec, "SCORE_REJECT_OBSERVER_");
            await DrainAvailableFramesAsync(observer.Stream, codec, TimeSpan.FromMilliseconds(200));

            await actorClient.ConnectAsync(actorOptions.Host, actorOptions.Port);
            var stream = actorClient.GetStream();
            var init = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(init, LegacyFrameCodec.InitCode);
            await stream.WriteAsync(init);

            var loginPayload = new byte[AccountLoginRequest.PacketSize - PacketHeader.SizeInBytes];
            WriteCString(loginPayload.AsSpan(0, 12), actorHeader.Password);
            WriteCString(loginPayload.AsSpan(12, 16), actorHeader.AccountName);
            BinaryPrimitives.WriteInt32LittleEndian(loginPayload.AsSpan(80), ClientReleasePolicy.RequiredClientVersion);
            await SendAsync(stream, codec, AccountLoginRequest.MessageType, loginPayload, actorOptions.ConnectionId);
            Require(await ReadFrameAsync(stream, codec, actorOptions.Timeout), AccountLoginConfirmationV769.MessageType,
                AccountLoginConfirmationV769.PacketSize, "account login confirmation");

            var securePayload = new byte[AccountSecureRequest.PacketSize - PacketHeader.SizeInBytes];
            actorHeader.NumericToken.CopyTo(securePayload, 0);
            BinaryPrimitives.WriteInt32LittleEndian(securePayload.AsSpan(16), 0);
            await SendAsync(stream, codec, AccountSecureRequest.MessageType, securePayload, actorOptions.ConnectionId);
            Require(await ReadFrameAsync(stream, codec, actorOptions.Timeout), AccountSecureSignal.SuccessType,
                PacketHeader.SizeInBytes, "account secure confirmation");

            var characterPayload = new byte[CharacterLoginRequest.Retail760PacketSize - PacketHeader.SizeInBytes];
            BinaryPrimitives.WriteInt32LittleEndian(characterPayload, actorOptions.Slot);
            await SendAsync(stream, codec, CharacterLoginRequest.MessageType, characterPayload, actorOptions.ConnectionId);
            await SendAsync(stream, codec, CharacterLogoutRequest.MessageType, [], actorOptions.ConnectionId);
            var barrier = await ReadFrameAsync(stream, codec, actorOptions.Timeout);
            Require(barrier, CharacterLogoutConfirmation.MessageType, CharacterLogoutConfirmation.PacketSize,
                "character-selection logout barrier");

            var observerFrames = await DrainAvailableFramesAsync(observer.Stream, codec, TimeSpan.FromMilliseconds(350));
            Ensure(!observerFrames.Any(frame => frame.Header.Type == CreateMobType && frame.Header.Size == CreateMobConfirmationV769.PacketSize &&
                frame.Payload.Span.Slice(CreateMobConfirmationV769.MobNameOffset, 10).SequenceEqual("SCOREACTOR"u8)),
                "Observer received CreateMob for a character whose score projection was rejected.");
            Ensure(SHA256.HashData(await File.ReadAllBytesAsync(actorOptions.AccountFile)).AsSpan().SequenceEqual(actorAccountHash),
                "Rejected projection changed the disposable actor account file.");
            Ensure(SHA256.HashData(await File.ReadAllBytesAsync(sourceAccountPath)).AsSpan().SequenceEqual(sourceAccountHash),
                "Rejected projection changed the immutable synthetic source account fixture.");
            Ensure(SHA256.HashData(await File.ReadAllBytesAsync(clientItemDataPath)).AsSpan().SequenceEqual(clientItemDataHash) &&
                SHA256.HashData(await File.ReadAllBytesAsync(modifiedItemDataPath)).AsSpan().SequenceEqual(modifiedItemDataHash),
                "The rejection smoke changed an ItemList input.");

            await observer.LogoutAsync();
            Console.WriteLine($"CURRENT_SCORE_REJECT_EVIDENCE actor-account-sha256={Convert.ToHexString(actorAccountHash)} modified-item-data-sha256={Convert.ToHexString(modifiedItemDataHash)}");
            Console.WriteLine("CLIENT_E2E_PASS current-score-login-projection-reject no-login-or-spawn");
        }
        finally
        {
            if (observer is not null)
            {
                try { await observer.LogoutAsync(); }
                catch (Exception) { }
                await observer.DisposeAsync();
            }
        }
    }

    public static async Task RunAsync(
        E2eOptions actorOptions,
        E2eOptions observerOptions,
        AccountHeader actorHeader,
        AccountHeader observerHeader,
        LegacyFrameCodec codec)
    {
        Ensure(actorOptions.Host == "127.0.0.1" && actorOptions.Port == 18283, "CurrentScore projection smoke requires loopback port 18283.");
        Ensure(actorOptions.ConnectionId != observerOptions.ConnectionId, "Actor and observer connection IDs must differ.");
        var evidence = ResolveEvidenceFiles(actorOptions.AccountFile);
        var sourceFixtureHash = SHA256.HashData(await File.ReadAllBytesAsync(evidence.SourceAccountPath));
        var itemDataHash = SHA256.HashData(await File.ReadAllBytesAsync(evidence.ItemDataPath));

        E2eSession? observer = null;
        E2eSession? actor = null;
        try
        {
            observer = await E2eSession.ConnectAndLoginAsync(observerOptions, observerHeader, codec, "SCORE_OBSERVER_");
            actor = await E2eSession.ConnectAndLoginAsync(actorOptions, actorHeader, codec, "SCORE_ACTOR_");

            var expected = VerifyProjectedLoginMob(actor.CharacterLoginFrame);
            var actorSpawn = await ReadActorSpawnAsync(observer, codec, observerOptions.Timeout);
            var actorMobId = MobId(actorSpawn);
            VerifyCreateMob(actorSpawn, expected);
            await VerifyLoginScoreFramesAsync(actor, codec, actorOptions.Timeout, actorMobId, expected);

            await actor.LogoutAsync();
            await actor.DisposeAsync();
            actor = null;
            VerifyPersistedProjection(actorOptions.AccountFile, expected);

            actor = await E2eSession.ConnectAndLoginAsync(actorOptions, actorHeader, codec, "SCORE_RELOGIN_");
            Ensure(VerifyProjectedLoginMob(actor.CharacterLoginFrame) == expected,
                "Relogin applied a supported score bonus more than once.");
            var reloginSpawn = await ReadActorSpawnAsync(observer, codec, observerOptions.Timeout);
            var reloginMobId = MobId(reloginSpawn);
            VerifyCreateMob(reloginSpawn, expected);
            await VerifyLoginScoreFramesAsync(actor, codec, actorOptions.Timeout, reloginMobId, expected);
            await actor.LogoutAsync();
            await actor.DisposeAsync();
            actor = null;
            VerifyPersistedProjection(actorOptions.AccountFile, expected);

            await observer.LogoutAsync();
            Ensure(SHA256.HashData(await File.ReadAllBytesAsync(evidence.SourceAccountPath)).AsSpan().SequenceEqual(sourceFixtureHash),
                "The immutable synthetic source account fixture changed during the smoke.");
            Ensure(SHA256.HashData(await File.ReadAllBytesAsync(evidence.ItemDataPath)).AsSpan().SequenceEqual(itemDataHash),
                "The authoritative Client/7670 ItemList.bin changed during the smoke.");
            Console.WriteLine($"CURRENT_SCORE_EVIDENCE source-account-sha256={Convert.ToHexString(sourceFixtureHash)} item-data-sha256={Convert.ToHexString(itemDataHash)}");
            Console.WriteLine("CLIENT_E2E_PASS current-score-login-projection");
        }
        finally
        {
            if (actor is not null)
            {
                try { await actor.LogoutAsync(); }
                catch (Exception) { }
                await actor.DisposeAsync();
            }
            if (observer is not null)
            {
                try { await observer.LogoutAsync(); }
                catch (Exception) { }
                await observer.DisposeAsync();
            }
        }
    }

    private static async Task VerifyLoginScoreFramesAsync(E2eSession session, LegacyFrameCodec codec, TimeSpan timeout, ushort mobId, ProjectedScore expected)
    {
        var frames = await ReadUntilAsync(session.Stream, codec, timeout,
            frame => frame.Header.Type == UpdateEtcType && frame.Header.Size == UpdateEtcConfirmationV769.PacketSize && frame.Header.Id == mobId,
            frame => frame.Header.Type == UpdateScoreType && frame.Header.Size == UpdateScoreConfirmationV769.PacketSize && frame.Header.Id == mobId);
        VerifyUpdateEtc(frames.Single(frame => frame.Header.Type == UpdateEtcType));
        VerifyUpdateScore(frames.Single(frame => frame.Header.Type == UpdateScoreType), expected);
    }

    private static async Task<DecodedFrame> ReadActorSpawnAsync(E2eSession observer, LegacyFrameCodec codec, TimeSpan timeout)
    {
        var frames = await ReadUntilAsync(observer.Stream, codec, timeout,
            frame => frame.Header.Type == CreateMobType && frame.Header.Size == CreateMobConfirmationV769.PacketSize &&
                frame.Payload.Span.Slice(CreateMobConfirmationV769.MobNameOffset, 10).SequenceEqual("SCOREACTOR"u8));
        return frames.Single(frame => frame.Header.Type == CreateMobType && frame.Header.Size == CreateMobConfirmationV769.PacketSize &&
            frame.Payload.Span.Slice(CreateMobConfirmationV769.MobNameOffset, 10).SequenceEqual("SCOREACTOR"u8));
    }

    private static ushort MobId(DecodedFrame frame) =>
        BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]);

    private static void VerifyPersistedProjection(string accountPath, ProjectedScore expected)
    {
        var account = File.ReadAllBytes(accountPath);
        const int mobOffset = 216;
        var score = LegacyScore.Read(account.AsSpan(mobOffset + 92, LegacyScore.SizeInBytes));
        Ensure(score.Level == expected.Level && score.Ac == expected.Ac && score.Damage == expected.Damage &&
            score.AttackRun == expected.AttackRun && score.MaxHp == expected.MaxHp && score.MaxMp == expected.MaxMp &&
            score.Hp == expected.Hp && score.Mp == expected.Mp,
            "Logout did not persist the same CurrentScore snapshot used at runtime.");
        Ensure(BinaryPrimitives.ReadInt32LittleEndian(account.AsSpan(mobOffset + 784)) == expected.Magic &&
            account[mobOffset + 794] == expected.Critical && account[mobOffset + 795] == expected.SaveMana &&
            account[mobOffset + 802] == expected.RegenHp && account[mobOffset + 804] == expected.RegenMp &&
            account[mobOffset + 806] == expected.Resist0 && account[mobOffset + 807] == expected.Resist1 &&
            account[mobOffset + 808] == expected.Resist2 && account[mobOffset + 809] == expected.Resist3,
            "Logout did not persist the projected terminal ability fields.");
    }

    private static EvidenceFiles ResolveEvidenceFiles(string accountPath)
    {
        var root = FindEvidenceRoot(accountPath);
        var sourceAccount = Path.Combine(root, "source", "account", "S", "SCORETEST");
        var serverRoot = FindServerRoot();
        var itemData = Path.GetFullPath(Path.Combine(serverRoot, "..", "Client", "7670", "ItemList.bin"));
        Ensure(File.Exists(sourceAccount) && File.Exists(itemData), "The immutable synthetic account or Client/7670 ItemList fixture is missing.");
        return new EvidenceFiles(sourceAccount, itemData);
    }

    private static string FindEvidenceRoot(string accountPath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(accountPath)!);
        while (directory is not null && !directory.Name.Equals("CurrentScoreLoginProjection-20261008", StringComparison.OrdinalIgnoreCase))
            directory = directory.Parent;
        Ensure(directory is not null, "The CurrentScore smoke fixture root is missing from the account path.");
        return directory!.FullName;
    }

    private static string FindServerRoot()
    {
        foreach (var origin in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(origin));
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WydCdk.Engine.slnx")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not find the Server checkout root for the CurrentScore smoke.");
    }

    private static ProjectedScore VerifyProjectedLoginMob(DecodedFrame login)
    {
        Require(login, CharacterLoginConfirmationV769.MessageType, CharacterLoginConfirmationV769.PacketSize, "character login confirmation");
        var mob = login.Payload.Span.Slice(CharacterLoginConfirmationV769.MobOffset, CharacterMobV769.SizeInBytes);
        var score = mob[CharacterMobV769.CurrentScoreOffset..];
        var actual = new ProjectedScore(
            BinaryPrimitives.ReadInt16LittleEndian(score),
            BinaryPrimitives.ReadInt32LittleEndian(score[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(score[8..]),
            score[13],
            BinaryPrimitives.ReadInt32LittleEndian(score[16..]),
            BinaryPrimitives.ReadInt32LittleEndian(score[20..]),
            BinaryPrimitives.ReadInt32LittleEndian(score[24..]),
            BinaryPrimitives.ReadInt32LittleEndian(score[28..]),
            mob[CharacterMobV769.MagicOffset],
            mob[CharacterMobV769.CriticalOffset],
            mob[CharacterMobV769.SaveManaOffset],
            mob[CharacterMobV769.RegenHpOffset],
            mob[CharacterMobV769.RegenMpOffset],
            mob[CharacterMobV769.ResistOffset],
            mob[CharacterMobV769.ResistOffset + 1],
            mob[CharacterMobV769.ResistOffset + 2],
            mob[CharacterMobV769.ResistOffset + 3],
            BinaryPrimitives.ReadUInt16LittleEndian(mob[CharacterMobV769.EquipmentOffset..]));

        Ensure(actual.Level == 2000 && actual.Ac == 100 && actual.Damage == 50 && actual.AttackRun == 0,
            $"login confirmation did not contain the expected rebuilt score (level={actual.Level}, ac={actual.Ac}, damage={actual.Damage}, attackRun={actual.AttackRun}).");
        Ensure(actual.MaxHp == 1150 && actual.MaxMp == 535 && actual.Hp == 900 && actual.Mp == 400,
            "login confirmation carried stale or unexpected HP/MP values.");
        Ensure(actual.Magic == 0 && actual.Critical == 0 && actual.SaveMana == 0 && actual.RegenHp == 0 && actual.RegenMp == 0,
            $"login confirmation carried stale derived scalar fields (Magic={actual.Magic}, Critical={actual.Critical}, SaveMana={actual.SaveMana}, RegenHP={actual.RegenHp}, RegenMP={actual.RegenMp}).");
        Ensure(actual.Resist0 == 10 && actual.Resist1 == 20 && actual.Resist2 == 30 && actual.Resist3 == 40,
            "login confirmation carried stale or unexpected resistance values.");
        return actual;
    }

    private static void VerifyUpdateEtc(DecodedFrame frame)
    {
        Require(frame, UpdateEtcConfirmationV769.MessageType, UpdateEtcConfirmationV769.PacketSize, "UpdateEtc");
        var payload = frame.Payload.Span;
        Ensure(BinaryPrimitives.ReadUInt32LittleEndian(payload[UpdateEtcConfirmationV769.LearnedSkillOffset..]) == 0 &&
            BinaryPrimitives.ReadUInt32LittleEndian(payload[(UpdateEtcConfirmationV769.LearnedSkillOffset + sizeof(uint))..]) == 0,
            "UpdateEtc did not carry the projected learned-skill words.");
    }

    private static void VerifyUpdateScore(DecodedFrame frame, ProjectedScore expected)
    {
        Require(frame, UpdateScoreConfirmationV769.MessageType, UpdateScoreConfirmationV769.PacketSize, "UpdateScore");
        var payload = frame.Payload.Span;
        var score = payload[UpdateScoreConfirmationV769.ScoreOffset..];
        Ensure(BinaryPrimitives.ReadInt16LittleEndian(score) == expected.Level &&
            BinaryPrimitives.ReadInt32LittleEndian(score[4..]) == expected.Ac &&
            BinaryPrimitives.ReadInt32LittleEndian(score[8..]) == expected.Damage &&
            score[13] == expected.AttackRun &&
            BinaryPrimitives.ReadInt32LittleEndian(score[16..]) == expected.MaxHp &&
            BinaryPrimitives.ReadInt32LittleEndian(score[20..]) == expected.MaxMp &&
            BinaryPrimitives.ReadInt32LittleEndian(score[24..]) == expected.Hp &&
            BinaryPrimitives.ReadInt32LittleEndian(score[28..]) == expected.Mp,
            "UpdateScore did not carry the same projected CurrentScore as character login.");
        Ensure(BinaryPrimitives.ReadUInt16LittleEndian(payload[UpdateScoreConfirmationV769.MagicOffset..]) == expected.Magic,
            "UpdateScore Magic differed from the login projection.");
    }

    private static void VerifyCreateMob(DecodedFrame frame, ProjectedScore expected)
    {
        Require(frame, CreateMobType, CreateMobConfirmationV769.PacketSize, "CreateMob");
        var score = frame.Payload.Span[CreateMobConfirmationV769.ScoreOffset..];
        Ensure(BinaryPrimitives.ReadInt16LittleEndian(score) == expected.Level &&
            BinaryPrimitives.ReadInt32LittleEndian(score[4..]) == expected.Ac &&
            BinaryPrimitives.ReadInt32LittleEndian(score[8..]) == expected.Damage &&
            score[13] == expected.AttackRun &&
            BinaryPrimitives.ReadInt32LittleEndian(score[16..]) == expected.MaxHp &&
            BinaryPrimitives.ReadInt32LittleEndian(score[20..]) == expected.MaxMp &&
            BinaryPrimitives.ReadInt32LittleEndian(score[24..]) == expected.Hp &&
            BinaryPrimitives.ReadInt32LittleEndian(score[28..]) == expected.Mp,
            "observer CreateMob did not carry the same projected CurrentScore as character login.");
    }

    private static async Task<IReadOnlyList<DecodedFrame>> ReadUntilAsync(
        Stream stream,
        LegacyFrameCodec codec,
        TimeSpan timeout,
        params Func<DecodedFrame, bool>[] conditions)
    {
        var received = new List<DecodedFrame>();
        var pending = conditions.ToList();
        while (pending.Count != 0)
        {
            var frame = await ReadFrameAsync(stream, codec, timeout);
            Ensure(frame.IsChecksumValid, $"Frame type=0x{frame.Header.Type:X4} has an invalid 7.69 checksum.");
            Console.WriteLine($"CURRENT_SCORE_RX type=0x{frame.Header.Type:X4} id={frame.Header.Id} size={frame.Header.Size} checksum={frame.IsChecksumValid}");
            received.Add(frame);
            pending.RemoveAll(condition => condition(frame));
        }
        return received;
    }

    private static async Task<DecodedFrame> ReadFrameAsync(Stream stream, LegacyFrameCodec codec, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
        var header = PacketHeader.Read(headerBytes);
        if (header.Size < PacketHeader.SizeInBytes || header.Size > PacketHeader.MaxPacketSize)
            throw new InvalidDataException($"Invalid response frame size {header.Size}.");
        var frame = new byte[header.Size];
        headerBytes.CopyTo(frame, 0);
        await stream.ReadExactlyAsync(frame.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
        return codec.Decode(frame);
    }

    private static async Task<IReadOnlyList<DecodedFrame>> DrainAvailableFramesAsync(Stream stream, LegacyFrameCodec codec, TimeSpan window)
    {
        var received = new List<DecodedFrame>();
        var until = DateTime.UtcNow + window;
        while (DateTime.UtcNow < until)
        {
            if (stream is not NetworkStream network || !network.DataAvailable)
            {
                await Task.Delay(10);
                continue;
            }
            var frame = await ReadFrameAsync(stream, codec, TimeSpan.FromSeconds(1));
            Ensure(frame.IsChecksumValid, $"Observer frame type=0x{frame.Header.Type:X4} has an invalid checksum.");
            received.Add(frame);
        }
        return received;
    }

    private static async Task SendAsync(Stream stream, LegacyFrameCodec codec, ushort type, byte[] payload, ushort id)
    {
        await stream.WriteAsync(codec.Encode(type, id, 33, payload, 16));
    }

    private static void WriteCString(Span<byte> destination, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        if (bytes.Length >= destination.Length)
            throw new InvalidDataException($"Account field does not fit in {destination.Length} bytes.");
        bytes.CopyTo(destination);
    }

    private static void Require(DecodedFrame frame, ushort type, int size, string label)
    {
        Ensure(frame.IsChecksumValid, $"{label} checksum is invalid.");
        Ensure(frame.Header.Type == type && frame.Header.Size == size,
            $"Expected {label} type=0x{type:X4}/size={size}; received type=0x{frame.Header.Type:X4}/size={frame.Header.Size}.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private readonly record struct ProjectedScore(
        int Level,
        int Ac,
        int Damage,
        byte AttackRun,
        int MaxHp,
        int MaxMp,
        int Hp,
        int Mp,
        byte Magic,
        byte Critical,
        byte SaveMana,
        byte RegenHp,
        byte RegenMp,
        byte Resist0,
        byte Resist1,
        byte Resist2,
        byte Resist3,
        ushort HeadIndex);

    private readonly record struct EvidenceFiles(string SourceAccountPath, string ItemDataPath);
}
