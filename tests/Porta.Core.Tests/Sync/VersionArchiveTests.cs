using System.Text;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

/// <summary>Ротация и откат версий. См. docs/features/32-version-history.md.</summary>
public class VersionArchiveTests : IDisposable
{
    private readonly string _base;
    private readonly string _archiveRoot;
    private readonly string _storage;

    public VersionArchiveTests()
    {
        _base = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _archiveRoot = Path.Combine(_base, "versions");
        _storage = Path.Combine(_base, "storage");
        Directory.CreateDirectory(_storage);
    }

    /// <summary>Часы, которые двигаются вперёд сами — метки версий должны различаться.</summary>
    private sealed class TickingClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            _now = _now.AddSeconds(1);
            return _now;
        }
    }

    private FileSystemVersionArchive Archive(int? max = null)
        => new(_archiveRoot, new TickingClock(), max);

    private string WriteStorageFile(string relativePath, string content)
    {
        string full = Path.Combine(_storage, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, Encoding.UTF8);
        return full;
    }

    [Fact]
    public void Rotation_keeps_only_the_newest_versions()
    {
        FileSystemVersionArchive archive = Archive(max: 3);
        IVersionStore store = archive.StoreFor("s1");
        string file = WriteStorageFile("док.txt", "v0");

        for (int i = 1; i <= 6; i++)
        {
            store.Archive(file, "док.txt");
            File.WriteAllText(file, $"v{i}");
        }

        IReadOnlyList<ArchivedVersion> versions = archive.ListVersions("s1", "док.txt");
        Assert.Equal(3, versions.Count);

        // Остались самые свежие: v3, v4, v5 (архивируется содержимое ДО очередной записи).
        var contents = versions
            .Select(v => File.ReadAllText(Path.Combine(_archiveRoot, "s1", v.Id)))
            .Order()
            .ToArray();
        Assert.Equal(["v3", "v4", "v5"], contents);
    }

    [Fact]
    public void Rotation_does_not_touch_other_files_or_storages()
    {
        FileSystemVersionArchive archive = Archive(max: 2);
        string a = WriteStorageFile("а.txt", "a0");
        string b = WriteStorageFile("б.txt", "b0");

        archive.StoreFor("s1").Archive(b, "б.txt");
        archive.StoreFor("s2").Archive(a, "а.txt");
        for (int i = 1; i <= 5; i++)
        {
            archive.StoreFor("s1").Archive(a, "а.txt");
            File.WriteAllText(a, $"a{i}");
        }

        Assert.Equal(2, archive.ListVersions("s1", "а.txt").Count);
        Assert.Single(archive.ListVersions("s1", "б.txt"));
        Assert.Single(archive.ListVersions("s2", "а.txt"));
    }

    [Fact]
    public void Two_versions_in_the_same_millisecond_do_not_collide()
    {
        // Часы стоят — обе копии просятся в одно имя. Раньше это роняло синхронизацию.
        var frozen = new FrozenClock();
        var archive = new FileSystemVersionArchive(_archiveRoot, frozen);
        IVersionStore store = archive.StoreFor("s1");
        string file = WriteStorageFile("док.txt", "первая");

        store.Archive(file, "док.txt");
        File.WriteAllText(file, "вторая");
        store.Archive(file, "док.txt");

        Assert.Equal(2, archive.ListVersions("s1", "док.txt").Count);
    }

    /// <summary>Часы, которые не идут — худший случай для меток версий.</summary>
    private sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void Versions_are_listed_newest_first()
    {
        FileSystemVersionArchive archive = Archive();
        IVersionStore store = archive.StoreFor("s1");
        string file = WriteStorageFile("док.txt", "первая");

        store.Archive(file, "док.txt");
        File.WriteAllText(file, "вторая");
        store.Archive(file, "док.txt");

        IReadOnlyList<ArchivedVersion> versions = archive.ListVersions("s1", "док.txt");

        Assert.Equal(2, versions.Count);
        Assert.True(versions[0].ArchivedAt > versions[1].ArchivedAt, "первой должна идти свежая версия");
        Assert.Equal("вторая", File.ReadAllText(Path.Combine(_archiveRoot, "s1", versions[0].Id)));
    }

    [Fact]
    public void Files_with_versions_are_listed_including_subfolders()
    {
        FileSystemVersionArchive archive = Archive();
        IVersionStore store = archive.StoreFor("s1");
        store.Archive(WriteStorageFile("док.txt", "x"), "док.txt");
        store.Archive(WriteStorageFile("папка/вложенный.txt", "y"), "папка/вложенный.txt");

        Assert.Equal(["док.txt", "папка/вложенный.txt"], archive.ListFiles("s1").Order());
    }

    [Fact]
    public void Restore_brings_back_the_chosen_content()
    {
        FileSystemVersionArchive archive = Archive();
        IVersionStore store = archive.StoreFor("s1");
        string file = WriteStorageFile("док.txt", "нужная версия");
        store.Archive(file, "док.txt");
        File.WriteAllText(file, "испорченная версия");

        ArchivedVersion version = archive.ListVersions("s1", "док.txt")[0];
        archive.Restore("s1", "док.txt", version.Id, _storage);

        Assert.Equal("нужная версия", File.ReadAllText(file));
    }

    [Fact]
    public void Restore_archives_the_current_content_so_it_can_be_undone()
    {
        FileSystemVersionArchive archive = Archive();
        IVersionStore store = archive.StoreFor("s1");
        string file = WriteStorageFile("док.txt", "старое");
        store.Archive(file, "док.txt");
        File.WriteAllText(file, "текущее");

        archive.Restore("s1", "док.txt", archive.ListVersions("s1", "док.txt")[0].Id, _storage);

        // Теперь «текущее» тоже лежит в архиве — откат можно откатить.
        var contents = archive.ListVersions("s1", "док.txt")
            .Select(v => File.ReadAllText(Path.Combine(_archiveRoot, "s1", v.Id)))
            .ToArray();
        Assert.Contains("текущее", contents);
    }

    [Fact]
    public void Restoring_an_unknown_version_fails_without_touching_the_file()
    {
        FileSystemVersionArchive archive = Archive();
        string file = WriteStorageFile("док.txt", "не трогать");

        Assert.Throws<FileNotFoundException>(
            () => archive.Restore("s1", "док.txt", "док~19700101-000000000.txt", _storage));
        Assert.Equal("не трогать", File.ReadAllText(file));
    }

    [Fact]
    public void Version_id_cannot_escape_the_archive()
    {
        FileSystemVersionArchive archive = Archive();
        WriteStorageFile("док.txt", "цел");
        File.WriteAllText(Path.Combine(_base, "секрет.txt"), "снаружи");

        Assert.Throws<InvalidOperationException>(
            () => archive.Restore("s1", "док.txt", "../../секрет.txt", _storage));
    }

    [Fact]
    public void No_versions_yet_is_an_empty_list_not_an_error()
    {
        FileSystemVersionArchive archive = Archive();

        Assert.Empty(archive.ListFiles("s1"));
        Assert.Empty(archive.ListVersions("s1", "нет.txt"));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_base, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки — не повод валить тест.
        }
    }
}
