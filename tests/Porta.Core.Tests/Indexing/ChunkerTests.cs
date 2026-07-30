using System.Security.Cryptography;
using Porta.Core.Indexing;

namespace Porta.Core.Tests.Indexing;

public class ChunkerTests
{
    private static byte[] RandomBytes(int length, int seed)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static List<ChunkInfo> Chunk(byte[] data)
        => Chunker.Split(new MemoryStream(data), ChunkerOptions.Default);

    [Fact]
    public void Empty_stream_yields_no_chunks()
    {
        Assert.Empty(Chunk([]));
    }

    [Fact]
    public void Data_smaller_than_min_size_is_single_chunk()
    {
        byte[] data = RandomBytes(100, seed: 1);

        List<ChunkInfo> chunks = Chunk(data);

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Offset);
        Assert.Equal(100, chunks[0].Length);
        Assert.Equal(SHA256.HashData(data), chunks[0].Hash);
    }

    [Fact]
    public void Chunking_is_deterministic()
    {
        byte[] data = RandomBytes(300_000, seed: 7);

        List<ChunkInfo> a = Chunk(data);
        List<ChunkInfo> b = Chunk(data);

        Assert.Equal(
            a.Select(c => (c.Offset, c.Length, c.HashHex)),
            b.Select(c => (c.Offset, c.Length, c.HashHex)));
    }

    [Fact]
    public void Chunks_cover_whole_file_without_gaps()
    {
        byte[] data = RandomBytes(300_000, seed: 11);
        var opt = ChunkerOptions.Default;

        List<ChunkInfo> chunks = Chunk(data);

        long expectedOffset = 0;
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(expectedOffset, chunks[i].Offset);
            Assert.InRange(chunks[i].Length, 1, opt.MaxSize);
            if (i < chunks.Count - 1)
                Assert.True(chunks[i].Length >= opt.MinSize, "Не-последний блок короче MinSize.");
            expectedOffset += chunks[i].Length;
        }
        Assert.Equal(data.Length, expectedOffset);
    }

    [Fact]
    public void Inserting_one_byte_at_start_keeps_most_chunks_stable()
    {
        // Ключевое свойство CDC: локальная правка не должна пересобрать весь файл.
        byte[] original = RandomBytes(300_000, seed: 21);
        byte[] edited = new byte[original.Length + 1];
        edited[0] = original[0];
        edited[1] = 0xAB;                    // вставленный байт около начала
        Array.Copy(original, 1, edited, 2, original.Length - 1);

        var before = Chunk(original).Select(c => c.HashHex).ToHashSet();
        var after = Chunk(edited).Select(c => c.HashHex).ToList();

        int survived = after.Count(h => before.Contains(h));
        // Подавляющее большинство блоков должно уцелеть (иначе CDC не работает).
        Assert.True(survived >= before.Count * 0.7,
            $"Уцелело слишком мало блоков: {survived} из {before.Count}.");
    }

    [Fact]
    public void FileHasher_receives_full_content()
    {
        byte[] data = RandomBytes(50_000, seed: 33);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Chunker.Split(new MemoryStream(data), ChunkerOptions.Default, hasher);

        Assert.Equal(SHA256.HashData(data), hasher.GetHashAndReset());
    }
}
