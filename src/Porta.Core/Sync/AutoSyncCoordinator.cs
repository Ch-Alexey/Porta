using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Identity;

namespace Porta.Core.Sync;

/// <summary>
/// Автоматическая синхронизация: при появлении доверенного устройства в сети запускает
/// синк хранилищ с ним. Дедуплицирует одновременные синки с одним устройством.
/// См. docs/features/22-auto-sync.md.
/// </summary>
public sealed class AutoSyncCoordinator : IDisposable
{
    private readonly IDeviceDiscovery _discovery;
    private readonly IDeviceRepository _devices;
    private readonly ISyncController _sync;
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _started;

    public AutoSyncCoordinator(IDeviceDiscovery discovery, IDeviceRepository devices, ISyncController sync)
    {
        _discovery = discovery;
        _devices = devices;
        _sync = sync;
    }

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _discovery.PeerDiscovered += OnPeerDiscovered;
    }

    private void OnPeerDiscovered(DiscoveredPeer peer)
    {
        // Синкаем только с доверенными устройствами.
        if (_devices.Get(peer.DeviceId) is null)
            return;

        string id = peer.DeviceId.ToString();
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
            await _sync.SyncWithPeerAsync(peer).ConfigureAwait(false);
        }
        catch
        {
            // Авто-синк не должен шуметь исключениями; ошибки видны при ручном синке.
        }
        finally
        {
            lock (_gate)
                _inFlight.Remove(id);
        }
    }

    public void Dispose()
    {
        if (_started)
            _discovery.PeerDiscovered -= OnPeerDiscovered;
    }
}
