using System.Net;
using Porta.App.Services;
using Porta.App.ViewModels;
using Porta.Core.Data;
using Porta.Core.Drop;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;
using Porta.Core.Transport;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Сквозная проверка разовой передачи «как у пользователя»: отправитель шлёт по QUIC,
/// приёмник показывает предложение во вкладке «Передача», человек жмёт «Принять» —
/// файл оказывается в папке загрузок. См. docs/features/28-drop-ui.md.
/// </summary>
[Trait("Category", "Integration")]
public class DropAcceptanceE2ETests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _source;
    private readonly string _downloads;

    public DropAcceptanceE2ETests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_baseDir, "source");
        _downloads = Path.Combine(_baseDir, "downloads");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_downloads);
    }

    /// <summary>Диспетчер без UI-потока — выполняет действие сразу.</summary>
    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private (DeviceRepository Devices, FileIndexRepository Index, PortaDatabase Db) Repos(string name)
    {
        var db = new PortaDatabase(Path.Combine(_baseDir, name + ".db"));
        db.Migrate();
        return (new DeviceRepository(db), new FileIndexRepository(db), db);
    }

    private static void Trust(DeviceRepository repo, DeviceIdentity other, string name)
        => repo.Add(new TrustedDevice(other.Id, other.ExportPublicKey(), name, DateTimeOffset.UnixEpoch, null));

    [Fact]
    public async Task User_accepts_an_incoming_transfer_and_the_file_lands_in_downloads()
    {
        if (!QuicTransport.IsSupported)
            return;

        byte[] payload = new byte[200_000];
        new Random(28).NextBytes(payload);
        string file = Path.Combine(_source, "отпуск.jpg");
        await File.WriteAllBytesAsync(file, payload);

        using var idReceiver = DeviceIdentity.Generate();
        using var idSender = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

        (DeviceRepository devicesR, FileIndexRepository indexR, PortaDatabase dbR) = Repos("receiver");
        (DeviceRepository devicesS, FileIndexRepository indexS, _) = Repos("sender");
        Trust(devicesR, idSender, "Отправитель");
        Trust(devicesS, idReceiver, "Приёмник");

        // Приёмная сторона собрана как в приложении: настройки → мост → view-модель.
        var settings = new SettingsRepository(dbR);
        settings.Set(SettingKeys.DownloadsFolder, _downloads);
        var acceptance = new UiDropAcceptance(
            () => settings.Get(SettingKeys.DownloadsFolder, "/нет"), TimeSpan.FromSeconds(20));
        var vm = new TransfersViewModel(settings, acceptance: acceptance, dispatcher: new ImmediateDispatcher());

        using var transportR = new QuicTransport(idReceiver);
        using var transportS = new QuicTransport(idSender);
        var receiver = new SyncService(transportR, idReceiver, "Приёмник",
            new DeviceRepositoryTrustPolicy(devicesR), indexR);
        var sender = new SyncService(transportS, idSender, "Отправитель",
            new DeviceRepositoryTrustPolicy(devicesS), indexS);

        await using ITransportListener listener =
            await transportR.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);

        Task serve = receiver.ServeOnceAsync(listener, _ => null, drops: acceptance, cancellationToken: ct);
        Task<DropSendResult> send = sender.SendFilesAsync(
            listener.LocalEndPoint, idReceiver.Id, [DropSourceFile.FromPath(file)], ct);

        // Ждём, пока предложение доедет до вкладки, и «нажимаем» Принять.
        IncomingOfferItem offer = await WaitForOfferAsync(vm, ct);
        // Разделитель дробной части зависит от локали машины — проверяем смысл, не запятую.
        Assert.StartsWith("1 файл(ов), 195", offer.Summary);
        Assert.EndsWith("КБ", offer.Summary);
        offer.AcceptCommand.Execute(null);

        await Task.WhenAll(serve, send);

        Assert.True((await send).Accepted);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(_downloads, "отпуск.jpg"), ct));
        Assert.Empty(vm.IncomingOffers);
    }

    [Fact]
    public async Task User_rejects_an_incoming_transfer_and_nothing_is_written()
    {
        if (!QuicTransport.IsSupported)
            return;

        string file = Path.Combine(_source, "секрет.txt");
        await File.WriteAllTextAsync(file, "не должно уйти");

        using var idReceiver = DeviceIdentity.Generate();
        using var idSender = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

        (DeviceRepository devicesR, FileIndexRepository indexR, PortaDatabase dbR) = Repos("receiver2");
        (DeviceRepository devicesS, FileIndexRepository indexS, _) = Repos("sender2");
        Trust(devicesR, idSender, "Отправитель");
        Trust(devicesS, idReceiver, "Приёмник");

        var settings = new SettingsRepository(dbR);
        settings.Set(SettingKeys.DownloadsFolder, _downloads);
        var acceptance = new UiDropAcceptance(
            () => settings.Get(SettingKeys.DownloadsFolder, "/нет"), TimeSpan.FromSeconds(20));
        var vm = new TransfersViewModel(settings, acceptance: acceptance, dispatcher: new ImmediateDispatcher());

        using var transportR = new QuicTransport(idReceiver);
        using var transportS = new QuicTransport(idSender);
        var receiver = new SyncService(transportR, idReceiver, "Приёмник",
            new DeviceRepositoryTrustPolicy(devicesR), indexR);
        var sender = new SyncService(transportS, idSender, "Отправитель",
            new DeviceRepositoryTrustPolicy(devicesS), indexS);

        await using ITransportListener listener =
            await transportR.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);

        Task serve = receiver.ServeOnceAsync(listener, _ => null, drops: acceptance, cancellationToken: ct);
        Task<DropSendResult> send = sender.SendFilesAsync(
            listener.LocalEndPoint, idReceiver.Id, [DropSourceFile.FromPath(file)], ct);

        IncomingOfferItem offer = await WaitForOfferAsync(vm, ct);
        offer.RejectCommand.Execute(null);

        await Task.WhenAll(serve, send);

        Assert.False((await send).Accepted);
        Assert.Empty(Directory.GetFiles(_downloads));
    }

    private static async Task<IncomingOfferItem> WaitForOfferAsync(TransfersViewModel vm, CancellationToken ct)
    {
        while (vm.IncomingOffers.Count == 0)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(25, ct);
        }
        return vm.IncomingOffers[0];
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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
