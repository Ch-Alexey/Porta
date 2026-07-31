using System.Net;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Protocol;
using Porta.Core.Sync;
using Porta.Infrastructure.Transport;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Полная синхронизация файла поверх реального QUIC: индекс → дельта → запрос блоков →
/// передача → сборка. Требует libmsquic (иначе пропускается).
/// См. docs/features/09-block-transfer.md.
/// </summary>
[Trait("Category", "Integration")]
public class FileSyncOverQuicTests : IDisposable
{
    private readonly string _serverDir;
    private readonly string _clientDir;

    public FileSyncOverQuicTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _serverDir = Path.Combine(baseDir, "server");
        _clientDir = Path.Combine(baseDir, "client");
        Directory.CreateDirectory(_serverDir);
        Directory.CreateDirectory(_clientDir);
    }

    [Fact]
    public async Task File_is_synchronized_from_sender_to_receiver()
    {
        if (!QuicTransport.IsSupported)
            return;

        // У отдающего — файл на несколько блоков; у принимающего пусто.
        byte[] original = new byte[40_000];
        new Random(123).NextBytes(original);
        await File.WriteAllBytesAsync(Path.Combine(_serverDir, "doc.bin"), original);

        IReadOnlyList<FileIndexEntry> serverIndex = new FolderScanner().Scan(_serverDir);
        var serverBlocks = new FolderBlockReader(_serverDir, serverIndex);

        using var serverId = DeviceIdentity.Generate();
        using var clientId = DeviceIdentity.Generate();
        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;

        using var server = new QuicTransport(serverId);
        using var client = new QuicTransport(clientId);
        await using var listener = await server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        var acceptTask = listener.AcceptAsync(ct).AsTask();
        var clientConn = await client.ConnectAsync(listener.LocalEndPoint, serverId.Id, ct);
        var serverConn = await acceptTask;

        var clientSessionTask = PeerSession.EstablishAsync(clientConn, new TrustList(serverId.Id), "Client", isInitiator: true, ct);
        var serverSessionTask = PeerSession.EstablishAsync(serverConn, new TrustList(clientId.Id), "Server", isInitiator: false, ct);
        await Task.WhenAll(clientSessionTask, serverSessionTask);
        await using PeerSession cs = clientSessionTask.Result;
        await using PeerSession ss = serverSessionTask.Result;

        async Task ServerFlow()
        {
            await ss.Control.WriteAsync(new FolderIndexMessage("s1", serverIndex), ct);
            BlockRequestMessage request = await ss.Control.ReadAsync<BlockRequestMessage>(ct);
            var served = new List<BlockData>();
            foreach (byte[] hash in request.Hashes)
                if (serverBlocks.TryGet(hash, out byte[] data))
                    served.Add(new BlockData(hash, data));
            await ss.Control.WriteAsync(new BlockResponseMessage(served), ct);
        }

        async Task ClientFlow()
        {
            FolderIndexMessage index = await cs.Control.ReadAsync<FolderIndexMessage>(ct);
            IReadOnlyList<FileIndexEntry> localIndex = new FolderScanner().Scan(_clientDir);
            IndexDiff diff = IndexComparer.Compare(localIndex, index.Entries);

            await cs.Control.WriteAsync(new BlockRequestMessage(diff.MissingBlocks.Select(b => b.Hash).ToArray()), ct);
            BlockResponseMessage response = await cs.Control.ReadAsync<BlockResponseMessage>(ct);

            var received = new MemoryBlockSource(response.Blocks.Select(b => new KeyValuePair<byte[], byte[]>(b.Hash, b.Data)));
            var source = new CompositeBlockSource(received, new FolderBlockReader(_clientDir, localIndex));
            foreach (FileIndexEntry file in diff.FilesToUpdate)
                FileAssembler.Write(_clientDir, file, source);
        }

        await Task.WhenAll(ServerFlow(), ClientFlow());

        byte[] synced = await File.ReadAllBytesAsync(Path.Combine(_clientDir, "doc.bin"), ct);
        Assert.Equal(original, synced);
    }

    public void Dispose()
    {
        string baseDir = Path.GetDirectoryName(_serverDir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }
}
