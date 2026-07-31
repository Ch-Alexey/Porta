using System.Text;
using Porta.Core.Indexing;
using Porta.Core.Protocol;

namespace Porta.Core.Tests.Protocol;

public class FolderIndexMessageTests : IDisposable
{
    private readonly string _root;

    public FolderIndexMessageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Real_scanned_index_roundtrips_through_channel()
    {
        File.WriteAllBytes(Path.Combine(_root, "a.txt"), Encoding.UTF8.GetBytes("alpha content"));
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllBytes(Path.Combine(_root, "sub", "b.bin"), new byte[5000]);

        IReadOnlyList<FileIndexEntry> entries = new FolderScanner().Scan(_root);
        var message = new FolderIndexMessage("storage-1", entries);

        var buffer = new MemoryStream();
        await new MessageChannel(buffer).WriteAsync(message);
        buffer.Position = 0;
        FolderIndexMessage read = await new MessageChannel(buffer).ReadAsync<FolderIndexMessage>();

        Assert.Equal("storage-1", read.StorageId);
        Assert.Equal(
            entries.Select(e => (e.RelativePath, e.Size, e.ContentHashHex)),
            read.Entries.Select(e => (e.RelativePath, e.Size, e.ContentHashHex)));
        // Блоки тоже долетели.
        Assert.Equal(
            entries.SelectMany(e => e.Chunks).Select(c => c.HashHex),
            read.Entries.SelectMany(e => e.Chunks).Select(c => c.HashHex));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
