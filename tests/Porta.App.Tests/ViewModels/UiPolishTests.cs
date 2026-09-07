using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Porta.App.Design;
using Porta.App.Services;
using Porta.App.ViewModels;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Sync;

namespace Porta.App.Tests.ViewModels;

/// <summary>Мелочи интерфейса. См. docs/features/34-ui-polish.md.</summary>
public class UiPolishTests : IDisposable
{
    private readonly string _root;

    public UiPolishTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StubQr : IQrCodeRenderer
    {
        public byte[] RenderPng(string content, int pixelsPerModule = 8) => [1, 2, 3];
    }

    [Fact]
    public void Pause_button_shows_the_current_state()
    {
        var storages = new InMemoryStorageRepository();
        storages.Add(new Storage("s1", "Док", "/tmp/s1",
            StorageExchangeMode.TwoWay, SyncMode.Automatic, false, DateTimeOffset.UnixEpoch));
        var vm = new StoragesViewModel(storages, new InMemoryDeviceRepository());

        Assert.Equal("Пауза", vm.Items.Single().PauseButtonText);
        vm.Items.Single().TogglePausedCommand.Execute(null);
        Assert.Equal("Продолжить", vm.Items.Single().PauseButtonText);
    }

    /// <summary>Настоящий токен приглашения — у фейкового нет разбираемого срока.</summary>
    private static FakeAppData WithRealInvitation(TimeSpan lifetime)
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        var token = new Porta.Core.Pairing.PairingService().CreateInvitation(identity, [], lifetime);
        return new FakeAppData { InvitationTokenValue = Porta.Core.Pairing.PairingTokenCodec.Encode(token) };
    }

    [Fact]
    public void An_expired_invitation_is_taken_off_the_screen()
    {
        var clock = new FrozenClock(DateTimeOffset.UtcNow);
        var vm = new DevicesViewModel(
            WithRealInvitation(TimeSpan.FromMinutes(5)),
            dispatcher: new ImmediateDispatcher(), qr: new StubQr(), clock: clock);

        vm.GenerateInvitationCommand.Execute(null);
        Assert.NotEmpty(vm.InvitationToken);
        Assert.NotNull(vm.InvitationExpiryText);

        // Токен живёт 5 минут — перематываем на час вперёд.
        clock.Now = clock.Now.AddHours(1);
        vm.DropExpiredInvitation();

        Assert.Equal(string.Empty, vm.InvitationToken);
        Assert.Null(vm.InvitationQrPng);
        Assert.Contains("истекло", vm.StatusMessage);
    }

    [Fact]
    public void A_fresh_invitation_survives_the_expiry_check()
    {
        var vm = new DevicesViewModel(
            WithRealInvitation(TimeSpan.FromMinutes(5)),
            dispatcher: new ImmediateDispatcher(), qr: new StubQr(),
            clock: new FrozenClock(DateTimeOffset.UtcNow));

        vm.GenerateInvitationCommand.Execute(null);
        vm.DropExpiredInvitation();

        Assert.NotEmpty(vm.InvitationToken);
    }

    [Fact]
    public void An_unusable_downloads_folder_is_reported_and_not_saved()
    {
        var settings = new InMemorySettings();
        var vm = new TransfersViewModel(settings, dispatcher: new ImmediateDispatcher());
        // Путь внутри файла — папку там создать нельзя.
        string file = Path.Combine(_root, "файл.txt");
        File.WriteAllText(file, "я не папка");
        vm.DownloadsFolder = Path.Combine(file, "подпапка");

        vm.SaveDownloadsFolderCommand.Execute(null);

        Assert.StartsWith("Не удалось использовать папку", vm.StatusMessage);
        Assert.Equal("нет", settings.Get(Porta.Core.Data.SettingKeys.DownloadsFolder, "нет"));
    }

    [Fact]
    public async Task Media_age_and_size_filters_reach_the_query()
    {
        string old = Path.Combine(_root, "старое.jpg");
        File.WriteAllBytes(old, new byte[10]);
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30));
        File.WriteAllBytes(Path.Combine(_root, "большое-свежее.jpg"), new byte[3 * 1024 * 1024]);
        File.WriteAllBytes(Path.Combine(_root, "мелкое-свежее.jpg"), new byte[10]);

        var vm = new MediaViewModel(dispatcher: new ImmediateDispatcher(), defaultRoot: _root)
        {
            MaxAgeDays = "7",
            MinSizeMb = "1",
        };
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Equal("большое-свежее.jpg", Assert.Single(vm.Results).FileName);
    }

    [Fact]
    public async Task Long_paths_are_trimmed_from_the_left_keeping_the_file_name()
    {
        string deep = Path.Combine(_root, new string('д', 40), new string('п', 40));
        Directory.CreateDirectory(deep);
        File.WriteAllBytes(Path.Combine(deep, "фото.jpg"), new byte[10]);

        var vm = new MediaViewModel(dispatcher: new ImmediateDispatcher(), defaultRoot: _root);
        await vm.ScanCommand.ExecuteAsync(null);

        MediaItem item = Assert.Single(vm.Results);
        Assert.StartsWith("…", item.DisplayPath);
        Assert.EndsWith("фото.jpg", item.DisplayPath);
        // Настоящий путь не испорчен — именно он уходит в отправку.
        Assert.True(File.Exists(item.FullPath));
    }

    [Fact]
    public async Task Send_found_fills_the_outgoing_list_without_duplicates()
    {
        File.WriteAllBytes(Path.Combine(_root, "a.jpg"), new byte[10]);
        var transfers = new TransfersViewModel(new InMemorySettings(), dispatcher: new ImmediateDispatcher());
        var media = new MediaViewModel(
            dispatcher: new ImmediateDispatcher(), defaultRoot: _root, onSend: transfers.AddFiles);

        await media.ScanCommand.ExecuteAsync(null);
        media.SendFoundCommand.Execute(null);
        media.SendFoundCommand.Execute(null);

        Assert.Single(transfers.SelectedFiles);
        Assert.Contains("Добавлено к отправке", media.StatusMessage);
    }

    [Fact]
    public void Revoking_trust_interrupts_running_operations_with_that_device()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        var data = new FakeAppData();
        data.Devices.Add(new TrustedDevice(
            identity.Id, identity.ExportPublicKey(), "Ноутбук", DateTimeOffset.UnixEpoch, null));
        using var operations = new PeerOperations();
        using PeerOperations.Operation op = operations.Begin(identity.Id);

        var vm = new DevicesViewModel(data, dispatcher: new ImmediateDispatcher(), operations: operations);
        vm.Items.Single().RevokeCommand.Execute(null);

        Assert.True(op.Token.IsCancellationRequested);
        Assert.Contains("операция прервана", vm.StatusMessage);
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
