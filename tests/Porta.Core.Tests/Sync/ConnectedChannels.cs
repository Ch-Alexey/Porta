using System.IO.Pipelines;
using Porta.Core.Protocol;

namespace Porta.Core.Tests.Sync;

/// <summary>Поток, читающий из одного потока и пишущий в другой (одна сторона дуплекса).</summary>
internal sealed class DuplexStream(Stream readSide, Stream writeSide) : Stream
{
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => readSide.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => readSide.ReadAsync(buffer, offset, count, cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => readSide.Read(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => writeSide.WriteAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => writeSide.Write(buffer, offset, count);

    public override Task FlushAsync(CancellationToken cancellationToken) => writeSide.FlushAsync(cancellationToken);
    public override void Flush() => writeSide.Flush();

    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>Пара связанных in-memory каналов сообщений — для тестов диалогов без сети.</summary>
internal static class ConnectedChannels
{
    public static (MessageChannel A, MessageChannel B) Create()
    {
        var aToB = new Pipe();
        var bToA = new Pipe();
        var a = new DuplexStream(bToA.Reader.AsStream(), aToB.Writer.AsStream());
        var b = new DuplexStream(aToB.Reader.AsStream(), bToA.Writer.AsStream());
        return (new MessageChannel(a), new MessageChannel(b));
    }
}
