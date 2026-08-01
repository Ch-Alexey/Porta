using CommunityToolkit.Mvvm.ComponentModel;
using Porta.App.Services;
using Porta.Core.App;
using Porta.Core.Discovery;

namespace Porta.App.ViewModels;

/// <summary>Оболочка приложения: личность устройства + вкладки.</summary>
public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(IAppData data, IDeviceDiscovery? discovery = null, IUiDispatcher? dispatcher = null)
    {
        DeviceName = data.DeviceName;
        DeviceId = data.DeviceId;
        Storages = new StoragesViewModel(data.Storages);
        Devices = new DevicesViewModel(data, discovery, dispatcher);
    }

    [ObservableProperty]
    public partial string DeviceName { get; set; }

    [ObservableProperty]
    public partial string DeviceId { get; set; }

    public StoragesViewModel Storages { get; }

    public DevicesViewModel Devices { get; }
}
