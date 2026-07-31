using System.Net;
using Porta.Core.Identity;

namespace Porta.Core.Discovery;

/// <summary>Что это устройство объявляет в сети. См. docs/features/05-discovery.md.</summary>
/// <param name="DeviceId">Device ID этого устройства.</param>
/// <param name="Name">Отображаемое имя.</param>
/// <param name="Port">Порт транспорта (QUIC).</param>
public sealed record PortaAdvertisement(DeviceId DeviceId, string Name, int Port);

/// <summary>
/// Найденный в сети кандидат. Это ещё не доверенное устройство — доверие проверяется
/// отдельно по ключам из связывания.
/// </summary>
/// <param name="DeviceId">Device ID, заявленный в TXT.</param>
/// <param name="Name">Отображаемое имя из TXT.</param>
/// <param name="Endpoints">Адреса и порт для подключения.</param>
/// <param name="DiscoveredAt">Когда обнаружен.</param>
public sealed record DiscoveredPeer(
    DeviceId DeviceId,
    string Name,
    IReadOnlyList<IPEndPoint> Endpoints,
    DateTimeOffset DiscoveredAt);
