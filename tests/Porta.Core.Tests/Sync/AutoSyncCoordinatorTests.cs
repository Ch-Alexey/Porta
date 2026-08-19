using System.Net;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;
using Porta.Core.Tests.Data;

namespace Porta.Core.Tests.Sync;

public class AutoSyncCoordinatorTests
{
    private sealed class FakeDiscovery : IDeviceDiscovery
    {
        public event Action<DiscoveredPeer>? PeerDiscovered;
#pragma warning disable CS0067 // требуется интерфейсом, в тесте не поднимается
        public event Action<DeviceId>? PeerLost;
#pragma warning restore CS0067

        public void Advertise(PortaAdvertisement self) { }
        public void StartBrowsing() { }
        public void Dispose() { }

        public void Raise(DiscoveredPeer peer) => PeerDiscovered?.Invoke(peer);
    }

    private sealed class ControllableSync : ISyncController
    {
        private readonly TaskCompletionSource? _gate;
        public int Calls;

        public ControllableSync(bool blocking = false)
        {
            if (blocking)
                _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<string> SyncWithPeerAsync(DiscoveredPeer peer, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            if (_gate is not null)
                await _gate.Task;
            return "ok";
        }
    }

    private sealed class FakeChanges : IChangeNotifier
    {
        public event Action? Changed;
        public void Raise() => Changed?.Invoke();
    }

    private static DiscoveredPeer Peer(DeviceId id) => new(id, "peer", [], DateTimeOffset.UnixEpoch);

    private static (DeviceRepository Repo, DeviceIdentity Identity) TrustedDevice(TempDatabase temp)
    {
        var repo = new DeviceRepository(temp.Database);
        var identity = DeviceIdentity.Generate();
        repo.Add(new TrustedDevice(identity.Id, identity.ExportPublicKey(), "peer", DateTimeOffset.UnixEpoch, null));
        return (repo, identity);
    }

    [Fact]
    public void Trusted_peer_triggers_sync()
    {
        using var temp = new TempDatabase();
        (DeviceRepository repo, DeviceIdentity peer) = TrustedDevice(temp);
        var discovery = new FakeDiscovery();
        var sync = new ControllableSync();
        using var coordinator = new AutoSyncCoordinator(discovery, repo, sync);
        coordinator.Start();

        discovery.Raise(Peer(peer.Id));

        Assert.Equal(1, sync.Calls);
        peer.Dispose();
    }

    [Fact]
    public void Untrusted_peer_is_ignored()
    {
        using var temp = new TempDatabase();
        var repo = new DeviceRepository(temp.Database);
        var discovery = new FakeDiscovery();
        var sync = new ControllableSync();
        using var coordinator = new AutoSyncCoordinator(discovery, repo, sync);
        coordinator.Start();
        using var stranger = DeviceIdentity.Generate();

        discovery.Raise(Peer(stranger.Id));

        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public void Concurrent_discovery_of_same_peer_syncs_once()
    {
        using var temp = new TempDatabase();
        (DeviceRepository repo, DeviceIdentity peer) = TrustedDevice(temp);
        var discovery = new FakeDiscovery();
        var sync = new ControllableSync(blocking: true); // синк «висит» в процессе
        using var coordinator = new AutoSyncCoordinator(discovery, repo, sync);
        coordinator.Start();

        discovery.Raise(Peer(peer.Id));
        discovery.Raise(Peer(peer.Id)); // повтор, пока первый не завершился

        Assert.Equal(1, sync.Calls);
        peer.Dispose();
    }

    [Fact]
    public void Local_change_syncs_known_trusted_peer()
    {
        using var temp = new TempDatabase();
        (DeviceRepository repo, DeviceIdentity peer) = TrustedDevice(temp);
        var discovery = new FakeDiscovery();
        var changes = new FakeChanges();
        var sync = new ControllableSync();
        using var coordinator = new AutoSyncCoordinator(discovery, repo, sync, changes);
        coordinator.Start();
        discovery.Raise(Peer(peer.Id)); // устройство найдено → синк 1

        changes.Raise(); // файл изменился → синк 2 с уже известным устройством

        Assert.Equal(2, sync.Calls);
        peer.Dispose();
    }

    [Fact]
    public void Local_change_without_known_peers_does_nothing()
    {
        using var temp = new TempDatabase();
        var repo = new DeviceRepository(temp.Database);
        var changes = new FakeChanges();
        var sync = new ControllableSync();
        using var coordinator = new AutoSyncCoordinator(new FakeDiscovery(), repo, sync, changes);
        coordinator.Start();

        changes.Raise();

        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public void Sync_can_retrigger_after_completion()
    {
        using var temp = new TempDatabase();
        (DeviceRepository repo, DeviceIdentity peer) = TrustedDevice(temp);
        var discovery = new FakeDiscovery();
        var sync = new ControllableSync(); // синхронно завершается
        using var coordinator = new AutoSyncCoordinator(discovery, repo, sync);
        coordinator.Start();

        discovery.Raise(Peer(peer.Id));
        discovery.Raise(Peer(peer.Id));

        Assert.Equal(2, sync.Calls);
        peer.Dispose();
    }
}
