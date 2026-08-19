using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;
using Porta.Core.Tests.Data;

namespace Porta.Core.Tests.Sync;

public class StorageSharingTests
{
    private static Storage Storage(string id) => new(
        id, id, $"/tmp/{id}", StorageExchangeMode.TwoWay, SyncMode.Automatic, false, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Returns_only_storages_linked_to_device()
    {
        using var temp = new TempDatabase();
        var storages = new StorageRepository(temp.Database);
        var devices = new DeviceRepository(temp.Database);
        using var device = DeviceIdentity.Generate();
        devices.Add(new TrustedDevice(device.Id, device.ExportPublicKey(), "D", DateTimeOffset.UnixEpoch, null));
        storages.Add(Storage("shared"));
        storages.Add(Storage("private"));
        storages.LinkDevice("shared", device.Id, autoSync: true);

        var result = StorageSharing.SharedWith(storages, device.Id);

        Assert.Equal("shared", Assert.Single(result).Id);
    }

    [Fact]
    public void No_links_means_nothing_shared()
    {
        using var temp = new TempDatabase();
        var storages = new StorageRepository(temp.Database);
        storages.Add(Storage("s1"));
        using var device = DeviceIdentity.Generate();

        Assert.Empty(StorageSharing.SharedWith(storages, device.Id));
    }
}
