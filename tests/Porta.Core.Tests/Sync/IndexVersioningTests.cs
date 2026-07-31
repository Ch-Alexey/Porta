using System.Security.Cryptography;
using System.Text;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class IndexVersioningTests
{
    private static readonly DeviceId Local = DeviceIdentity.Generate().Id;
    private static readonly DeviceId Other = DeviceIdentity.Generate().Id;

    private static FileIndexEntry Entry(string path, string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return new FileIndexEntry(path, content.Length, DateTimeOffset.UnixEpoch, hash,
            [new ChunkInfo(0, content.Length, hash)]);
    }

    [Fact]
    public void New_file_gets_local_counter_one()
    {
        var result = IndexVersioning.Apply([], [Entry("a.txt", "v1")], Local);

        Assert.Equal(1, result.Single().Version.Get(Local));
    }

    [Fact]
    public void Unchanged_file_keeps_version()
    {
        var prior = new VersionedFileEntry(Entry("a.txt", "v1"),
            VersionVector.Empty.Increment(Local).Increment(Local)); // local:2

        var result = IndexVersioning.Apply([prior], [Entry("a.txt", "v1")], Local);

        Assert.Equal(2, result.Single().Version.Get(Local));
    }

    [Fact]
    public void Changed_file_increments_local_counter()
    {
        var prior = new VersionedFileEntry(Entry("a.txt", "v1"), VersionVector.Empty.Increment(Local));

        var result = IndexVersioning.Apply([prior], [Entry("a.txt", "v2")], Local);

        Assert.Equal(2, result.Single().Version.Get(Local));
    }

    [Fact]
    public void Foreign_counter_is_preserved_on_local_change()
    {
        VersionVector priorVersion = VersionVector.Empty.Increment(Other).Increment(Local);
        var prior = new VersionedFileEntry(Entry("a.txt", "v1"), priorVersion);

        var result = IndexVersioning.Apply([prior], [Entry("a.txt", "v2")], Local);
        VersionVector version = result.Single().Version;

        Assert.Equal(2, version.Get(Local));
        Assert.Equal(1, version.Get(Other));
    }
}
