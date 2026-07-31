using System.Security.Cryptography;
using System.Text;
using Porta.Core.Indexing;

namespace Porta.Core.Tests.Indexing;

public class FolderScannerTests : IDisposable
{
    private readonly string _root;

    public FolderScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private void WriteFile(string relativePath, byte[] content)
    {
        string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
    }

    [Fact]
    public void Scan_returns_entries_sorted_with_forward_slash_paths()
    {
        WriteFile("sub/b.bin", Encoding.UTF8.GetBytes("beta"));
        WriteFile("a.txt", Encoding.UTF8.GetBytes("alpha"));

        var entries = new FolderScanner().Scan(_root);

        Assert.Equal(new[] { "a.txt", "sub/b.bin" }, entries.Select(e => e.RelativePath));
    }

    [Fact]
    public void Scan_computes_size_and_content_hash()
    {
        byte[] content = Encoding.UTF8.GetBytes("some file content");
        WriteFile("file.dat", content);

        FileIndexEntry entry = new FolderScanner().Scan(_root).Single();

        Assert.Equal(content.Length, entry.Size);
        Assert.Equal(SHA256.HashData(content), entry.ContentHash);
        Assert.NotEmpty(entry.Chunks);
    }

    [Fact]
    public void Identical_content_gives_identical_content_hash()
    {
        byte[] content = Encoding.UTF8.GetBytes("dup");
        WriteFile("one.txt", content);
        WriteFile("two.txt", content);

        var entries = new FolderScanner().Scan(_root);

        Assert.Equal(entries[0].ContentHashHex, entries[1].ContentHashHex);
    }

    [Fact]
    public void Scan_skips_ignored_files()
    {
        WriteFile("keep.txt", Encoding.UTF8.GetBytes("keep"));
        WriteFile(".porta/versions/old~1.txt", Encoding.UTF8.GetBytes("archived"));
        WriteFile("build/out.js", Encoding.UTF8.GetBytes("built"));
        WriteFile("scratch.tmp", Encoding.UTF8.GetBytes("temp"));

        var ignore = new IgnoreRules([".porta/", "/build/", "*.tmp"]);
        var entries = new FolderScanner().Scan(_root, ignore);

        Assert.Equal(["keep.txt"], entries.Select(e => e.RelativePath));
    }

    [Fact]
    public void Scan_missing_folder_throws()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => new FolderScanner().Scan(Path.Combine(_root, "nope")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
