using System.Net;
using System.Text;
using Porta.Core.Identity;
using Porta.Core.Transport;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Интеграционный тест QUIC-транспорта. Требует поддержки QUIC (libmsquic); при её
/// отсутствии тест тихо пропускается. Запуск на macOS:
/// <c>DYLD_LIBRARY_PATH=/opt/homebrew/opt/libmsquic/lib dotnet test</c>.
/// См. docs/features/06-transport.md.
/// </summary>
[Trait("Category", "Integration")]
public class QuicTransportTests
{
    private static CancellationToken Timeout => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    [Fact]
    public async Task Peers_authenticate_by_pinning_and_exchange_data()
    {
        if (!QuicTransport.IsSupported)
            return; // нет libmsquic — пропускаем

        using var serverIdentity = DeviceIdentity.Generate();
        using var clientIdentity = DeviceIdentity.Generate();
        CancellationToken ct = Timeout;

        using var server = new QuicTransport(serverIdentity);
        using var client = new QuicTransport(clientIdentity);

        await using ITransportListener listener = await server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        Task<IPeerConnection> acceptTask = listener.AcceptAsync(ct).AsTask();

        await using IPeerConnection clientConn =
            await client.ConnectAsync(listener.LocalEndPoint, serverIdentity.Id, ct);
        await using IPeerConnection serverConn = await acceptTask;

        // Взаимная аутентификация: каждая сторона знает подтверждённый Device ID другой.
        Assert.Equal(serverIdentity.Id, clientConn.RemoteDeviceId);
        Assert.Equal(clientIdentity.Id, serverConn.RemoteDeviceId);

        // Обмен данными по потоку.
        byte[] payload = Encoding.UTF8.GetBytes("hello porta");
        await using Stream outbound = await clientConn.OpenStreamAsync(ct);
        await outbound.WriteAsync(payload, ct);
        await outbound.FlushAsync(ct);

        await using Stream inbound = await serverConn.AcceptStreamAsync(ct);
        byte[] received = new byte[payload.Length];
        await inbound.ReadExactlyAsync(received, ct);

        Assert.Equal(payload, received);
    }

    [Fact]
    public async Task Connecting_with_wrong_pinned_id_is_rejected()
    {
        if (!QuicTransport.IsSupported)
            return;

        using var serverIdentity = DeviceIdentity.Generate();
        using var clientIdentity = DeviceIdentity.Generate();
        using var stranger = DeviceIdentity.Generate();
        CancellationToken ct = Timeout;

        using var server = new QuicTransport(serverIdentity);
        using var client = new QuicTransport(clientIdentity);

        await using ITransportListener listener = await server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        _ = listener.AcceptAsync(ct).AsTask().ContinueWith(t => t.Exception, TaskScheduler.Default);

        // Клиент ждёт stranger, а сервер предъявит serverIdentity → pinning не сходится.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using IPeerConnection _ = await client.ConnectAsync(listener.LocalEndPoint, stranger.Id, ct);
        });
    }
}
