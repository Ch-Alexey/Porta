using System.Security.Cryptography;
using System.Text;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Data;

public class FileIndexRepositoryTests
{
    private static readonly DeviceId Device = DeviceIdentity.Generate().Id;

    private static VersionedFileEntry Entry(string path, string content, long localCounter)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        var fileEntry = new FileIndexEntry(path, content.Length, DateTimeOffset.UnixEpoch, hash,
            [new ChunkInfo(0, content.Length, hash)]);
        VersionVector version = VersionVector.Empty;
        for (long i = 0; i < localCounter; i++)
            version = version.Increment(Device);
        return new VersionedFileEntry(fileEntry, version);
    }

    [Fact]
    public void Replace_then_Load_roundtrips_entries_and_versions()
    {
        using var temp = new TempDatabase();
        var repo = new FileIndexRepository(temp.Database);
        var entries = new[] { Entry("a.txt", "alpha", 1), Entry("dir/b.bin", "beta", 3) };

        repo.Replace("s1", entries);
        var loaded = repo.Load("s1");

        Assert.Equal(2, loaded.Count);
        Assert.Equal("a.txt", loaded[0].Entry.RelativePath);
        Assert.Equal(1, loaded[0].Version.Get(Device));
        Assert.Equal(3, loaded[1].Version.Get(Device));
        Assert.Equal(entries[1].Entry.ContentHash, loaded[1].Entry.ContentHash);
    }

    [Fact]
    public void Replace_overwrites_previous_index_of_storage()
    {
        using var temp = new TempDatabase();
        var repo = new FileIndexRepository(temp.Database);
        repo.Replace("s1", [Entry("old.txt", "old", 1)]);

        repo.Replace("s1", [Entry("new.txt", "new", 1)]);
        var loaded = repo.Load("s1");

        Assert.Equal("new.txt", Assert.Single(loaded).Entry.RelativePath);
    }

    [Fact]
    public void Load_unknown_storage_is_empty()
    {
        using var temp = new TempDatabase();
        Assert.Empty(new FileIndexRepository(temp.Database).Load("nope"));
    }

    [Fact]
    public void Storages_are_isolated()
    {
        using var temp = new TempDatabase();
        var repo = new FileIndexRepository(temp.Database);
        repo.Replace("s1", [Entry("a.txt", "a", 1)]);
        repo.Replace("s2", [Entry("b.txt", "b", 1)]);

        Assert.Equal("a.txt", Assert.Single(repo.Load("s1")).Entry.RelativePath);
        Assert.Equal("b.txt", Assert.Single(repo.Load("s2")).Entry.RelativePath);
    }
}
