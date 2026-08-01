using System.Text;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Sync;
using Porta.Core.Tests.Data;
using Porta.Core.Tests.Pairing;

namespace Porta.Core.Tests.Sync;

public class VersionedSyncTests : IDisposable
{
    private static readonly DeviceId IdA = DeviceIdentity.Generate().Id;
    private static readonly DeviceId IdB = DeviceIdentity.Generate().Id;

    private readonly TempDatabase _dbA = new();
    private readonly TempDatabase _dbB = new();
    private readonly string _folderA;
    private readonly string _folderB;

    public VersionedSyncTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _folderA = Path.Combine(baseDir, "A");
        _folderB = Path.Combine(baseDir, "B");
        Directory.CreateDirectory(_folderA);
        Directory.CreateDirectory(_folderB);
    }

    private void WriteFile(string folder, string name, string content)
        => File.WriteAllText(Path.Combine(folder, name), content);

    private async Task<SyncApplyReport> PullBFromAAsync()
    {
        var repoA = new FileIndexRepository(_dbA.Database);
        var repoB = new FileIndexRepository(_dbB.Database);
        var (chA, chB) = ConnectedChannels.Create();

        Task serve = VersionedSync.ServeAsync(chA, repoA, id => id == "s1" ? _folderA : null, IdA);
        Task<SyncApplyReport> pull = VersionedSync.PullAsync(
            chB, repoB, _folderB, "s1", IdB, IdA, clock: new MutableTimeProvider());

        await Task.WhenAll(serve, pull);
        return await pull;
    }

    [Fact]
    public async Task Fresh_pull_copies_file_and_adopts_remote_version()
    {
        WriteFile(_folderA, "doc.txt", "hello from A");

        SyncApplyReport report = await PullBFromAAsync();

        Assert.Equal(1, report.Accepted);
        Assert.Equal("hello from A", File.ReadAllText(Path.Combine(_folderB, "doc.txt")));

        // В индексе B файл несёт версию устройства A.
        VersionedFileEntry stored = Assert.Single(new FileIndexRepository(_dbB.Database).Load("s1"));
        Assert.Equal(1, stored.Version.Get(IdA));
        Assert.Equal(0, stored.Version.Get(IdB));
    }

    [Fact]
    public async Task Concurrent_edits_produce_a_conflict_copy_on_receiver()
    {
        WriteFile(_folderA, "doc.txt", "A version");
        WriteFile(_folderB, "doc.txt", "B version");

        SyncApplyReport report = await PullBFromAAsync();

        Assert.Equal(1, report.Conflicts);
        Assert.Equal("B version", File.ReadAllText(Path.Combine(_folderB, "doc.txt"))); // локальный цел
        string conflict = Assert.Single(Directory.GetFiles(_folderB, "doc.sync-conflict-*"));
        Assert.Equal("A version", File.ReadAllText(conflict));
    }

    public void Dispose()
    {
        _dbA.Dispose();
        _dbB.Dispose();
        string baseDir = Path.GetDirectoryName(_folderA)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }
}
