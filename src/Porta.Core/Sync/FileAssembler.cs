using System.Security.Cryptography;
using Porta.Core.Indexing;

namespace Porta.Core.Sync;

/// <summary>
/// Собирает файл из блоков по его записи индекса. Проверяет хеш каждого блока и итоговый
/// контент-хеш, пишет во временный файл и атомарно перемещает, сохраняет mtime и защищает
/// от выхода за пределы папки. См. docs/features/09-block-transfer.md.
/// </summary>
public static class FileAssembler
{
    public static void Write(string rootPath, FileIndexEntry entry, IBlockSource blocks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(blocks);

        string fullPath = ResolveSafePath(rootPath, entry.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string tempPath = fullPath + ".porta-tmp";

        try
        {
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (FileStream stream = File.Create(tempPath))
            {
                foreach (ChunkInfo chunk in entry.Chunks)
                {
                    if (!blocks.TryGet(chunk.Hash, out byte[] data))
                        throw new InvalidOperationException($"Отсутствует блок {chunk.HashHex}.");

                    if (data.Length != chunk.Length || !SHA256.HashData(data).AsSpan().SequenceEqual(chunk.Hash))
                        throw new InvalidDataException($"Блок {chunk.HashHex} не прошёл проверку.");

                    stream.Write(data);
                    hasher.AppendData(data);
                }

                if (!hasher.GetHashAndReset().AsSpan().SequenceEqual(entry.ContentHash))
                    throw new InvalidDataException($"Контент-хеш собранного файла не совпал: {entry.RelativePath}.");
            }

            File.Move(tempPath, fullPath, overwrite: true);
            File.SetLastWriteTimeUtc(fullPath, entry.ModifiedAt.UtcDateTime);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static string ResolveSafePath(string root, string relativePath)
    {
        string rootFull = Path.GetFullPath(root);
        string full = Path.GetFullPath(Path.Combine(rootFull, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;

        if (full != rootFull && !full.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Небезопасный путь вне хранилища: {relativePath}");

        return full;
    }
}
