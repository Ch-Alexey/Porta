using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Infrastructure.Discovery;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Интеграционный тест реального mDNS: два узла в одном процессе объявляют и находят
/// друг друга через multicast. Требует рабочего multicast в сети/окружении.
/// См. docs/features/05-discovery.md.
/// </summary>
[Trait("Category", "Integration")]
public class MdnsDeviceDiscoveryTests
{
    [Fact]
    public async Task Two_nodes_discover_each_other_over_mdns()
    {
        using var identityA = DeviceIdentity.Generate();
        using var identityB = DeviceIdentity.Generate();
        const int portA = 47111;

        using var advertiser = new MdnsDeviceDiscovery(selfId: identityA.Id);
        using var browser = new MdnsDeviceDiscovery(selfId: identityB.Id);

        var found = new TaskCompletionSource<DiscoveredPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.PeerDiscovered += peer =>
        {
            if (peer.DeviceId == identityA.Id)
                found.TrySetResult(peer);
        };

        browser.StartBrowsing();
        advertiser.Advertise(new PortaAdvertisement(identityA.Id, "Node A", portA));

        Task completed = await Task.WhenAny(found.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.True(completed == found.Task, "Устройство A не обнаружено по mDNS за отведённое время.");

        DiscoveredPeer discovered = await found.Task;
        Assert.Equal(identityA.Id, discovered.DeviceId);
        Assert.Equal("Node A", discovered.Name);
        Assert.NotEmpty(discovered.Endpoints);
        Assert.All(discovered.Endpoints, endpoint => Assert.Equal(portA, endpoint.Port));
    }
}
