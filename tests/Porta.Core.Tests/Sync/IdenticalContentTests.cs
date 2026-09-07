using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Sync;
using Porta.Core.Tests.Data;

namespace Porta.Core.Tests.Sync;

/// <summary>
/// Файл, который уже есть на устройстве. Регрессия на дефект из ревизии: одинаковое
/// содержимое при параллельных векторах плодило копию-двойник.
/// См. docs/features/31-audit-fixes.md.
/// </summary>
public class IdenticalContentTests : IDisposable
{
    private static readonly DeviceId IdA = DeviceIdentity.Generate().Id;
    private static readonly DeviceId IdB = DeviceIdentity.Generate().Id;

    private readonly TempDatabase _dbA = new();
    private readonly TempDatabase _dbB = new();
    private readonly string _folderA;
    private readonly string _folderB;
    private readonly string _versions;

    public IdenticalContentTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _folderA = Path.Combine(baseDir, "A");
        _folderB = Path.Combine(baseDir, "B");
        _versions = Path.Combine(baseDir, "versions");
        Directory.CreateDirectory(_folderA);
        Directory.CreateDirectory(_folderB);
    }

    private async Task<SyncApplyReport> PullBFromAAsync(IVersionStore? versions = null)
    {
        var (chA, chB) = ConnectedChannels.Create();
        Task serve = VersionedSync.ServeAsync(chA, new FileIndexRepository(_dbA.Database), _ => _folderA, IdA);
        Task<SyncApplyReport> pull = VersionedSync.PullAsync(
            chB, new FileIndexRepository(_dbB.Database), _folderB, "s1", IdB, IdA, versions);
        await Task.WhenAll(serve, pull);
        return await pull;
    }

    private static string[] FilesIn(string folder)
        => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(folder, f))
            .Order()
            .ToArray();

    [Fact]
    public async Task Same_content_on_both_devices_creates_no_duplicate()
    {
        // Обычный первый запуск: папку скопировали на второй компьютер, потом настроили синк.
        File.WriteAllText(Path.Combine(_folderA, "фото.jpg"), "одинаковое содержимое");
        File.WriteAllText(Path.Combine(_folderB, "фото.jpg"), "одинаковое содержимое");

        SyncApplyReport report = await PullBFromAAsync();

        Assert.Equal(["фото.jpg"], FilesIn(_folderB));
        Assert.Equal(0, report.Conflicts);
        Assert.Equal(1, report.Skipped);
    }

    [Fact]
    public async Task Repeated_sync_of_identical_files_stays_quiet()
    {
        File.WriteAllText(Path.Combine(_folderA, "фото.jpg"), "одинаковое содержимое");
        File.WriteAllText(Path.Combine(_folderB, "фото.jpg"), "одинаковое содержимое");

        await PullBFromAAsync();
        SyncApplyReport second = await PullBFromAAsync();

        Assert.Equal(["фото.jpg"], FilesIn(_folderB));
        Assert.Equal(0, second.Conflicts);
    }

    [Fact]
    public async Task Different_content_still_conflicts()
    {
        // Починка не должна проглотить настоящее расхождение.
        File.WriteAllText(Path.Combine(_folderA, "фото.jpg"), "версия A");
        File.WriteAllText(Path.Combine(_folderB, "фото.jpg"), "версия B");

        SyncApplyReport report = await PullBFromAAsync();

        Assert.Equal(1, report.Conflicts);
        Assert.Equal(2, FilesIn(_folderB).Length);
        Assert.Equal("версия B", File.ReadAllText(Path.Combine(_folderB, "фото.jpg")));
        Assert.Contains(FilesIn(_folderB), f => f.Contains("sync-conflict"));
    }

    [Fact]
    public async Task Update_with_a_version_store_keeps_the_previous_content()
    {
        File.WriteAllText(Path.Combine(_folderA, "док.txt"), "первая версия");
        await PullBFromAAsync();

        File.WriteAllText(Path.Combine(_folderA, "док.txt"), "вторая версия");
        var store = new FileSystemVersionStore(_versions);
        SyncApplyReport report = await PullBFromAAsync(store);

        Assert.Equal(1, report.Accepted);
        Assert.Equal("вторая версия", File.ReadAllText(Path.Combine(_folderB, "док.txt")));

        string archived = Assert.Single(store.ListVersions("док.txt"));
        Assert.Equal("первая версия", File.ReadAllText(archived));
    }

    [Fact]
    public async Task Archived_versions_stay_out_of_the_synced_folder()
    {
        File.WriteAllText(Path.Combine(_folderA, "док.txt"), "первая версия");
        await PullBFromAAsync();
        File.WriteAllText(Path.Combine(_folderA, "док.txt"), "вторая версия");

        await PullBFromAAsync(new FileSystemVersionStore(_versions));

        // Архив рядом с папкой хранилища попал бы в индекс и уехал на другое устройство.
        Assert.Equal(["док.txt"], FilesIn(_folderB));
    }

    public void Dispose()
    {
        _dbA.Dispose();
        _dbB.Dispose();
    }
}
