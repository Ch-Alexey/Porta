using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;
using Porta.Core.Tests.Data;

namespace Porta.Core.Tests.Sync;

public class StorageSelectionTests
{
    private static Storage Make(string id, SyncMode mode = SyncMode.Automatic, bool paused = false)
        => new(id, id, "/tmp/" + id, StorageExchangeMode.TwoWay, mode, paused, DateTimeOffset.UnixEpoch);

    private static DeviceId Trust(PortaDatabase db, string name)
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        new DeviceRepository(db).Add(
            new TrustedDevice(identity.Id, identity.ExportPublicKey(), name, DateTimeOffset.UnixEpoch, null));
        return identity.Id;
    }

    [Fact]
    public void Automatic_trigger_skips_manual_storages()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        DeviceId peer = Trust(temp.Database, "Peer");
        repo.Add(Make("auto"));
        repo.Add(Make("manual", SyncMode.Manual));
        repo.LinkDevice("auto", peer, true);
        repo.LinkDevice("manual", peer, true);

        var selected = StorageSelection.ForSync(repo, peer, SyncTrigger.Automatic);

        Assert.Equal("auto", Assert.Single(selected).Id);
    }

    [Fact]
    public void Manual_trigger_takes_every_shared_storage()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        DeviceId peer = Trust(temp.Database, "Peer");
        repo.Add(Make("auto"));
        repo.Add(Make("manual", SyncMode.Manual));
        repo.LinkDevice("auto", peer, true);
        repo.LinkDevice("manual", peer, true);

        var selected = StorageSelection.ForSync(repo, peer, SyncTrigger.Manual);

        Assert.Equal(["auto", "manual"], selected.Select(s => s.Id).Order());
    }

    [Fact]
    public void Paused_storage_is_skipped_by_both_triggers()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        DeviceId peer = Trust(temp.Database, "Peer");
        repo.Add(Make("paused", paused: true));
        repo.LinkDevice("paused", peer, true);

        Assert.Empty(StorageSelection.ForSync(repo, peer, SyncTrigger.Automatic));
        Assert.Empty(StorageSelection.ForSync(repo, peer, SyncTrigger.Manual));
    }

    [Fact]
    public void Storage_shared_with_another_device_is_not_taken()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        DeviceId peer = Trust(temp.Database, "Peer");
        DeviceId other = Trust(temp.Database, "Other");
        repo.Add(Make("theirs"));
        repo.LinkDevice("theirs", other, true);

        Assert.Empty(StorageSelection.ForSync(repo, peer, SyncTrigger.Manual));
    }

    [Fact]
    public void Storage_shared_with_nobody_is_not_taken()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        DeviceId peer = Trust(temp.Database, "Peer");
        repo.Add(Make("lonely"));

        Assert.Empty(StorageSelection.ForSync(repo, peer, SyncTrigger.Manual));
    }
}
