using System.Security.Cryptography;

namespace Porta.Core.Indexing;

/// <summary>
/// Content-defined chunking (gear-hash, FastCDC-lite). Режет поток на блоки по содержимому.
/// См. docs/features/03-indexer.md.
/// </summary>
public static class Chunker
{
    private const int ReadBufferSize = 64 * 1024;

    /// <summary>
    /// Разрезать поток на блоки. Опционально скармливает все прочитанные байты
    /// <paramref name="fileHasher"/> (чтобы за один проход посчитать хеш всего файла).
    /// </summary>
    public static List<ChunkInfo> Split(Stream stream, ChunkerOptions options, IncrementalHash? fileHasher = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        var chunks = new List<ChunkInfo>();
        byte[] readBuffer = new byte[ReadBufferSize];
        byte[] chunkBuffer = new byte[options.MaxSize];
        int chunkLength = 0;
        long chunkOffset = 0;
        ulong fingerprint = 0;
        ulong[] gear = GearTable.Values;

        int read;
        while ((read = stream.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            fileHasher?.AppendData(readBuffer, 0, read);

            for (int i = 0; i < read; i++)
            {
                byte b = readBuffer[i];
                chunkBuffer[chunkLength++] = b;

                // До MinSize границы не ищем (FastCDC пропускает первые MinSize байт).
                if (chunkLength < options.MinSize)
                    continue;

                fingerprint = (fingerprint << 1) + gear[b];
                ulong mask = chunkLength <= options.AvgSize ? options.MaskS : options.MaskL;

                if ((fingerprint & mask) == 0 || chunkLength >= options.MaxSize)
                {
                    chunks.Add(Finalize(chunkBuffer, chunkLength, chunkOffset));
                    chunkOffset += chunkLength;
                    chunkLength = 0;
                    fingerprint = 0;
                }
            }
        }

        if (chunkLength > 0)
            chunks.Add(Finalize(chunkBuffer, chunkLength, chunkOffset));

        return chunks;
    }

    private static ChunkInfo Finalize(byte[] buffer, int length, long offset)
    {
        byte[] hash = SHA256.HashData(buffer.AsSpan(0, length));
        return new ChunkInfo(offset, length, hash);
    }
}
