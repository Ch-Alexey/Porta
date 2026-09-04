using CommunityToolkit.Mvvm.ComponentModel;
using Porta.App.Services;
using Porta.Core.App;
using Porta.Core.Discovery;
using Porta.Core.Media;
using Porta.Core.Sync;

namespace Porta.App.ViewModels;

/// <summary>Оболочка приложения: личность устройства + вкладки.</summary>
public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(
        IAppData data,
        IDeviceDiscovery? discovery = null,
        IUiDispatcher? dispatcher = null,
        ISyncController? sync = null,
        MediaScanner? mediaScanner = null)
    {
        DeviceName = data.DeviceName;
        DeviceId = data.DeviceId;
        Storages = new StoragesViewModel(data.Storages, data.Devices);
        Devices = new DevicesViewModel(data, discovery, dispatcher, sync);
        Media = new MediaViewModel(mediaScanner, dispatcher);
    }

    [ObservableProperty]
    public partial string DeviceName { get; set; }

    [ObservableProperty]
    public partial string DeviceId { get; set; }

    public StoragesViewModel Storages { get; }

    public DevicesViewModel Devices { get; }

    public MediaViewModel Media { get; }
}
