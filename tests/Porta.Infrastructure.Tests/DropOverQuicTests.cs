using System.Net;
using Porta.Core.Data;
using Porta.Core.Drop;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Protocol;
using Porta.Core.Sync;
using Porta.Core.Transport;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Разовая передача поверх реального QUIC через `SyncService`: проверяет и маршрутизацию
/// по намерению сессии, и приём с подтверждением. Требует libmsquic.
/// См. docs/features/26-drop-transfer.md.
/// </summary>
[Trait("Category", "Integration")]
public class DropOverQuicTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _source;
    private readonly string _downloads;

    public DropOverQuicTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_baseDir, "source");
        _downloads = Path.Combine(_baseDir, "downloads");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_downloads);
    }

    private (DeviceRepository Devices, FileIndexRepository Index) Repos(string name)
    {
        var db = new PortaDatabase(Path.Combine(_baseDir, name + ".db"));
        db.Migrate();
        return (new DeviceRepository(db), new FileIndexRepository(db));
    }

    private static void Trust(DeviceRepository repo, DeviceIdentity other, string name)
        => repo.Add(new TrustedDevice(other.Id, other.ExportPublicKey(), name, DateTimeOffset.UnixEpoch, null));

    /// <summary>Принимает всё в заданную папку; запоминает, кто прислал.</summary>
    private sealed class AcceptInto(string folder) : IDropAcceptance
    {
        public DeviceId? Sender { get; private set; }

        public Task<DropDecision> DecideAsync(DropOfferMessage offer, DeviceId sender, CancellationToken cancellationToken = default)
        {
            Sender = sender;
            return Task.FromResult(DropDecision.Accept(folder));
        }
    }

    [Fact]
    public async Task Files_are_dropped_to_trusted_peer_over_quic()
    {
        if (!QuicTransport.IsSupported)
            return;

        byte[] payload = new byte[40_000];
        new Random(17).NextBytes(payload);
        string file = Path.Combine(_source, "video.mp4");
        await File.WriteAllBytesAsync(file, payload);

        using var idA = DeviceIdentity.Generate();
        using var idB = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;

        (DeviceRepository devicesA, FileIndexRepository indexA) = Repos("A");
        (DeviceRepository devicesB, FileIndexRepository indexB) = Repos("B");
        Trust(devicesA, idB, "B");
        Trust(devicesB, idA, "A");

        using var transportA = new QuicTransport(idA);
        using var transportB = new QuicTransport(idB);
        var receiver = new SyncService(transportA, idA, "A", new DeviceRepositoryTrustPolicy(devicesA), indexA);
        var sender = new SyncService(transportB, idB, "B", new DeviceRepositoryTrustPolicy(devicesB), indexB);
        var acceptance = new AcceptInto(_downloads);

        await using ITransportListener listener = await transportA.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        Task serve = receiver.ServeOnceAsync(listener, _ => null, drops: acceptance, cancellationToken: ct);
        Task<DropSendResult> send = sender.SendFilesAsync(
            listener.LocalEndPoint, idA.Id, [DropSourceFile.FromPath(file)], cancellationToken: ct);

        await Task.WhenAll(serve, send);

        Assert.True((await send).Accepted);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(_downloads, "video.mp4")));
        Assert.Equal(idB.Id, acceptance.Sender);
    }

    [Fact]
    public async Task Drop_is_refused_when_receiving_is_not_configured()
    {
        if (!QuicTransport.IsSupported)
            return;

        string file = Path.Combine(_source, "note.txt");
        await File.WriteAllTextAsync(file, "привет");

        using var idA = DeviceIdentity.Generate();
        using var idB = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;

        (DeviceRepository devicesA, FileIndexRepository indexA) = Repos("A");
        (DeviceRepository devicesB, FileIndexRepository indexB) = Repos("B");
        Trust(devicesA, idB, "B");
        Trust(devicesB, idA, "A");

        using var transportA = new QuicTransport(idA);
        using var transportB = new QuicTransport(idB);
        var receiver = new SyncService(transportA, idA, "A", new DeviceRepositoryTrustPolicy(devicesA), indexA);
        var sender = new SyncService(transportB, idB, "B", new DeviceRepositoryTrustPolicy(devicesB), indexB);

        await using ITransportListener listener = await transportA.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        Task serve = receiver.ServeOnceAsync(listener, _ => null, cancellationToken: ct);
        Task<DropSendResult> send = sender.SendFilesAsync(
            listener.LocalEndPoint, idA.Id, [DropSourceFile.FromPath(file)], cancellationToken: ct);

        await Task.WhenAll(serve, send);

        DropSendResult result = await send;
        Assert.False(result.Accepted);
        Assert.Equal("Приём разовых передач не настроен", result.RejectReason);
        Assert.Empty(Directory.GetFiles(_downloads));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки — не повод валить тест.
        }
    }
}
