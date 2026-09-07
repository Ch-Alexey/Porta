using Porta.Core.Protocol;

namespace Porta.Core.Sync;

/// <summary>
/// Потоковая отдача блоков пачками: объём синка больше не ограничен размером одного
/// сообщения. См. docs/features/27-block-streaming.md.
/// </summary>
public static class BlockStreaming
{
    /// <summary>
    /// Бюджет одной пачки по содержимому блоков. С запасом под лимит сообщения —
    /// накладные MessagePack и один «хвостовой» блок за него не выведут.
    /// </summary>
    public const int BatchBudget = 4 * 1024 * 1024;

    /// <summary>
    /// Отдающая сторона: выслать запрошенные блоки пачками. Блоки, которых нет,
    /// молча пропускаются — принимающая сторона обнаружит нехватку при сборке файла.
    /// </summary>
    public static async Task<int> SendAsync(
        MessageChannel channel,
        IReadOnlyList<byte[]> hashes,
        IBlockSource? source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(hashes);

        var batch = new List<BlockData>();
        long budget = 0;
        int sent = 0;

        foreach (byte[] hash in hashes)
        {
            if (source is null || !source.TryGet(hash, out byte[] data))
                continue;

            batch.Add(new BlockData(hash, data));
            budget += data.Length;
            sent++;

            if (budget < BatchBudget)
                continue;

            await channel.WriteAsync(new BlockBatchMessage(batch, false), cancellationToken).ConfigureAwait(false);
            batch = [];
            budget = 0;
        }

        // Последняя пачка всегда одна и помечена явно — в том числе пустая.
        await channel.WriteAsync(new BlockBatchMessage(batch, true), cancellationToken).ConfigureAwait(false);
        return sent;
    }

    /// <summary>
    /// Принимающая сторона: вычитать пачки до последней, складывая блоки на диск.
    /// Возвращает число полученных блоков.
    /// </summary>
    public static async Task<int> ReceiveAsync(
        MessageChannel channel,
        SpooledBlockSource spool,
        long expectedBytes = 0,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(spool);

        var reporter = new ThrottledProgress(progress);
        int received = 0;
        long bytes = 0;
        while (true)
        {
            BlockBatchMessage batch = await channel.ReadAsync<BlockBatchMessage>(cancellationToken).ConfigureAwait(false);

            foreach (BlockData block in batch.Blocks)
            {
                spool.Add(block.Hash, block.Data);
                bytes += block.Data.Length;
                received++;
            }

            reporter.Report(new TransferProgress(0, 0, bytes, expectedBytes));
            if (!batch.IsLast)
                continue;

            reporter.ReportFinal(new TransferProgress(0, 0, bytes, expectedBytes));
            return received;
        }
    }
}
