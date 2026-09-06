using System.Security.Cryptography;
using System.Text;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class SpooledBlockSourceTests
{
    private static (byte[] Hash, byte[] Data) Block(string content)
    {
        byte[] data = Encoding.UTF8.GetBytes(content);
        return (SHA256.HashData(data), data);
    }

    [Fact]
    public void Returns_what_was_stored()
    {
        using var spool = new SpooledBlockSource();
        (byte[] hash, byte[] data) = Block("содержимое блока");

        spool.Add(hash, data);

        Assert.True(spool.TryGet(hash, out byte[] read));
        Assert.Equal(data, read);
    }

    [Fact]
    public void Missing_block_is_reported_without_throwing()
    {
        using var spool = new SpooledBlockSource();
        (byte[] hash, _) = Block("не клали");

        Assert.False(spool.TryGet(hash, out byte[] read));
        Assert.Empty(read);
    }

    [Fact]
    public void Same_block_is_stored_once()
    {
        using var spool = new SpooledBlockSource();
        (byte[] hash, byte[] data) = Block("дубликат");

        spool.Add(hash, data);
        spool.Add(hash, data);

        Assert.Equal(1, spool.Count);
    }

    [Fact]
    public void Dispose_removes_the_temp_folder()
    {
        string directory = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        (byte[] hash, byte[] data) = Block("временный");

        using (var spool = new SpooledBlockSource(directory))
        {
            spool.Add(hash, data);
            Assert.True(Directory.Exists(directory));
        }

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Use_after_dispose_is_refused()
    {
        var spool = new SpooledBlockSource();
        (byte[] hash, byte[] data) = Block("после закрытия");
        spool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => spool.Add(hash, data));
    }
}
