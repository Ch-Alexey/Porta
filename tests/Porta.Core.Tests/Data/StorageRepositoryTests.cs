using Microsoft.Data.Sqlite;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Tests.Data;

public class StorageRepositoryTests
{
    private static Storage SampleStorage(string id = "store-1") => new(
        id,
        Name: "Documents",
        LocalPath: "/home/user/Documents",
        ExchangeMode: StorageExchangeMode.ReceiveOnly,
        SyncMode: SyncMode.Manual,
        Paused: true,
        CreatedAt: DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

    private static TrustedDevice AddSampleDevice(PortaDatabase db)
    {
        using var identity = DeviceIdentity.Generate();
        var device = new TrustedDevice(identity.Id, identity.ExportPublicKey(), "Phone",
            DateTimeOffset.FromUnixTimeSeconds(1000), null);
        new DeviceRepository(db).Add(device);
        return device;
    }

    [Fact]
    public void Add_then_Get_roundtrips_enums_and_flags()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        Storage storage = SampleStorage();

        repo.Add(storage);
        Storage? loaded = repo.Get(storage.Id);

        Assert.Equal(storage, loaded);
    }

    [Fact]
    public void Update_changes_modes_without_adding_a_row()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        Storage storage = SampleStorage();
        repo.Add(storage);

        repo.Update(storage with { SyncMode = SyncMode.Automatic, Paused = false, Name = "Документы" });

        Storage? loaded = repo.Get(storage.Id);
        Assert.NotNull(loaded);
        Assert.Equal(SyncMode.Automatic, loaded!.SyncMode);
        Assert.False(loaded.Paused);
        Assert.Equal("Документы", loaded.Name);
        Assert.Single(repo.List());
    }

    [Fact]
    public void Update_of_an_unknown_storage_changes_nothing()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);

        repo.Update(SampleStorage("нет-такого"));

        Assert.Empty(repo.List());
    }

    [Fact]
    public void LinkDevice_is_upsert_and_listed()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        repo.Add(SampleStorage());
        TrustedDevice device = AddSampleDevice(temp.Database);

        repo.LinkDevice("store-1", device.Id, autoSync: false);
        repo.LinkDevice("store-1", device.Id, autoSync: true); // upsert обновляет флаг

        var links = repo.ListDevices("store-1");
        Assert.Single(links);
        Assert.True(links[0].AutoSync);
        Assert.Equal(device.Id, links[0].DeviceId);
    }

    [Fact]
    public void Remove_storage_cascades_links()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        repo.Add(SampleStorage());
        TrustedDevice device = AddSampleDevice(temp.Database);
        repo.LinkDevice("store-1", device.Id, autoSync: true);

        Assert.True(repo.Remove("store-1"));

        Assert.Empty(repo.ListDevices("store-1"));
    }

    [Fact]
    public void UnlinkDevice_removes_single_link()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        repo.Add(SampleStorage());
        TrustedDevice device = AddSampleDevice(temp.Database);
        repo.LinkDevice("store-1", device.Id, autoSync: true);

        Assert.True(repo.UnlinkDevice("store-1", device.Id));
        Assert.Empty(repo.ListDevices("store-1"));
    }

    [Fact]
    public void LinkDevice_to_missing_storage_violates_foreign_key()
    {
        using var temp = new TempDatabase();
        var repo = new StorageRepository(temp.Database);
        TrustedDevice device = AddSampleDevice(temp.Database);

        Assert.Throws<SqliteException>(() => repo.LinkDevice("no-such-storage", device.Id, autoSync: false));
    }
}
