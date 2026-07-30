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
public sealed record FileIndexEntry(
    string RelativePath,
    long Size,
    DateTimeOffset ModifiedAt,
    byte[] ContentHash,
    IReadOnlyList<ChunkInfo> Chunks)
{
    /// <summary>Контент-хеш в hex-виде.</summary>
    public string ContentHashHex => Convert.ToHexString(ContentHash);
}
