using System;
using Avalonia;
using Porta.App;
using Porta.Core.App;
using Porta.Core.Discovery;
using Porta.Infrastructure.Discovery;

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
        // Композиция: ядро + mDNS-обнаружение из инфраструктуры.
        AppEnvironment environment = AppEnvironment.Create();
        App.InjectedData = environment;
        App.InjectedDiscovery = TryStartDiscovery(environment);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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
