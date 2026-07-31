using System.Net;
using Porta.Core.Identity;
using Porta.Core.Protocol;
using Porta.Core.Transport;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Установление сессии протокола поверх реального QUIC: проверка доверия и рукопожатие.
/// Требует libmsquic (иначе тихо пропускается). См. docs/features/07-protocol.md.
/// </summary>
[Trait("Category", "Integration")]
public class SessionOverQuicTests
{
    private static CancellationToken Timeout => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    private static async Task<(IPeerConnection Client, IPeerConnection Server, ITransportListener Listener)>
        ConnectAsync(DeviceIdentity serverId, DeviceIdentity clientId, QuicTransport server, QuicTransport client, CancellationToken ct)
    {
        ITransportListener listener = await server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        Task<IPeerConnection> acceptTask = listener.AcceptAsync(ct).AsTask();
        IPeerConnection clientConn = await client.ConnectAsync(listener.LocalEndPoint, serverId.Id, ct);
        IPeerConnection serverConn = await acceptTask;
        return (clientConn, serverConn, listener);
    }

    [Fact]
    public async Task Trusted_peers_establish_sessions_and_exchange_hello()
    {
        if (!QuicTransport.IsSupported)
            return;

        using var serverId = DeviceIdentity.Generate();
        using var clientId = DeviceIdentity.Generate();
        CancellationToken ct = Timeout;

        using var server = new QuicTransport(serverId);
        using var client = new QuicTransport(clientId);
        var (clientConn, serverConn, listener) = await ConnectAsync(serverId, clientId, server, client, ct);
        await using var listenerScope = listener;

        var clientTrust = new TrustList(serverId.Id);
        var serverTrust = new TrustList(clientId.Id);

        Task<PeerSession> clientSession = PeerSession.EstablishAsync(clientConn, clientTrust, "Client", isInitiator: true, ct);
        Task<PeerSession> serverSession = PeerSession.EstablishAsync(serverConn, serverTrust, "Server", isInitiator: false, ct);
        await Task.WhenAll(clientSession, serverSession);

        await using PeerSession cs = await clientSession;
        await using PeerSession ss = await serverSession;

        Assert.Equal(serverId.Id, cs.RemoteDeviceId);
        Assert.Equal("Server", cs.RemoteDeviceName);
        Assert.Equal(clientId.Id, ss.RemoteDeviceId);
        Assert.Equal("Client", ss.RemoteDeviceName);
    }

    [Fact]
    public async Task Untrusted_peer_is_rejected_at_session_establishment()
    {
        if (!QuicTransport.IsSupported)
            return;

        using var serverId = DeviceIdentity.Generate();
        using var clientId = DeviceIdentity.Generate();
        CancellationToken ct = Timeout;

        using var server = new QuicTransport(serverId);
        using var client = new QuicTransport(clientId);
        var (clientConn, serverConn, listener) = await ConnectAsync(serverId, clientId, server, client, ct);
        await using var listenerScope = listener;

        // Сервер не доверяет никому; клиент доверяет серверу.
        var serverTrust = new TrustList();
        Task<PeerSession> serverSession = PeerSession.EstablishAsync(serverConn, serverTrust, "Server", isInitiator: false, ct);

        // Клиентская сторона может упасть на закрытом соединении — наблюдаем и игнорируем.
        Task<PeerSession> clientSession = PeerSession.EstablishAsync(clientConn, new TrustList(serverId.Id), "Client", isInitiator: true, ct);
        _ = clientSession.ContinueWith(t => t.Exception, TaskScheduler.Default);

        await Assert.ThrowsAsync<UntrustedPeerException>(async () => await serverSession);
    }
}
