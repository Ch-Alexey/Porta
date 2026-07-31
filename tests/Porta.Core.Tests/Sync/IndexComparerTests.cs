using System.Security.Cryptography;
using System.Text;
using Porta.Core.Indexing;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class IndexComparerTests
{
    private static ChunkInfo Block(string id, int length = 100)
        => new(0, length, SHA256.HashData(Encoding.UTF8.GetBytes(id)));

    private static FileIndexEntry Entry(string path, string contentId, params ChunkInfo[] blocks)
        => new(path, blocks.Sum(b => (long)b.Length), DateTimeOffset.UnixEpoch,
            SHA256.HashData(Encoding.UTF8.GetBytes(contentId)), blocks);

    private static string Hex(string blockId) => Block(blockId).HashHex;

    [Fact]
    public void New_remote_file_needs_all_its_blocks()
    {
        var local = Array.Empty<FileIndexEntry>();
        var remote = new[] { Entry("a.txt", "ca", Block("b1"), Block("b2")) };

        IndexDiff diff = IndexComparer.Compare(local, remote);

        Assert.Equal(["a.txt"], diff.FilesToUpdate.Select(f => f.RelativePath));
        Assert.Equal([Hex("b1"), Hex("b2")], diff.MissingBlocks.Select(b => b.HashHex));
    }

    [Fact]
    public void Identical_indexes_produce_empty_diff()
    {
        var index = new[] { Entry("a.txt", "ca", Block("b1")) };

        IndexDiff diff = IndexComparer.Compare(index, index);

        Assert.Empty(diff.FilesToUpdate);
        Assert.Empty(diff.MissingBlocks);
    }

    [Fact]
    public void Changed_file_needs_only_changed_block()
    {
        var local = new[] { Entry("a.txt", "old", Block("b1"), Block("b3")) };
        var remote = new[] { Entry("a.txt", "new", Block("b1"), Block("b2")) };

        IndexDiff diff = IndexComparer.Compare(local, remote);

        Assert.Equal(["a.txt"], diff.FilesToUpdate.Select(f => f.RelativePath));
        Assert.Equal([Hex("b2")], diff.MissingBlocks.Select(b => b.HashHex)); // b1 уже есть локально
    }

    [Fact]
    public void Block_present_in_another_local_file_is_reused()
    {
        var local = new[] { Entry("y.txt", "cy", Block("shared")) };
        var remote = new[] { Entry("x.txt", "cx", Block("shared")) };

        IndexDiff diff = IndexComparer.Compare(local, remote);

        Assert.Equal(["x.txt"], diff.FilesToUpdate.Select(f => f.RelativePath));
        Assert.Empty(diff.MissingBlocks); // блок дедуплицирован из y.txt
    }

    [Fact]
    public void Local_only_file_is_ignored_no_deletion()
    {
        var local = new[] { Entry("z.txt", "cz", Block("b1")) };
        var remote = Array.Empty<FileIndexEntry>();

        IndexDiff diff = IndexComparer.Compare(local, remote);

        Assert.Empty(diff.FilesToUpdate);
        Assert.Empty(diff.MissingBlocks);
    }
}
