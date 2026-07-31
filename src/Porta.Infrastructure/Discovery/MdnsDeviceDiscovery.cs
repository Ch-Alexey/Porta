using Makaretu.Dns;
using Porta.Core.Discovery;
using Porta.Core.Identity;

namespace Porta.Infrastructure.Discovery;

/// <summary>
/// mDNS/DNS-SD обнаружение устройств на Makaretu.Dns. Живёт в инфраструктуре, чтобы
/// не тянуть сетевую зависимость в ядро. Логика склейки записей вынесена в чистый
/// <see cref="PeerResolver"/> (тестируемый); здесь — только сетевой ввод-вывод и трансляция
/// записей Makaretu в вызовы резолвера. См. docs/features/05-discovery.md.
/// </summary>
public sealed class MdnsDeviceDiscovery : IDeviceDiscovery
{
    // Без ".local" — Makaretu добавляет домен сам (иначе получается "_porta._tcp.local.local").
    private static readonly DomainName ServiceName = new(DiscoveryTxt.ServiceType);

    private readonly MulticastService _mdns;
    private readonly ServiceDiscovery _sd;
    private readonly object _gate = new();
    private readonly PeerResolver _resolver;
    private bool _started;

    public MdnsDeviceDiscovery(DeviceId? selfId = null, TimeProvider? clock = null)
    {
        _resolver = new PeerResolver(selfId, clock);
        _mdns = new MulticastService();
        _sd = new ServiceDiscovery(_mdns);
    }

    public event Action<DiscoveredPeer>? PeerDiscovered;
    public event Action<DeviceId>? PeerLost;

    public void Advertise(PortaAdvertisement self)
    {
        ArgumentNullException.ThrowIfNull(self);

        var profile = new ServiceProfile(
            new DomainName(DiscoveryTxt.InstanceName(self.DeviceId)),
            ServiceName,
            (ushort)self.Port);
        foreach (var (key, value) in DiscoveryTxt.BuildPairs(self))
            profile.AddProperty(key, value);

        // Сначала стартуем сервис, потом объявляем — иначе анонс уходит «в никуда»
        // (сокеты ещё не готовы).
        EnsureStarted();
        _sd.Advertise(profile); // отвечать на будущие запросы
        _sd.Announce(profile);  // проактивно объявить себя уже слушающим browser'ам
    }

    public void StartBrowsing()
    {
        _sd.ServiceInstanceDiscovered += OnInstanceDiscovered;
        _sd.ServiceInstanceShutdown += OnInstanceShutdown;
        _mdns.AnswerReceived += OnAnswerReceived;
        _mdns.NetworkInterfaceDiscovered += (_, _) => _sd.QueryServiceInstances(ServiceName);
        EnsureStarted();
    }

    private void EnsureStarted()
    {
        if (_started)
            return;
        _started = true;
        _mdns.Start();
    }

    private void OnInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
    {
        // Запрашиваем детали инстанса: порт/хост (SRV) и свойства (TXT).
        _mdns.SendQuery(e.ServiceInstanceName, DnsClass.IN, DnsType.SRV);
        _mdns.SendQuery(e.ServiceInstanceName, DnsClass.IN, DnsType.TXT);
    }

    private void OnAnswerReceived(object? sender, MessageEventArgs e)
    {
        var records = e.Message.Answers.Concat(e.Message.AdditionalRecords);
        List<DiscoveredPeer> discovered = [];

        lock (_gate)
        {
            foreach (ResourceRecord record in records)
                discovered.AddRange(Ingest(record));
        }

        foreach (DiscoveredPeer peer in discovered)
            PeerDiscovered?.Invoke(peer);
    }

    private IReadOnlyList<DiscoveredPeer> Ingest(ResourceRecord record) => record switch
    {
        SRVRecord srv => IngestService(srv),
        TXTRecord txt => _resolver.AddTxt(txt.Name!.ToString(), txt.Strings),
        AddressRecord address => _resolver.AddAddress(address.Name!.ToString(), address.Address),
        _ => [],
    };

    private IReadOnlyList<DiscoveredPeer> IngestService(SRVRecord srv)
    {
        // Целевой хост может ещё не иметь адреса — запросим A/AAAA.
        _mdns.SendQuery(srv.Target, DnsClass.IN, DnsType.A);
        _mdns.SendQuery(srv.Target, DnsClass.IN, DnsType.AAAA);
        return _resolver.AddService(srv.Name!.ToString(), srv.Port, srv.Target.ToString());
    }

    private void OnInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs e)
    {
        DeviceId? lost;
        lock (_gate)
        {
            lost = _resolver.Remove(e.ServiceInstanceName.ToString());
        }

        if (lost is not null)
            PeerLost?.Invoke(lost);
    }

    public void Dispose()
    {
        _sd.Dispose();
        _mdns.Stop();
        _mdns.Dispose();
    }
}
