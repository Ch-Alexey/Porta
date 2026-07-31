using System.Net;
using Porta.Core.Identity;

namespace Porta.Core.Discovery;

/// <summary>
/// Склейка DNS-SD записей (TXT + SRV + A/AAAA) в <see cref="DiscoveredPeer"/>. Чистая
/// логика без сети — принимает уже разобранные записи, что делает её тестируемой в
/// отрыве от mDNS-стека. НЕ потокобезопасна: вызывающий сериализует доступ.
/// См. docs/features/05-discovery.md.
/// </summary>
public sealed class PeerResolver
{
    private readonly DeviceId? _selfId;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, InstanceState> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<IPAddress>> _hostAddresses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _announced = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="selfId">Свой Device ID — чтобы не «обнаруживать» самих себя (или null).</param>
    /// <param name="clock">Источник времени (для <see cref="DiscoveredPeer.DiscoveredAt"/>).</param>
    public PeerResolver(DeviceId? selfId, TimeProvider? clock = null)
    {
        _selfId = selfId;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Добавить TXT-записи инстанса.</summary>
    public IReadOnlyList<DiscoveredPeer> AddTxt(string instance, IReadOnlyList<string> txtStrings)
    {
        GetOrAdd(instance).Txt = txtStrings.ToList();
        return TryComplete(instance);
    }

    /// <summary>Добавить SRV-запись инстанса (порт + целевой хост).</summary>
    public IReadOnlyList<DiscoveredPeer> AddService(string instance, int port, string host)
    {
        InstanceState state = GetOrAdd(instance);
        state.Port = port;
        state.Host = host;
        return TryComplete(instance);
    }

    /// <summary>Добавить адрес хоста (A/AAAA). Может достроить несколько инстансов на этом хосте.</summary>
    public IReadOnlyList<DiscoveredPeer> AddAddress(string host, IPAddress address)
    {
        if (!_hostAddresses.TryGetValue(host, out List<IPAddress>? list))
            _hostAddresses[host] = list = [];
        if (!list.Contains(address))
            list.Add(address);

        List<DiscoveredPeer>? peers = null;
        foreach (string instance in _instances.Keys)
        {
            if (!string.Equals(_instances[instance].Host, host, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (DiscoveredPeer peer in TryComplete(instance))
                (peers ??= []).Add(peer);
        }
        return peers ?? [];
    }

    /// <summary>
    /// Забыть инстанс (устройство ушло). Возвращает его Device ID, если он был известен —
    /// для события «peer потерян».
    /// </summary>
    public DeviceId? Remove(string instance)
    {
        _announced.Remove(instance);
        DeviceId? id = _instances.TryGetValue(instance, out InstanceState? state) ? state.Id : null;
        _instances.Remove(instance);
        return id;
    }

    private IReadOnlyList<DiscoveredPeer> TryComplete(string instance)
    {
        InstanceState state = _instances[instance];

        if (!DiscoveryTxt.TryParse(state.Txt, out DeviceId? id, out string name, out _))
            return [];
        state.Id = id;

        if (id == _selfId)
            return [];
        if (state.Port is not int port || state.Host is null)
            return [];
        if (!_hostAddresses.TryGetValue(state.Host, out List<IPAddress>? addresses) || addresses.Count == 0)
            return [];
        if (!_announced.Add(instance))
            return [];

        var endpoints = addresses.Select(a => new IPEndPoint(a, port)).ToList();
        return [new DiscoveredPeer(id!, name, endpoints, _clock.GetUtcNow())];
    }

    private InstanceState GetOrAdd(string instance)
    {
        if (!_instances.TryGetValue(instance, out InstanceState? state))
            _instances[instance] = state = new InstanceState();
        return state;
    }

    private sealed class InstanceState
    {
        public List<string> Txt { get; set; } = [];
        public int? Port { get; set; }
        public string? Host { get; set; }
        public DeviceId? Id { get; set; }
    }
}
