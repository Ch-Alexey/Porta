using MessagePack;

namespace Porta.Core.Indexing;

/// <summary>
/// Запись индекса о файле: метаданные, контент-хеш и список блоков.
/// См. docs/features/03-indexer.md.
/// </summary>
/// <param name="RelativePath">Путь относительно корня хранилища, разделитель — '/'.</param>
/// <param name="Size">Размер файла в байтах.</param>
/// <param name="ModifiedAt">Время последнего изменения (UTC).</param>
/// <param name="ContentHash">SHA-256 всего содержимого файла.</param>
/// <param name="Chunks">Блоки файла в порядке следования.</param>
[MessagePackObject]
public sealed record FileIndexEntry(
    [property: Key(0)] string RelativePath,
    [property: Key(1)] long Size,
    [property: Key(2)] DateTimeOffset ModifiedAt,
    [property: Key(3)] byte[] ContentHash,
    [property: Key(4)] IReadOnlyList<ChunkInfo> Chunks)
{
    /// <summary>Контент-хеш в hex-виде.</summary>
    [IgnoreMember]
    public string ContentHashHex => Convert.ToHexString(ContentHash);
}
