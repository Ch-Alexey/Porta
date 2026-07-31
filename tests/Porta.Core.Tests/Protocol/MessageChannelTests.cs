using System.Buffers.Binary;
using Porta.Core.Protocol;

namespace Porta.Core.Tests.Protocol;

public class MessageChannelTests
{
    [Fact]
    public async Task Write_then_read_roundtrips_message()
    {
        var buffer = new MemoryStream();
        var writer = new MessageChannel(buffer);
        var message = new HelloMessage(ProtocolVersion.Current, "Laptop");

        await writer.WriteAsync(message);
        buffer.Position = 0;
        var read = await new MessageChannel(buffer).ReadAsync<HelloMessage>();

        Assert.Equal(message, read);
    }

    [Fact]
    public async Task Multiple_messages_read_in_order()
    {
        var buffer = new MemoryStream();
        var writer = new MessageChannel(buffer);
        await writer.WriteAsync(new HelloMessage(1, "A"));
        await writer.WriteAsync(new HelloMessage(1, "B"));
        buffer.Position = 0;
        var reader = new MessageChannel(buffer);

        Assert.Equal("A", (await reader.ReadAsync<HelloMessage>()).DeviceName);
        Assert.Equal("B", (await reader.ReadAsync<HelloMessage>()).DeviceName);
    }

    [Fact]
    public async Task Read_rejects_oversized_length()
    {
        var buffer = new MemoryStream();
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, MessageChannel.MaxMessageSize + 1);
        buffer.Write(header);
        buffer.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await new MessageChannel(buffer).ReadAsync<HelloMessage>());
    }

    [Fact]
    public async Task Read_on_truncated_stream_throws()
    {
        var buffer = new MemoryStream([0, 0, 0, 10, 1, 2]); // заявлено 10 байт, есть 2
        await Assert.ThrowsAsync<EndOfStreamException>(
            async () => await new MessageChannel(buffer).ReadAsync<HelloMessage>());
    }
}
