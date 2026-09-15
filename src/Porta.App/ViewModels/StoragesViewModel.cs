using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Porta.App.Services;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;

namespace Porta.App.ViewModels;

/// <summary>
/// Вкладка «Хранилища»: список папок, добавление и удаление, режим синхронизации и
/// выборочный доступ (поделиться папкой с доверенным устройством).
/// См. docs/features/24-storage-sharing.md и 29-managing-what-exists.md.
/// </summary>
public partial class StoragesViewModel : ViewModelBase
{
    private readonly IStorageRepository _storages;
    private readonly IDeviceRepository _devices;
    private readonly IFolderPicker? _folders;
    private readonly IWatchedFolders? _watched;

    public StoragesViewModel(
        IStorageRepository storages,
        IDeviceRepository devices,
        IFolderPicker? folders = null,
        IWatchedFolders? watched = null)
    {
        _storages = storages;
        _devices = devices;
        _folders = folders;
        _watched = watched;
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

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

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
    private async Task BrowseAsync()
    {
        if (_folders is null)
            return;

        string? picked = await _folders.PickFolderAsync("Выберите папку хранилища");
        if (string.IsNullOrEmpty(picked))
            return;

        NewPath = picked;
        if (string.IsNullOrWhiteSpace(NewName))
            NewName = System.IO.Path.GetFileName(picked.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Убрать папку из синхронизации. Файлы на диске остаются — Porta не удаляет
    /// пользовательские данные (см. docs/features/00-functionality.md §6).
    /// </summary>
    private void Remove(StorageItem item)
    {
        _storages.Remove(item.Id);
        if (SelectedStorage?.Id == item.Id)
            SelectedStorage = null;
        StatusMessage = $"Хранилище «{item.Name}» убрано из синхронизации. Файлы на диске остались.";
        Reload();
    }

    /// <summary>Переключить хранилище между авто- и ручной синхронизацией.</summary>
    private void ToggleSyncMode(StorageItem item)
    {
        if (_storages.Get(item.Id) is not { } storage)
            return;

        SyncMode next = storage.SyncMode == SyncMode.Automatic ? SyncMode.Manual : SyncMode.Automatic;
        _storages.Update(storage with { SyncMode = next });
        Reload();
    }

    /// <summary>Поставить хранилище на паузу или снять с паузы.</summary>
    private void TogglePaused(StorageItem item)
    {
        if (_storages.Get(item.Id) is not { } storage)
            return;

        _storages.Update(storage with { Paused = !storage.Paused });
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
            Items.Add(new StorageItem(
                storage.Id,
                storage.Name,
                storage.LocalPath,
                string.Join(", ", shared),
                storage.SyncMode == SyncMode.Automatic ? "Авто" : "Вручную",
                storage.Paused ? "На паузе" : string.Empty,
                storage.Paused ? "Продолжить" : "Пауза",
                Remove,
                ToggleSyncMode,
                TogglePaused));
        }

        TrustedDevices.Clear();
        foreach (TrustedDevice device in _devices.List())
            TrustedDevices.Add(new DeviceChoice(device.Name, device.Id.ToString()));

        // Список папок изменился — наблюдатель за файлами должен следить за новым набором,
        // иначе новое хранилище подхватится только после перезапуска.
        _watched?.Reconfigure(Items.Select(i => i.LocalPath).ToList());
    }
}

/// <summary>
/// Строка списка хранилищ. Команды живут на самой строке — шаблону не нужно искать
/// view-модель через предка.
/// </summary>
public sealed class StorageItem
{
    internal StorageItem(
        string id,
        string name,
        string localPath,
        string sharedWith,
        string syncModeText,
        string pausedText,
        string pauseButtonText,
        Action<StorageItem> remove,
        Action<StorageItem> toggleSyncMode,
        Action<StorageItem> togglePaused)
    {
        Id = id;
        Name = name;
        LocalPath = localPath;
        SharedWith = sharedWith;
        SyncModeText = syncModeText;
        PausedText = pausedText;
        PauseButtonText = pauseButtonText;
        RemoveCommand = new RelayCommand(() => remove(this));
        ToggleSyncModeCommand = new RelayCommand(() => toggleSyncMode(this));
        TogglePausedCommand = new RelayCommand(() => togglePaused(this));
    }

    public string Id { get; }
    public string Name { get; }
    public string LocalPath { get; }
    public string SharedWith { get; }

    /// <summary>«Авто» или «Вручную».</summary>
    public string SyncModeText { get; }

    /// <summary>«На паузе» или пусто.</summary>
    public string PausedText { get; }

    /// <summary>Что написано на кнопке паузы — «Пауза» или «Продолжить».</summary>
    public string PauseButtonText { get; }

    public ICommand RemoveCommand { get; }
    public ICommand ToggleSyncModeCommand { get; }
    public ICommand TogglePausedCommand { get; }

    public override string ToString() => Name;
}

/// <summary>Доверенное устройство для выбора в списке.</summary>
public sealed record DeviceChoice(string Name, string DeviceId)
{
    public override string ToString() => Name;
}
