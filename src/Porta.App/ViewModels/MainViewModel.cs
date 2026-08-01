using CommunityToolkit.Mvvm.ComponentModel;
using Porta.Core.App;

namespace Porta.App.ViewModels;

/// <summary>Оболочка приложения: личность устройства + вкладки.</summary>
public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(IAppData data)
    {
        DeviceName = data.DeviceName;
        DeviceId = data.DeviceId;
        Storages = new StoragesViewModel(data.Storages);
        Devices = new DevicesViewModel(data);
    }

    [ObservableProperty]
    public partial string DeviceName { get; set; }

    [ObservableProperty]
    public partial string DeviceId { get; set; }

    public StoragesViewModel Storages { get; }

    public DevicesViewModel Devices { get; }
}
