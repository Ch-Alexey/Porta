using System;
using System.IO;
using System.Linq;
using Porta.App.Design;
using Porta.App.ViewModels;
using Porta.Core.Model;
using Porta.Core.Sync;

namespace Porta.App.Tests.ViewModels;

/// <summary>Раздел «История версий». См. docs/features/32-version-history.md.</summary>
public class VersionsViewModelTests : IDisposable
{
    private readonly string _base;
    private readonly string _storageDir;
    private readonly InMemoryStorageRepository _storages = new();
    private readonly FileSystemVersionArchive _archive;

    public VersionsViewModelTests()
    {
        _base = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _storageDir = Path.Combine(_base, "storage");
        Directory.CreateDirectory(_storageDir);
        _archive = new FileSystemVersionArchive(Path.Combine(_base, "versions"));

        _storages.Add(new Storage("s1", "Документы", _storageDir,
            StorageExchangeMode.TwoWay, SyncMode.Automatic, false, DateTimeOffset.UnixEpoch));
    }

    private StorageItem StorageChoice()
        => new StoragesViewModel(_storages, new InMemoryDeviceRepository()).Items.Single();

    private string WriteAndArchive(string name, params string[] contents)
    {
        string path = Path.Combine(_storageDir, name);
        foreach (string content in contents)
        {
            File.WriteAllText(path, content);
            _archive.StoreFor("s1").Archive(path, name);
        }
        return path;
    }

    [Fact]
    public void Without_an_archive_the_section_reports_it_has_nothing()
    {
        var vm = new VersionsViewModel(_storages);

        Assert.False(vm.HasArchive);
        Assert.Empty(vm.Files);
    }

    [Fact]
    public void Selecting_a_storage_lists_files_that_have_versions()
    {
        WriteAndArchive("док.txt", "первая");
        var vm = new VersionsViewModel(_storages, _archive);

        vm.SelectedStorage = StorageChoice();

        Assert.Equal(["док.txt"], vm.Files);
    }

    [Fact]
    public void Selecting_a_file_lists_its_versions()
    {
        WriteAndArchive("док.txt", "первая", "вторая");
        var vm = new VersionsViewModel(_storages, _archive) { SelectedStorage = StorageChoice() };

        vm.SelectedFile = "док.txt";

        Assert.Equal(2, vm.Versions.Count);
    }

    [Fact]
    public void Restoring_brings_the_content_back_and_reports_it()
    {
        string path = WriteAndArchive("док.txt", "нужная");
        File.WriteAllText(path, "испорченная");
        var vm = new VersionsViewModel(_storages, _archive) { SelectedStorage = StorageChoice() };
        vm.SelectedFile = "док.txt";

        vm.Versions[0].RestoreCommand.Execute(null);

        Assert.Equal("нужная", File.ReadAllText(path));
        Assert.Contains("восстановлен", vm.StatusMessage);
    }

    [Fact]
    public void Changing_the_storage_clears_the_previous_selection()
    {
        WriteAndArchive("док.txt", "первая");
        var vm = new VersionsViewModel(_storages, _archive) { SelectedStorage = StorageChoice() };
        vm.SelectedFile = "док.txt";
        Assert.NotEmpty(vm.Versions);

        vm.SelectedStorage = null;

        Assert.Empty(vm.Files);
        Assert.Empty(vm.Versions);
        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public void Refresh_says_so_when_there_is_nothing_archived()
    {
        var vm = new VersionsViewModel(_storages, _archive) { SelectedStorage = StorageChoice() };

        vm.RefreshCommand.Execute(null);

        Assert.Equal("Сохранённых версий пока нет", vm.StatusMessage);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_base, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки — не повод валить тест.
        }
    }
}
