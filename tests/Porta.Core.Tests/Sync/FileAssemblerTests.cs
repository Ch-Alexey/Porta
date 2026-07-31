using System.Security.Cryptography;
using System.Text;
using Porta.Core.Indexing;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class FileAssemblerTests : IDisposable
{
    private readonly string _srcDir;
    private readonly string _dstDir;

    public FileAssemblerTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _srcDir = Path.Combine(baseDir, "src");
        _dstDir = Path.Combine(baseDir, "dst");
        Directory.CreateDirectory(_srcDir);
        Directory.CreateDirectory(_dstDir);
    }

    [Fact]
    public void Assembles_file_identical_to_original()
    {
        byte[] original = new byte[20_000];
        new Random(5).NextBytes(original);
        File.WriteAllBytes(Path.Combine(_srcDir, "f.bin"), original);
        IReadOnlyList<FileIndexEntry> index = new FolderScanner().Scan(_srcDir);
        var reader = new FolderBlockReader(_srcDir, index);

        FileAssembler.Write(_dstDir, index.Single(), reader);

        Assert.Equal(original, File.ReadAllBytes(Path.Combine(_dstDir, "f.bin")));
    }

    [Fact]
    public void Preserves_modification_time()
    {
        File.WriteAllBytes(Path.Combine(_srcDir, "f.bin"), Encoding.UTF8.GetBytes("data"));
        FileIndexEntry scanned = new FolderScanner().Scan(_srcDir).Single();
        var when = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        FileIndexEntry entry = scanned with { ModifiedAt = when };
        var reader = new FolderBlockReader(_srcDir, [scanned]);

        FileAssembler.Write(_dstDir, entry, reader);

        Assert.Equal(when.UtcDateTime, File.GetLastWriteTimeUtc(Path.Combine(_dstDir, "f.bin")));
    }

    [Fact]
    public void Rejects_block_with_wrong_content()
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("hello"));
        var entry = new FileIndexEntry("x.txt", 5, DateTimeOffset.UnixEpoch, hash,
            [new ChunkInfo(0, 5, hash)]);
        // Источник отдаёт 5 байт, но не те (хеш не сойдётся).
        var badSource = new MemoryBlockSource([new(hash, Encoding.UTF8.GetBytes("world"))]);

        Assert.Throws<InvalidDataException>(() => FileAssembler.Write(_dstDir, entry, badSource));
        Assert.False(File.Exists(Path.Combine(_dstDir, "x.txt")));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("../../etc/passwd")]
    public void Rejects_path_traversal(string relativePath)
    {
        byte[] data = Encoding.UTF8.GetBytes("x");
        byte[] hash = SHA256.HashData(data);
        var entry = new FileIndexEntry(relativePath, 1, DateTimeOffset.UnixEpoch, hash,
            [new ChunkInfo(0, 1, hash)]);
        var source = new MemoryBlockSource([new(hash, data)]);

        Assert.Throws<InvalidOperationException>(() => FileAssembler.Write(_dstDir, entry, source));
    }

    public void Dispose()
    {
        string baseDir = Path.GetDirectoryName(_srcDir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }
}
