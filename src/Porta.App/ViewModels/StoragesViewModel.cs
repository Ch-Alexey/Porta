using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Core.Data;
using Porta.Core.Model;

namespace Porta.App.ViewModels;

/// <summary>Вкладка «Хранилища»: список папок синхронизации и добавление новой.</summary>
public partial class StoragesViewModel : ViewModelBase
{
    private readonly IStorageRepository _storages;

    public StoragesViewModel(IStorageRepository storages)
    {
        _storages = storages;
        Reload();
    }

    public ObservableCollection<StorageItem> Items { get; } = [];

    [ObservableProperty]
    public partial string NewName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPath { get; set; } = string.Empty;

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

    private void Reload()
    {
        Items.Clear();
        foreach (Storage storage in _storages.List())
            Items.Add(new StorageItem(storage.Name, storage.LocalPath));
    }
}

/// <summary>Строка списка хранилищ для отображения.</summary>
public sealed record StorageItem(string Name, string LocalPath);
