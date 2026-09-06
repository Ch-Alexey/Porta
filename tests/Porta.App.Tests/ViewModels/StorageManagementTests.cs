using System;
using System.Linq;
using System.Threading.Tasks;
using Porta.App.Design;
using Porta.App.ViewModels;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.Tests.ViewModels;

/// <summary>
/// Управление уже добавленным: удаление, режимы, выбор папки, переинициализация
/// наблюдателя. См. docs/features/29-managing-what-exists.md.
/// </summary>
public class StorageManagementTests
{
    private static Storage Make(string id, SyncMode mode = SyncMode.Automatic, bool paused = false)
        => new(id, id, "/tmp/" + id, StorageExchangeMode.TwoWay, mode, paused, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Removing_a_storage_takes_it_out_of_the_list_and_the_repository()
    {
        var storages = new InMemoryStorageRepository();
        storages.Add(Make("s1"));
        var vm = new StoragesViewModel(storages, new InMemoryDeviceRepository());

        vm.Items.Single().RemoveCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.Empty(storages.List());
    }

    [Fact]
    public void Removing_a_storage_says_that_files_stay_on_disk()
    {
        var storages = new InMemoryStorageRepository();
        storages.Add(Make("Документы"));
        var vm = new StoragesViewModel(storages, new InMemoryDeviceRepository());

        vm.Items.Single().RemoveCommand.Execute(null);

        Assert.Contains("Файлы на диске остались", vm.StatusMessage);
    }

    [Fact]
    public void Sync_mode_toggles_and_is_persisted()
    {
        var storages = new InMemoryStorageRepository();
        storages.Add(Make("s1"));
        var vm = new StoragesViewModel(storages, new InMemoryDeviceRepository());
        Assert.Equal("Авто", vm.Items.Single().SyncModeText);

        vm.Items.Single().ToggleSyncModeCommand.Execute(null);

        Assert.Equal("Вручную", vm.Items.Single().SyncModeText);
        Assert.Equal(SyncMode.Manual, storages.Get("s1")!.SyncMode);

        vm.Items.Single().ToggleSyncModeCommand.Execute(null);
        Assert.Equal(SyncMode.Automatic, storages.Get("s1")!.SyncMode);
    }

    [Fact]
    public void Pause_toggles_and_is_persisted()
    {
        var storages = new InMemoryStorageRepository();
        storages.Add(Make("s1"));
        var vm = new StoragesViewModel(storages, new InMemoryDeviceRepository());

        vm.Items.Single().TogglePausedCommand.Execute(null);

        Assert.Equal("На паузе", vm.Items.Single().PausedText);
        Assert.True(storages.Get("s1")!.Paused);
    }

    [Fact]
    public async Task Browsing_fills_the_path_and_suggests_a_name()
    {
        var vm = new StoragesViewModel(
            new InMemoryStorageRepository(), new InMemoryDeviceRepository(),
            new FakeFolderPicker("/Users/me/Фотографии"));

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("/Users/me/Фотографии", vm.NewPath);
        Assert.Equal("Фотографии", vm.NewName);
    }

    [Fact]
    public async Task Browsing_does_not_overwrite_a_name_the_user_typed()
    {
        var vm = new StoragesViewModel(
            new InMemoryStorageRepository(), new InMemoryDeviceRepository(),
            new FakeFolderPicker("/Users/me/Фотографии"));
        vm.NewName = "Моё";

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("Моё", vm.NewName);
    }

    [Fact]
    public async Task Cancelled_browse_changes_nothing()
    {
        var vm = new StoragesViewModel(
            new InMemoryStorageRepository(), new InMemoryDeviceRepository(),
            new FakeFolderPicker(null));

        await vm.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.NewPath);
    }

    [Fact]
    public void Adding_a_storage_reconfigures_the_watcher()
    {
        var storages = new InMemoryStorageRepository();
        var watched = new RecordingWatchedFolders();
        var vm = new StoragesViewModel(storages, new InMemoryDeviceRepository(), watched: watched);
        int atStart = watched.Calls.Count;

        vm.NewName = "Фото";
        vm.NewPath = "/tmp/фото";
        vm.AddCommand.Execute(null);

        Assert.True(watched.Calls.Count > atStart, "наблюдатель должен переконфигурироваться");
        Assert.Equal(["/tmp/фото"], watched.Calls[^1]);
    }

    [Fact]
    public void Removing_a_storage_reconfigures_the_watcher()
    {
        var storages = new InMemoryStorageRepository();
        storages.Add(Make("s1"));
        var watched = new RecordingWatchedFolders();
        var vm = new StoragesViewModel(storages, new InMemoryDeviceRepository(), watched: watched);

        vm.Items.Single().RemoveCommand.Execute(null);

        Assert.Empty(watched.Calls[^1]);
    }

    [Fact]
    public void Revoking_trust_removes_the_device()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        var data = new FakeAppData();
        data.Devices.Add(new TrustedDevice(
            identity.Id, identity.ExportPublicKey(), "Ноутбук", DateTimeOffset.UnixEpoch, null));
        var vm = new DevicesViewModel(data, dispatcher: new ImmediateDispatcher());

        // Строка показывает читаемую форму ID (с дефисами) — отзыв должен её понимать.
        DeviceItem item = Assert.Single(vm.Items);
        Assert.Contains('-', item.DeviceId);
        item.RevokeCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.Empty(data.Devices.List());
        Assert.Contains("отозвано", vm.StatusMessage);
    }
}
