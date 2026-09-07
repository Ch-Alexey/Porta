using CommunityToolkit.Mvvm.ComponentModel;
using Porta.App.Services;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Drop;
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
        MediaScanner? mediaScanner = null,
        ISettingsRepository? settings = null,
        IDropController? drops = null,
        IFilePicker? filePicker = null,
        UiDropAcceptance? dropAcceptance = null,
        IFolderPicker? folderPicker = null,
        IWatchedFolders? watchedFolders = null,
        IVersionArchive? versions = null,
        IQrCodeRenderer? qrRenderer = null,
        PeerOperations? operations = null)
    {
        DeviceName = data.DeviceName;
        DeviceId = data.DeviceId;
        Storages = new StoragesViewModel(data.Storages, data.Devices, folderPicker, watchedFolders);
        Versions = new VersionsViewModel(data.Storages, versions);
        Devices = new DevicesViewModel(
            data, discovery, dispatcher, sync, qrRenderer ?? new QrCoderRenderer(),
            operations: operations);
        Transfers = new TransfersViewModel(
            settings ?? new InMemorySettingsRepository(),
            drops, filePicker, discovery, dropAcceptance, dispatcher, folderPicker);
        // «Отправить найденное» перекладывает пути во вкладку «Передача».
        Media = new MediaViewModel(mediaScanner, dispatcher, onSend: Transfers.AddFiles);
    }

    [ObservableProperty]
    public partial string DeviceName { get; set; }

    [ObservableProperty]
    public partial string DeviceId { get; set; }

    public StoragesViewModel Storages { get; }

    public DevicesViewModel Devices { get; }

    public MediaViewModel Media { get; }

    public TransfersViewModel Transfers { get; }

    public VersionsViewModel Versions { get; }
}
