using Porta.Core.Indexing;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class FolderBlockReaderTests : IDisposable
{
    private readonly string _dir;

    public FolderBlockReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public void Reads_block_bytes_matching_file_slice()
    {
        byte[] content = new byte[15_000];
        new Random(9).NextBytes(content);
        File.WriteAllBytes(Path.Combine(_dir, "f.bin"), content);
        IReadOnlyList<FileIndexEntry> index = new FolderScanner().Scan(_dir);
        ChunkInfo chunk = index.Single().Chunks[0];
        var reader = new FolderBlockReader(_dir, index);

        Assert.True(reader.TryGet(chunk.Hash, out byte[] data));
        Assert.Equal(content.AsSpan(0, chunk.Length).ToArray(), data);
    }

    [Fact]
    public void Unknown_hash_is_a_miss()
    {
        File.WriteAllBytes(Path.Combine(_dir, "f.bin"), [1, 2, 3]);
        var reader = new FolderBlockReader(_dir, new FolderScanner().Scan(_dir));

        Assert.False(reader.TryGet(new byte[32], out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
