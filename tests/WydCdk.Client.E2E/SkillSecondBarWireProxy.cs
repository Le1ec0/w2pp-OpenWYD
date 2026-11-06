using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal sealed record SkillSecondBarWireProxyOptions(int ListenPort, int UpstreamPort, uint LoginWord1, uint UpdateWord1)
{
    public static SkillSecondBarWireProxyOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !values.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException("Proxy options must be unique name/value pairs.");
        }

        if (values.Keys.Any(key => key is not ("--listen-port" or "--upstream-port" or "--login-word1" or "--update-word1")) ||
            !values.TryGetValue("--listen-port", out var listenText) ||
            !values.TryGetValue("--upstream-port", out var upstreamText) ||
            !values.TryGetValue("--login-word1", out var loginText) ||
            !values.TryGetValue("--update-word1", out var updateText))
            throw new ArgumentException("Second-bar proxy requires exactly --skill-second-bar-wire-proxy --listen-port <1..65535> --upstream-port <1..65535> --login-word1 <uint32> --update-word1 <uint32>.");

        var listenPort = ParsePort(listenText, "--listen-port");
        var upstreamPort = ParsePort(upstreamText, "--upstream-port");
        if (listenPort == upstreamPort)
            throw new ArgumentException("The second-bar proxy listen and upstream ports must differ.");

        return new SkillSecondBarWireProxyOptions(
            listenPort,
            upstreamPort,
            SkillSecondBarVisualFixture.ParseUInt32(loginText),
            SkillSecondBarVisualFixture.ParseUInt32(updateText));
    }

    private static int ParsePort(string value, string name) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= ushort.MaxValue
            ? port
            : throw new ArgumentOutOfRangeException(name, value, "Port must be in the range 1..65535.");
}

internal static class SkillSecondBarWireProxy
{
    // The final four bytes in the 16-byte 7.69 MOB name field carry chaos/kill counters.
    private const int CharacterTextLength = 12;

    public static async Task RunAsync(SkillSecondBarWireProxyOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        using var listener = new TcpListener(IPAddress.Loopback, options.ListenPort);
        try
        {
            listener.Start(8);
            Console.WriteLine($"SKILL_SECOND_BAR_PROXY_READY listen=127.0.0.1:{options.ListenPort} upstream=127.0.0.1:{options.UpstreamPort} login-word1=0x{options.LoginWord1:X8} update-word1=0x{options.UpdateWord1:X8} external-bind=false");
            while (!shutdown.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(shutdown.Token);
                }
                catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
                {
                    break;
                }

                await HandleClientAsync(client, options, shutdown.Token);
            }
        }
        finally
        {
            listener.Stop();
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task HandleClientAsync(TcpClient client, SkillSecondBarWireProxyOptions options, CancellationToken cancellationToken)
    {
        using (client)
        using (var upstream = new TcpClient(AddressFamily.InterNetwork))
        {
            try
            {
                await upstream.ConnectAsync(IPAddress.Loopback, options.UpstreamPort, cancellationToken);
            }
            catch
            {
                Console.Error.WriteLine($"SKILL_SECOND_BAR_PROXY_UPSTREAM_FAILED port={options.UpstreamPort}");
                return;
            }

            Console.WriteLine($"SKILL_SECOND_BAR_PROXY_CONNECTED remote={client.Client.RemoteEndPoint}");
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Client-to-server uses a framing variant this proxy has not decoded; preserve its byte stream.
            var clientToHost = RelayRawAsync(client.GetStream(), upstream.GetStream(), connection.Token);
            var hostToClient = RelayAndRewriteFramesAsync(upstream.GetStream(), client.GetStream(), options, connection.Token);
            await Task.WhenAny(clientToHost, hostToClient);
            connection.Cancel();
            client.Close();
            upstream.Close();
            try { await Task.WhenAll(clientToHost, hostToClient); }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            Console.WriteLine("SKILL_SECOND_BAR_PROXY_DISCONNECTED");
        }
    }

    private static async Task RelayRawAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) return;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task RelayAndRewriteFramesAsync(
        Stream source,
        Stream destination,
        SkillSecondBarWireProxyOptions options,
        CancellationToken cancellationToken)
    {
        var codec = LegacyFrameCodec.CreateDefault();
        ushort? loginSlot = null;
        string loginCharacter = "unknown";
        short? loginPosX = null;
        short? loginPosY = null;
        while (true)
        {
            var headerBytes = new byte[PacketHeader.SizeInBytes];
            if (!await ReadExactlyOrEofAsync(source, headerBytes, cancellationToken)) return;
            var packetSize = BinaryPrimitives.ReadUInt16LittleEndian(headerBytes);
            if (packetSize is < PacketHeader.SizeInBytes or > PacketHeader.MaxPacketSize)
                throw new InvalidDataException($"Proxy received invalid server frame size {packetSize}.");

            var frameBytes = new byte[packetSize];
            headerBytes.CopyTo(frameBytes, 0);
            await source.ReadExactlyAsync(frameBytes.AsMemory(PacketHeader.SizeInBytes), cancellationToken);
            var frame = codec.Decode(frameBytes);
            if (!frame.IsChecksumValid)
            {
                Console.Error.WriteLine($"SKILL_SECOND_BAR_PROXY_BAD_CHECKSUM type=0x{frame.Header.Type:X4}; forwarding unchanged");
                await destination.WriteAsync(frameBytes, cancellationToken);
                continue;
            }

            uint? replacement = null;
            int fieldOffset = -1;
            string identity = string.Empty;
            if (frame.Header.Type == CharacterLoginConfirmationV769.MessageType && frame.Header.Size == CharacterLoginConfirmationV769.PacketSize)
            {
                replacement = options.LoginWord1;
                fieldOffset = CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.LearnedSkillOffset + sizeof(uint);

                loginSlot = BinaryPrimitives.ReadUInt16LittleEndian(
                    frame.Payload.Span[CharacterLoginConfirmationV769.SlotOffset..]);
                loginPosX = BinaryPrimitives.ReadInt16LittleEndian(
                    frame.Payload.Span[CharacterLoginConfirmationV769.PosXOffset..]);
                loginPosY = BinaryPrimitives.ReadInt16LittleEndian(
                    frame.Payload.Span[CharacterLoginConfirmationV769.PosYOffset..]);
                var rawName = Encoding.ASCII.GetString(frame.Payload.Span.Slice(
                    CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.NameOffset,
                    CharacterTextLength)).TrimEnd('\0');
                loginCharacter = new string(rawName.Where(character => char.IsAsciiLetterOrDigit(character) || character == '_').ToArray());
                var skillBar = Convert.ToHexString(frame.Payload.Span.Slice(
                    CharacterLoginConfirmationV769.MobOffset + CharacterMobV769.ShortSkillOffset, 4));
                var shortSkills = Convert.ToHexString(frame.Payload.Span.Slice(
                    CharacterLoginConfirmationV769.ShortSkillOffset, CharacterLoginConfirmationV769.ShortSkillSize));
                identity = $" slot={loginSlot} character={loginCharacter} position=({loginPosX},{loginPosY}) skillbar={skillBar} shortskills={shortSkills}";
            }
            else if (frame.Header.Type == UpdateEtcConfirmationV769.MessageType && frame.Header.Size == UpdateEtcConfirmationV769.PacketSize)
            {
                replacement = options.UpdateWord1;
                fieldOffset = UpdateEtcConfirmationV769.LearnedSkillOffset + sizeof(uint);
                identity = $" slot={loginSlot?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} character={loginCharacter} position=({loginPosX?.ToString(CultureInfo.InvariantCulture) ?? "unknown"},{loginPosY?.ToString(CultureInfo.InvariantCulture) ?? "unknown"})";
            }

            if (replacement is not uint word1 || fieldOffset < 0)
            {
                await destination.WriteAsync(frameBytes, cancellationToken);
                continue;
            }

            var payload = frame.Payload.ToArray();
            var word0 = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(fieldOffset - sizeof(uint)));
            var oldWord1 = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(fieldOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(fieldOffset), word1);
            var rewritten = codec.Encode(frame.Header.Type, frame.Header.Id, frame.Header.ClientTick, payload, frame.Header.KeywordIndex);
            var verify = codec.Decode(rewritten);
            if (!verify.IsChecksumValid || BinaryPrimitives.ReadUInt32LittleEndian(verify.Payload.Span[fieldOffset..]) != word1)
                throw new InvalidDataException($"Proxy failed to re-encode second-bar word for 0x{frame.Header.Type:X4}.");

            Console.WriteLine($"SKILL_SECOND_BAR_WIRE type=0x{frame.Header.Type:X4} id={frame.Header.Id}{identity} word0=0x{word0:X8} word1-old=0x{oldWord1:X8} word1-sent=0x{word1:X8}");
            await destination.WriteAsync(rewritten, cancellationToken);
        }
    }

    private static async Task<bool> ReadExactlyOrEofAsync(Stream stream, Memory<byte> destination, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken);
            if (read == 0)
            {
                if (offset == 0) return false;
                throw new EndOfStreamException("Peer closed in the middle of a legacy frame header.");
            }
            offset += read;
        }
        return true;
    }
}
