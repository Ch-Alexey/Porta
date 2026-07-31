using MessagePack;
using Porta.Core.Indexing;

namespace Porta.Core.Protocol;

/// <summary>Запрос индекса хранилища. См. docs/features/10-sync-engine.md.</summary>
[MessagePackObject]
public sealed record FolderIndexRequest(
    [property: Key(0)] string StorageId);

/// <summary>
/// Индекс хранилища на проводе: список записей файлов с их блоками.
/// См. docs/features/08-index-exchange.md.
/// </summary>
[MessagePackObject]
public sealed record FolderIndexMessage(
    [property: Key(0)] string StorageId,
    [property: Key(1)] IReadOnlyList<FileIndexEntry> Entries);
