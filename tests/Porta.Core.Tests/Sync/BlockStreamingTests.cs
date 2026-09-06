using System.Security.Cryptography;
using System.Text;
using Porta.Core.Protocol;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class BlockStreamingTests
{
    /// <summary>Блоки в памяти для отдающей стороны.</summary>
    private static (byte[][] Hashes, MemoryBlockSource Source) MakeBlocks(int count, int size)
    {
        var pairs = new List<KeyValuePair<byte[], byte[]>>(count);
        var hashes = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            byte[] data = new byte[size];
            new Random(i).NextBytes(data);
            byte[] hash = SHA256.HashData(data);
            hashes[i] = hash;
            pairs.Add(new KeyValuePair<byte[], byte[]>(hash, data));
        }
        return (hashes, new MemoryBlockSource(pairs));
    }

    [Fact]
    public async Task Sends_all_blocks_through_the_spool()
    {
        (byte[][] hashes, MemoryBlockSource source) = MakeBlocks(count: 8, size: 1024);
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        using var spool = new SpooledBlockSource();

        Task<int> send = BlockStreaming.SendAsync(a, hashes, source);
        Task<int> receive = BlockStreaming.ReceiveAsync(b, spool);
        await Task.WhenAll(send, receive);

        Assert.Equal(8, await send);
        Assert.Equal(8, await receive);
        foreach (byte[] hash in hashes)
            Assert.True(spool.TryGet(hash, out _));
    }

    [Fact]
    public async Task Large_payload_is_split_into_several_batches()
    {
        // Иначе тест «файл больше лимита сообщения» ничего не доказывал бы:
        // важно, что пачек действительно несколько.
        (byte[][] hashes, MemoryBlockSource source) = MakeBlocks(count: 200, size: 64 * 1024);
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();

        Task<int> send = BlockStreaming.SendAsync(a, hashes, source);

        int batches = 0;
        while (true)
        {
            BlockBatchMessage batch = await b.ReadAsync<BlockBatchMessage>();
            batches++;
            if (batch.IsLast)
                break;
        }
        await send;

        // 200 × 64 КиБ = 12.5 МиБ при бюджете 4 МиБ.
        Assert.True(batches >= 4, $"ожидалось несколько пачек, получено {batches}");
    }

    [Fact]
    public async Task Every_batch_stays_under_the_message_limit()
    {
        (byte[][] hashes, MemoryBlockSource source) = MakeBlocks(count: 200, size: 64 * 1024);
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();

        Task<int> send = BlockStreaming.SendAsync(a, hashes, source);

        while (true)
        {
            BlockBatchMessage batch = await b.ReadAsync<BlockBatchMessage>();
            long bytes = batch.Blocks.Sum(block => (long)block.Data.Length);
            Assert.True(bytes < MessageChannel.MaxMessageSize, $"пачка {bytes} байт превысила лимит сообщения");
            if (batch.IsLast)
                break;
        }
        await send;
    }

    [Fact]
    public async Task Empty_request_ends_with_a_single_empty_batch()
    {
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        using var spool = new SpooledBlockSource();

        Task<int> send = BlockStreaming.SendAsync(a, [], new MemoryBlockSource([]));
        Task<int> receive = BlockStreaming.ReceiveAsync(b, spool);
        await Task.WhenAll(send, receive);

        Assert.Equal(0, await send);
        Assert.Equal(0, await receive);
    }

    [Fact]
    public async Task Unknown_hashes_are_skipped_without_breaking_the_dialogue()
    {
        byte[] missing = SHA256.HashData(Encoding.UTF8.GetBytes("нет такого блока"));
        (byte[][] hashes, MemoryBlockSource source) = MakeBlocks(count: 2, size: 512);
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        using var spool = new SpooledBlockSource();

        Task<int> send = BlockStreaming.SendAsync(a, [hashes[0], missing, hashes[1]], source);
        Task<int> receive = BlockStreaming.ReceiveAsync(b, spool);
        await Task.WhenAll(send, receive);

        Assert.Equal(2, await receive);
        Assert.False(spool.TryGet(missing, out _));
    }
}
