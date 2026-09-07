using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Porta.App.ViewModels;
using Porta.App.Views;
using Porta.App.Services;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Drop;
using Porta.Core.Sync;

namespace Porta.App;

public partial class App : Application
{
    /// <summary>Данные приложения, внедряемые головой (иначе создаются по умолчанию).</summary>
    public static IAppData? InjectedData { get; set; }

    /// <summary>Обнаружение устройств, внедряемое головой (напр. mDNS из инфраструктуры).</summary>
    public static IDeviceDiscovery? InjectedDiscovery { get; set; }

    /// <summary>Контроллер синхронизации, внедряемый головой (QUIC-транспорт).</summary>
    public static ISyncController? InjectedSync { get; set; }

    /// <summary>Настройки приложения, внедряемые головой.</summary>
    public static ISettingsRepository? InjectedSettings { get; set; }

    /// <summary>Разовая отправка файлов, внедряемая головой (QUIC-транспорт).</summary>
    public static IDropController? InjectedDrops { get; set; }

    /// <summary>Мост подтверждения входящих передач между ядром и UI.</summary>
    public static UiDropAcceptance? InjectedDropAcceptance { get; set; }

    /// <summary>Выбор файлов (системный диалог); голова может подменить.</summary>
    public static IFilePicker? InjectedFilePicker { get; set; }

    /// <summary>Выбор папки (системный диалог); голова может подменить.</summary>
    public static IFolderPicker? InjectedFolderPicker { get; set; }

    /// <summary>Наблюдатель за папками хранилищ — чтобы UI мог его переконфигурировать.</summary>
    public static IWatchedFolders? InjectedWatchedFolders { get; set; }

    /// <summary>Архив прежних версий файлов — для раздела «История версий».</summary>
    public static IVersionArchive? InjectedVersions { get; set; }

    /// <summary>Учёт идущих операций — чтобы отзыв доверия их обрывал.</summary>
    public static PeerOperations? InjectedOperations { get; set; }

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
        MainViewModel CreateViewModel() => new(
            data,
            InjectedDiscovery,
            dispatcher: null,
            InjectedSync,
            mediaScanner: null,
            InjectedSettings,
            InjectedDrops,
            InjectedFilePicker ?? new AvaloniaFilePicker(),
            InjectedDropAcceptance,
            InjectedFolderPicker ?? new AvaloniaFilePicker(),
            InjectedWatchedFolders,
            InjectedVersions,
            operations: InjectedOperations);

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