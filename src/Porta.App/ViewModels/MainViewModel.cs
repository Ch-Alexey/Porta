using CommunityToolkit.Mvvm.ComponentModel;
using Porta.Core.App;

namespace Porta.App.ViewModels;

/// <summary>Оболочка приложения: личность устройства + вкладки.</summary>
public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(AppEnvironment environment)
    {
        DeviceName = environment.DeviceName;
        DeviceId = environment.Identity.Id.ToDisplayString();
        Storages = new StoragesViewModel(environment);
        Devices = new DevicesViewModel(environment);
    }

    [ObservableProperty]
    public partial string DeviceName { get; set; }

    [ObservableProperty]
    public partial string DeviceId { get; set; }

    public StoragesViewModel Storages { get; }

    public DevicesViewModel Devices { get; }
}
