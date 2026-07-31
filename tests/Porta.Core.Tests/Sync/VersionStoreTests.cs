using System.Text;
using Porta.Core.Sync;
using Porta.Core.Tests.Pairing;

namespace Porta.Core.Tests.Sync;

public class VersionStoreTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _storageDir;
    private readonly string _versionsRoot;
    private readonly MutableTimeProvider _clock = new();

    public VersionStoreTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _storageDir = Path.Combine(_baseDir, "storage");
        _versionsRoot = Path.Combine(_baseDir, "versions");
        Directory.CreateDirectory(_storageDir);
    }

    private string Write(string relativePath, string content)
    {
        string full = Path.Combine(_storageDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public void Archive_saves_current_content_as_version()
    {
        var store = new FileSystemVersionStore(_versionsRoot, _clock);
        string file = Write("a.txt", "version-1");

        store.Archive(file, "a.txt");

        string version = Assert.Single(store.ListVersions("a.txt"));
        Assert.Equal("version-1", File.ReadAllText(version));
    }

    [Fact]
    public void Multiple_archives_create_multiple_versions_in_time_order()
    {
        var store = new FileSystemVersionStore(_versionsRoot, _clock);
        string file = Write("dir/a.txt", "v1");
        store.Archive(file, "dir/a.txt");

        _clock.Advance(TimeSpan.FromSeconds(1));
        File.WriteAllText(file, "v2");
        store.Archive(file, "dir/a.txt");

        var versions = store.ListVersions("dir/a.txt");
        Assert.Equal(2, versions.Count);
        Assert.Equal("v1", File.ReadAllText(versions[0]));
        Assert.Equal("v2", File.ReadAllText(versions[1]));
    }

    [Fact]
    public void Archiving_missing_file_is_noop()
    {
        var store = new FileSystemVersionStore(_versionsRoot, _clock);

        store.Archive(Path.Combine(_storageDir, "ghost.txt"), "ghost.txt");

        Assert.Empty(store.ListVersions("ghost.txt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }
}
