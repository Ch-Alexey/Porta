using System.Net;
using Porta.Core.Discovery;
using Porta.Core.Identity;

namespace Porta.Core.Tests.Discovery;

public class PeerResolverTests
{
    private const string Host = "device.local";
    private const int Port = 12345;

    private static (DeviceId Id, string Instance, IReadOnlyList<string> Txt) MakeDevice(string name = "Peer")
    {
        using var identity = DeviceIdentity.Generate();
        var ad = new PortaAdvertisement(identity.Id, name, Port);
        return (identity.Id, identity.Id.ToString(), DiscoveryTxt.Build(ad));
    }

    [Fact]
    public void Peer_is_announced_only_after_txt_srv_and_address()
    {
        var (id, instance, txt) = MakeDevice("Kitchen");
        var resolver = new PeerResolver(selfId: null);

        Assert.Empty(resolver.AddTxt(instance, txt));
        Assert.Empty(resolver.AddService(instance, Port, Host));
        var peers = resolver.AddAddress(Host, IPAddress.Parse("192.168.0.5"));

        DiscoveredPeer peer = Assert.Single(peers);
        Assert.Equal(id, peer.DeviceId);
        Assert.Equal("Kitchen", peer.Name);
        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.0.5"), Port), peer.Endpoints.Single());
    }

    [Fact]
    public void Announcement_is_order_independent()
    {
        var (id, instance, txt) = MakeDevice();
        var resolver = new PeerResolver(selfId: null);

        Assert.Empty(resolver.AddAddress(Host, IPAddress.Parse("10.0.0.2")));
        Assert.Empty(resolver.AddService(instance, Port, Host));
        var peers = resolver.AddTxt(instance, txt);

        Assert.Equal(id, Assert.Single(peers).DeviceId);
    }

    [Fact]
    public void Peer_is_announced_only_once()
    {
        var (_, instance, txt) = MakeDevice();
        var resolver = new PeerResolver(selfId: null);
        resolver.AddTxt(instance, txt);
        resolver.AddService(instance, Port, Host);
        resolver.AddAddress(Host, IPAddress.Parse("10.0.0.2"));

        // Повторный адрес не должен снова объявлять того же peer'а.
        Assert.Empty(resolver.AddAddress(Host, IPAddress.Parse("10.0.0.3")));
    }

    [Fact]
    public void Own_device_is_not_announced()
    {
        var (id, instance, txt) = MakeDevice();
        var resolver = new PeerResolver(selfId: id);

        resolver.AddTxt(instance, txt);
        resolver.AddService(instance, Port, Host);
        var peers = resolver.AddAddress(Host, IPAddress.Parse("10.0.0.2"));

        Assert.Empty(peers);
    }

    [Fact]
    public void Single_address_for_shared_host_completes_multiple_instances()
    {
        var a = MakeDevice("A");
        var b = MakeDevice("B");
        var resolver = new PeerResolver(selfId: null);
        resolver.AddTxt(a.Instance, a.Txt);
        resolver.AddService(a.Instance, Port, Host);
        resolver.AddTxt(b.Instance, b.Txt);
        resolver.AddService(b.Instance, Port, Host);

        var peers = resolver.AddAddress(Host, IPAddress.Parse("10.0.0.9"));

        Assert.Equal(2, peers.Count);
        Assert.Contains(peers, p => p.DeviceId == a.Id);
        Assert.Contains(peers, p => p.DeviceId == b.Id);
    }

    [Fact]
    public void Invalid_txt_never_announces()
    {
        var resolver = new PeerResolver(selfId: null);
        resolver.AddTxt("weird-instance", ["name=No Id", "v=1"]);
        resolver.AddService("weird-instance", Port, Host);

        Assert.Empty(resolver.AddAddress(Host, IPAddress.Parse("10.0.0.2")));
    }

    [Fact]
    public void Remove_returns_id_and_allows_reannounce()
    {
        var (id, instance, txt) = MakeDevice();
        var resolver = new PeerResolver(selfId: null);
        resolver.AddTxt(instance, txt);
        resolver.AddService(instance, Port, Host);
        resolver.AddAddress(Host, IPAddress.Parse("10.0.0.2"));

        Assert.Equal(id, resolver.Remove(instance));

        // После ухода peer может быть обнаружен снова (на каком именно шаге он
        // достроится — неважно, адреса хоста могли сохраниться).
        var reAnnounced = new List<DiscoveredPeer>();
        reAnnounced.AddRange(resolver.AddTxt(instance, txt));
        reAnnounced.AddRange(resolver.AddService(instance, Port, Host));
        reAnnounced.AddRange(resolver.AddAddress(Host, IPAddress.Parse("10.0.0.2")));
        Assert.Equal(id, Assert.Single(reAnnounced).DeviceId);
    }
}
