using Porta.Core.Identity;

namespace Porta.Core.Discovery;

/// <summary>
/// Обнаружение устройств в локальной сети. Абстракция: конкретная реализация (mDNS)
/// живёт в инфраструктуре, ядро от неё не зависит. См. docs/features/05-discovery.md.
/// </summary>
public interface IDeviceDiscovery : IDisposable
{
    /// <summary>Обнаружен (или обновлён) кандидат в сети.</summary>
    event Action<DiscoveredPeer>? PeerDiscovered;

    /// <summary>Кандидат пропал из сети.</summary>
    event Action<DeviceId>? PeerLost;

    /// <summary>Начать объявлять это устройство в сети.</summary>
    void Advertise(PortaAdvertisement self);

    /// <summary>Начать искать другие устройства.</summary>
    void StartBrowsing();
}
