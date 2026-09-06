using System.Net;
using System.Net.Sockets;
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
/// Авто-синхронизация: устройство B находит доверенное A по реальному mDNS и синхронизирует
/// хранилище АВТОМАТИЧЕСКИ (без кнопки). Требует multicast + libmsquic.
/// См. docs/features/22-auto-sync.md.
/// </summary>
[Trait("Category", "Integration")]
public class AutoSyncE2ETests : IDisposable
{
    private const int PortA = 47120;

    private readonly string _baseDir;
    private readonly string _folderA;
    private readonly string _folderB;

    public AutoSyncE2ETests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _folderA = Path.Combine(_baseDir, "A");
        _folderB = Path.Combine(_baseDir, "B");
        Directory.CreateDirectory(_folderA);
        Directory.CreateDirectory(_folderB);
    }

    /// <summary>Инлайн-контроллер: тянет все локальные хранилища и сигналит о завершении.</summary>
    private sealed class PullAllController(SyncService service, IStorageRepository storages, TaskCompletionSource done)
        : ISyncController
    {
        public async Task<string> SyncWithPeerAsync(DiscoveredPeer peer, SyncTrigger trigger = SyncTrigger.Manual, CancellationToken cancellationToken = default)
        {
            IPEndPoint endpoint = peer.Endpoints.First(e => e.Address.AddressFamily == AddressFamily.InterNetwork);
            foreach (Storage storage in storages.List())
                await service.PullAsync(endpoint, peer.DeviceId, storage.Id, storage.LocalPath, cancellationToken: cancellationToken);
            done.TrySetResult();
            return "ok";
        }
    }

    [Fact]
    public async Task DeviceB_auto_syncs_from_trusted_A_on_discovery()
    {
        if (!QuicTransport.IsSupported)
            return;

        byte[] payload = new byte[15_000];
        new Random(3).NextBytes(payload);
        await File.WriteAllBytesAsync(Path.Combine(_folderA, "auto.bin"), payload);

        using var idA = DeviceIdentity.Generate();
        using var idB = DeviceIdentity.Generate();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        CancellationToken ct = cts.Token;

        var dbA = new PortaDatabase(Path.Combine(_baseDir, "A.db"));
        dbA.Migrate();
        var devicesA = new DeviceRepository(dbA);
        var indexA = new FileIndexRepository(dbA);
        devicesA.Add(new TrustedDevice(idB.Id, idB.ExportPublicKey(), "B", DateTimeOffset.UnixEpoch, null));

        var dbB = new PortaDatabase(Path.Combine(_baseDir, "B.db"));
        dbB.Migrate();
        var devicesB = new DeviceRepository(dbB);
        var storagesB = new StorageRepository(dbB);
        var indexB = new FileIndexRepository(dbB);
        devicesB.Add(new TrustedDevice(idA.Id, idA.ExportPublicKey(), "A", DateTimeOffset.UnixEpoch, null));
        storagesB.Add(new Storage("s1", "Shared", _folderB, StorageExchangeMode.TwoWay, SyncMode.Automatic, false, DateTimeOffset.UnixEpoch));

        // A: объявляет себя, слушает и отдаёт хранилище.
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

        // B: авто-синк при обнаружении доверенного A.
        using var transportB = new QuicTransport(idB);
        var serviceB = new SyncService(transportB, idB, "Device B", new DeviceRepositoryTrustPolicy(devicesB), indexB);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new PullAllController(serviceB, storagesB, done);

        using var discoveryB = new MdnsDeviceDiscovery(idB.Id);
        using var autoSync = new AutoSyncCoordinator(discoveryB, devicesB, controller);
        autoSync.Start();
        discoveryB.StartBrowsing();

        Task finished = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(22), ct));
        Assert.True(finished == done.Task, "Авто-синк не запустился/не завершился за отведённое время.");

        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(_folderB, "auto.bin"), ct));

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
