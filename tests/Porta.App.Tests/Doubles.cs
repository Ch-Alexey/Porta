using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Porta.App.Services;
using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Core.Sync;

namespace Porta.App.Tests;

/// <summary>Диспетчер, выполняющий действие немедленно (для тестов без UI-потока).</summary>
internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>Управляемое обнаружение устройств: тест сам поднимает события.</summary>
internal sealed class FakeDeviceDiscovery : IDeviceDiscovery
{
    public event Action<DiscoveredPeer>? PeerDiscovered;
    public event Action<DeviceId>? PeerLost;

    public void Advertise(PortaAdvertisement self) { }
    public void StartBrowsing() { }
    public void Dispose() { }

    public void RaiseDiscovered(DiscoveredPeer peer) => PeerDiscovered?.Invoke(peer);
    public void RaiseLost(DeviceId deviceId) => PeerLost?.Invoke(deviceId);
}

/// <summary>Контроллер синка, записывающий вызовы (для тестов VM).</summary>
internal sealed class FakeSyncController : ISyncController
{
    public List<DiscoveredPeer> Calls { get; } = [];

    public Task<string> SyncWithPeerAsync(DiscoveredPeer peer, CancellationToken cancellationToken = default)
    {
        Calls.Add(peer);
        return Task.FromResult("ok");
    }
}
