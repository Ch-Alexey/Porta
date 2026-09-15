using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.Core.Data;
using Porta.Core.Model;
using Porta.Core.Sync;

namespace Porta.App.ViewModels;

/// <summary>
/// Раздел «История версий»: посмотреть сохранённые версии файлов хранилища и откатиться.
/// См. docs/features/32-version-history.md.
/// </summary>
public partial class VersionsViewModel : ViewModelBase
{
    private readonly IStorageRepository _storages;
    private readonly IVersionArchive? _archive;

    public VersionsViewModel(IStorageRepository storages, IVersionArchive? archive = null)
    {
        _storages = storages;
        _archive = archive;
    }

    /// <summary>Файлы выбранного хранилища, у которых есть сохранённые версии.</summary>
    public ObservableCollection<string> Files { get; } = [];

    /// <summary>Версии выбранного файла, от новых к старым.</summary>
    public ObservableCollection<VersionItem> Versions { get; } = [];

    [ObservableProperty]
    public partial StorageItem? SelectedStorage { get; set; }

    [ObservableProperty]
    public partial string? SelectedFile { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Есть ли что показывать — чтобы раздел не занимал место впустую.</summary>
    public bool HasArchive => _archive is not null;

    partial void OnSelectedStorageChanged(StorageItem? value) => ReloadFiles();

    partial void OnSelectedFileChanged(string? value) => ReloadVersions();

    [RelayCommand]
    private void Refresh()
    {
        ReloadFiles();
        StatusMessage = Files.Count == 0 ? "Сохранённых версий пока нет" : null;
    }

    private void ReloadFiles()
    {
        Files.Clear();
        Versions.Clear();
        SelectedFile = null;

        if (_archive is null || SelectedStorage is null)
            return;

        foreach (string file in _archive.ListFiles(SelectedStorage.Id))
            Files.Add(file);
    }

    private void ReloadVersions()
    {
        Versions.Clear();
        if (_archive is null || SelectedStorage is null || SelectedFile is null)
            return;

        foreach (ArchivedVersion version in _archive.ListVersions(SelectedStorage.Id, SelectedFile))
            Versions.Add(VersionItem.From(version, Restore));
    }

    private void Restore(VersionItem item)
    {
        if (_archive is null || SelectedStorage is null || SelectedFile is null)
            return;

        Storage? storage = _storages.Get(SelectedStorage.Id);
        if (storage is null)
        {
            StatusMessage = "Хранилище больше не существует";
            return;
        }

        try
        {
            _archive.Restore(SelectedStorage.Id, SelectedFile, item.Id, storage.LocalPath);
            StatusMessage = $"Файл «{SelectedFile}» восстановлен на версию от {item.ArchivedAt}";
            ReloadVersions();
        }
        catch (Exception ex)
        {
            StatusMessage = "Не удалось восстановить: " + ex.Message;
        }
    }
}

/// <summary>
/// Строка списка версий. Команда живёт на строке — шаблону не нужно искать view-модель
/// через предка.
/// </summary>
public sealed class VersionItem
{
    private VersionItem(string id, string archivedAt, string size, Action<VersionItem> restore)
    {
        Id = id;
        ArchivedAt = archivedAt;
        Size = size;
        RestoreCommand = new RelayCommand(() => restore(this));
    }

    public static VersionItem From(ArchivedVersion version, Action<VersionItem> restore) => new(
        version.Id,
        version.ArchivedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
        TransfersViewModel.FormatSize(version.Size),
        restore);

    internal string Id { get; }

    public string ArchivedAt { get; }

    public string Size { get; }

    public ICommand RestoreCommand { get; }
}
