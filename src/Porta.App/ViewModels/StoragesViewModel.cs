using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.ViewModels;

/// <summary>
/// Вкладка «Хранилища»: список папок, добавление и выборочный доступ (поделиться папкой
/// с доверенным устройством). См. docs/features/24-storage-sharing.md.
/// </summary>
public partial class StoragesViewModel : ViewModelBase
{
    private readonly IStorageRepository _storages;
    private readonly IDeviceRepository _devices;

    public StoragesViewModel(IStorageRepository storages, IDeviceRepository devices)
    {
        _storages = storages;
        _devices = devices;
        Reload();
    }

    public ObservableCollection<StorageItem> Items { get; } = [];

    /// <summary>Доверенные устройства — для выбора, с кем поделиться.</summary>
    public ObservableCollection<DeviceChoice> TrustedDevices { get; } = [];

    [ObservableProperty]
    public partial string NewName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial StorageItem? SelectedStorage { get; set; }

    [ObservableProperty]
    public partial DeviceChoice? SelectedDevice { get; set; }

    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewPath))
            return;

        var storage = new Storage(
            Guid.NewGuid().ToString("N"),
            NewName.Trim(),
            NewPath.Trim(),
            StorageExchangeMode.TwoWay,
            SyncMode.Automatic,
            Paused: false,
            DateTimeOffset.UtcNow);

        _storages.Add(storage);
        NewName = string.Empty;
        NewPath = string.Empty;
        Reload();
    }

    [RelayCommand]
    private void Share()
    {
        if (SelectedStorage is null || SelectedDevice is null)
            return;
        _storages.LinkDevice(SelectedStorage.Id, DeviceId.Parse(SelectedDevice.DeviceId), autoSync: true);
        Reload();
    }

    [RelayCommand]
    private void Unshare()
    {
        if (SelectedStorage is null || SelectedDevice is null)
            return;
        _storages.UnlinkDevice(SelectedStorage.Id, DeviceId.Parse(SelectedDevice.DeviceId));
        Reload();
    }

    private void Reload()
    {
        var deviceNames = _devices.List().ToDictionary(d => d.Id.ToString(), d => d.Name, StringComparer.Ordinal);

        Items.Clear();
        foreach (Storage storage in _storages.List())
        {
            IEnumerable<string> shared = _storages.ListDevices(storage.Id)
                .Select(link => deviceNames.GetValueOrDefault(link.DeviceId.ToString(), link.DeviceId.ToString()));
            Items.Add(new StorageItem(storage.Id, storage.Name, storage.LocalPath, string.Join(", ", shared)));
        }

        TrustedDevices.Clear();
        foreach (TrustedDevice device in _devices.List())
            TrustedDevices.Add(new DeviceChoice(device.Name, device.Id.ToString()));
    }
}

/// <summary>Строка списка хранилищ.</summary>
public sealed record StorageItem(string Id, string Name, string LocalPath, string SharedWith)
{
    public override string ToString() => Name;
}

/// <summary>Доверенное устройство для выбора в списке.</summary>
public sealed record DeviceChoice(string Name, string DeviceId)
{
    public override string ToString() => Name;
}
