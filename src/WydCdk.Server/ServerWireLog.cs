using System.Text;
using WydCdk.Protocol;

internal sealed class ServerWireLog : IDisposable
{
    private readonly StreamWriter writer;
    private readonly object gate = new();

    private ServerWireLog(StreamWriter writer)
    {
        this.writer = writer;
    }

    public static ServerWireLog? TryOpen(string worldKey)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"server-{worldKey}.log");
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };
            var log = new ServerWireLog(writer);
            log.Write($"LOG OPEN world={worldKey} path={path}");
            return log;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Could not open persistent server log for world {worldKey}: {error.GetType().Name}: {error.Message}");
            return null;
        }
    }

    public void Write(string message)
    {
        lock (gate)
            writer.WriteLine($"{DateTimeOffset.Now:O} {message}");
    }

    public void WriteFrame(string direction, int connectionId, DecodedFrame frame, string? note = null)
    {
        var payload = IsSensitiveFrame(frame.Header.Type)
            ? "<redacted-auth-payload>"
            : Convert.ToHexString(frame.Payload.Span);
        Write($"{direction} connection={connectionId} type=0x{frame.Header.Type:X4} id={frame.Header.Id} size={frame.Header.Size} tick={frame.Header.ClientTick} checksum={frame.IsChecksumValid} payload={payload}{(note is null ? string.Empty : $" {note}")}");
    }

    public void WriteRawFrame(string direction, int connectionId, ReadOnlySpan<byte> encodedFrame, LegacyFrameCodec codec, string? note = null)
    {
        try
        {
            var frame = codec.Decode(encodedFrame);
            WriteFrame(direction, connectionId, frame, note);
        }
        catch (Exception error)
        {
            Write($"{direction} connection={connectionId} invalid-frame bytes={encodedFrame.Length} error={error.GetType().Name}: {error.Message}{(note is null ? string.Empty : $" {note}")}");
        }
    }

    public TextWriter CreateTee(TextWriter consoleWriter) => new TeeTextWriter(consoleWriter, this);

    private static bool IsSensitiveFrame(ushort type)
    {
        return type is
            AccountLoginRequest.MessageType or
            AccountSecureRequest.MessageType or
            CreateCharacterRequest.MessageType;
    }

    public void Dispose()
    {
        lock (gate)
        {
            writer.WriteLine($"{DateTimeOffset.Now:O} LOG CLOSE");
            writer.Dispose();
        }
    }

    private sealed class TeeTextWriter(TextWriter consoleWriter, ServerWireLog log) : TextWriter
    {
        public override Encoding Encoding => consoleWriter.Encoding;

        public override void Write(char value)
        {
            consoleWriter.Write(value);
        }

        public override void Write(string? value)
        {
            consoleWriter.Write(value);
        }

        public override void WriteLine(string? value)
        {
            consoleWriter.WriteLine(value);
            if (!string.IsNullOrEmpty(value))
                log.Write($"CONSOLE {value}");
        }

        protected override void Dispose(bool disposing)
        {
            // Console.Out is process-owned; only the ServerWireLog owns its file.
            base.Dispose(disposing);
        }
    }
}
