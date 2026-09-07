using System;
using System.IO;
using System.Linq;
using Avalonia;
using Porta.App;
using Porta.App.Services;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Sync;
using Porta.App.ViewModels;
using Porta.Infrastructure.Discovery;
using Porta.Infrastructure.Sync;

namespace Porta.App.Desktop;

sealed class Program
{
    /// <summary>Порт, который устройство объявляет для будущего QUIC-транспорта синка.</summary>
    private const int SyncPort = 47100;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Композиция: ядро + mDNS-обнаружение + приём/запуск синка (QUIC) из инфраструктуры.
        AppEnvironment environment = AppEnvironment.Create();
        App.InjectedData = environment;
        App.InjectedDiscovery = TryStartDiscovery(environment);

        // Разовые передачи: приём спрашивает человека через UI, отправка идёт по QUIC.
        var settings = new SettingsRepository(environment.Database);

        // Уборка времянок, брошенных при жёстком обрыве прошлого запуска.
        // См. docs/features/35-cleanup.md.
        CleanStaleTemporaries(settings);
        var acceptance = new UiDropAcceptance(
            () => settings.Get(SettingKeys.DownloadsFolder, TransfersViewModel.DefaultDownloadsFolder()));
        App.InjectedSettings = settings;
        App.InjectedDropAcceptance = acceptance;
        // операции регистрируются ниже, вместе с контроллером синка

        var syncListener = new BackgroundSyncListener(environment, SyncPort, acceptance, acceptance.Progress);
        syncListener.Start();
        // Архив прежних версий — в папке данных приложения, вне папок хранилищ,
        // иначе копии попали бы в индекс и уехали на другое устройство.
        var versions = new FileSystemVersionArchive(Path.Combine(environment.DataDirectory, "versions"));
        App.InjectedVersions = versions;
        var operations = new PeerOperations();
        App.InjectedOperations = operations;
        var syncController = new QuicSyncController(environment, versions, operations);
        App.InjectedSync = syncController;
        App.InjectedDrops = new QuicDropController(environment, operations);

        // Авто-синхро: при появлении доверенного устройства И при локальных изменениях файлов.
        AutoSyncCoordinator? autoSync = null;
        FileSystemChangeNotifier? changes = null;
        if (App.InjectedDiscovery is not null)
        {
            changes = new FileSystemChangeNotifier(environment.Storages.List().Select(s => s.LocalPath));
            changes.Start();
            // Чтобы новое хранилище подхватывалось сразу, а не после перезапуска.
            App.InjectedWatchedFolders = changes;
            autoSync = new AutoSyncCoordinator(App.InjectedDiscovery, environment.Devices, syncController, changes);
            autoSync.Start();
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            autoSync?.Dispose();
            operations.Dispose();
            changes?.Dispose();
            syncListener.DisposeAsync().AsTask().GetAwaiter().GetResult();
            (App.InjectedDiscovery as IDisposable)?.Dispose();
        }
    }

    /// <summary>Убрать времянки, оставшиеся от убитого процесса. Молча, но не вслепую.</summary>
    private static void CleanStaleTemporaries(SettingsRepository settings)
    {
        string downloads = settings.Get(SettingKeys.DownloadsFolder, TransfersViewModel.DefaultDownloadsFolder());
        int removed = StaleFileCleanup.CleanDropTemporaries(downloads) + StaleFileCleanup.CleanBlockSpools();
        if (removed > 0)
            Console.WriteLine($"Убрано брошенных времянок: {removed}");
    }

    private static IDeviceDiscovery? TryStartDiscovery(AppEnvironment environment)
    {
        try
        {
            var discovery = new MdnsDeviceDiscovery(environment.Identity.Id);
            discovery.Advertise(new PortaAdvertisement(environment.Identity.Id, environment.DeviceName, SyncPort));
            discovery.StartBrowsing();
            return discovery;
        }
        catch (Exception)
        {
            // Без mDNS (нет multicast в сети) приложение всё равно должно запуститься.
            return null;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
