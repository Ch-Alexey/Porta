using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.Core.Tests.Data;

public class DeviceRepositoryTests
{
    private static TrustedDevice SampleDevice(string name = "Laptop", DateTimeOffset? addedAt = null)
    {
        using var identity = DeviceIdentity.Generate();
        return new TrustedDevice(
            identity.Id,
            identity.ExportPublicKey(),
            name,
            addedAt ?? DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            LastSeenAt: null);
    }

    [Fact]
    public void Add_then_Get_roundtrips_all_fields()
    {
        using var temp = new TempDatabase();
        var repo = new DeviceRepository(temp.Database);
        TrustedDevice device = SampleDevice();

        repo.Add(device);
        TrustedDevice? loaded = repo.Get(device.Id);

        Assert.NotNull(loaded);
        Assert.Equal(device.Id, loaded!.Id);
        Assert.Equal(device.PublicKey, loaded.PublicKey);
        Assert.Equal(device.Name, loaded.Name);
        Assert.Equal(device.AddedAt, loaded.AddedAt);
        Assert.Null(loaded.LastSeenAt);
    }

    [Fact]
    public void List_returns_all_ordered_by_added_at()
    {
        using var temp = new TempDatabase();
        var repo = new DeviceRepository(temp.Database);
        var older = SampleDevice("Old", DateTimeOffset.FromUnixTimeSeconds(1000));
        var newer = SampleDevice("New", DateTimeOffset.FromUnixTimeSeconds(2000));
        repo.Add(newer);
        repo.Add(older);

        var list = repo.List();

        Assert.Equal(2, list.Count);
        Assert.Equal("Old", list[0].Name);
        Assert.Equal("New", list[1].Name);
    }

    [Fact]
    public void Remove_returns_true_when_existed_and_deletes()
    {
        using var temp = new TempDatabase();
        var repo = new DeviceRepository(temp.Database);
        TrustedDevice device = SampleDevice();
        repo.Add(device);

        Assert.True(repo.Remove(device.Id));
        Assert.Null(repo.Get(device.Id));
        Assert.False(repo.Remove(device.Id));
    }

    [Fact]
    public void UpdateLastSeen_persists_timestamp()
    {
        using var temp = new TempDatabase();
        var repo = new DeviceRepository(temp.Database);
        TrustedDevice device = SampleDevice();
        repo.Add(device);
        var seen = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        repo.UpdateLastSeen(device.Id, seen);

        Assert.Equal(seen, repo.Get(device.Id)!.LastSeenAt);
    }
}
