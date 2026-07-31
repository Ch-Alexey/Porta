using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;
using Porta.Core.Transport;

namespace Porta.Infrastructure.Transport;

/// <summary>Приёмник входящих QUIC-соединений.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("windows")]
internal sealed class QuicTransportListener : ITransportListener
{
    private readonly QuicListener _listener;

    public QuicTransportListener(QuicListener listener) => _listener = listener;

    public IPEndPoint LocalEndPoint => _listener.LocalEndPoint;

    public async ValueTask<IPeerConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        QuicConnection connection = await _listener.AcceptConnectionAsync(cancellationToken).ConfigureAwait(false);
        return QuicPeerConnection.Create(connection);
    }

    public async ValueTask DisposeAsync() => await _listener.DisposeAsync().ConfigureAwait(false);
}
