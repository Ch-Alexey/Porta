using Porta.Core.Identity;
using Porta.Core.Indexing;

namespace Porta.Core.Sync;

/// <summary>
/// Обновление версий файлов по прежнему версионированному индексу и свежему скану.
/// Изменившийся файл увеличивает счётчик этого устройства. Чистая логика.
/// См. docs/features/14-versioned-index.md.
/// </summary>
public static class IndexVersioning
{
    public static IReadOnlyList<VersionedFileEntry> Apply(
        IReadOnlyList<VersionedFileEntry> previous,
        IReadOnlyList<FileIndexEntry> current,
        DeviceId localDeviceId)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var previousByPath = new Dictionary<string, VersionedFileEntry>(StringComparer.Ordinal);
        foreach (VersionedFileEntry entry in previous)
            previousByPath[entry.Entry.RelativePath] = entry;

        var result = new List<VersionedFileEntry>(current.Count);
        foreach (FileIndexEntry entry in current)
        {
            if (previousByPath.TryGetValue(entry.RelativePath, out VersionedFileEntry? prior))
            {
                bool changed = !prior.Entry.ContentHash.AsSpan().SequenceEqual(entry.ContentHash);
                VersionVector version = changed ? prior.Version.Increment(localDeviceId) : prior.Version;
                result.Add(new VersionedFileEntry(entry, version));
            }
            else
            {
                result.Add(new VersionedFileEntry(entry, VersionVector.Empty.Increment(localDeviceId)));
            }
        }

        return result;
    }
}
