using System.Net;
using System.Text;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Protocol;
using Porta.Core.Sync;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Обмен индексом хранилища поверх реального QUIC и вычисление дельты принимающей
/// стороной. Требует libmsquic (иначе пропускается). См. docs/features/08-index-exchange.md.
/// </summary>
[Trait("Category", "Integration")]
public class IndexExchangeOverQuicTests : IDisposable
{
    private readonly string _serverDir;
    private readonly string _clientDir;

    public IndexExchangeOverQuicTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _serverDir = Path.Combine(baseDir, "server");
        _clientDir = Path.Combine(baseDir, "client");
        Directory.CreateDirectory(_serverDir);
        Directory.CreateDirectory(_clientDir);
    }

    [Fact]
    public async Task Receiver_computes_missing_blocks_from_sent_index()
    {
        if (!QuicTransport.IsSupported)
            return;

        File.WriteAllBytes(Path.Combine(_serverDir, "a.txt"), Encoding.UTF8.GetBytes("server-only file"));
        IReadOnlyList<FileIndexEntry> serverIndex = new FolderScanner().Scan(_serverDir);
        IReadOnlyList<FileIndexEntry> clientIndex = new FolderScanner().Scan(_clientDir); // пусто

        using var serverId = DeviceIdentity.Generate();
        using var clientId = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

        using var server = new QuicTransport(serverId);
        using var client = new QuicTransport(clientId);
        await using var listener = await server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        var acceptTask = listener.AcceptAsync(ct).AsTask();
        var clientConn = await client.ConnectAsync(listener.LocalEndPoint, serverId.Id, ct);
        var serverConn = await acceptTask;

        var clientSessionTask = PeerSession.EstablishAsync(clientConn, new TrustList(serverId.Id), "Client", isInitiator: true, cancellationToken: ct);
        var serverSessionTask = PeerSession.EstablishAsync(serverConn, new TrustList(clientId.Id), "Server", isInitiator: false, cancellationToken: ct);
        await Task.WhenAll(clientSessionTask, serverSessionTask);
        await using var cs = await clientSessionTask;
        await using var ss = await serverSessionTask;

        // Сервер шлёт свой индекс, клиент читает и вычисляет дельту.
        var writeTask = ss.Control.WriteAsync(new FolderIndexMessage("s1", serverIndex), ct).AsTask();
        FolderIndexMessage received = await cs.Control.ReadAsync<FolderIndexMessage>(ct);
        await writeTask;

        IndexDiff diff = IndexComparer.Compare(clientIndex, received.Entries);

        Assert.Equal("s1", received.StorageId);
        Assert.Equal("a.txt", diff.FilesToUpdate.Single().RelativePath);
        Assert.NotEmpty(diff.MissingBlocks);
    }

    public void Dispose()
    {
        string baseDir = Path.GetDirectoryName(_serverDir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }
}
