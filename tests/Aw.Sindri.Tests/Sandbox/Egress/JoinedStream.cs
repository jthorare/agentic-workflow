using System.IO.Pipelines;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>A stream that reads from one pipe and writes to another; disposing it ends its writes (the peer sees EOF).</summary>
internal sealed class JoinedStream(Stream read, Stream write) : Stream, IHalfCloseable
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public static (Stream A, Stream B) Pair()
    {
        Pipe ab = new(), ba = new();
        return (new JoinedStream(ba.Reader.AsStream(), ab.Writer.AsStream()), new JoinedStream(ab.Reader.AsStream(), ba.Writer.AsStream()));
    }

    public void ShutdownSend() => write.Dispose();

    public override void Flush() => write.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => write.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => read.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => read.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => write.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => write.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            write.Dispose();
            read.Dispose();
        }

        base.Dispose(disposing);
    }
}
