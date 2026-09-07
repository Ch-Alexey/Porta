using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Porta.App.ViewModels;

namespace Porta.App.Tests.ViewModels;

public class MediaViewModelTests : IDisposable
{
    private readonly string _root;

    public MediaViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private void WriteFile(string relativePath)
    {
        string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[16]);
    }

    private MediaViewModel CreateViewModel()
        => new(scanner: null, dispatcher: new ImmediateDispatcher(), defaultRoot: _root);

    [Fact]
    public async Task Scan_finds_media_and_reports_count()
    {
        WriteFile("a.jpg");
        WriteFile("sub/b.mp4");
        WriteFile("notes.txt");

        MediaViewModel vm = CreateViewModel();
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Equal(["a.jpg", "b.mp4"], vm.Results.Select(r => r.FileName).Order());
        Assert.Equal("Найдено: 2", vm.StatusMessage);
        Assert.False(vm.IsScanning);
    }

    [Fact]
    public async Task Kind_checkboxes_filter_results()
    {
        WriteFile("a.jpg");
        WriteFile("b.mp4");

        MediaViewModel vm = CreateViewModel();
        vm.IncludePhotos = false;
        await vm.ScanCommand.ExecuteAsync(null);

        MediaItem item = Assert.Single(vm.Results);
        Assert.Equal("b.mp4", item.FileName);
        Assert.Equal("Видео", item.Kind);
    }

    [Fact]
    public async Task Name_filter_is_applied()
    {
        WriteFile("otpusk.jpg");
        WriteFile("work.jpg");

        MediaViewModel vm = CreateViewModel();
        vm.NameFilter = "OTPUSK";
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Equal("otpusk.jpg", Assert.Single(vm.Results).FileName);
    }

    [Fact]
    public async Task Results_arrive_in_batches_not_only_at_the_end()
    {
        // Больше одной пачки — иначе проверка ничего не доказывает.
        for (int i = 0; i < MediaViewModel.BatchSize * 2 + 5; i++)
            WriteFile($"sub{i}/photo{i}.jpg");

        MediaViewModel vm = CreateViewModel();
        var growth = new List<int>();
        vm.Results.CollectionChanged += (_, _) => growth.Add(vm.Results.Count);

        await vm.ScanCommand.ExecuteAsync(null);

        // Список наполнялся частями, а не одним куском в конце.
        Assert.True(growth.Count > MediaViewModel.BatchSize,
            $"список менялся {growth.Count} раз — похоже, всё пришло одним куском");
        Assert.Equal(MediaViewModel.BatchSize * 2 + 5, vm.Results.Count);
    }

    [Fact]
    public async Task Rescan_replaces_previous_results()
    {
        WriteFile("a.jpg");

        MediaViewModel vm = CreateViewModel();
        await vm.ScanCommand.ExecuteAsync(null);
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Single(vm.Results);
    }

    [Fact]
    public async Task Without_any_kind_selected_scan_asks_to_choose()
    {
        WriteFile("a.jpg");

        MediaViewModel vm = CreateViewModel();
        vm.IncludePhotos = false;
        vm.IncludeVideos = false;
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.Equal("Выберите хотя бы один тип: фото или видео", vm.StatusMessage);
    }

    [Fact]
    public async Task Empty_root_is_reported()
    {
        MediaViewModel vm = CreateViewModel();
        vm.RootPath = "   ";
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Equal("Укажите папку для поиска", vm.StatusMessage);
    }

    [Fact]
    public async Task Missing_root_is_reported_as_error()
    {
        MediaViewModel vm = CreateViewModel();
        vm.RootPath = Path.Combine(_root, "nope");
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.StartsWith("Ошибка поиска:", vm.StatusMessage);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки — не повод валить тест.
        }
    }
}
