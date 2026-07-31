using Porta.Core.Identity;
using Porta.Core.Transport;

namespace Porta.Core.Protocol;

/// <summary>Второй стороне не доверяем — соединение отклонено.</summary>
public sealed class UntrustedPeerException(DeviceId deviceId)
    : Exception($"Устройство {deviceId} не в списке доверенных.")
{
    public DeviceId DeviceId { get; } = deviceId;
}

/// <summary>Несовместимая версия протокола.</summary>
public sealed class ProtocolVersionException(int local, int remote)
    : Exception($"Несовместимая версия протокола: локальная {local}, удалённая {remote}.")
{
    public int LocalVersion { get; } = local;
    public int RemoteVersion { get; } = remote;
}

/// <summary>
/// Установленная сессия с доверенным устройством: доверие проверено, версии согласованы.
/// Несёт контрольный канал для дальнейших сообщений. См. docs/features/07-protocol.md.
/// </summary>
public sealed class PeerSession : IAsyncDisposable
{
    private readonly IPeerConnection _connection;

    private PeerSession(IPeerConnection connection, MessageChannel control, string remoteName)
    {
        _connection = connection;
        Control = control;
        RemoteDeviceName = remoteName;
    }

    /// <summary>Device ID второй стороны (подтверждён транспортом).</summary>
    public DeviceId RemoteDeviceId => _connection.RemoteDeviceId;

    /// <summary>Имя второй стороны (из рукопожатия).</summary>
    public string RemoteDeviceName { get; }

    /// <summary>Контрольный канал сообщений.</summary>
    public MessageChannel Control { get; }

    /// <summary>
    /// Установить сессию поверх аутентифицированного соединения: проверить доверие и
    /// обменяться рукопожатием. Инициатор открывает контрольный поток, ответчик принимает.
    /// </summary>
    public static async Task<PeerSession> EstablishAsync(
        IPeerConnection connection,
        ITrustPolicy trust,
        string localDeviceName,
        bool isInitiator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(trust);

        if (!trust.IsTrusted(connection.RemoteDeviceId))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new UntrustedPeerException(connection.RemoteDeviceId);
        }

        Stream stream = isInitiator
            ? await connection.OpenStreamAsync(cancellationToken).ConfigureAwait(false)
            : await connection.AcceptStreamAsync(cancellationToken).ConfigureAwait(false);

        var control = new MessageChannel(stream);

        await control.WriteAsync(new HelloMessage(ProtocolVersion.Current, localDeviceName), cancellationToken)
            .ConfigureAwait(false);
        HelloMessage remoteHello = await control.ReadAsync<HelloMessage>(cancellationToken).ConfigureAwait(false);

        if (remoteHello.ProtocolVersion != ProtocolVersion.Current)
            throw new ProtocolVersionException(ProtocolVersion.Current, remoteHello.ProtocolVersion);

        return new PeerSession(connection, control, remoteHello.DeviceName);
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
