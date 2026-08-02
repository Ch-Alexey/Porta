using System.Net;
using System.Net.Sockets;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;
using Porta.Core.Transport;
using Porta.Infrastructure.Discovery;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Сквозной сценарий как в UI: устройство B находит A по реальному mDNS и синхронизирует
/// папку по обнаруженному адресу через реальный QUIC. Требует multicast + libmsquic.
/// См. docs/features/21-sync-from-ui.md.
/// </summary>
[Trait("Category", "Integration")]
public class DiscoverAndSyncE2ETests : IDisposable
{
    private const int PortA = 47119;

    private readonly string _baseDir;
    private readonly string _folderA;
    private readonly string _folderB;

    public DiscoverAndSyncE2ETests()
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

    private static IPEndPoint SelectEndpoint(DiscoveredPeer peer)
        => peer.Endpoints.FirstOrDefault(e => e.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(e.Address))
           ?? peer.Endpoints.First(e => e.Address.AddressFamily == AddressFamily.InterNetwork);

    [Fact]
    public async Task DeviceB_discovers_and_syncs_from_DeviceA()
    {
        if (!QuicTransport.IsSupported)
            return;

        byte[] payload = new byte[20_000];
        new Random(7).NextBytes(payload);
        await File.WriteAllBytesAsync(Path.Combine(_folderA, "shared.bin"), payload);

        using var idA = DeviceIdentity.Generate();
        using var idB = DeviceIdentity.Generate();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        CancellationToken ct = cts.Token;

        (DeviceRepository devicesA, FileIndexRepository indexA) = Repos("A");
        (DeviceRepository devicesB, FileIndexRepository indexB) = Repos("B");
        Trust(devicesA, idB, "B");
        Trust(devicesB, idA, "A");

        // --- Устройство A: объявляет себя, слушает и отдаёт хранилище ---
        using var discoveryA = new MdnsDeviceDiscovery(idA.Id);
        discoveryA.Advertise(new PortaAdvertisement(idA.Id, "Device A", PortA));

        using var transportA = new QuicTransport(idA);
        var serviceA = new SyncService(transportA, idA, "Device A", new DeviceRepositoryTrustPolicy(devicesA), indexA);
        await using ITransportListener listenerA = await transportA.ListenAsync(new IPEndPoint(IPAddress.Any, PortA), ct);

        var serveLoop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await serviceA.ServeOnceAsync(listenerA, id => id == "s1" ? _folderA : null, cancellationToken: ct); }
                catch when (!ct.IsCancellationRequested) { }
                catch { break; }
            }
        }, ct);

        // --- Устройство B: находит A по mDNS ---
        using var discoveryB = new MdnsDeviceDiscovery(idB.Id);
        var found = new TaskCompletionSource<DiscoveredPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        discoveryB.PeerDiscovered += peer =>
        {
            if (peer.DeviceId == idA.Id && peer.Endpoints.Any(e => e.Address.AddressFamily == AddressFamily.InterNetwork))
                found.TrySetResult(peer);
        };
        discoveryB.StartBrowsing();

        Task completed = await Task.WhenAny(found.Task, Task.Delay(TimeSpan.FromSeconds(20), ct));
        Assert.True(completed == found.Task, "Устройство A не найдено по mDNS.");
        DiscoveredPeer discovered = await found.Task;

        // --- Устройство B: синхронизирует по обнаруженному адресу ---
        using var transportB = new QuicTransport(idB);
        var serviceB = new SyncService(transportB, idB, "Device B", new DeviceRepositoryTrustPolicy(devicesB), indexB);
        SyncApplyReport report = await serviceB.PullAsync(SelectEndpoint(discovered), idA.Id, "s1", _folderB, cancellationToken: ct);

        Assert.Equal(1, report.Accepted);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(_folderB, "shared.bin"), ct));

        await cts.CancelAsync();
        try { await serveLoop; } catch { /* остановка */ }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_baseDir))
            Directory.Delete(_baseDir, recursive: true);
    }
}
