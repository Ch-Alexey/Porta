using System.Net;
using Porta.Core.Identity;
using Porta.Core.Protocol;
using Porta.Core.Sync;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// `SyncProtocol` поверх реального QUIC: движок синхронизирует хранилище между узлами.
/// Требует libmsquic (иначе пропускается). См. docs/features/10-sync-engine.md.
/// </summary>
[Trait("Category", "Integration")]
public class SyncEngineOverQuicTests : IDisposable
{
    private readonly string _senderDir;
    private readonly string _receiverDir;

    public SyncEngineOverQuicTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _senderDir = Path.Combine(baseDir, "sender");
        _receiverDir = Path.Combine(baseDir, "receiver");
        Directory.CreateDirectory(_senderDir);
        Directory.CreateDirectory(_receiverDir);
    }

    [Fact]
    public async Task Engine_synchronizes_storage_over_quic()
    {
        if (!QuicTransport.IsSupported)
            return;

        byte[] payload = new byte[50_000];
        new Random(77).NextBytes(payload);
        Directory.CreateDirectory(Path.Combine(_senderDir, "docs"));
        await File.WriteAllBytesAsync(Path.Combine(_senderDir, "docs", "big.bin"), payload);
        await File.WriteAllTextAsync(Path.Combine(_senderDir, "note.txt"), "hello");

        using var serverId = DeviceIdentity.Generate();
        using var clientId = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;

        using var server = new QuicTransport(serverId);
        using var client = new QuicTransport(clientId);
        await using var listener = await server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        var acceptTask = listener.AcceptAsync(ct).AsTask();
        var clientConn = await client.ConnectAsync(listener.LocalEndPoint, serverId.Id, ct);
        var serverConn = await acceptTask;

        var clientSessionTask = PeerSession.EstablishAsync(clientConn, new TrustList(serverId.Id), "Client", isInitiator: true, ct);
        var serverSessionTask = PeerSession.EstablishAsync(serverConn, new TrustList(clientId.Id), "Server", isInitiator: false, ct);
        await Task.WhenAll(clientSessionTask, serverSessionTask);
        await using PeerSession cs = clientSessionTask.Result;
        await using PeerSession ss = serverSessionTask.Result;

        // Сервер отдаёт своё хранилище, клиент тянет.
        Task serve = SyncProtocol.ServeAsync(ss.Control, _senderDir, "s1", ct);
        Task<SyncResult> pull = SyncProtocol.PullAsync(cs.Control, _receiverDir, "s1", ct);
        await Task.WhenAll(serve, pull);

        Assert.Equal(2, pull.Result.FilesUpdated);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(_receiverDir, "docs", "big.bin"), ct));
        Assert.Equal("hello", await File.ReadAllTextAsync(Path.Combine(_receiverDir, "note.txt"), ct));
    }

    public void Dispose()
    {
        string baseDir = Path.GetDirectoryName(_senderDir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }
}
