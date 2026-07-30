using System.Security.Cryptography;

namespace Porta.Core.Indexing;

/// <summary>
/// Рекурсивный обход папки хранилища с построением индекса файлов (метаданные + блоки).
/// См. docs/features/03-indexer.md.
/// </summary>
public sealed class FolderScanner
{
    private readonly ChunkerOptions _options;

    public FolderScanner(ChunkerOptions? options = null)
        => _options = options ?? ChunkerOptions.Default;

    /// <summary>
    /// Просканировать папку. Возвращает записи, отсортированные по относительному пути
    /// (стабильный порядок для сравнения индексов между устройствами).
    /// </summary>
    public IReadOnlyList<FileIndexEntry> Scan(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Папка хранилища не найдена: {rootPath}");

        var entries = new List<FileIndexEntry>();
        foreach (string file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            entries.Add(IndexFile(rootPath, file));

        entries.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));
        return entries;
    }

    private FileIndexEntry IndexFile(string rootPath, string filePath)
    {
        string relativePath = NormalizeRelativePath(rootPath, filePath);
        var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);

        using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using FileStream stream = File.OpenRead(filePath);
        List<ChunkInfo> chunks = Chunker.Split(stream, _options, fileHasher);

        long size = chunks.Count == 0 ? 0 : chunks[^1].Offset + chunks[^1].Length;

        return new FileIndexEntry(
            relativePath,
            size,
            modifiedAt,
            fileHasher.GetHashAndReset(),
            chunks);
    }

    private static string NormalizeRelativePath(string rootPath, string filePath)
        => Path.GetRelativePath(rootPath, filePath).Replace(Path.DirectorySeparatorChar, '/');
}
