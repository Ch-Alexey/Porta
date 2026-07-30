using Porta.Core.Identity;

namespace Porta.Core.Tests.Identity;

public class FileDeviceIdentityStoreTests : IDisposable
{
    private readonly string _dir;

    public FileDeviceIdentityStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
    }

    private string KeyPath => Path.Combine(_dir, "device.key");

    [Fact]
    public void LoadOrCreate_creates_key_file_when_missing()
    {
        var store = new FileDeviceIdentityStore(KeyPath);

        using var identity = store.LoadOrCreate();

        Assert.True(File.Exists(KeyPath));
        Assert.NotNull(identity.Id);
    }

    [Fact]
    public void LoadOrCreate_returns_same_identity_on_second_call()
    {
        var store = new FileDeviceIdentityStore(KeyPath);

        using var first = store.LoadOrCreate();
        using var second = store.LoadOrCreate();

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void Separate_stores_with_same_path_share_identity()
    {
        using var created = new FileDeviceIdentityStore(KeyPath).LoadOrCreate();

        using var reopened = new FileDeviceIdentityStore(KeyPath).LoadOrCreate();

        Assert.Equal(created.Id, reopened.Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
