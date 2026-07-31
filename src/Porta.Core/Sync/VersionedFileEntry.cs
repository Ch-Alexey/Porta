using MessagePack;
using Porta.Core.Indexing;

namespace Porta.Core.Sync;

/// <summary>
/// Запись файла вместе с её версией (version vector). Отдельная обёртка над
/// <see cref="FileIndexEntry"/>, чтобы не менять формат контент-индекса.
/// См. docs/features/14-versioned-index.md.
/// </summary>
[MessagePackObject]
public sealed record VersionedFileEntry(
    [property: Key(0)] FileIndexEntry Entry,
    [property: Key(1)] VersionVector Version);
