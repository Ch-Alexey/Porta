using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Porta.App.Services;
using Porta.App.ViewModels;
using Porta.App.Views;
using Porta.Core.App;
using Porta.Core.Discovery;

namespace Porta.App;

public partial class App : Application
{
    /// <summary>Данные приложения, внедряемые головой (иначе создаются по умолчанию).</summary>
    public static IAppData? InjectedData { get; set; }

    /// <summary>Обнаружение устройств, внедряемое головой (напр. mDNS из инфраструктуры).</summary>
    public static IDeviceDiscovery? InjectedDiscovery { get; set; }

    /// <summary>Контроллер синхронизации, внедряемый головой (QUIC-транспорт).</summary>
    public static ISyncController? InjectedSync { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IAppData data = InjectedData ?? AppEnvironment.Create();
        MainViewModel CreateViewModel() => new(data, InjectedDiscovery, dispatcher: null, InjectedSync);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = CreateViewModel() };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = CreateViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView { DataContext = CreateViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}