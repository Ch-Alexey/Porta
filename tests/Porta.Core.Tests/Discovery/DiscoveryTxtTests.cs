using Porta.Core.Discovery;
using Porta.Core.Identity;

namespace Porta.Core.Tests.Discovery;

public class DiscoveryTxtTests
{
    private static PortaAdvertisement SampleAd(string name = "My Laptop")
    {
        using var identity = DeviceIdentity.Generate();
        return new PortaAdvertisement(identity.Id, name, 12345);
    }

    [Fact]
    public void InstanceName_is_device_id()
    {
        using var identity = DeviceIdentity.Generate();
        Assert.Equal(identity.Id.ToString(), DiscoveryTxt.InstanceName(identity.Id));
    }

    [Fact]
    public void Build_then_Parse_roundtrips()
    {
        PortaAdvertisement ad = SampleAd("Kitchen PC");

        var txt = DiscoveryTxt.Build(ad);
        bool ok = DiscoveryTxt.TryParse(txt, out DeviceId? id, out string name, out int version);

        Assert.True(ok);
        Assert.Equal(ad.DeviceId, id);
        Assert.Equal("Kitchen PC", name);
        Assert.Equal(DiscoveryTxt.Version, version);
    }

    [Fact]
    public void Parse_preserves_name_with_spaces_unicode_and_equals()
    {
        PortaAdvertisement ad = SampleAd("Алексей = ноутбук #1");

        var txt = DiscoveryTxt.Build(ad);
        DiscoveryTxt.TryParse(txt, out _, out string name, out _);

        Assert.Equal("Алексей = ноутбук #1", name);
    }

    [Fact]
    public void Parse_fails_without_id()
    {
        string[] txt = ["name=No Id", $"v={DiscoveryTxt.Version}"];
        Assert.False(DiscoveryTxt.TryParse(txt, out _, out _, out _));
    }

    [Fact]
    public void Parse_fails_with_broken_id()
    {
        string[] txt = ["id=not-a-valid-device-id", $"v={DiscoveryTxt.Version}"];
        Assert.False(DiscoveryTxt.TryParse(txt, out _, out _, out _));
    }

    [Fact]
    public void Parse_fails_with_unsupported_or_missing_version()
    {
        using var identity = DeviceIdentity.Generate();
        string[] wrongVersion = [$"id={identity.Id}", "name=X", "v=999"];
        string[] noVersion = [$"id={identity.Id}", "name=X"];

        Assert.False(DiscoveryTxt.TryParse(wrongVersion, out _, out _, out _));
        Assert.False(DiscoveryTxt.TryParse(noVersion, out _, out _, out _));
    }
}
