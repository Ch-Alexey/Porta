using System;
using Porta.App.ViewModels;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.Design;

/// <summary>Данные приложения с образцами — для design-time превью в IDE.</summary>
public sealed class DesignAppData : IAppData
{
    public DesignAppData()
    {
        var storages = new InMemoryStorageRepository();
        storages.Add(new Storage("s1", "Документы", "/Users/me/Documents",
            StorageExchangeMode.TwoWay, SyncMode.Automatic, false, DateTimeOffset.UnixEpoch));
        storages.Add(new Storage("s2", "Фотоархив", "/Users/me/Photos",
            StorageExchangeMode.TwoWay, SyncMode.Automatic, false, DateTimeOffset.UnixEpoch));
        Storages = storages;

        var devices = new InMemoryDeviceRepository();
        using var sample = DeviceIdentity.Generate();
        devices.Add(new TrustedDevice(sample.Id, sample.ExportPublicKey(), "Ноутбук",
            DateTimeOffset.UnixEpoch, null));
        Devices = devices;
    }

    public string DeviceName => "Это устройство";
    public string DeviceId => "PREVIEW-DEVICE-ID";
    public IStorageRepository Storages { get; }
    public IDeviceRepository Devices { get; }

    public string CreateInvitation() => "porta:preview-invitation-token";

    public TrustedDevice AcceptInvitation(string token, string deviceName)
        => throw new NotSupportedException("design-time");
}

/// <summary>MainViewModel с образцовыми данными для design-time (параметрless-конструктор).</summary>
public sealed class DesignMainViewModel : MainViewModel
{
    public DesignMainViewModel() : base(new DesignAppData())
    {
    }
}
