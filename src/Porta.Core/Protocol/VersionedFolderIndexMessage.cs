using MessagePack;
using Porta.Core.Sync;

namespace Porta.Core.Protocol;

/// <summary>
/// Версионированный индекс хранилища на проводе: файлы вместе с их version vectors.
/// См. docs/features/17-versioned-sync.md.
/// </summary>
[MessagePackObject]
public sealed record VersionedFolderIndexMessage(
    [property: Key(0)] string StorageId,
    [property: Key(1)] IReadOnlyList<VersionedFileEntry> Entries);
