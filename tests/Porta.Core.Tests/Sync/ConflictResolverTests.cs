using System.Security.Cryptography;
using System.Text;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class ConflictResolverTests
{
    private static readonly DeviceId A = DeviceIdentity.Generate().Id;
    private static readonly DeviceId B = DeviceIdentity.Generate().Id;

    private static VersionedFileEntry Versioned(string content, VersionVector version)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        var entry = new FileIndexEntry("a.txt", content.Length, DateTimeOffset.UnixEpoch, hash,
            [new ChunkInfo(0, content.Length, hash)]);
        return new VersionedFileEntry(entry, version);
    }

    [Fact]
    public void No_local_accepts_remote()
    {
        var remote = Versioned("v1", VersionVector.Empty.Increment(A));
        Assert.Equal(SyncAction.Accept, ConflictResolver.Decide(null, remote));
    }

    [Fact]
    public void Remote_newer_is_accepted()
    {
        var local = Versioned("v1", VersionVector.Empty.Increment(A));
        var remote = Versioned("v2", VersionVector.Empty.Increment(A).Increment(A));
        Assert.Equal(SyncAction.Accept, ConflictResolver.Decide(local, remote));
    }

    [Fact]
    public void Local_newer_is_skipped()
    {
        var local = Versioned("v2", VersionVector.Empty.Increment(A).Increment(A));
        var remote = Versioned("v1", VersionVector.Empty.Increment(A));
        Assert.Equal(SyncAction.Skip, ConflictResolver.Decide(local, remote));
    }

    [Fact]
    public void Equal_version_same_content_is_skipped()
    {
        var version = VersionVector.Empty.Increment(A);
        Assert.Equal(SyncAction.Skip, ConflictResolver.Decide(Versioned("same", version), Versioned("same", version)));
    }

    [Fact]
    public void Equal_version_different_content_is_conflict()
    {
        var version = VersionVector.Empty.Increment(A);
        Assert.Equal(SyncAction.Conflict, ConflictResolver.Decide(Versioned("local", version), Versioned("remote", version)));
    }

    [Fact]
    public void Concurrent_edits_are_conflict()
    {
        var local = Versioned("local", VersionVector.Empty.Increment(A));
        var remote = Versioned("remote", VersionVector.Empty.Increment(B));
        Assert.Equal(SyncAction.Conflict, ConflictResolver.Decide(local, remote));
    }

    [Fact]
    public void ConflictName_inserts_marker_before_extension_and_keeps_directory()
    {
        var when = new DateTimeOffset(2026, 7, 31, 10, 20, 30, TimeSpan.Zero);
        string name = ConflictResolver.ConflictName("docs/report.txt", A, when);

        Assert.StartsWith("docs/report.sync-conflict-20260731-102030-", name);
        Assert.EndsWith(".txt", name);
    }

    [Fact]
    public void ConflictName_handles_file_without_extension()
    {
        var when = new DateTimeOffset(2026, 7, 31, 10, 20, 30, TimeSpan.Zero);
        string name = ConflictResolver.ConflictName("README", A, when);

        Assert.StartsWith("README.sync-conflict-", name);
        Assert.DoesNotContain(".", name["README.sync-conflict-".Length..]);
    }
}
