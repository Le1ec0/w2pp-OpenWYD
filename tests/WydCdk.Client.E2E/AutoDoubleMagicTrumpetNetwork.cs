using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;

internal static class AutoDoubleMagicTrumpetNetwork
{
    private const string StartPanel = "MessagePanel:Double EXP started";
    private const string StartBanner = "MagicTrumpet:[Event:]> Double EXP started";
    private const string EndPanel = "MessagePanel:Double EXP ended";
    private const string EndBanner = "MagicTrumpet:[Event:]> Double EXP ended";
    private static readonly string[] ExpectedSequence = [StartPanel, StartBanner, EndPanel, EndBanner];

    public static async Task RunAsync(E2eOptions options, AccountHeader actorHeader, LegacyFrameCodec codec)
    {
        var peerFile = options.AutoTradeClosePeerAccountFile
            ?? throw new ArgumentException("AutoDouble notification smoke requires an isolated peer account fixture.");
        if (!IPAddress.TryParse(options.Host, out var hostAddress) || !IPAddress.IsLoopback(hostAddress) || options.Port != 18283 ||
            Path.GetFullPath(options.AccountFile).Equals(Path.GetFullPath(peerFile), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("AutoDouble notification smoke requires two distinct account fixtures on loopback port 18283.");
        var actorBefore = await File.ReadAllBytesAsync(options.AccountFile);
        var peerBefore = await File.ReadAllBytesAsync(peerFile);
        var actorIdentity = actorBefore.AsSpan(0, 28).ToArray();
        var peerIdentity = peerBefore.AsSpan(0, 28).ToArray();
        var peerHeader = new AccountHeader(
            CString(peerBefore.AsSpan(0, 16)), CString(peerBefore.AsSpan(16, 12)), peerBefore.AsSpan(202, 6).ToArray());

        var actorLogin = E2eSession.ConnectAndLoginAsync(options, actorHeader, codec, "AUTODOUBLE_ACTOR_").AsTask();
        var peerLogin = E2eSession.ConnectAndLoginAsync(options with { AccountFile = peerFile }, peerHeader, codec, "AUTODOUBLE_PEER_").AsTask();
        await Task.WhenAll(actorLogin, peerLogin);
        await using var actor = actorLogin.Result;
        await using var peer = peerLogin.Result;
        var actorId = BinaryPrimitives.ReadUInt16LittleEndian(actor.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
        var peerId = BinaryPrimitives.ReadUInt16LittleEndian(peer.CharacterLoginFrame.Payload.Span[CharacterLoginConfirmationV769.ClientIdOffset..]);
        Check(actorId > 0 && peerId > 0 && actorId != peerId, "AutoDouble smoke did not enter two distinct playing clients.");

        var actorFrames = ReadEventSequenceAsync(actor.Stream, codec, options.Timeout, "actor");
        var peerFrames = ReadEventSequenceAsync(peer.Stream, codec, options.Timeout, "peer");
        await Task.WhenAll(actorFrames, peerFrames);
        Check(actorFrames.Result.SequenceEqual(ExpectedSequence), "Actor did not receive start and end MessagePanel/MagicTrumpet pairs in order.");
        Check(peerFrames.Result.SequenceEqual(ExpectedSequence), "Second online character did not receive start and end MessagePanel/MagicTrumpet pairs in order.");

        await actor.LogoutAsync();
        await peer.LogoutAsync();
        var actorAfter = await File.ReadAllBytesAsync(options.AccountFile);
        var peerAfter = await File.ReadAllBytesAsync(peerFile);
        Check(actorAfter.Length == actorBefore.Length && actorAfter.AsSpan(0, 28).SequenceEqual(actorIdentity), "Actor isolated account identity or record shape changed.");
        Check(peerAfter.Length == peerBefore.Length && peerAfter.AsSpan(0, 28).SequenceEqual(peerIdentity), "Peer isolated account identity or record shape changed.");
        Console.WriteLine("CLIENT_E2E_PASS auto-double-start-end-messagepanel-and-magictrumpet-reached-two-playing-clients");
    }

    private static async Task<string[]> ReadEventSequenceAsync(NetworkStream stream, LegacyFrameCodec codec, TimeSpan timeout, string recipient)
    {
        var deadline = DateTime.UtcNow + timeout;
        var messages = new List<string>(ExpectedSequence.Length);
        for (var index = 0; index < 128 && messages.Count < ExpectedSequence.Length; index++)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException($"AutoDouble did not deliver both start/end message pairs to {recipient}; observed: {string.Join(" | ", messages)}.");

            using var cancellation = new CancellationTokenSource(remaining);
            var headerBytes = new byte[PacketHeader.SizeInBytes];
            DecodedFrame frame;
            try
            {
                await stream.ReadExactlyAsync(headerBytes, cancellation.Token);
                var header = PacketHeader.Read(headerBytes);
                if (header.Size < PacketHeader.SizeInBytes || header.Size > PacketHeader.MaxPacketSize)
                    throw new InvalidDataException($"Invalid response frame size {header.Size}.");
                var bytes = new byte[header.Size];
                headerBytes.CopyTo(bytes, 0);
                await stream.ReadExactlyAsync(bytes.AsMemory(PacketHeader.SizeInBytes), cancellation.Token);
                frame = codec.Decode(bytes);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw new TimeoutException($"AutoDouble timed out waiting for start/end messages for {recipient}; observed: {string.Join(" | ", messages)}.");
            }

            Console.WriteLine($"AUTODOUBLE_EVENT_WAIT_RX recipient={recipient} type=0x{frame.Header.Type:X4} size={frame.Header.Size} checksum={frame.IsChecksumValid}");
            Check(frame.IsChecksumValid, "AutoDouble announcement checksum is invalid.");

            if (frame.Header.Type == MessagePanelConfirmation.MessageType)
            {
                Check(frame.Header.Size == MessagePanelConfirmation.PacketSize, "AutoDouble MessagePanel size differs from the 7.69 wire.");
                var message = ReadCString(frame.Payload.Span);
                messages.Add($"MessagePanel:{message}");
                Console.WriteLine($"AUTODOUBLE_EVENT_MESSAGE recipient={recipient} kind=MessagePanel text={message}");
            }
            else if (frame.Header.Type == MagicTrumpetConfirmation.MessageType)
            {
                Check(frame.Header.Size == MagicTrumpetConfirmation.PacketSize, "AutoDouble MagicTrumpet size differs from the 7.69 wire.");
                var payload = frame.Payload.Span;
                Check(BinaryPrimitives.ReadInt32LittleEndian(payload[MagicTrumpetConfirmation.MessageLength..]) == MagicTrumpetConfirmation.SpeakColor,
                    "AutoDouble MagicTrumpet did not use TNColor::Speak.");
                var message = ReadCString(payload[..MagicTrumpetConfirmation.MessageLength]);
                messages.Add($"MagicTrumpet:{message}");
                Console.WriteLine($"AUTODOUBLE_EVENT_MESSAGE recipient={recipient} kind=MagicTrumpet text={message}");
            }
        }

        Check(messages.SequenceEqual(ExpectedSequence),
            $"AutoDouble messages arrived in an unexpected order/content for {recipient}: {string.Join(" | ", messages)}.");
        return messages.ToArray();
    }

    private static string ReadCString(ReadOnlySpan<byte> source)
    {
        var end = source.IndexOf((byte)0);
        return Encoding.Latin1.GetString(end < 0 ? source : source[..end]);
    }

    private static string CString(ReadOnlySpan<byte> source)
    {
        var end = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? source : source[..end]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
