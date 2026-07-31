using Porta.Core.Indexing;

namespace Porta.Core.Sync;

/// <summary>
/// Читает блоки из файлов папки по их хешу, используя индекс (карта хеш → файл/смещение).
/// Отдающая сторона синхронизации. См. docs/features/09-block-transfer.md.
/// </summary>
public sealed class FolderBlockReader : IBlockSource
{
    private readonly string _root;
    private readonly Dictionary<string, (string Path, long Offset, int Length)> _map = new(StringComparer.Ordinal);

    public FolderBlockReader(string rootPath, IReadOnlyList<FileIndexEntry> index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(index);
        _root = rootPath;

        foreach (FileIndexEntry entry in index)
            foreach (ChunkInfo chunk in entry.Chunks)
                _map.TryAdd(chunk.HashHex, (entry.RelativePath, chunk.Offset, chunk.Length));
    }

    public bool TryGet(byte[] hash, out byte[] data)
    {
        if (!_map.TryGetValue(Convert.ToHexString(hash), out (string Path, long Offset, int Length) location))
        {
            data = [];
            return false;
        }

        string full = Path.Combine(_root, location.Path.Replace('/', Path.DirectorySeparatorChar));
        using FileStream stream = File.OpenRead(full);
        stream.Seek(location.Offset, SeekOrigin.Begin);
        var buffer = new byte[location.Length];
        stream.ReadExactly(buffer);
        data = buffer;
        return true;
    }
}
