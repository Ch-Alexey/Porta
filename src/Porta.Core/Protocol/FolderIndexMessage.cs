using MessagePack;
using Porta.Core.Indexing;

namespace Porta.Core.Protocol;

/// <summary>
/// Индекс хранилища на проводе: список записей файлов с их блоками.
/// См. docs/features/08-index-exchange.md.
/// </summary>
[MessagePackObject]
public sealed record FolderIndexMessage(
    [property: Key(0)] string StorageId,
    [property: Key(1)] IReadOnlyList<FileIndexEntry> Entries);
