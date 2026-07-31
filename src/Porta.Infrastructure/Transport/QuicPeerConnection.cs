using System.Net.Quic;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Porta.Core.Identity;
using Porta.Core.Transport;

namespace Porta.Infrastructure.Transport;

/// <summary>Соединение QUIC с известным Device ID второй стороны.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("windows")]
internal sealed class QuicPeerConnection : IPeerConnection
{
    private readonly QuicConnection _connection;

    private QuicPeerConnection(QuicConnection connection, DeviceId remoteDeviceId)
    {
        _connection = connection;
        RemoteDeviceId = remoteDeviceId;
    }

    public DeviceId RemoteDeviceId { get; }

    /// <summary>Обернуть соединение, вычислив Device ID второй стороны из её сертификата.</summary>
    public static QuicPeerConnection Create(QuicConnection connection)
    {
        if (connection.RemoteCertificate is not X509Certificate2 certificate)
            throw new InvalidOperationException("Вторая сторона не предъявила сертификат.");

        return new QuicPeerConnection(connection, DeviceCertificate.FromCertificate(certificate));
    }

    public async ValueTask<Stream> OpenStreamAsync(CancellationToken cancellationToken = default)
        => await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken).ConfigureAwait(false);

    public async ValueTask<Stream> AcceptStreamAsync(CancellationToken cancellationToken = default)
        => await _connection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync().ConfigureAwait(false);
}
