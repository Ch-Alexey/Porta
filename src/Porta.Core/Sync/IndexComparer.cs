using Porta.Core.Indexing;

namespace Porta.Core.Sync;

/// <summary>
/// Сравнение локального и удалённого индексов хранилища → что нужно догрузить.
/// Удаления НЕ распространяются (локальные-только файлы игнорируются). Блоки
/// дедуплицируются по всему локальному набору. См. docs/features/08-index-exchange.md.
/// </summary>
public static class IndexComparer
{
    public static IndexDiff Compare(
        IReadOnlyList<FileIndexEntry> local,
        IReadOnlyList<FileIndexEntry> remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        var localByPath = new Dictionary<string, FileIndexEntry>(StringComparer.Ordinal);
        foreach (FileIndexEntry entry in local)
            localByPath[entry.RelativePath] = entry;

        var localBlocks = new HashSet<string>(StringComparer.Ordinal);
        foreach (FileIndexEntry entry in local)
            foreach (ChunkInfo chunk in entry.Chunks)
                localBlocks.Add(chunk.HashHex);

        // Новые или изменившиеся файлы (по контент-хешу). Отсутствующие у удалённого —
        // игнорируем: удаления не распространяем.
        var filesToUpdate = new List<FileIndexEntry>();
        foreach (FileIndexEntry remoteEntry in remote)
        {
            if (localByPath.TryGetValue(remoteEntry.RelativePath, out FileIndexEntry? localEntry)
                && localEntry.ContentHash.AsSpan().SequenceEqual(remoteEntry.ContentHash))
                continue;
            filesToUpdate.Add(remoteEntry);
        }

        // Различные блоки, которых нет ни в одном локальном файле.
        var missingBlocks = new List<NeededBlock>();
        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (FileIndexEntry file in filesToUpdate)
            foreach (ChunkInfo chunk in file.Chunks)
            {
                if (localBlocks.Contains(chunk.HashHex))
                    continue;
                if (!requested.Add(chunk.HashHex))
                    continue;
                missingBlocks.Add(new NeededBlock(chunk.Hash, chunk.Length));
            }

        return new IndexDiff(filesToUpdate, missingBlocks);
    }
}
