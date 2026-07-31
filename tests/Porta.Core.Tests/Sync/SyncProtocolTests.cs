using System.Text;
using Porta.Core.Protocol;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class SyncProtocolTests : IDisposable
{
    private readonly string _senderDir;
    private readonly string _receiverDir;

    public SyncProtocolTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _senderDir = Path.Combine(baseDir, "sender");
        _receiverDir = Path.Combine(baseDir, "receiver");
        Directory.CreateDirectory(_senderDir);
        Directory.CreateDirectory(_receiverDir);
    }

    private void SenderFile(string relativePath, byte[] content)
    {
        string full = Path.Combine(_senderDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
    }

    private byte[] ReceiverFile(string relativePath)
        => File.ReadAllBytes(Path.Combine(_receiverDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private async Task<SyncResult> RunSyncAsync(IVersionStore? versions = null)
    {
        var (a, b) = ConnectedChannels.Create();
        Task serve = SyncProtocol.ServeAsync(b, _senderDir, "s1");
        Task<SyncResult> pull = SyncProtocol.PullAsync(a, _receiverDir, "s1", versions);
        await Task.WhenAll(serve, pull);
        return pull.Result;
    }

    [Fact]
    public async Task Syncs_multiple_files_including_nested()
    {
        byte[] big = new byte[30_000];
        new Random(1).NextBytes(big);
        SenderFile("root.txt", Encoding.UTF8.GetBytes("root file"));
        SenderFile("nested/deep/data.bin", big);

        SyncResult result = await RunSyncAsync();

        Assert.Equal(2, result.FilesUpdated);
        Assert.Equal(Encoding.UTF8.GetBytes("root file"), ReceiverFile("root.txt"));
        Assert.Equal(big, ReceiverFile("nested/deep/data.bin"));
    }

    [Fact]
    public async Task Skips_files_already_identical_on_receiver()
    {
        byte[] same = Encoding.UTF8.GetBytes("identical content");
        SenderFile("a.txt", same);
        SenderFile("b.txt", Encoding.UTF8.GetBytes("only on sender"));
        // У принимающего уже есть идентичный a.txt.
        File.WriteAllBytes(Path.Combine(_receiverDir, "a.txt"), same);

        SyncResult result = await RunSyncAsync();

        Assert.Equal(1, result.FilesUpdated); // только b.txt
        Assert.Equal("only on sender", Encoding.UTF8.GetString(ReceiverFile("b.txt")));
    }

    [Fact]
    public async Task Overwriting_changed_file_preserves_previous_version()
    {
        SenderFile("a.txt", Encoding.UTF8.GetBytes("new content from sender"));
        File.WriteAllText(Path.Combine(_receiverDir, "a.txt"), "old content on receiver");
        string versionsRoot = Path.Combine(Path.GetDirectoryName(_senderDir)!, "versions");
        var versions = new FileSystemVersionStore(versionsRoot);

        SyncResult result = await RunSyncAsync(versions);

        Assert.Equal(1, result.FilesUpdated);
        Assert.Equal("new content from sender", Encoding.UTF8.GetString(ReceiverFile("a.txt")));
        string archived = Assert.Single(versions.ListVersions("a.txt"));
        Assert.Equal("old content on receiver", File.ReadAllText(archived));
    }

    [Fact]
    public async Task Empty_sender_updates_nothing()
    {
        SyncResult result = await RunSyncAsync();

        Assert.Equal(0, result.FilesUpdated);
        Assert.Empty(Directory.GetFiles(_receiverDir));
    }

    public void Dispose()
    {
        string baseDir = Path.GetDirectoryName(_senderDir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }
}
