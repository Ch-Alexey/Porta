using System.Security.Cryptography;
using System.Text;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class SyncApplierTests : IDisposable
{
    private static readonly DeviceId Local = DeviceIdentity.Generate().Id;
    private static readonly DeviceId Remote = DeviceIdentity.Generate().Id;

    private readonly string _root;

    public SyncApplierTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private static (FileIndexEntry Entry, byte[] Data) Make(string relativePath, string content)
    {
        byte[] data = Encoding.UTF8.GetBytes(content);
        byte[] hash = SHA256.HashData(data);
        var entry = new FileIndexEntry(relativePath, data.Length, DateTimeOffset.UnixEpoch, hash,
            [new ChunkInfo(0, data.Length, hash)]);
        return (entry, data);
    }

    private VersionedFileEntry LocalOnDisk(string relativePath, string content, VersionVector version)
    {
        (FileIndexEntry entry, byte[] data) = Make(relativePath, content);
        File.WriteAllBytes(Path.Combine(_root, relativePath), data);
        return new VersionedFileEntry(entry, version);
    }

    [Fact]
    public void Accept_overwrites_local_with_remote()
    {
        var local = LocalOnDisk("a.txt", "old", VersionVector.Empty.Increment(Local));
        (FileIndexEntry remoteEntry, byte[] remoteData) = Make("a.txt", "new remote");
        var remote = new VersionedFileEntry(remoteEntry, VersionVector.Empty.Increment(Local).Increment(Local));
        var blocks = new MemoryBlockSource([new(remoteEntry.ContentHash, remoteData)]);

        SyncApplyReport report = SyncApplier.Apply(_root, [local], [remote], Remote, blocks);

        Assert.Equal(1, report.Accepted);
        Assert.Equal("new remote", File.ReadAllText(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void Concurrent_edit_keeps_local_and_writes_conflict_copy()
    {
        var local = LocalOnDisk("a.txt", "local edit", VersionVector.Empty.Increment(Local));
        (FileIndexEntry remoteEntry, byte[] remoteData) = Make("a.txt", "remote edit");
        var remote = new VersionedFileEntry(remoteEntry, VersionVector.Empty.Increment(Remote));
        var blocks = new MemoryBlockSource([new(remoteEntry.ContentHash, remoteData)]);

        SyncApplyReport report = SyncApplier.Apply(_root, [local], [remote], Remote, blocks);

        Assert.Equal(1, report.Conflicts);
        Assert.Equal("local edit", File.ReadAllText(Path.Combine(_root, "a.txt"))); // локальный цел
        string conflictFile = Assert.Single(Directory.GetFiles(_root, "a.sync-conflict-*"));
        Assert.Equal("remote edit", File.ReadAllText(conflictFile));
    }

    [Fact]
    public void Local_newer_is_skipped()
    {
        var local = LocalOnDisk("a.txt", "local newer", VersionVector.Empty.Increment(Local).Increment(Local));
        (FileIndexEntry remoteEntry, byte[] remoteData) = Make("a.txt", "remote older");
        var remote = new VersionedFileEntry(remoteEntry, VersionVector.Empty.Increment(Local));
        var blocks = new MemoryBlockSource([new(remoteEntry.ContentHash, remoteData)]);

        SyncApplyReport report = SyncApplier.Apply(_root, [local], [remote], Remote, blocks);

        Assert.Equal(1, report.Skipped);
        Assert.Equal("local newer", File.ReadAllText(Path.Combine(_root, "a.txt")));
        Assert.Empty(Directory.GetFiles(_root, "a.sync-conflict-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
