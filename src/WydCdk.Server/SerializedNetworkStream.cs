using System.Net.Sockets;

/// <summary>
/// Serializes writes to one client connection. WorldHub broadcasts can run while
/// the connection loop is sending its own response; overlapping writes would
/// otherwise be able to interleave legacy frames on the TCP stream.
/// </summary>
internal sealed class SerializedNetworkStream(NetworkStream inner) : Stream
{
    private readonly SemaphoreSlim writeGate = new(1, 1);

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }

    public override void Flush() => inner.Flush();

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try { await inner.FlushAsync(cancellationToken); }
        finally { writeGate.Release(); }
    }

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => inner.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        writeGate.Wait();
        try { inner.Write(buffer, offset, count); }
        finally { writeGate.Release(); }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        writeGate.Wait();
        try { inner.Write(buffer); }
        finally { writeGate.Release(); }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try { await inner.WriteAsync(buffer, offset, count, cancellationToken); }
        finally { writeGate.Release(); }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await writeGate.WaitAsync(cancellationToken);
        try { await inner.WriteAsync(buffer, cancellationToken); }
        finally { writeGate.Release(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
            writeGate.Dispose();
        }

        base.Dispose(disposing);
    }
}
