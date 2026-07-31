using System.Net;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Protocol;
using Porta.Core.Sync;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Версионированный синк поверх реального QUIC: обмен версионированными индексами и
/// синхронизация файла. Требует libmsquic (иначе пропускается).
/// См. docs/features/17-versioned-sync.md.
/// </summary>
[Trait("Category", "Integration")]
public class VersionedSyncOverQuicTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _folderA;
    private readonly string _folderB;

    public VersionedSyncOverQuicTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _folderA = Path.Combine(_baseDir, "A");
        _folderB = Path.Combine(_baseDir, "B");
        Directory.CreateDirectory(_folderA);
        Directory.CreateDirectory(_folderB);
    }

    private FileIndexRepository Repo(string name)
    {
        var db = new PortaDatabase(Path.Combine(_baseDir, name + ".db"));
        db.Migrate();
        return new FileIndexRepository(db);
    }

    [Fact]
    public async Task Versioned_storage_syncs_over_quic()
    {
        if (!QuicTransport.IsSupported)
            return;

        byte[] payload = new byte[30_000];
        new Random(55).NextBytes(payload);
        await File.WriteAllBytesAsync(Path.Combine(_folderA, "data.bin"), payload);

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
        await using PeerSession cs = await clientSessionTask;
        await using PeerSession ss = await serverSessionTask;

        Task serve = VersionedSync.ServeAsync(ss.Control, Repo("server"), _folderA, "s1", serverId.Id, cancellationToken: ct);
        Task<SyncApplyReport> pull = VersionedSync.PullAsync(
            cs.Control, Repo("client"), _folderB, "s1", clientId.Id, serverId.Id, cancellationToken: ct);
        await Task.WhenAll(serve, pull);

        Assert.Equal(1, (await pull).Accepted);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(_folderB, "data.bin"), ct));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }
}
