using System.IO.Pipelines;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Protocol;
using Porta.Core.Sync;
using Porta.Core.Tests.Data;

namespace Porta.Core.Tests.Sync;

/// <summary>Поток, считающий, сколько байт через него уехало от A к B.</summary>
internal sealed class CountingStream(Stream inner) : Stream
{
    public long Written { get; private set; }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        Written += buffer.Length;
        await inner.WriteAsync(buffer, ct);
    }

    public override void Write(byte[] b, int o, int c) { Written += c; inner.Write(b, o, c); }
    public override ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default) => inner.ReadAsync(b, ct);
    public override int Read(byte[] b, int o, int c) => inner.Read(b, o, c);
    public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
    public override void Flush() => inner.Flush();
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();
}

/// <summary>
/// Дельта-синк: после правки уезжают только изменившиеся блоки, а не файл целиком.
/// Заявка проекта «передаём только изменения» — держим её измерением.
/// См. docs/features/31-audit-fixes.md.
/// </summary>
public class DeltaTransferTests : IDisposable
{
    private static readonly DeviceId IdA = DeviceIdentity.Generate().Id;
    private static readonly DeviceId IdB = DeviceIdentity.Generate().Id;
    private readonly TempDatabase _dbA = new();
    private readonly TempDatabase _dbB = new();
    private readonly string _folderA;
    private readonly string _folderB;

    public DeltaTransferTests()
    {
        string b = Path.Combine(Path.GetTempPath(), "porta-delta", Guid.NewGuid().ToString("N"));
        _folderA = Path.Combine(b, "A");
        _folderB = Path.Combine(b, "B");
        Directory.CreateDirectory(_folderA);
        Directory.CreateDirectory(_folderB);
    }

    /// <summary>Синк с подсчётом байт, ушедших от отдающего к принимающему.</summary>
    private async Task<long> PullCountingAsync()
    {
        var aToB = new Pipe();
        var bToA = new Pipe();
        var counted = new CountingStream(aToB.Writer.AsStream());
        var a = new DuplexStream(bToA.Reader.AsStream(), counted);
        var b = new DuplexStream(aToB.Reader.AsStream(), bToA.Writer.AsStream());

        Task serve = VersionedSync.ServeAsync(new MessageChannel(a), new FileIndexRepository(_dbA.Database), _ => _folderA, IdA);
        Task<SyncApplyReport> pull = VersionedSync.PullAsync(
            new MessageChannel(b), new FileIndexRepository(_dbB.Database), _folderB, "s1", IdB, IdA);
        await Task.WhenAll(serve, pull);
        return counted.Written;
    }

    [Fact]
    public async Task Small_edit_transfers_only_the_changed_blocks()
    {
        byte[] data = new byte[8 * 1024 * 1024];
        new Random(31).NextBytes(data);
        string file = Path.Combine(_folderA, "большой.bin");
        await File.WriteAllBytesAsync(file, data);

        long whole = await PullCountingAsync();

        // Правим 100 байт в середине восьмимегабайтного файла.
        for (int i = 0; i < 100; i++)
            data[4_000_000 + i] = (byte)(i + 1);
        await File.WriteAllBytesAsync(file, data);

        long afterEdit = await PullCountingAsync();

        Assert.Equal(data, await File.ReadAllBytesAsync(Path.Combine(_folderB, "большой.bin")));
        Assert.True(
            afterEdit < whole / 10,
            $"после правки уехало {afterEdit} байт из {whole} — это не похоже на дельту");
    }

    [Fact]
    public async Task Unchanged_file_transfers_almost_nothing()
    {
        byte[] data = new byte[2 * 1024 * 1024];
        new Random(32).NextBytes(data);
        await File.WriteAllBytesAsync(Path.Combine(_folderA, "большой.bin"), data);

        long whole = await PullCountingAsync();
        long repeat = await PullCountingAsync();

        Assert.True(repeat < whole / 20, $"повторный синк отдал {repeat} байт из {whole}");
    }

    public void Dispose() { _dbA.Dispose(); _dbB.Dispose(); }
}
