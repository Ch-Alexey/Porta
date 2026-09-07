using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Protocol;

namespace Porta.Core.Sync;

/// <summary>
/// Версионированная синхронизация хранилища: обмен версионированными индексами,
/// разрешение конфликтов и персистентность версий. См. docs/features/17-versioned-sync.md.
/// </summary>
public static class VersionedSync
{
    /// <summary>Пересканировать папку, обновив версии, и сохранить индекс.</summary>
    public static IReadOnlyList<VersionedFileEntry> Rescan(
        FileIndexRepository repository,
        string storageId,
        string folder,
        DeviceId localDeviceId,
        IgnoreRules? ignore = null)
    {
        IReadOnlyList<VersionedFileEntry> previous = repository.Load(storageId);
        IReadOnlyList<FileIndexEntry> scanned = new FolderScanner().Scan(folder, ignore);
        IReadOnlyList<VersionedFileEntry> versioned = IndexVersioning.Apply(previous, scanned, localDeviceId);
        repository.Replace(storageId, versioned);
        return versioned;
    }

    /// <summary>
    /// Отдающая сторона: по запрошенному storageId отдать версионированный индекс и блоки.
    /// Папку хранилища определяет <paramref name="resolveFolder"/> (null → хранилище
    /// неизвестно/недоступно, отдаём пустой индекс).
    /// </summary>
    public static async Task ServeAsync(
        MessageChannel channel,
        FileIndexRepository repository,
        Func<string, string?> resolveFolder,
        DeviceId localDeviceId,
        IgnoreRules? ignore = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(resolveFolder);

        FolderIndexRequest requestIndex = await channel.ReadAsync<FolderIndexRequest>(cancellationToken).ConfigureAwait(false);
        string storageId = requestIndex.StorageId;
        string? folder = resolveFolder(storageId);

        IReadOnlyList<VersionedFileEntry> index = folder is null
            ? []
            : Rescan(repository, storageId, folder, localDeviceId, ignore);
        await channel.WriteAsync(new VersionedFolderIndexMessage(storageId, index), cancellationToken).ConfigureAwait(false);

        var reader = folder is null ? null : new FolderBlockReader(folder, index.Select(v => v.Entry).ToList());
        BlockRequestMessage request = await channel.ReadAsync<BlockRequestMessage>(cancellationToken).ConfigureAwait(false);

        // Пачками, а не одним сообщением: объём синка не ограничен лимитом сообщения.
        await BlockStreaming.SendAsync(channel, request.Hashes, reader, cancellationToken).ConfigureAwait(false);

        // Подтверждение приёма: даём принимающей стороне дочитать ответ до закрытия.
        // Best-effort — если она уже закрыла соединение, данные всё равно доставлены.
        try
        {
            _ = await channel.ReadAsync<SyncCompleteMessage>(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Соединение закрыто второй стороной после получения данных — это норма.
        }
    }

    /// <summary>Принимающая сторона: получить удалённый индекс, разрешить конфликты, применить.</summary>
    public static async Task<SyncApplyReport> PullAsync(
        MessageChannel channel,
        FileIndexRepository repository,
        string folder,
        string storageId,
        DeviceId localDeviceId,
        DeviceId remoteDeviceId,
        IVersionStore? versions = null,
        IgnoreRules? ignore = null,
        TimeProvider? clock = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        IReadOnlyList<VersionedFileEntry> localIndex = Rescan(repository, storageId, folder, localDeviceId, ignore);
        var localByPath = ToDictionary(localIndex);

        await channel.WriteAsync(new FolderIndexRequest(storageId), cancellationToken).ConfigureAwait(false);
        VersionedFolderIndexMessage remote =
            await channel.ReadAsync<VersionedFolderIndexMessage>(cancellationToken).ConfigureAwait(false);

        byte[][] needed = CollectNeededBlocks(localIndex, localByPath, remote.Entries);
        await channel.WriteAsync(new BlockRequestMessage(needed), cancellationToken).ConfigureAwait(false);

        // Полученные блоки складываются на диск: память не зависит от объёма синка.
        using var received = new SpooledBlockSource();
        await BlockStreaming.ReceiveAsync(channel, received, cancellationToken).ConfigureAwait(false);

        var source = new CompositeBlockSource(received, new FolderBlockReader(folder, localIndex.Select(v => v.Entry).ToList()));

        SyncApplyReport report = SyncApplier.Apply(folder, localIndex, remote.Entries, remoteDeviceId, source, versions, clock);

        repository.Replace(storageId, MergeAfterPull(localByPath, remote.Entries));

        // Подтвердить приём — сигнал отдающей стороне, что можно закрывать соединение.
        // Best-effort: данные уже применены, обрыв на этом шаге не влияет на результат.
        try
        {
            await channel.WriteAsync(new SyncCompleteMessage(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Отдающая сторона уже закрыла соединение — это норма.
        }

        return report;
    }

    private static byte[][] CollectNeededBlocks(
        IReadOnlyList<VersionedFileEntry> localIndex,
        Dictionary<string, VersionedFileEntry> localByPath,
        IReadOnlyList<VersionedFileEntry> remote)
    {
        var localBlocks = new HashSet<string>(StringComparer.Ordinal);
        foreach (VersionedFileEntry entry in localIndex)
            foreach (ChunkInfo chunk in entry.Entry.Chunks)
                localBlocks.Add(chunk.HashHex);

        var needed = new List<byte[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (VersionedFileEntry remoteEntry in remote)
        {
            localByPath.TryGetValue(remoteEntry.Entry.RelativePath, out VersionedFileEntry? local);
            if (ConflictResolver.Decide(local, remoteEntry) == SyncAction.Skip)
                continue;

            foreach (ChunkInfo chunk in remoteEntry.Entry.Chunks)
            {
                if (localBlocks.Contains(chunk.HashHex) || !seen.Add(chunk.HashHex))
                    continue;
                needed.Add(chunk.Hash);
            }
        }
        return needed.ToArray();
    }

    private static IReadOnlyList<VersionedFileEntry> MergeAfterPull(
        Dictionary<string, VersionedFileEntry> localByPath,
        IReadOnlyList<VersionedFileEntry> remote)
    {
        var merged = new Dictionary<string, VersionedFileEntry>(localByPath, StringComparer.Ordinal);
        foreach (VersionedFileEntry remoteEntry in remote)
        {
            localByPath.TryGetValue(remoteEntry.Entry.RelativePath, out VersionedFileEntry? local);
            // Принятые файлы принимают версию удалённого; конфликт/skip оставляют локальное
            // (конфликтная копия попадёт в индекс при следующем rescan).
            if (ConflictResolver.Decide(local, remoteEntry) == SyncAction.Accept)
            {
                merged[remoteEntry.Entry.RelativePath] = remoteEntry;
                continue;
            }

            // Содержимое совпало, а векторы разошлись — сливаем их, иначе стороны так и
            // останутся расходящимися. См. docs/features/31-audit-fixes.md.
            if (local is not null && local.Entry.ContentHash.AsSpan().SequenceEqual(remoteEntry.Entry.ContentHash))
                merged[remoteEntry.Entry.RelativePath] = local with { Version = local.Version.Merge(remoteEntry.Version) };
        }

        return merged.Values.OrderBy(v => v.Entry.RelativePath, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, VersionedFileEntry> ToDictionary(IReadOnlyList<VersionedFileEntry> entries)
    {
        var map = new Dictionary<string, VersionedFileEntry>(StringComparer.Ordinal);
        foreach (VersionedFileEntry entry in entries)
            map[entry.Entry.RelativePath] = entry;
        return map;
    }
}
