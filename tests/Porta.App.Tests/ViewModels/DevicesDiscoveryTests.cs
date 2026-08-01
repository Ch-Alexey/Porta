using System;
using Porta.App.ViewModels;
using Porta.Core.Discovery;
using Porta.Core.Identity;

namespace Porta.App.Tests.ViewModels;

public class DevicesDiscoveryTests
{
    private static DiscoveredPeer Peer(DeviceId id, string name)
        => new(id, name, [], DateTimeOffset.UnixEpoch);

    [Fact]
    public void Discovered_peer_appears_in_list()
    {
        var discovery = new FakeDeviceDiscovery();
        var vm = new DevicesViewModel(new FakeAppData(), discovery, new ImmediateDispatcher());
        using var peerId = DeviceIdentity.Generate();

        discovery.RaiseDiscovered(Peer(peerId.Id, "Kitchen PC"));

        DiscoveredPeerItem item = Assert.Single(vm.DiscoveredPeers);
        Assert.Equal("Kitchen PC", item.Name);
        Assert.Equal(peerId.Id.ToString(), item.DeviceId);
    }

    [Fact]
    public void Duplicate_discovery_is_ignored()
    {
        var discovery = new FakeDeviceDiscovery();
        var vm = new DevicesViewModel(new FakeAppData(), discovery, new ImmediateDispatcher());
        using var peerId = DeviceIdentity.Generate();

        discovery.RaiseDiscovered(Peer(peerId.Id, "PC"));
        discovery.RaiseDiscovered(Peer(peerId.Id, "PC"));

        Assert.Single(vm.DiscoveredPeers);
    }

    [Fact]
    public void Lost_peer_is_removed()
    {
        var discovery = new FakeDeviceDiscovery();
        var vm = new DevicesViewModel(new FakeAppData(), discovery, new ImmediateDispatcher());
        using var peerId = DeviceIdentity.Generate();
        discovery.RaiseDiscovered(Peer(peerId.Id, "PC"));

        discovery.RaiseLost(peerId.Id);

        Assert.Empty(vm.DiscoveredPeers);
    }

    [Fact]
    public void Without_discovery_list_stays_empty()
    {
        var vm = new DevicesViewModel(new FakeAppData(), discovery: null, new ImmediateDispatcher());
        Assert.Empty(vm.DiscoveredPeers);
    }

    [Fact]
    public async Task Sync_with_found_calls_controller_for_each_peer()
    {
        var discovery = new FakeDeviceDiscovery();
        var controller = new FakeSyncController();
        var vm = new DevicesViewModel(new FakeAppData(), discovery, new ImmediateDispatcher(), controller);
        using var a = DeviceIdentity.Generate();
        using var b = DeviceIdentity.Generate();
        discovery.RaiseDiscovered(Peer(a.Id, "A"));
        discovery.RaiseDiscovered(Peer(b.Id, "B"));

        Assert.True(vm.CanSync);
        await vm.SyncWithFoundCommand.ExecuteAsync(null);

        Assert.Equal(2, controller.Calls.Count);
        Assert.NotNull(vm.StatusMessage);
    }

    [Fact]
    public void CanSync_is_false_without_controller_or_peers()
    {
        var vm = new DevicesViewModel(new FakeAppData(), new FakeDeviceDiscovery(), new ImmediateDispatcher(), sync: null);
        Assert.False(vm.CanSync);
    }
}
