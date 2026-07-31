using Porta.Core.Indexing;
using Porta.Core.Protocol;

namespace Porta.Core.Sync;

/// <summary>Итог одного pull'а хранилища.</summary>
/// <param name="FilesUpdated">Сколько файлов создано/обновлено.</param>
/// <param name="BlocksReceived">Сколько блоков получено от второй стороны.</param>
/// <param name="BytesReceived">Суммарный размер обновлённых файлов (байты).</param>
public sealed record SyncResult(int FilesUpdated, int BlocksReceived, long BytesReceived);

/// <summary>
/// Оркестрация синхронизации хранилища поверх канала сообщений: одна сторона тянет
/// (pull) изменения у другой. См. docs/features/10-sync-engine.md.
/// </summary>
public static class SyncProtocol
{
    /// <summary>Отдающая сторона: отвечает на запрос индекса и на запрос блоков.</summary>
    public static async Task ServeAsync(
        MessageChannel channel,
        string folder,
        string storageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        _ = await channel.ReadAsync<FolderIndexRequest>(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<FileIndexEntry> index = new FolderScanner().Scan(folder);
        await channel.WriteAsync(new FolderIndexMessage(storageId, index), cancellationToken).ConfigureAwait(false);

        var reader = new FolderBlockReader(folder, index);
        BlockRequestMessage request = await channel.ReadAsync<BlockRequestMessage>(cancellationToken).ConfigureAwait(false);

        var served = new List<BlockData>(request.Hashes.Count);
        foreach (byte[] hash in request.Hashes)
            if (reader.TryGet(hash, out byte[] data))
                served.Add(new BlockData(hash, data));

        await channel.WriteAsync(new BlockResponseMessage(served), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Принимающая сторона: ведёт весь диалог и собирает обновлённые файлы.</summary>
    public static async Task<SyncResult> PullAsync(
        MessageChannel channel,
        string folder,
        string storageId,
        IVersionStore? versions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await channel.WriteAsync(new FolderIndexRequest(storageId), cancellationToken).ConfigureAwait(false);
        FolderIndexMessage remote = await channel.ReadAsync<FolderIndexMessage>(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<FileIndexEntry> localIndex = new FolderScanner().Scan(folder);
        IndexDiff diff = IndexComparer.Compare(localIndex, remote.Entries);

        await channel.WriteAsync(
            new BlockRequestMessage(diff.MissingBlocks.Select(b => b.Hash).ToArray()),
            cancellationToken).ConfigureAwait(false);
        BlockResponseMessage response = await channel.ReadAsync<BlockResponseMessage>(cancellationToken).ConfigureAwait(false);

        var received = new MemoryBlockSource(
            response.Blocks.Select(b => new KeyValuePair<byte[], byte[]>(b.Hash, b.Data)));
        var source = new CompositeBlockSource(received, new FolderBlockReader(folder, localIndex));

        long bytes = 0;
        foreach (FileIndexEntry file in diff.FilesToUpdate)
        {
            FileAssembler.Write(folder, file, source, versions);
            bytes += file.Size;
        }

        return new SyncResult(diff.FilesToUpdate.Count, response.Blocks.Count, bytes);
    }
}
