using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Identity;

namespace Porta.Core.Sync;

/// <summary>
/// Автоматическая синхронизация: синкает с доверенными устройствами при их появлении в
/// сети И при локальных изменениях файлов. Дедуплицирует одновременные синки с одним
/// устройством. См. docs/features/22-auto-sync.md, docs/features/23-file-change-sync.md.
/// </summary>
public sealed class AutoSyncCoordinator : IDisposable
{
    private readonly IDeviceDiscovery _discovery;
    private readonly IDeviceRepository _devices;
    private readonly ISyncController _sync;
    private readonly IChangeNotifier? _changes;

    private readonly Dictionary<string, DiscoveredPeer> _known = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _started;

    public AutoSyncCoordinator(
        IDeviceDiscovery discovery,
        IDeviceRepository devices,
        ISyncController sync,
        IChangeNotifier? changes = null)
    {
        _discovery = discovery;
        _devices = devices;
        _sync = sync;
        _changes = changes;
    }

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _discovery.PeerDiscovered += OnPeerDiscovered;
        _discovery.PeerLost += OnPeerLost;
        if (_changes is not null)
            _changes.Changed += OnLocalChange;
    }

    private void OnPeerDiscovered(DiscoveredPeer peer)
    {
        if (_devices.Get(peer.DeviceId) is null)
            return; // не доверенное

        string id = peer.DeviceId.ToString();
        lock (_gate)
            _known[id] = peer;

        TriggerSync(peer, id);
    }

    private void OnPeerLost(DeviceId deviceId)
    {
        lock (_gate)
            _known.Remove(deviceId.ToString());
    }

    private void OnLocalChange()
    {
        List<DiscoveredPeer> peers;
        lock (_gate)
            peers = _known.Values.ToList();

        foreach (DiscoveredPeer peer in peers)
        {
            if (_devices.Get(peer.DeviceId) is null)
                continue;
            TriggerSync(peer, peer.DeviceId.ToString());
        }
    }

    private void TriggerSync(DiscoveredPeer peer, string id)
    {
        lock (_gate)
        {
            if (!_inFlight.Add(id))
                return; // синк с этим устройством уже идёт
        }

        _ = RunAsync(peer, id);
    }

    private async Task RunAsync(DiscoveredPeer peer, string id)
    {
        try
        {
            await _sync.SyncWithPeerAsync(peer, SyncTrigger.Automatic).ConfigureAwait(false);
        }
        catch
        {
            // Авто-синк не должен шуметь исключениями.
        }
        finally
        {
            lock (_gate)
                _inFlight.Remove(id);
        }
    }

    public void Dispose()
    {
        if (!_started)
            return;
        _discovery.PeerDiscovered -= OnPeerDiscovered;
        _discovery.PeerLost -= OnPeerLost;
        if (_changes is not null)
            _changes.Changed -= OnLocalChange;
    }
}
