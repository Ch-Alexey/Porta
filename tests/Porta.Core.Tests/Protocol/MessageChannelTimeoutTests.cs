using Porta.Core.Protocol;
using Porta.Core.Tests.Sync;

namespace Porta.Core.Tests.Protocol;

/// <summary>
/// Таймаут чтения: молчащая вторая сторона больше не вешает нашу навсегда.
/// См. docs/features/27-block-streaming.md.
/// </summary>
public class MessageChannelTimeoutTests
{
    [Fact]
    public async Task Silent_peer_ends_in_timeout_instead_of_hanging()
    {
        var channel = new MessageChannel(Null.Stream) { IdleTimeout = TimeSpan.FromMilliseconds(150) };

        await Assert.ThrowsAsync<TimeoutException>(async () => await channel.ReadAsync<HelloMessage>());
    }

    [Fact]
    public async Task Message_arriving_in_time_is_read_normally()
    {
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();

        await a.WriteAsync(new HelloMessage(ProtocolVersion.Current, "A"));
        HelloMessage hello = await b.ReadAsync<HelloMessage>();

        Assert.Equal("A", hello.DeviceName);
    }

    [Fact]
    public async Task Caller_cancellation_stays_a_cancellation_not_a_timeout()
    {
        var channel = new MessageChannel(Null.Stream) { IdleTimeout = TimeSpan.FromMinutes(5) };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await channel.ReadAsync<HelloMessage>(cts.Token));
    }

    [Fact]
    public async Task Infinite_timeout_is_available_for_callers_who_want_it()
    {
        var channel = new MessageChannel(Null.Stream) { IdleTimeout = Timeout.InfiniteTimeSpan };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        // Без предела ожидания чтение живёт ровно до отмены вызывающего.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await channel.ReadAsync<HelloMessage>(cts.Token));
    }
}

/// <summary>Поток, который никогда ничего не отдаёт — изображает молчащего пира.</summary>
internal static class Null
{
    public static Stream Stream => new SilentStream();

    private sealed class SilentStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
