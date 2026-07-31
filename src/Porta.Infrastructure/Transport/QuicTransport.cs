using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Porta.Core.Identity;
using Porta.Core.Transport;

namespace Porta.Infrastructure.Transport;

/// <summary>
/// Транспорт на QUIC (TLS 1.3). Обе стороны предъявляют сертификат устройства; доверие —
/// по совпадению ключа сертификата с ожидаемым Device ID (pinning), а не по цепочке CA.
/// Требует поддержки QUIC (libmsquic). См. docs/features/06-transport.md и ADR-0006.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("windows")]
public sealed class QuicTransport : ITransport, IDisposable
{
    private static readonly SslApplicationProtocol Alpn = new("porta");

    private readonly X509Certificate2 _certificate;

    public QuicTransport(DeviceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _certificate = PrepareCertificate(identity);
    }

    /// <summary>Доступен ли QUIC в текущем окружении (наличие libmsquic).</summary>
    public static bool IsSupported => QuicListener.IsSupported && QuicConnection.IsSupported;

    public async ValueTask<ITransportListener> ListenAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
    {
        var options = new QuicListenerOptions
        {
            ListenEndPoint = endpoint,
            ApplicationProtocols = [Alpn],
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(ServerConnectionOptions()),
        };

        QuicListener listener = await QuicListener.ListenAsync(options, cancellationToken).ConfigureAwait(false);
        return new QuicTransportListener(listener);
    }

    public async ValueTask<IPeerConnection> ConnectAsync(
        IPEndPoint endpoint,
        DeviceId expectedRemoteId,
        CancellationToken cancellationToken = default)
    {
        var options = new QuicClientConnectionOptions
        {
            RemoteEndPoint = endpoint,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = [Alpn],
                TargetHost = "porta",
                ClientCertificates = [_certificate],
                // Pinning: принимаем только сертификат ожидаемого устройства.
                RemoteCertificateValidationCallback = (_, cert, _, _) =>
                    cert is X509Certificate2 c && DeviceCertificate.Matches(c, expectedRemoteId),
            },
        };

        QuicConnection connection = await QuicConnection.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        return QuicPeerConnection.Create(connection);
    }

    private QuicServerConnectionOptions ServerConnectionOptions() => new()
    {
        DefaultStreamErrorCode = 0,
        DefaultCloseErrorCode = 0,
        ServerAuthenticationOptions = new SslServerAuthenticationOptions
        {
            ApplicationProtocols = [Alpn],
            ServerCertificate = _certificate,
            ClientCertificateRequired = true,
            // Принимаем любой корректный сертификат клиента; доверие решает слой выше
            // по RemoteDeviceId (сверяя с доверенными устройствами).
            RemoteCertificateValidationCallback = (_, cert, _, _) => cert is not null,
        },
    };

    private static X509Certificate2 PrepareCertificate(DeviceIdentity identity)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 ephemeral = identity.CreateSelfSignedCertificate(now.AddDays(-1), now.AddYears(1));
        // PFX round-trip: гарантирует, что приватный ключ пригоден для TLS-стека на всех платформах.
        byte[] pfx = ephemeral.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, password: null, X509KeyStorageFlags.Exportable);
    }

    public void Dispose() => _certificate.Dispose();
}
