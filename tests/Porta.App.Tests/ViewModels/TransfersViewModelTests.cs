using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Porta.App.Services;
using Porta.App.ViewModels;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Drop;
using Porta.Core.Identity;
using Porta.Core.Protocol;

namespace Porta.App.Tests.ViewModels;

public class TransfersViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly InMemorySettings _settings = new();

    public TransfersViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private string WriteFile(string name, int size)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    private static DiscoveredPeer Peer(DeviceId id, string name = "Ноутбук")
        => new(id, name, [new IPEndPoint(IPAddress.Loopback, 7000)], DateTimeOffset.UnixEpoch);

    private TransfersViewModel Create(
        IDropController? drops = null,
        IFilePicker? picker = null,
        IDeviceDiscovery? discovery = null,
        UiDropAcceptance? acceptance = null)
        => new(_settings, drops, picker, discovery, acceptance, new ImmediateDispatcher());

    [Fact]
    public async Task Picked_files_show_up_with_their_size()
    {
        string a = WriteFile("a.bin", 2048);
        TransfersViewModel vm = Create(picker: new FakeFilePicker(a));

        await vm.PickFilesCommand.ExecuteAsync(null);

        OutgoingFileItem item = Assert.Single(vm.SelectedFiles);
        Assert.Equal("a.bin", item.FileName);
        Assert.Equal(2048, item.Size);
        Assert.Equal("Выбрано файлов: 1, объём: 2 КБ", vm.SelectionSummary);
    }

    [Fact]
    public async Task Picking_the_same_file_twice_does_not_duplicate_it()
    {
        string a = WriteFile("a.bin", 16);
        TransfersViewModel vm = Create(picker: new FakeFilePicker(a));

        await vm.PickFilesCommand.ExecuteAsync(null);
        await vm.PickFilesCommand.ExecuteAsync(null);

        Assert.Single(vm.SelectedFiles);
    }

    [Fact]
    public async Task Sending_without_files_is_reported()
    {
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        TransfersViewModel vm = Create(drops: new FakeDropController(), discovery: discovery);
        discovery.RaiseDiscovered(Peer(id.Id));
        vm.SelectedPeer = vm.Peers.Single();

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Сначала выберите файлы", vm.StatusMessage);
    }

    [Fact]
    public async Task Sending_without_a_device_is_reported()
    {
        string a = WriteFile("a.bin", 16);
        TransfersViewModel vm = Create(drops: new FakeDropController(), picker: new FakeFilePicker(a));
        await vm.PickFilesCommand.ExecuteAsync(null);

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Выберите устройство", vm.StatusMessage);
    }

    [Fact]
    public async Task Successful_send_reports_and_clears_the_selection()
    {
        string a = WriteFile("a.bin", 16);
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        var drops = new FakeDropController(new DropSendResult(true, 1, 2048));
        TransfersViewModel vm = Create(drops, new FakeFilePicker(a), discovery);

        discovery.RaiseDiscovered(Peer(id.Id));
        vm.SelectedPeer = vm.Peers.Single();
        await vm.PickFilesCommand.ExecuteAsync(null);
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Отправлено файлов: 1 (2 КБ)", vm.StatusMessage);
        Assert.Empty(vm.SelectedFiles);
        Assert.Equal(id.Id, Assert.Single(drops.Calls).Peer.DeviceId);
    }

    [Fact]
    public async Task Rejected_send_keeps_the_selection_and_shows_the_reason()
    {
        string a = WriteFile("a.bin", 16);
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        var drops = new FakeDropController(new DropSendResult(false, 0, 0, "Получатель не ответил"));
        TransfersViewModel vm = Create(drops, new FakeFilePicker(a), discovery);

        discovery.RaiseDiscovered(Peer(id.Id));
        vm.SelectedPeer = vm.Peers.Single();
        await vm.PickFilesCommand.ExecuteAsync(null);
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Отклонено: Получатель не ответил", vm.StatusMessage);
        Assert.Single(vm.SelectedFiles);
    }

    [Fact]
    public async Task Controller_failure_does_not_break_the_ui()
    {
        string a = WriteFile("a.bin", 16);
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        var drops = new FakeDropController { Throws = new InvalidOperationException("сеть упала") };
        TransfersViewModel vm = Create(drops, new FakeFilePicker(a), discovery);

        discovery.RaiseDiscovered(Peer(id.Id));
        vm.SelectedPeer = vm.Peers.Single();
        await vm.PickFilesCommand.ExecuteAsync(null);
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal("Ошибка отправки: сеть упала", vm.StatusMessage);
        Assert.False(vm.IsSending);
    }

    [Fact]
    public async Task Progress_is_shown_while_sending()
    {
        string a = WriteFile("a.bin", 16);
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        var drops = new FakeDropController();
        drops.Reports.Add(new Porta.Core.Sync.TransferProgress(0, 2, 512, 2048, "a.bin"));
        var progressSeen = new List<string?>();

        TransfersViewModel vm = Create(drops, new FakeFilePicker(a), discovery);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.ProgressText))
                progressSeen.Add(vm.ProgressText);
        };
        discovery.RaiseDiscovered(Peer(id.Id));
        vm.SelectedPeer = vm.Peers.Single();
        await vm.PickFilesCommand.ExecuteAsync(null);
        await vm.SendCommand.ExecuteAsync(null);

        Assert.Contains(progressSeen, t => t is not null && t.Contains("из 2 файлов"));
        // По завершении полоса убирается — иначе она врала бы про идущую работу.
        Assert.Null(vm.ProgressText);
        Assert.Equal(0, vm.ProgressFraction);
    }

    [Fact]
    public void Incoming_progress_is_shown_and_cleared_when_finished()
    {
        var acceptance = new UiDropAcceptance(() => _root, TimeSpan.FromSeconds(5));
        TransfersViewModel vm = Create(acceptance: acceptance);

        acceptance.Progress.Report(new Porta.Core.Sync.TransferProgress(0, 2, 512, 2048, "a.jpg"));
        Assert.Contains("Приём:", vm.IncomingProgressText);

        acceptance.Progress.Report(new Porta.Core.Sync.TransferProgress(2, 2, 2048, 2048));
        Assert.Null(vm.IncomingProgressText);
    }

    [Fact]
    public void Downloads_folder_defaults_and_is_saved()
    {
        TransfersViewModel vm = Create();
        Assert.Equal(TransfersViewModel.DefaultDownloadsFolder(), vm.DownloadsFolder);

        // Путь во временной папке: сохранение теперь проверяет папку, создавая её.
        string target = Path.Combine(_root, "Принятое");
        vm.DownloadsFolder = target;
        vm.SaveDownloadsFolderCommand.Execute(null);

        Assert.Equal(target, _settings.Get(SettingKeys.DownloadsFolder, "нет"));
        Assert.Equal(target, vm.CurrentDownloadsFolder());
        Assert.True(Directory.Exists(target), "папка должна быть создана при сохранении");
    }

    [Fact]
    public void Empty_downloads_folder_is_not_saved()
    {
        TransfersViewModel vm = Create();
        vm.DownloadsFolder = "   ";

        vm.SaveDownloadsFolderCommand.Execute(null);

        Assert.Equal("нет", _settings.Get(SettingKeys.DownloadsFolder, "нет"));
        Assert.Equal("Укажите папку для принятых файлов", vm.StatusMessage);
    }

    [Fact]
    public async Task Incoming_offer_appears_and_accepting_removes_it()
    {
        using var id = DeviceIdentity.Generate();
        var acceptance = new UiDropAcceptance(() => _root, TimeSpan.FromSeconds(5));
        TransfersViewModel vm = Create(acceptance: acceptance);

        Task<DropDecision> decision = acceptance.DecideAsync(
            new DropOfferMessage("t1", [new DropFileInfo("photo.jpg", 4096, 0)]), id.Id);

        IncomingOfferItem item = Assert.Single(vm.IncomingOffers);
        Assert.Equal("1 файл(ов), 4 КБ", item.Summary);

        item.AcceptCommand.Execute(null);
        Assert.True((await decision).Accepted);
        Assert.Empty(vm.IncomingOffers);
    }

    [Fact]
    public async Task Rejecting_an_offer_removes_it_and_refuses_the_transfer()
    {
        using var id = DeviceIdentity.Generate();
        var acceptance = new UiDropAcceptance(() => _root, TimeSpan.FromSeconds(5));
        TransfersViewModel vm = Create(acceptance: acceptance);

        Task<DropDecision> decision = acceptance.DecideAsync(
            new DropOfferMessage("t1", [new DropFileInfo("photo.jpg", 4096, 0)]), id.Id);
        Assert.Single(vm.IncomingOffers).RejectCommand.Execute(null);

        Assert.False((await decision).Accepted);
        Assert.Empty(vm.IncomingOffers);
    }

    [Fact]
    public async Task Offer_from_a_known_peer_is_shown_under_its_name()
    {
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        var acceptance = new UiDropAcceptance(() => _root, TimeSpan.FromSeconds(5));
        TransfersViewModel vm = Create(discovery: discovery, acceptance: acceptance);
        discovery.RaiseDiscovered(Peer(id.Id, "Телефон Алексея"));

        Task<DropDecision> decision = acceptance.DecideAsync(
            new DropOfferMessage("t1", [new DropFileInfo("a.jpg", 1, 0)]), id.Id);

        Assert.Equal("Телефон Алексея", Assert.Single(vm.IncomingOffers).SenderName);
        vm.IncomingOffers[0].RejectCommand.Execute(null);
        await decision;
    }

    [Fact]
    public void Lost_peer_disappears_from_the_recipient_list()
    {
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        TransfersViewModel vm = Create(discovery: discovery);

        discovery.RaiseDiscovered(Peer(id.Id));
        Assert.Single(vm.Peers);

        discovery.RaiseLost(id.Id);
        Assert.Empty(vm.Peers);
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
