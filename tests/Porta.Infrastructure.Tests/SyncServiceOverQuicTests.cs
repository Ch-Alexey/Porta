using System.Net;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;
using Porta.Core.Transport;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// `SyncService` поверх реального QUIC с доверием из БД: устройство подтягивает
/// хранилище у другого. Требует libmsquic. См. docs/features/19-sync-service.md.
/// </summary>
[Trait("Category", "Integration")]
public class SyncServiceOverQuicTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _folderA;
    private readonly string _folderB;

    public SyncServiceOverQuicTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _folderA = Path.Combine(_baseDir, "A");
        _folderB = Path.Combine(_baseDir, "B");
        Directory.CreateDirectory(_folderA);
        Directory.CreateDirectory(_folderB);
    }

    private (DeviceRepository Devices, FileIndexRepository Index) Repos(string name)
    {
        var db = new PortaDatabase(Path.Combine(_baseDir, name + ".db"));
        db.Migrate();
        return (new DeviceRepository(db), new FileIndexRepository(db));
    }

    private static void Trust(DeviceRepository repo, DeviceIdentity other, string name)
        => repo.Add(new TrustedDevice(other.Id, other.ExportPublicKey(), name, DateTimeOffset.UnixEpoch, null));

    [Fact]
    public async Task Service_pulls_storage_from_trusted_peer()
    {
        if (!QuicTransport.IsSupported)
            return;

        byte[] payload = new byte[25_000];
        new Random(88).NextBytes(payload);
        await File.WriteAllBytesAsync(Path.Combine(_folderA, "file.bin"), payload);

        using var idA = DeviceIdentity.Generate();
        using var idB = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;

        (DeviceRepository devicesA, FileIndexRepository indexA) = Repos("A");
        (DeviceRepository devicesB, FileIndexRepository indexB) = Repos("B");
        Trust(devicesA, idB, "B"); // A доверяет B (входящее соединение)
        Trust(devicesB, idA, "A"); // B доверяет A (исходящее соединение)

        using var transportA = new QuicTransport(idA);
        using var transportB = new QuicTransport(idB);
        var serviceA = new SyncService(transportA, idA, "A", new DeviceRepositoryTrustPolicy(devicesA), indexA);
        var serviceB = new SyncService(transportB, idB, "B", new DeviceRepositoryTrustPolicy(devicesB), indexB);

        await using ITransportListener listener = await transportA.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        Task serve = serviceA.ServeOnceAsync(listener, "s1", _folderA, cancellationToken: ct);
        Task<SyncApplyReport> pull = serviceB.PullAsync(listener.LocalEndPoint, idA.Id, "s1", _folderB, cancellationToken: ct);
        await Task.WhenAll(serve, pull);

        Assert.Equal(1, (await pull).Accepted);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(_folderB, "file.bin"), ct));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }
}
