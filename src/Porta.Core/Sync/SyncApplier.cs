using Porta.Core.Identity;

namespace Porta.Core.Sync;

/// <summary>Итог применения удалённого версионированного индекса.</summary>
public sealed record SyncApplyReport(int Accepted, int Skipped, int Conflicts);

/// <summary>
/// Применяет удалённый версионированный индекс к локальной папке с учётом конфликтов:
/// принять (с архивом прежней версии), пропустить или сохранить обе версии (конфликтная
/// копия). См. docs/features/16-conflict-apply.md.
/// </summary>
public static class SyncApplier
{
    public static SyncApplyReport Apply(
        string rootPath,
        IReadOnlyList<VersionedFileEntry> local,
        IReadOnlyList<VersionedFileEntry> remote,
        DeviceId remoteDeviceId,
        IBlockSource blocks,
        IVersionStore? versions = null,
        TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(blocks);
        clock ??= TimeProvider.System;

        var localByPath = new Dictionary<string, VersionedFileEntry>(StringComparer.Ordinal);
        foreach (VersionedFileEntry entry in local)
            localByPath[entry.Entry.RelativePath] = entry;

        int accepted = 0, skipped = 0, conflicts = 0;
        foreach (VersionedFileEntry remoteEntry in remote)
        {
            localByPath.TryGetValue(remoteEntry.Entry.RelativePath, out VersionedFileEntry? localEntry);

            switch (ConflictResolver.Decide(localEntry, remoteEntry))
            {
                case SyncAction.Accept:
                    FileAssembler.Write(rootPath, remoteEntry.Entry, blocks, versions);
                    accepted++;
                    break;

                case SyncAction.Skip:
                    skipped++;
                    break;

                case SyncAction.Conflict:
                    string conflictPath = ConflictResolver.ConflictName(
                        remoteEntry.Entry.RelativePath, remoteDeviceId, clock.GetUtcNow());
                    // Локальный файл не трогаем; удалённую версию пишем под конфликтным именем.
                    FileAssembler.Write(rootPath, remoteEntry.Entry with { RelativePath = conflictPath }, blocks, versions);
                    conflicts++;
                    break;
            }
        }

        return new SyncApplyReport(accepted, skipped, conflicts);
    }
}
