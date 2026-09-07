using System.Security.Cryptography;
using System.Text;
using Porta.Core.Drop;
using Porta.Core.Identity;
using Porta.Core.Protocol;
using Porta.Core.Tests.Sync;

namespace Porta.Core.Tests.Drop;

public class DropProtocolTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _destination;
    private readonly DeviceId _sender;

    public DropProtocolTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "source");
        _destination = Path.Combine(_root, "downloads");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_destination);

        using DeviceIdentity identity = DeviceIdentity.Generate();
        _sender = identity.Id;
    }

    private string WriteSource(string name, byte[] content)
    {
        string full = Path.Combine(_source, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
        return full;
    }

    private string WriteSource(string name, string content)
        => WriteSource(name, Encoding.UTF8.GetBytes(content));

    /// <summary>Прогнать обе стороны диалога навстречу друг другу.</summary>
    private async Task<(DropSendResult Send, DropReceiveResult Receive)> ExchangeAsync(
        IReadOnlyList<DropSourceFile> files,
        IDropAcceptance acceptance)
    {
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();

        Task<DropSendResult> send = DropProtocol.SendAsync(a, files);
        Task<DropReceiveResult> receive = DropProtocol.ReceiveAsync(b, acceptance, _sender);

        await Task.WhenAll(send, receive);
        return (send.Result, receive.Result);
    }

    [Fact]
    public async Task Transfers_files_with_content_metadata_and_folder_structure()
    {
        string photo = WriteSource("photo.jpg", "содержимое фото");
        string nested = WriteSource("отпуск/видео.mp4", "содержимое видео");
        var modified = new DateTime(2024, 5, 17, 12, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(photo, modified);

        var files = new[]
        {
            DropSourceFile.FromPath(photo),
            new DropSourceFile(nested, "отпуск/видео.mp4"),
        };
        var acceptance = new FakeAcceptance(DropDecision.Accept(_destination));

        (DropSendResult send, DropReceiveResult receive) = await ExchangeAsync(files, acceptance);

        Assert.True(send.Accepted);
        Assert.Equal(2, send.FilesSent);
        Assert.True(receive.Accepted);
        Assert.Equal(2, receive.FilesReceived);

        string received = Path.Combine(_destination, "photo.jpg");
        Assert.Equal("содержимое фото", File.ReadAllText(received));
        Assert.Equal(modified, File.GetLastWriteTimeUtc(received));
        Assert.Equal(
            "содержимое видео",
            File.ReadAllText(Path.Combine(_destination, "отпуск", "видео.mp4")));
    }

    [Fact]
    public async Task Offer_reaches_receiver_with_sizes_and_sender()
    {
        WriteSource("a.txt", "12345");
        var files = new[] { DropSourceFile.FromPath(Path.Combine(_source, "a.txt")) };
        var acceptance = new FakeAcceptance(DropDecision.Accept(_destination));

        await ExchangeAsync(files, acceptance);

        DropOfferMessage offer = Assert.IsType<DropOfferMessage>(acceptance.SeenOffer);
        Assert.Equal(5, offer.TotalSize);
        Assert.Equal("a.txt", Assert.Single(offer.Files).RelativePath);
        Assert.Equal(_sender, acceptance.SeenSender);
    }

    [Fact]
    public async Task Rejected_transfer_sends_no_content()
    {
        WriteSource("secret.txt", "не должно уйти");
        var files = new[] { DropSourceFile.FromPath(Path.Combine(_source, "secret.txt")) };
        var acceptance = new FakeAcceptance(DropDecision.Reject("Не сейчас"));

        (DropSendResult send, DropReceiveResult receive) = await ExchangeAsync(files, acceptance);

        Assert.False(send.Accepted);
        Assert.Equal("Не сейчас", send.RejectReason);
        Assert.Equal(0, send.FilesSent);
        Assert.False(receive.Accepted);
        Assert.Empty(Directory.GetFiles(_destination));
    }

    [Fact]
    public async Task Transfers_file_larger_than_message_limit()
    {
        // Больше MessageChannel.MaxMessageSize (16 МБ) — доказывает, что содержимое идёт
        // потоком, а не одним сообщением, как в блочном обмене синка.
        byte[] big = RandomNumberGenerator.GetBytes(20 * 1024 * 1024);
        string path = WriteSource("big.bin", big);
        var files = new[] { DropSourceFile.FromPath(path) };

        (DropSendResult send, DropReceiveResult receive) =
            await ExchangeAsync(files, new FakeAcceptance(DropDecision.Accept(_destination)));

        Assert.True(send.Accepted);
        Assert.Equal(big.Length, send.BytesSent);
        Assert.Equal(big.Length, receive.BytesReceived);
        Assert.Equal(big, File.ReadAllBytes(Path.Combine(_destination, "big.bin")));
    }

    [Fact]
    public async Task Transfers_empty_file()
    {
        string path = WriteSource("empty.txt", []);
        var files = new[] { DropSourceFile.FromPath(path) };

        (_, DropReceiveResult receive) =
            await ExchangeAsync(files, new FakeAcceptance(DropDecision.Accept(_destination)));

        Assert.Equal(1, receive.FilesReceived);
        Assert.Empty(File.ReadAllBytes(Path.Combine(_destination, "empty.txt")));
    }

    [Fact]
    public async Task Existing_file_is_not_overwritten()
    {
        File.WriteAllText(Path.Combine(_destination, "photo.jpg"), "уже лежит");
        string path = WriteSource("photo.jpg", "новое");
        var files = new[] { DropSourceFile.FromPath(path) };

        (_, DropReceiveResult receive) =
            await ExchangeAsync(files, new FakeAcceptance(DropDecision.Accept(_destination)));

        Assert.Equal("уже лежит", File.ReadAllText(Path.Combine(_destination, "photo.jpg")));
        Assert.Equal("новое", File.ReadAllText(Path.Combine(_destination, "photo (2).jpg")));
        Assert.Equal(Path.Combine(_destination, "photo (2).jpg"), Assert.Single(receive.Paths));
    }

    [Fact]
    public async Task Identical_file_is_not_duplicated()
    {
        // «Передать файл, который уже есть»: цель уже достигнута, копия не нужна.
        // См. docs/features/31-audit-fixes.md.
        File.WriteAllText(Path.Combine(_destination, "фото.jpg"), "точно такое же");
        string path = WriteSource("фото.jpg", "точно такое же");

        (DropSendResult send, DropReceiveResult receive) = await ExchangeAsync(
            [DropSourceFile.FromPath(path)], new FakeAcceptance(DropDecision.Accept(_destination)));

        Assert.True(send.Accepted);
        Assert.Single(Directory.GetFiles(_destination));
        Assert.Equal("точно такое же", File.ReadAllText(Path.Combine(_destination, "фото.jpg")));
        Assert.Equal(Path.Combine(_destination, "фото.jpg"), Assert.Single(receive.Paths));
    }

    [Fact]
    public async Task Leftover_temp_files_are_not_left_behind_after_a_duplicate()
    {
        File.WriteAllText(Path.Combine(_destination, "фото.jpg"), "одинаково");
        string path = WriteSource("фото.jpg", "одинаково");

        await ExchangeAsync([DropSourceFile.FromPath(path)],
            new FakeAcceptance(DropDecision.Accept(_destination)));

        Assert.DoesNotContain(
            Directory.GetFiles(_destination),
            f => f.Contains(".porta-drop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Path_escaping_destination_is_refused()
    {
        string path = WriteSource("evil.txt", "наружу");
        var files = new[] { new DropSourceFile(path, "../../evil.txt") };

        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        Task<DropSendResult> send = DropProtocol.SendAsync(a, files);
        Task<DropReceiveResult> receive = DropProtocol.ReceiveAsync(
            b, new FakeAcceptance(DropDecision.Accept(_destination)), _sender);

        await Assert.ThrowsAsync<InvalidOperationException>(() => receive);
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        Assert.Empty(Directory.GetFiles(_destination));
        _ = send;
    }

    [Fact]
    public async Task Corrupted_content_is_rejected_and_leaves_nothing_behind()
    {
        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        Task<DropReceiveResult> receive = DropProtocol.ReceiveAsync(
            b, new FakeAcceptance(DropDecision.Accept(_destination)), _sender);

        // Отправителя изображаем вручную: заголовок обещает один хеш, содержимое другое.
        byte[] content = Encoding.UTF8.GetBytes("подменённые данные");
        await a.WriteAsync(new DropOfferMessage("t1", [new DropFileInfo("photo.jpg", content.Length, 0)]));
        _ = await a.ReadAsync<DropDecisionMessage>();
        await a.WriteAsync(new DropFileHeaderMessage("photo.jpg", content.Length, 0, SHA256.HashData([1, 2, 3])));
        await a.WriteAsync(new DropChunkMessage(content, true));

        await Assert.ThrowsAsync<InvalidDataException>(() => receive);
        Assert.Empty(Directory.GetFiles(_destination));
    }

    /// <summary>Собирает отчёты о ходе передачи.</summary>
    private sealed class ProgressSink : IProgress<Porta.Core.Sync.TransferProgress>
    {
        public List<Porta.Core.Sync.TransferProgress> Reports { get; } = [];

        public void Report(Porta.Core.Sync.TransferProgress value) => Reports.Add(value);
    }

    [Fact]
    public async Task Sending_reports_progress_and_finishes_at_the_full_amount()
    {
        byte[] big = RandomNumberGenerator.GetBytes(3 * 1024 * 1024);
        string path = WriteSource("big.bin", big);
        var sent = new ProgressSink();
        var got = new ProgressSink();

        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        Task<DropSendResult> send = DropProtocol.SendAsync(a, [DropSourceFile.FromPath(path)], null, sent);
        Task<DropReceiveResult> receive = DropProtocol.ReceiveAsync(
            b, new FakeAcceptance(DropDecision.Accept(_destination)), _sender, got);
        await Task.WhenAll(send, receive);

        // Последний отчёт обязателен и содержит полные числа — на него и смотрит человек.
        Assert.Equal(big.Length, sent.Reports[^1].BytesDone);
        Assert.Equal(1, sent.Reports[^1].FilesDone);
        Assert.Equal(big.Length, got.Reports[^1].BytesDone);
        Assert.Equal(1.0, sent.Reports[^1].Fraction);
    }

    [Fact]
    public async Task Progress_is_throttled_not_one_report_per_chunk()
    {
        // 3 МиБ кусками по 256 КиБ — это 12 кусков; отчётов должно быть меньше.
        byte[] big = RandomNumberGenerator.GetBytes(3 * 1024 * 1024);
        string path = WriteSource("big.bin", big);
        var sink = new ProgressSink();

        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        Task<DropSendResult> send = DropProtocol.SendAsync(a, [DropSourceFile.FromPath(path)], null, sink);
        Task<DropReceiveResult> receive = DropProtocol.ReceiveAsync(
            b, new FakeAcceptance(DropDecision.Accept(_destination)), _sender);
        await Task.WhenAll(send, receive);

        int chunks = big.Length / DropProtocol.ChunkSize;
        Assert.True(sink.Reports.Count <= chunks, $"отчётов {sink.Reports.Count}, кусков {chunks} — прореживание не работает");
    }

    [Fact]
    public async Task Cancelling_mid_transfer_leaves_no_temp_files()
    {
        byte[] big = RandomNumberGenerator.GetBytes(6 * 1024 * 1024);
        string path = WriteSource("big.bin", big);
        using var cts = new CancellationTokenSource();

        (MessageChannel a, MessageChannel b) = ConnectedChannels.Create();
        // Отменяем, как только пошло содержимое.
        var progress = new Progress<Porta.Core.Sync.TransferProgress>(_ => cts.Cancel());

        Task<DropSendResult> send = DropProtocol.SendAsync(
            a, [DropSourceFile.FromPath(path)], null, progress, cts.Token);
        Task<DropReceiveResult> receive = DropProtocol.ReceiveAsync(
            b, new FakeAcceptance(DropDecision.Accept(_destination)), _sender, null, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.WhenAll(send, receive));

        Assert.DoesNotContain(
            Directory.GetFiles(_destination),
            f => f.Contains(".porta-drop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_source_file_is_reported_before_offer()
    {
        var files = new[] { new DropSourceFile(Path.Combine(_source, "нет.jpg"), "нет.jpg") };
        (MessageChannel a, _) = ConnectedChannels.Create();

        await Assert.ThrowsAsync<FileNotFoundException>(() => DropProtocol.SendAsync(a, files));
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
