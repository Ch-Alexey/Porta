using System.Net;
using Porta.Core.Identity;

namespace Porta.Core.Transport;

/// <summary>
/// Аутентифицированное соединение с доверенным устройством. Личность второй стороны
/// подтверждена сертификатом (pinning по Device ID). См. docs/features/06-transport.md.
/// </summary>
public interface IPeerConnection : IAsyncDisposable
{
    /// <summary>Device ID второй стороны (из её сертификата).</summary>
    DeviceId RemoteDeviceId { get; }

    /// <summary>Открыть исходящий двунаправленный поток данных.</summary>
    ValueTask<Stream> OpenStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>Принять входящий поток данных от второй стороны.</summary>
    ValueTask<Stream> AcceptStreamAsync(CancellationToken cancellationToken = default);
}

/// <summary>Приёмник входящих соединений.</summary>
public interface ITransportListener : IAsyncDisposable
{
    /// <summary>Локальный адрес, на котором слушаем (порт может быть назначен ОС).</summary>
    IPEndPoint LocalEndPoint { get; }

    /// <summary>Принять следующее входящее соединение (после успешного TLS-хендшейка).</summary>
    ValueTask<IPeerConnection> AcceptAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Транспорт для защищённого обмена между устройствами. Реализация (QUIC) живёт в
/// инфраструктуре; ядро зависит только от этой абстракции. См. ADR-0006.
/// </summary>
public interface ITransport
{
    /// <summary>Начать слушать входящие соединения на указанном адресе.</summary>
    ValueTask<ITransportListener> ListenAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Подключиться к адресу и проверить, что вторая сторона — именно
    /// <paramref name="expectedRemoteId"/> (pinning). Иначе соединение отклоняется.
    /// </summary>
    ValueTask<IPeerConnection> ConnectAsync(
        IPEndPoint endpoint,
        DeviceId expectedRemoteId,
        CancellationToken cancellationToken = default);
}
